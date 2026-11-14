using System;

namespace Fps.Core
{
    /// <summary>
    /// ゲームのアセットを置くルート (<see cref="DefaultRoot"/>) と、その下のパスの組み立て方。
    /// Editor は生成物の出力先に、実行時はシーンを読む経路に使う。ルートは区切りを / に揃え、末尾の / を落として扱う。
    /// </summary>
    public static class KitPaths
    {
        /// <summary>このリポジトリで開いたときのルート。パッケージとして読み込んだときは Editor 側で上書きする。</summary>
        public const string DefaultRoot = "Assets/game";

        public const string ScenesFolderName = "Scenes";
        public const string SceneExtension = ".unity";

        /// <summary><see cref="DefaultRoot"/> のシーンのフォルダ。実行時の既定のシーン経路を const で持つために const にしてある。</summary>
        public const string DefaultScenesFolder = DefaultRoot + "/" + ScenesFolderName;

        private static readonly char[] _separators = { '/', '\\' };

        /// <summary>
        /// root の区切りを / に揃え、前後の空白と末尾の / を落として返す。
        /// </summary>
        /// <exception cref="ArgumentException">root が空、または空・<c>.</c>・<c>..</c> の区切りを含む。</exception>
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

        /// <summary>root の下の relative (例: <c>Weapons/Rifle.asset</c>) のパス。</summary>
        /// <exception cref="ArgumentException">root か relative が空、または空・<c>.</c>・<c>..</c> の区切りを含む。</exception>
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

        /// <summary>root の下のシーン <c>&lt;root&gt;/Scenes/&lt;sceneName&gt;.unity</c>。</summary>
        /// <exception cref="ArgumentException">sceneName が空か / を含む。</exception>
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
