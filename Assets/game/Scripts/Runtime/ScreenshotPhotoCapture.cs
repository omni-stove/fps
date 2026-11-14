using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Fps.Runtime
{
    public sealed class ScreenshotPhotoCapture : IPhotoCapture
    {
        public const string FolderName = "Photos";
        public const string FilePrefix = "photo_";
        public const string FileExtension = ".png";

        private readonly string _directory;
        private readonly int _superSize;

        public ScreenshotPhotoCapture(string directory = null, int superSize = 1)
        {
            if (superSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(superSize), superSize, "superSize must be 1 or more");
            }

            _directory = string.IsNullOrEmpty(directory) ? null : directory;
            _superSize = superSize;
        }

        // persistentDataPath は端末ごとに必ず書き込める場所で、ビルドの中 (dataPath) は書き込めないことがある
        public static string DefaultDirectory => Path.Combine(Application.persistentDataPath, FolderName);

        public string OutputDirectory => _directory ?? DefaultDirectory;

        public int SuperSize => _superSize;

        // ミリ秒まで入れるのは、連写したときに同じ名前で上書きしないため
        public static string FileNameFor(DateTime time)
        {
            return FilePrefix + time.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + FileExtension;
        }

        public static string UniquePath(string directory, string fileName)
        {
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("directory must not be empty", nameof(directory));
            }

            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException("fileName must not be empty", nameof(fileName));
            }

            var path = Path.Combine(directory, fileName);
            if (!File.Exists(path))
            {
                return path;
            }

            var stem = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            for (var i = 2; ; i++)
            {
                var candidate = Path.Combine(directory, stem + "_" + i.ToString(CultureInfo.InvariantCulture) + extension);
                if (!File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        public string Capture()
        {
            var directory = OutputDirectory;
            Directory.CreateDirectory(directory);
            var path = UniquePath(directory, FileNameFor(DateTime.Now));
            ScreenCapture.CaptureScreenshot(path, _superSize);
            return path;
        }
    }
}
