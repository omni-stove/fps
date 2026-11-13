using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// キャンペーンの起動設定。コマンドライン引数 <c>-difficulty easy|normal|hard</c> から作る。
    /// 知らない引数 (Unity 自身の <c>-batchmode</c> やネットワークの <c>-mode</c> など) は無視する。
    /// </summary>
    public sealed class CampaignLaunchOptions
    {
        public const string DifficultyFlag = "-difficulty";

        private static readonly CampaignDifficulty[] _difficulties =
        {
            CampaignDifficulty.Easy, CampaignDifficulty.Normal, CampaignDifficulty.Hard,
        };

        /// <summary>難易度を指定しない設定。シーンに配線したルールをそのまま使う。</summary>
        public static CampaignLaunchOptions None { get; } = new CampaignLaunchOptions(null);

        /// <summary>引数で選んだ難易度。指定が無ければ null。</summary>
        public CampaignDifficulty? Difficulty { get; }

        private CampaignLaunchOptions(CampaignDifficulty? difficulty)
        {
            Difficulty = difficulty;
        }

        /// <summary>difficulty を選んだ設定。</summary>
        public static CampaignLaunchOptions WithDifficulty(CampaignDifficulty difficulty)
        {
            if (!Enum.IsDefined(typeof(CampaignDifficulty), difficulty))
            {
                throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "unknown difficulty");
            }

            return new CampaignLaunchOptions(difficulty);
        }

        /// <summary>
        /// コマンドライン引数を解析する。<c>-difficulty</c> が無ければ <see cref="None"/>。
        /// フラグと値は大文字・小文字を区別しない。
        /// </summary>
        /// <param name="args">コマンドライン引数。先頭が実行ファイルのパスでもよい。</param>
        /// <param name="options">成功したときの設定。失敗したときは null。</param>
        /// <param name="error">失敗したときの理由。成功したときは null。</param>
        /// <returns>引数が正しければ true。値が無い・2 回以上ある・知らない難易度なら false。</returns>
        public static bool TryParse(IReadOnlyList<string> args, out CampaignLaunchOptions options, out string error)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            options = null;
            string text = null;
            for (var i = 0; i < args.Count; i++)
            {
                if (!IsFlag(args[i], DifficultyFlag))
                {
                    continue;
                }

                if (i + 1 >= args.Count || IsFlagLike(args[i + 1]))
                {
                    error = DifficultyFlag + " requires a value (easy, normal or hard)";
                    return false;
                }

                if (text != null)
                {
                    error = DifficultyFlag + " is given more than once";
                    return false;
                }

                text = args[++i];
            }

            if (text == null)
            {
                options = None;
                error = null;
                return true;
            }

            if (!TryParseDifficulty(text, out var difficulty))
            {
                error = DifficultyFlag + " must be easy, normal or hard (got '" + text + "')";
                return false;
            }

            options = new CampaignLaunchOptions(difficulty);
            error = null;
            return true;
        }

        /// <summary>小文字の難易度名 (easy / normal / hard) を読む。大文字・小文字は区別しない。</summary>
        public static bool TryParseDifficulty(string text, out CampaignDifficulty difficulty)
        {
            foreach (var candidate in _difficulties)
            {
                if (string.Equals(text, DifficultyName(candidate), StringComparison.OrdinalIgnoreCase))
                {
                    difficulty = candidate;
                    return true;
                }
            }

            difficulty = default;
            return false;
        }

        /// <summary>ログ・引数向けの小文字の難易度名 (easy / normal / hard)。</summary>
        public static string DifficultyName(CampaignDifficulty difficulty)
        {
            switch (difficulty)
            {
                case CampaignDifficulty.Easy:
                    return "easy";
                case CampaignDifficulty.Normal:
                    return "normal";
                case CampaignDifficulty.Hard:
                    return "hard";
                default:
                    throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "unknown difficulty");
            }
        }

        public override string ToString()
        {
            return Difficulty.HasValue ? DifficultyName(Difficulty.Value) : "(none)";
        }

        private static bool IsFlag(string arg, string flag)
        {
            return string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>次の引数が値ではなく別のフラグに見えるか (値の書き忘れを検出する)。</summary>
        private static bool IsFlagLike(string arg)
        {
            return string.IsNullOrEmpty(arg) || arg[0] == '-';
        }
    }
}
