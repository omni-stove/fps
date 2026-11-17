using System;

namespace Shooter.Core
{
    public static class KitPaths
    {
        // パッケージとして読み込んだときは Editor 側で上書きするので、このリポジトリで開いたときの値を既定として持つ。
        public const string DefaultRoot = "Assets/game";

        public const string ScenesFolderName = "Scenes";
        public const string SceneExtension = ".unity";

        // 実行時の既定のシーン経路を const で持つために const にしてある。
        public const string DefaultScenesFolder = DefaultRoot + "/" + ScenesFolderName;

        private static readonly char[] _separators = { '/', '\\' };

        public static string NormalizeRoot(string root)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                throw new ArgumentException("root must not be empty", nameof(root));
            }

            var normalized = root.Trim().Replace('\\', '/').TrimEnd('/');
            RequireSegments(normalized, root, nameof(root));
            return normalized;
        }

        public static string Combine(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative))
            {
                throw new ArgumentException("relative path must not be empty", nameof(relative));
            }

            var normalizedRelative = relative.Trim().Replace('\\', '/').Trim('/');
            RequireSegments(normalizedRelative, relative, nameof(relative));
            return NormalizeRoot(root) + "/" + normalizedRelative;
        }

        public static string ScenePath(string root, string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName) || sceneName.IndexOfAny(_separators) >= 0)
            {
                throw new ArgumentException($"scene name '{sceneName}' must be a plain file name", nameof(sceneName));
            }

            return Combine(root, ScenesFolderName + "/" + sceneName.Trim() + SceneExtension);
        }

        private static void RequireSegments(string normalized, string original, string parameterName)
        {
            foreach (var segment in normalized.Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == "..")
                {
                    throw new ArgumentException($"path '{original}' must not contain empty, '.' or '..' segments", parameterName);
                }
            }
        }
    }
}
