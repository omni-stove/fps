using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// 生成スクリプトが書き込むルート (<see cref="Output"/>) と、同梱のアセットを読むルート (<see cref="Bundled"/>) を決める。
    /// <para>
    /// 書き込み先は、batchmode の引数 <c>-fpsKitRoot &lt;path&gt;</c>、このプロジェクトの EditorPrefs (<see cref="SetPreferredOutput"/>)、
    /// 既定の <see cref="DefaultOutput"/> の順に決める。パッケージの中は読み取り専用なので、書き込み先は Assets/ の下に限る。
    /// </para>
    /// <para>
    /// 読み取り元は、このアセンブリを含むパッケージのルート (<c>Packages/com.omnistove.fps</c> など)。
    /// パッケージでなく Assets/ の下に置かれている (このリポジトリ自身) なら <see cref="KitPaths.DefaultRoot"/>。
    /// </para>
    /// </summary>
    public static class FpsKitRoot
    {
        public const string ArgumentName = "-fpsKitRoot";

        /// <summary>EditorPrefs の鍵の頭。EditorPrefs はマシン全体で共有されるので、後ろにプロジェクトの Assets の絶対パスを付けて分ける。</summary>
        public const string PrefsKeyPrefix = "Fps.KitRoot:";

        public const string LogTag = "[FpsKitRoot]";

        private const string WritablePrefix = "Assets/";
        private const string PackagesPrefix = "Packages/";

        private static string _defaultOutput;

        /// <summary>
        /// 引数も EditorPrefs も無いときの書き込み先。<see cref="UseDefaultOutput"/> で登録した値、無ければ <see cref="KitPaths.DefaultRoot"/>。
        /// </summary>
        public static string DefaultOutput => _defaultOutput ?? KitPaths.DefaultRoot;

        /// <summary>このプロジェクトの EditorPrefs の鍵。</summary>
        public static string PrefsKey => PrefsKeyPrefix + Application.dataPath;

        /// <summary>生成物を書き込むルート (<see cref="ResolveOutput"/>)。呼ぶたびに引数と EditorPrefs を読み直す。</summary>
        /// <exception cref="ArgumentException">引数か EditorPrefs の値が Assets/ の下でない、または引数の値が無い・重なっている。</exception>
        public static string Output => ResolveOutput(Environment.GetCommandLineArgs(), EditorPrefs.GetString(PrefsKey, string.Empty));

        /// <summary>同梱のアセットを読むルート (<see cref="ResolveBundled"/>)。</summary>
        public static string Bundled
        {
            get
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(FpsKitRoot).Assembly);
                return ResolveBundled(package?.assetPath);
            }
        }

        /// <summary><see cref="Output"/> の下の relative (例: <c>Weapons/Rifle.asset</c>)。</summary>
        public static string OutputPath(string relative)
        {
            return KitPaths.Combine(Output, relative);
        }

        /// <summary><see cref="Output"/> の下のシーン <c>Scenes/&lt;sceneName&gt;.unity</c>。</summary>
        public static string OutputScene(string sceneName)
        {
            return KitPaths.ScenePath(Output, sceneName);
        }

        /// <summary><see cref="Bundled"/> の下の relative。</summary>
        public static string BundledPath(string relative)
        {
            return KitPaths.Combine(Bundled, relative);
        }

        /// <summary>
        /// path が <c>Packages/</c> で始まればディスク上の絶対パス (区切りは /) に直し、そうでなければそのまま返す。
        /// git や registry から入れたパッケージの実体は <c>Library/PackageCache/&lt;name&gt;@&lt;hash&gt;</c> にあり、
        /// System.IO からは <c>Packages/&lt;name&gt;</c> が見えないので、Unity が解決する <see cref="System.IO.Path.GetFullPath(string)"/> を通す。
        /// </summary>
        public static string PhysicalPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            var normalized = path.Replace('\\', '/');
            if (!normalized.StartsWith(PackagesPrefix, StringComparison.Ordinal))
            {
                return path;
            }

            return System.IO.Path.GetFullPath(normalized).Replace('\\', '/');
        }

        /// <summary>
        /// 書き込むルートを決める。args に <see cref="ArgumentName"/> があればその次の値、無ければ prefsValue、
        /// それも空なら <see cref="DefaultOutput"/>。どれも <see cref="ValidateOutput"/> を通して返す。
        /// </summary>
        /// <exception cref="ArgumentException">引数の値が無い・重なっている、または値が Assets/ の下でない。</exception>
        public static string ResolveOutput(IReadOnlyList<string> args, string prefsValue)
        {
            var fromArgs = FindArgument(args);
            if (fromArgs != null)
            {
                return ValidateOutput(fromArgs);
            }

            if (!string.IsNullOrWhiteSpace(prefsValue))
            {
                return ValidateOutput(prefsValue);
            }

            return DefaultOutput;
        }

        /// <summary>
        /// 引数も EditorPrefs も無いときの書き込み先を root にする。パッケージの外のフォルダを持つもの (取り込んだサンプルなど) が
        /// Editor の読み込み時に呼ぶ。null で <see cref="KitPaths.DefaultRoot"/> に戻す。
        /// </summary>
        /// <exception cref="ArgumentException">root が Assets/ の下でない。</exception>
        public static void UseDefaultOutput(string root)
        {
            _defaultOutput = root == null ? null : ValidateOutput(root);
        }

        /// <summary>
        /// 同梱のアセットを読むルート。packageAssetPath (このアセンブリを含むパッケージの <c>Packages/...</c>) が空なら、
        /// パッケージでなくプロジェクトの中にあるとみなして <see cref="KitPaths.DefaultRoot"/> を返す。
        /// </summary>
        public static string ResolveBundled(string packageAssetPath)
        {
            return string.IsNullOrWhiteSpace(packageAssetPath) ? KitPaths.DefaultRoot : KitPaths.NormalizeRoot(packageAssetPath);
        }

        /// <summary>root を書き込み先として正規化して返す。Assets/ の下のフォルダであること。</summary>
        /// <exception cref="ArgumentException">root が空、Assets/ の下でない、または空・<c>.</c>・<c>..</c> の区切りを含む。</exception>
        public static string ValidateOutput(string root)
        {
            var normalized = KitPaths.NormalizeRoot(root);
            if (!normalized.StartsWith(WritablePrefix, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"{LogTag} output root '{root}' must be a folder under {WritablePrefix} (packages are read-only)", nameof(root));
            }

            return normalized;
        }

        /// <summary>このプロジェクトの書き込み先を EditorPrefs に覚える。引数 <see cref="ArgumentName"/> があればそちらが優先。</summary>
        /// <exception cref="ArgumentException">root が Assets/ の下でない。</exception>
        public static void SetPreferredOutput(string root)
        {
            var normalized = ValidateOutput(root);
            EditorPrefs.SetString(PrefsKey, normalized);
            Debug.Log($"{LogTag} output root set to '{normalized}'");
        }

        /// <summary>EditorPrefs に覚えた書き込み先を消し、既定 (<see cref="DefaultOutput"/>) に戻す。</summary>
        public static void ClearPreferredOutput()
        {
            EditorPrefs.DeleteKey(PrefsKey);
            Debug.Log($"{LogTag} output root reset to '{DefaultOutput}'");
        }

        private static string FindArgument(IReadOnlyList<string> args)
        {
            if (args == null)
            {
                return null;
            }

            string found = null;
            for (var i = 0; i < args.Count; i++)
            {
                if (!string.Equals(args[i], ArgumentName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"{ArgumentName} needs a folder such as {KitPaths.DefaultRoot}", nameof(args));
                }

                if (found != null)
                {
                    throw new ArgumentException($"{ArgumentName} is given more than once", nameof(args));
                }

                found = args[i + 1];
                i++;
            }

            return found;
        }
    }
}
