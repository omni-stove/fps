using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    // パッケージの中は読み取り専用なので、書き込み先は Assets/ の下に限る。
    public static class ShooterKitRoot
    {
        public const string ArgumentName = "-shooterKitRoot";

        // EditorPrefs はマシン全体で共有されるので、後ろにプロジェクトの Assets の絶対パスを付けて分ける。
        public const string PrefsKeyPrefix = "Shooter.KitRoot:";

        public const string LogTag = "[ShooterKitRoot]";

        private const string WritablePrefix = "Assets/";
        private const string PackagesPrefix = "Packages/";

        private static string _defaultOutput;

        public static string DefaultOutput => _defaultOutput ?? KitPaths.DefaultRoot;

        public static string PrefsKey => PrefsKeyPrefix + Application.dataPath;

        public static string Output => ResolveOutput(Environment.GetCommandLineArgs(), EditorPrefs.GetString(PrefsKey, string.Empty));

        public static string Bundled
        {
            get
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ShooterKitRoot).Assembly);
                return ResolveBundled(package?.assetPath);
            }
        }

        public static string OutputPath(string relative)
        {
            return KitPaths.Combine(Output, relative);
        }

        public static string OutputScene(string sceneName)
        {
            return KitPaths.ScenePath(Output, sceneName);
        }

        public static string BundledPath(string relative)
        {
            return KitPaths.Combine(Bundled, relative);
        }

        // git や registry から入れたパッケージの実体は Library/PackageCache/<name>@<hash> にあり、
        // System.IO からは Packages/<name> が見えないので、Unity が解決する System.IO.Path.GetFullPath を通す。
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

        public static void UseDefaultOutput(string root)
        {
            _defaultOutput = root == null ? null : ValidateOutput(root);
        }

        public static string ResolveBundled(string packageAssetPath)
        {
            return string.IsNullOrWhiteSpace(packageAssetPath) ? KitPaths.DefaultRoot : KitPaths.NormalizeRoot(packageAssetPath);
        }

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

        public static void SetPreferredOutput(string root)
        {
            var normalized = ValidateOutput(root);
            EditorPrefs.SetString(PrefsKey, normalized);
            Debug.Log($"{LogTag} output root set to '{normalized}'");
        }

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
