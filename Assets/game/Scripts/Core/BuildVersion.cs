using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class BuildVersion
    {
        public const string Flag = "-buildVersion";

        public const string LogTag = "[Version]";

        // EOS のロビーのバケツ ID に入れられる版の長さに合わせ、長い版が途中で切られて別の版と同じバケツに入らないようにする。
        public const int MaxLength = LobbyLaunchOptions.MaxBucketVersionLength;

        // 同じ文字列を Docker イメージのタグにも付けるので、Docker のタグに使えない + などは通さない。
        public static bool TryNormalize(string text, out string version, out string error)
        {
            version = null;
            var trimmed = text == null ? "" : text.Trim();
            if (trimmed.Length >= 2 && (trimmed[0] == 'v' || trimmed[0] == 'V') && IsDigit(trimmed[1]))
            {
                trimmed = trimmed.Substring(1);
            }

            if (trimmed.Length == 0)
            {
                error = "build version is empty";
                return false;
            }

            if (trimmed.Length > MaxLength)
            {
                error = "build version '" + trimmed + "' is longer than " + MaxLength + " characters";
                return false;
            }

            foreach (var c in trimmed)
            {
                if (!IsAllowed(c))
                {
                    error = "build version '" + trimmed + "' contains '" + c
                        + "'; use only letters, digits, '.', '-' and '_'";
                    return false;
                }
            }

            if (trimmed[0] == '.' || trimmed[0] == '-')
            {
                error = "build version '" + trimmed + "' starts with '" + trimmed[0] + "'; start with a letter, digit or '_'";
                return false;
            }

            version = trimmed;
            error = null;
            return true;
        }

        public static string Normalize(string text)
        {
            if (!TryNormalize(text, out var version, out var error))
            {
                throw new ArgumentException(error, nameof(text));
            }

            return version;
        }

        // 知らない引数を拒まないのは、Unity 自身の -batchmode なども同じ引数に並ぶため。
        public static bool TryParseArgs(IReadOnlyList<string> args, out string version, out string error)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            version = null;
            string text = null;
            for (var i = 0; i < args.Count; i++)
            {
                if (!string.Equals(args[i], Flag, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1][0] == '-')
                {
                    error = Flag + " requires a value";
                    return false;
                }

                if (text != null)
                {
                    error = Flag + " is given more than once";
                    return false;
                }

                text = args[++i];
            }

            if (text == null)
            {
                error = null;
                return true;
            }

            if (!TryNormalize(text, out version, out var normalizeError))
            {
                error = Flag + ": " + normalizeError;
                return false;
            }

            error = null;
            return true;
        }

        public static string LogLine(string version)
        {
            return LogTag + " " + (string.IsNullOrWhiteSpace(version) ? "unknown" : version.Trim());
        }

        private static bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        private static bool IsAllowed(char c)
        {
            return IsDigit(c) || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '.' || c == '-' || c == '_';
        }
    }
}
