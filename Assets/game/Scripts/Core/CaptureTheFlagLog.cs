using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Shooter.Core
{
    /// <summary>
    /// 旗取り (<see cref="CaptureTheFlag"/>) の <c>[Mode]</c> 付きログの文言。参加者の出入りや状態・キルは <see cref="ModeLog"/> のものを使い、
    /// ここには旗を持つ・落とす・戻す・運んで点を取ることと、旗の様子・bot の用事を置く。旗と陣営は陣営の ID、人は ID の数字で書き、分からない人は <c>-</c>。
    /// </summary>
    public static class CaptureTheFlagLog
    {
        public const string Tag = ModeLog.Tag;

        /// <example><c>[Mode] flag-taken flag=1 player=0</c></example>
        public static string Taken(Side flag, int playerId)
        {
            return Tag + " flag-taken flag=" + Number(flag.Id) + " player=" + Number(playerId);
        }

        /// <example><c>[Mode] flag-dropped flag=1 player=0</c></example>
        public static string Dropped(Side flag, int playerId)
        {
            return Tag + " flag-dropped flag=" + Number(flag.Id) + " player=" + Number(playerId);
        }

        /// <summary>落ちた旗が台に戻った。味方が触れて戻したなら by= にその人、秒数がたって戻ったなら <c>-</c>。</summary>
        /// <example><c>[Mode] flag-returned flag=0 by=2</c>、秒数で戻ったら <c>[Mode] flag-returned flag=0 by=-</c></example>
        public static string Returned(Side flag, int? playerId)
        {
            return Tag + " flag-returned flag=" + Number(flag.Id) + " by=" + Optional(playerId);
        }

        /// <summary>敵の旗を自陣の台へ運んで点が入った。運んだ人の陣営と、その陣営の今の得点を書く。</summary>
        /// <example><c>[Mode] flag-captured flag=1 player=0 side=0 score=2</c></example>
        public static string Captured(Side flag, int playerId, Side side, double score)
        {
            return Tag + " flag-captured flag=" + Number(flag.Id) + " player=" + Number(playerId)
                + " side=" + Number(side.Id) + " score=" + ModeLog.FormatScore(score);
        }

        /// <summary>両陣営の旗の様子。運ばれていれば運ぶ人、落ちていれば戻るまでの秒数を括弧で添える。</summary>
        /// <example><c>[Mode] flags 0=AtBase 1=Carried(3)</c>、落ちていれば <c>[Mode] flags 0=Dropped(12.5) 1=AtBase</c></example>
        public static string Flags(CaptureTheFlag game)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }

            var text = new StringBuilder(Tag).Append(" flags");
            foreach (var side in CaptureTheFlag.FlagSides)
            {
                var state = game.GetFlagState(side);
                text.Append(' ').Append(Number(side.Id)).Append('=').Append(state);
                switch (state)
                {
                    case FlagState.Carried:
                        text.Append('(').Append(Optional(game.GetCarrierId(side))).Append(')');
                        break;
                    case FlagState.Dropped:
                        text.Append('(').Append((game.GetReturnRemaining(side) ?? 0f).ToString("F1", CultureInfo.InvariantCulture)).Append(')');
                        break;
                }
            }

            return text.ToString();
        }

        /// <summary>bot の用事 (<see cref="CaptureTheFlagBotPlanner.Plan"/> が決めた <see cref="BotErrand"/>) が変わった。用事が無ければ行き先は書かない。</summary>
        /// <example><c>[Mode] errand player=3 kind=TakeFlag at=(4.00, 0.00, 12.00)</c>、用事が無ければ <c>[Mode] errand player=3 kind=None</c></example>
        public static string Errand(int playerId, BotErrandKind kind, Vector3 destination)
        {
            var text = Tag + " errand player=" + Number(playerId) + " kind=" + kind;
            if (kind == BotErrandKind.None)
            {
                return text;
            }

            return text + " at=(" + destination.X.ToString("F2", CultureInfo.InvariantCulture)
                + ", " + destination.Y.ToString("F2", CultureInfo.InvariantCulture)
                + ", " + destination.Z.ToString("F2", CultureInfo.InvariantCulture) + ")";
        }

        private static string Optional(int? value)
        {
            return value.HasValue ? Number(value.Value) : "-";
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
