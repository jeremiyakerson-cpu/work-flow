using System;
using System.IO;
using System.Text.Json;
using TowerDefense.Persistence;

namespace TowerDefense.PlatformTests
{
    /// <summary>System.Text.Json stand-in for Unity's JsonUtility (public fields, no properties).</summary>
    public sealed class SystemTextJsonSaveSerializer : ISaveSerializer
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            IncludeFields = true,
            WriteIndented = true,
            // JsonUtility writes NaN as a bare token; tolerate the same on read.
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
        };

        public string Serialize(SaveData data) => JsonSerializer.Serialize(data, Options);
        public SaveData Deserialize(string json) => JsonSerializer.Deserialize<SaveData>(json, Options);
    }

    /// <summary>Points at which <see cref="FaultyFileSystem"/> can "crash".</summary>
    public enum CrashPoint
    {
        None,
        /// <summary>Half the bytes reach the temp file, then the process dies.</summary>
        MidTempWrite,
        /// <summary>Temp fully written and flushed, process dies before the rename.</summary>
        BeforeReplace,
        /// <summary>Non-atomic fallback path: main deleted, temp not yet renamed.</summary>
        AfterDeleteBeforeMove,
        /// <summary>Disk full / permission error on write.</summary>
        WriteThrows,
    }

    /// <summary>Real disk + injected failures, to simulate a crash at each step of a save.</summary>
    public sealed class FaultyFileSystem : ISaveFileSystem
    {
        private readonly PhysicalSaveFileSystem real = new PhysicalSaveFileSystem();
        public CrashPoint Crash = CrashPoint.None;

        public bool FileExists(string path) => real.FileExists(path);
        public string ReadAllText(string path) => real.ReadAllText(path);

        public void WriteAllTextDurable(string path, string contents)
        {
            if (Crash == CrashPoint.WriteThrows) throw new IOException("No space left on device");
            if (Crash == CrashPoint.MidTempWrite)
            {
                File.WriteAllText(path, contents.Substring(0, contents.Length / 2));
                throw new IOException("simulated crash mid-write");
            }
            real.WriteAllTextDurable(path, contents);
        }

        public void Replace(string source, string destination, string backup)
        {
            if (Crash == CrashPoint.BeforeReplace) throw new IOException("simulated crash before replace");
            if (Crash == CrashPoint.AfterDeleteBeforeMove)
            {
                File.Copy(destination, backup, true);
                File.Delete(destination);
                throw new IOException("simulated crash between delete and move");
            }
            real.Replace(source, destination, backup);
        }

        public void Move(string source, string destination)
        {
            if (Crash == CrashPoint.BeforeReplace) throw new IOException("simulated crash before rename");
            real.Move(source, destination);
        }

        public void Copy(string source, string destination, bool overwrite) => real.Copy(source, destination, overwrite);
        public void Delete(string path) => real.Delete(path);
        public void CreateDirectory(string path) => real.CreateDirectory(path);
    }

    /// <summary>Manually advanced clock for debounce tests.</summary>
    public sealed class FakeClock
    {
        public double Now;
        public double Get() => Now;
    }

    public static class TempDir
    {
        public static string Create()
        {
            string dir = Path.Combine(Path.GetTempPath(), "td-platform-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static void Delete(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
