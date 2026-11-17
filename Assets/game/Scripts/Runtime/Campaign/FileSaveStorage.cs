using System;
using System.IO;
using System.Text;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 書き込みを <slot>.json.tmp に書き切ってから置き換えるのは、途中で落ちても前の内容を残すため。
    public sealed class FileSaveStorage : ISaveStorage
    {
        public const string LogTag = CampaignRunner.LogTag;

        public const string DefaultDirectoryName = "saves";

        public const string FileExtension = ".json";
        public const string TempSuffix = ".tmp";

        // BOM を付けず、読み込みでは不正なバイト列を黙って置き換えずに例外にする。
        private static readonly Encoding _encoding = new UTF8Encoding(false, true);

        public string RootDirectory { get; }

        public FileSaveStorage(string rootDirectory)
        {
            if (rootDirectory == null)
            {
                throw new ArgumentNullException(nameof(rootDirectory));
            }

            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                throw new ArgumentException("rootDirectory must not be empty", nameof(rootDirectory));
            }

            RootDirectory = Path.GetFullPath(rootDirectory);
        }

        public static FileSaveStorage CreateDefault()
        {
            return new FileSaveStorage(Path.Combine(Application.persistentDataPath, DefaultDirectoryName));
        }

        public string PathFor(string slot)
        {
            ValidateSlot(slot);
            return Path.Combine(RootDirectory, slot + FileExtension);
        }

        public static void ValidateSlot(string slot)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            if (string.IsNullOrWhiteSpace(slot))
            {
                throw new ArgumentException("slot must not be empty", nameof(slot));
            }

            // 実行環境の区切り文字に関わらず、どちらの区切りも拒む (Windows で作った名前を Linux で使っても同じ判断になる)。
            if (slot.IndexOf('/') >= 0 || slot.IndexOf('\\') >= 0
                || slot.IndexOf(Path.DirectorySeparatorChar) >= 0 || slot.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
            {
                throw new ArgumentException($"slot '{slot}' must not contain path separators", nameof(slot));
            }

            if (slot == "." || slot.Contains(".."))
            {
                throw new ArgumentException($"slot '{slot}' must not contain '..' or be '.'", nameof(slot));
            }

            if (slot.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || slot.IndexOf(':') >= 0)
            {
                throw new ArgumentException($"slot '{slot}' contains characters that cannot be used in a file name", nameof(slot));
            }
        }

        public bool TryRead(string slot, out string text)
        {
            var path = PathFor(slot);
            text = null;

            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                text = File.ReadAllText(path, _encoding);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is DecoderFallbackException)
            {
                Debug.LogError($"{LogTag} failed to read save slot={slot} path={path}; treating it as no save: {e.Message}");
                text = null;
                return false;
            }
        }

        public void Write(string slot, string text)
        {
            var path = PathFor(slot);
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            Directory.CreateDirectory(RootDirectory);
            var temp = path + TempSuffix;
            try
            {
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var bytes = _encoding.GetBytes(text);
                    stream.Write(bytes, 0, bytes.Length);

                    // 置き換えの前に中身をディスクまで書き切る。途中で電源が落ちても空のファイルに置き換わらない。
                    stream.Flush(true);
                }

                Replace(temp, path);
            }
            catch
            {
                TryDeleteQuietly(temp);
                throw;
            }
        }

        public void Delete(string slot)
        {
            var path = PathFor(slot);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            // 書き込みの途中で落ちたときの残りも片付ける。
            var temp = path + TempSuffix;
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }

        private static void Replace(string source, string destination)
        {
            if (!File.Exists(destination))
            {
                File.Move(source, destination);
                return;
            }

            try
            {
                File.Replace(source, destination, null);
            }
            catch (PlatformNotSupportedException)
            {
                // File.Replace が無い環境では、消してから移す。間で落ちるとセーブ無しになり、新しい内容は .tmp に残る。
                File.Delete(destination);
                File.Move(source, destination);
            }
        }

        private static void TryDeleteQuietly(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // 元の例外を優先する。残った .tmp は次の書き込みか削除で上書き・削除される。
            }
        }
    }
}
