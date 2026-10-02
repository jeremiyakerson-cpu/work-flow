using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace TowerDefense.Persistence
{
    /// <summary>Where a load got its data from.</summary>
    public enum SaveLoadSource
    {
        /// <summary>No usable file: fresh defaults (first launch, or everything was corrupt).</summary>
        Defaults,
        /// <summary>The main save file.</summary>
        Main,
        /// <summary>A fully written temp file left by a crash between write and rename.</summary>
        Temp,
        /// <summary>The previous save (.bak), because the main file was missing or corrupt.</summary>
        Backup,
    }

    /// <summary>Outcome of <see cref="SaveFileStore.Load"/>.</summary>
    public sealed class SaveLoadResult
    {
        public SaveData Data;
        public SaveLoadSource Source;
        /// <summary>True if at least one file existed but could not be read.</summary>
        public bool CorruptionDetected;
        /// <summary>Version stored in the file before migration (0 when defaults were used).</summary>
        public int OriginalVersion;
        /// <summary>The file came from a newer build than this one; unknown fields were ignored.</summary>
        public bool FromFutureVersion;
    }

    /// <summary>
    /// Crash-safe JSON save file. Write path: serialize -> write "save.json.tmp"
    /// -> fsync -> atomic replace over "save.json" (old file kept as
    /// "save.json.bak"). Read path: main -> tmp -> bak -> defaults, so a crash
    /// at any point of a write, a truncated file or a corrupt file never loses
    /// more than the last unsaved change and never throws.
    ///
    /// File format: the JSON document, then a footer line
    /// "#td-save crc32=XXXXXXXX length=N" that detects truncation and bit rot.
    /// A file without a footer (hand-edited) is accepted if its JSON parses.
    /// </summary>
    public sealed class SaveFileStore
    {
        public const string DefaultFileName = "save.json";
        public const string TempSuffix = ".tmp";
        public const string BackupSuffix = ".bak";
        public const string CorruptSuffix = ".corrupt";
        private const string FooterPrefix = "#td-save ";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly ISaveFileSystem fs;
        private readonly ISaveSerializer serializer;
        private readonly Action<string> log;

        public string Directory { get; }
        public string MainPath { get; }
        public string TempPath => MainPath + TempSuffix;
        public string BackupPath => MainPath + BackupSuffix;
        public string CorruptPath => MainPath + CorruptSuffix;

        /// <summary>Last error message from Save/Load, for logs and tests.</summary>
        public string LastError { get; private set; }

        public SaveFileStore(string directory, ISaveSerializer serializer, ISaveFileSystem fileSystem = null,
                             string fileName = DefaultFileName, Action<string> log = null)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("Save directory required", nameof(directory));
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            fs = fileSystem ?? new PhysicalSaveFileSystem();
            this.log = log;
            Directory = directory;
            MainPath = Path.Combine(directory, string.IsNullOrEmpty(fileName) ? DefaultFileName : fileName);
        }

        // ------------------------------------------------------------------ write

        /// <summary>Write <paramref name="data"/> atomically. Returns false (never throws) on I/O failure.</summary>
        public bool Save(SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            try
            {
                if (data.version < SaveData.CurrentVersion) data.version = SaveData.CurrentVersion;
                string text = Encode(serializer.Serialize(data));

                fs.CreateDirectory(Directory);
                fs.WriteAllTextDurable(TempPath, text);
                if (fs.FileExists(MainPath)) fs.Replace(TempPath, MainPath, BackupPath);
                else fs.Move(TempPath, MainPath);
                LastError = null;
                return true;
            }
            catch (Exception e)
            {
                LastError = "Save failed: " + e.Message;
                log?.Invoke(LastError);
                return false;
            }
        }

        // ------------------------------------------------------------------ read

        /// <summary>
        /// Load the newest readable copy, migrated and sanitized. Never throws.
        /// A corrupt main file is moved aside to "save.json.corrupt" so the next
        /// save does not rotate it into the backup slot.
        /// </summary>
        public SaveLoadResult Load()
        {
            var result = new SaveLoadResult();
            try
            {
                ReadState main = TryRead(MainPath, out SaveData data, out int version);
                if (main == ReadState.Ok)
                {
                    SafeDelete(TempPath); // stale temp from an interrupted write; main is intact
                    return Finish(result, data, SaveLoadSource.Main, version);
                }
                if (main == ReadState.Corrupt)
                {
                    result.CorruptionDetected = true;
                    Quarantine();
                }

                ReadState temp = TryRead(TempPath, out data, out version);
                if (temp == ReadState.Ok)
                {
                    // Crash after the temp was fully written but before the rename finished: promote it.
                    try { if (!fs.FileExists(MainPath)) fs.Move(TempPath, MainPath); }
                    catch (Exception e) { log?.Invoke("Could not promote temp save: " + e.Message); }
                    return Finish(result, data, SaveLoadSource.Temp, version);
                }
                if (temp == ReadState.Corrupt) SafeDelete(TempPath); // partial write: worthless

                ReadState backup = TryRead(BackupPath, out data, out version);
                if (backup == ReadState.Ok) return Finish(result, data, SaveLoadSource.Backup, version);
                if (backup == ReadState.Corrupt) result.CorruptionDetected = true;
            }
            catch (Exception e)
            {
                LastError = "Load failed: " + e.Message;
                log?.Invoke(LastError);
            }

            result.Data = SaveData.CreateDefault();
            result.Source = SaveLoadSource.Defaults;
            result.OriginalVersion = 0;
            return result;
        }

        private static SaveLoadResult Finish(SaveLoadResult result, SaveData data, SaveLoadSource source, int version)
        {
            result.Data = data;
            result.Source = source;
            result.OriginalVersion = version;
            result.FromFutureVersion = version > SaveData.CurrentVersion;
            return result;
        }

        private enum ReadState { Missing, Corrupt, Ok }

        private ReadState TryRead(string path, out SaveData data, out int originalVersion)
        {
            data = null;
            originalVersion = 0;
            try
            {
                if (!fs.FileExists(path)) return ReadState.Missing;
                string text = fs.ReadAllText(path);
                if (!TryDecode(text, out string json, out string reason))
                {
                    LastError = path + ": " + reason;
                    log?.Invoke("Corrupt save " + LastError);
                    return ReadState.Corrupt;
                }
                SaveData parsed = serializer.Deserialize(json);
                if (parsed == null)
                {
                    LastError = path + ": JSON did not produce save data";
                    log?.Invoke("Corrupt save " + LastError);
                    return ReadState.Corrupt;
                }
                originalVersion = SaveMigrator.Migrate(parsed);
                data = SaveValidator.Sanitize(parsed);
                return ReadState.Ok;
            }
            catch (Exception e)
            {
                LastError = path + ": " + e.Message;
                log?.Invoke("Corrupt save " + LastError);
                data = null;
                return ReadState.Corrupt;
            }
        }

        private void Quarantine()
        {
            try
            {
                fs.Copy(MainPath, CorruptPath, true);
                fs.Delete(MainPath);
            }
            catch (Exception e)
            {
                log?.Invoke("Could not move corrupt save aside: " + e.Message);
            }
        }

        private void SafeDelete(string path)
        {
            try { if (fs.FileExists(path)) fs.Delete(path); }
            catch (Exception e) { log?.Invoke("Could not delete " + path + ": " + e.Message); }
        }

        // ------------------------------------------------------------------ format

        /// <summary>Append the integrity footer to a JSON document.</summary>
        public static string Encode(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            byte[] bytes = Utf8NoBom.GetBytes(json);
            return json + "\n" + FooterPrefix + "crc32=" + Crc32.Compute(bytes).ToString("x8", CultureInfo.InvariantCulture)
                   + " length=" + bytes.Length.ToString(CultureInfo.InvariantCulture) + "\n";
        }

        /// <summary>Strip and verify the footer. Footer-less text is accepted as plain JSON.</summary>
        public static bool TryDecode(string text, out string json, out string reason)
        {
            json = null;
            reason = null;
            if (string.IsNullOrWhiteSpace(text)) { reason = "file is empty"; return false; }

            int footerAt = text.LastIndexOf("\n" + FooterPrefix, StringComparison.Ordinal);
            if (footerAt < 0)
            {
                json = text;
                return true;
            }

            json = text.Substring(0, footerAt);
            string footer = text.Substring(footerAt + 1 + FooterPrefix.Length).Trim();
            uint expectedCrc = 0;
            int expectedLength = -1;
            bool hasCrc = false;
            string[] parts = footer.Split(' ');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i];
                if (p.StartsWith("crc32=", StringComparison.Ordinal))
                    hasCrc = uint.TryParse(p.Substring(6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out expectedCrc);
                else if (p.StartsWith("length=", StringComparison.Ordinal))
                    int.TryParse(p.Substring(7), NumberStyles.None, CultureInfo.InvariantCulture, out expectedLength);
            }
            if (!hasCrc || expectedLength < 0) { reason = "malformed footer"; return false; }

            byte[] bytes = Utf8NoBom.GetBytes(json);
            if (bytes.Length != expectedLength) { reason = "length mismatch (truncated?)"; return false; }
            if (Crc32.Compute(bytes) != expectedCrc) { reason = "checksum mismatch"; return false; }
            return true;
        }
    }

    /// <summary>Standard CRC-32 (IEEE 802.3, reflected 0xEDB88320).</summary>
    public static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                table[i] = c;
            }
            return table;
        }

        public static uint Compute(byte[] data)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < data.Length; i++) crc = Table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }
    }
}
