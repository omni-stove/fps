using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class CampaignLaunchOptions
    {
        public const string DifficultyFlag = "-difficulty";

        private static readonly CampaignDifficulty[] _difficulties =
        {
            CampaignDifficulty.Easy, CampaignDifficulty.Normal, CampaignDifficulty.Hard,
        };

        public static CampaignLaunchOptions None { get; } = new CampaignLaunchOptions(null);

        public CampaignDifficulty? Difficulty { get; }

        private CampaignLaunchOptions(CampaignDifficulty? difficulty)
        {
            Difficulty = difficulty;
        }

        public static CampaignLaunchOptions WithDifficulty(CampaignDifficulty difficulty)
        {
            if (!Enum.IsDefined(typeof(CampaignDifficulty), difficulty))
            {
                throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "unknown difficulty");
            }

            return new CampaignLaunchOptions(difficulty);
        }

        // 知らない引数を拒まないのは、Unity 自身の -batchmode やネットワークの -mode なども同じ引数に並ぶため。
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

        // 値を書き忘れたとき、次のフラグを値として読まないようにする。
        private static bool IsFlagLike(string arg)
        {
            return string.IsNullOrEmpty(arg) || arg[0] == '-';
        }
    }
}
