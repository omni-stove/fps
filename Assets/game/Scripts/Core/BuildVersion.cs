using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// ビルドの版 (PlayerSettings.bundleVersion、実行時の Application.version) に入れる文字列の検証と正規化。
    /// git のタグ (<c>v0.2.0</c>) や <c>git describe --tags --always --dirty</c> の出力 (<c>v0.2.0-3-gabc1234-dirty</c>、
    /// タグが無ければ短い sha) を受け取り、先頭の <c>v</c> を外して <c>0.2.0</c> のような形にそろえる。
    /// </summary>
    public static class BuildVersion
    {
        public const string Flag = "-buildVersion";

        /// <summary>起動時に版を出すログの接頭辞。</summary>
        public const string LogTag = "[Version]";

        /// <summary>
        /// 版の最大の長さ。EOS のロビーのバケツ ID に入れられる版の長さ (<see cref="LobbyLaunchOptions.MaxBucketVersionLength"/>) に合わせ、
        /// 長い版が途中で切られて別の版と同じバケツに入らないようにする。
        /// </summary>
        public const int MaxLength = LobbyLaunchOptions.MaxBucketVersionLength;

        /// <summary>
        /// text を版として検証し、正規化する。前後の空白 (git の出力の改行を含む) を落とし、<c>v</c> か <c>V</c> の直後が数字なら
        /// その <c>v</c> を外す。空、<see cref="MaxLength"/> 文字超、英数字と <c>. - _</c> 以外を含むもの、<c>.</c> か <c>-</c> で始まるものは拒否する。
        /// 同じ文字列を Docker イメージのタグにも付けるので、Docker のタグに使えない <c>+</c> などは通さない。
        /// </summary>
        /// <param name="text">タグ名や git describe の出力。</param>
        /// <param name="version">成功したときの正規化した版。失敗したときは null。</param>
        /// <param name="error">失敗したときの理由。成功したときは null。</param>
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

        /// <summary><see cref="TryNormalize"/> と同じだが、失敗したら <see cref="ArgumentException"/> を投げる。</summary>
        public static string Normalize(string text)
        {
            if (!TryNormalize(text, out var version, out var error))
            {
                throw new ArgumentException(error, nameof(text));
            }

            return version;
        }

        /// <summary>
        /// コマンドライン引数から <c>-buildVersion &lt;版&gt;</c> を探して正規化する。無ければ version を null にして true を返す。
        /// 知らない引数 (Unity 自身の <c>-batchmode</c> など) は無視する。
        /// </summary>
        /// <param name="args">コマンドライン引数。</param>
        /// <param name="version">見つかったときの正規化した版。無いか失敗したときは null。</param>
        /// <param name="error">失敗したときの理由。成功したときは null。</param>
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

        /// <summary>起動時に出すログの行。例: <c>[Version] 0.2.0</c>。版が空なら <c>unknown</c> と出す。</summary>
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
