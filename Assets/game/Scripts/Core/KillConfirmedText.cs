using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// キルコンファームド (<see cref="KillConfirmed"/>) の HUD の行と <c>[Mode] killconfirmed</c> のログの文言。
    /// HUD の陣営名は <see cref="ModeHudText.SideName"/>、ログの陣営は ID の数字で書く。
    /// </summary>
    public static class KillConfirmedText
    {
        public const string LogPrefix = ModeLog.Tag + " killconfirmed";

        /// <summary>陣営ごとの確定数の行。</summary>
        /// <example><c>Confirmed(3, 2)</c> は <c>確定 ALPHA 3 / BRAVO 2</c></example>
        public static string Confirmed(int alpha, int bravo)
        {
            return GameText.Format("killConfirmed.confirmed", PerSide(alpha, bravo));
        }

        /// <summary>陣営ごとの阻止数の行。</summary>
        /// <example><c>Denied(1, 0)</c> は <c>阻止 ALPHA 1 / BRAVO 0</c></example>
        public static string Denied(int alpha, int bravo)
        {
            return GameText.Format("killConfirmed.denied", PerSide(alpha, bravo));
        }

        /// <summary>落ちていて拾われるのを待っているタグの数の行。</summary>
        /// <example><c>TagsOnField(2)</c> は <c>落ちているタグ 2</c></example>
        public static string TagsOnField(int count)
        {
            return GameText.Format("killConfirmed.tagsOnField", count);
        }

        /// <summary>killConfirmed の HUD の 3 行 (確定数・阻止数・落ちているタグの数)。</summary>
        public static string[] HudLines(KillConfirmed killConfirmed)
        {
            if (killConfirmed == null)
            {
                throw new ArgumentNullException(nameof(killConfirmed));
            }

            return new[]
            {
                Confirmed(killConfirmed.GetConfirmed(Side.Alpha), killConfirmed.GetConfirmed(Side.Bravo)),
                Denied(killConfirmed.GetDenied(Side.Alpha), killConfirmed.GetDenied(Side.Bravo)),
                TagsOnField(killConfirmed.Tags.Count),
            };
        }

        /// <example><c>[Mode] killconfirmed drop tag=3 owner=2 side=1</c></example>
        public static string DropLog(KillConfirmedTag tag)
        {
            return LogPrefix + " drop" + Describe(tag);
        }

        /// <summary>タグを拾った結果。by は拾った人の ID、side はその陣営。</summary>
        /// <example>
        /// <c>[Mode] killconfirmed confirm tag=3 owner=2 side=1 by=0 by-side=0</c>、
        /// <c>[Mode] killconfirmed deny tag=3 owner=2 side=1 by=4 by-side=1</c>、
        /// <c>[Mode] killconfirmed ignore tag=3 owner=2 side=1 by=4 by-side=1 reason=Gone</c>
        /// </example>
        public static string CollectLog(KillConfirmedTag tag, int collectorId, Side collectorSide, KillConfirmedResult result)
        {
            string action;
            switch (result)
            {
                case KillConfirmedResult.Confirmed:
                    action = " confirm";
                    break;
                case KillConfirmedResult.Denied:
                    action = " deny";
                    break;
                default:
                    action = " ignore";
                    break;
            }

            var text = LogPrefix + action + Describe(tag) + " by=" + Number(collectorId) + " by-side=" + Number(collectorSide.Id);
            return result == KillConfirmedResult.Confirmed || result == KillConfirmedResult.Denied ? text : text + " reason=" + result;
        }

        /// <summary>参加者でない者に拾われ、点も阻止も数えずに無くした。</summary>
        /// <example><c>[Mode] killconfirmed discard tag=3 owner=2 side=1 by=TargetDummy</c></example>
        public static string DiscardLog(KillConfirmedTag tag, string collectorName)
        {
            return LogPrefix + " discard" + Describe(tag) + " by=" + (string.IsNullOrEmpty(collectorName) ? "-" : collectorName);
        }

        /// <example><c>[Mode] killconfirmed expire tag=3 owner=2 side=1</c></example>
        public static string ExpireLog(KillConfirmedTag tag)
        {
            return LogPrefix + " expire" + Describe(tag);
        }

        private static string Describe(KillConfirmedTag tag)
        {
            if (tag == null)
            {
                throw new ArgumentNullException(nameof(tag));
            }

            return " tag=" + Number(tag.Id) + " owner=" + Number(tag.OwnerId) + " side=" + Number(tag.OwnerSide.Id);
        }

        private static string PerSide(int alpha, int bravo)
        {
            return GameText.Resolve(ModeHudText.SideName(Side.Alpha)) + " " + Number(alpha) + " / "
                + GameText.Resolve(ModeHudText.SideName(Side.Bravo)) + " " + Number(bravo);
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
