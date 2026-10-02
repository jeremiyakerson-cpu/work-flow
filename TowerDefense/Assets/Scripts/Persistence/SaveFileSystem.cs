using System;
using System.IO;
using System.Text;

namespace TowerDefense.Persistence
{
    /// <summary>
    /// The handful of file operations the save store needs. Abstracted so tests
    /// can inject failures (simulated crash mid-write) without touching Unity.
    /// </summary>
    public interface ISaveFileSystem
    {
        bool FileExists(string path);
        string ReadAllText(string path);
        /// <summary>Create/overwrite <paramref name="path"/> and flush it to the storage device before returning.</summary>
        void WriteAllTextDurable(string path, string contents);
        /// <summary>Atomically move <paramref name="source"/> over <paramref name="destination"/>, keeping the old destination as <paramref name="backup"/>.</summary>
        void Replace(string source, string destination, string backup);
        /// <summary>Rename; <paramref name="destination"/> must not exist.</summary>
        void Move(string source, string destination);
        void Copy(string source, string destination, bool overwrite);
        void Delete(string path);
        void CreateDirectory(string path);
    }

    /// <summary>
    /// Real disk implementation. On iOS (APFS) and every POSIX target,
    /// File.Replace / File.Move are rename(2) calls, which are atomic: a reader
    /// sees either the old or the new file, never a mix.
    /// </summary>
    public sealed class PhysicalSaveFileSystem : ISaveFileSystem
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public bool FileExists(string path) => File.Exists(path);

        public string ReadAllText(string path) => File.ReadAllText(path, Utf8NoBom);

        public void WriteAllTextDurable(string path, string contents)
        {
            byte[] bytes = Utf8NoBom.GetBytes(contents);
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true); // fsync: data is on disk before we rename over the real save
            }
        }

        public void Replace(string source, string destination, string backup)
        {
            try
            {
                File.Replace(source, destination, backup, true);
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceFallback(source, destination, backup);
            }
            catch (IOException)
            {
                // Some file systems refuse File.Replace (e.g. across volumes).
                // The fallback is not atomic, but the store's load order
                // (main -> tmp -> bak) recovers from a crash at any step.
                if (!File.Exists(source)) throw;
                ReplaceFallback(source, destination, backup);
            }
        }

        private static void ReplaceFallback(string source, string destination, string backup)
        {
            if (File.Exists(destination))
            {
                File.Copy(destination, backup, true);
                File.Delete(destination);
            }
            File.Move(source, destination);
        }

        public void Move(string source, string destination) => File.Move(source, destination);

        public void Copy(string source, string destination, bool overwrite) => File.Copy(source, destination, overwrite);

        public void Delete(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        public void CreateDirectory(string path)
        {
            if (!string.IsNullOrEmpty(path)) Directory.CreateDirectory(path);
        }
    }
}
