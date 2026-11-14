using System;
using System.Globalization;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>分隊への指示 (<see cref="BotErrand.IsOrder"/>) の <c>[Squad]</c> 付きログの文言。</summary>
    public static class SquadOrderLog
    {
        public const string Tag = "[Squad]";

        /// <summary>指示する人の設定。</summary>
        /// <example><c>[Squad] spec hold=0.50 distance=150.0 nearby=30.0 follow=3.0 leash=15.0</c></example>
        public static string Spec(SquadOrderSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} spec hold={1:0.00} distance={2:0.0} nearby={3:0.0} follow={4:0.0} leash={5:0.0}",
                Tag,
                spec.HoldSeconds,
                spec.MaxDistance,
                spec.NearbyRadius,
                spec.FollowDistance,
                spec.LeashDistance);
        }

        /// <summary>指示を出した。target は狙わせる敵の名前 (無ければ null)、squads は分隊のあるモードか。</summary>
        /// <example><c>[Squad] order kind=Hold target=none at=(1.00, 0.00, 10.00) recipients=2 squads=false</c></example>
        public static string Issued(BotErrandKind kind, string target, Vector3 at, int recipients, bool squads)
        {
            return Tag + " order kind=" + kind + " target=" + Token(target) + " at=" + Point(at)
                + " recipients=" + recipients.ToString(CultureInfo.InvariantCulture) + " squads=" + (squads ? "true" : "false");
        }

        /// <summary>指示を出そうとして出さなかった。</summary>
        /// <example><c>[Squad] refused reason=NoRecipients</c></example>
        public static string Refused(SquadOrderRefusal reason)
        {
            return Tag + " refused reason=" + reason;
        }

        /// <summary>bot が指示を受けた。</summary>
        /// <example><c>[Squad] accepted id=Bot_1 kind=Attack target=Dummy_0 at=(1.00, 0.00, 10.00)</c></example>
        public static string Accepted(string botId, BotErrandKind kind, string target, Vector3 at)
        {
            return Tag + " accepted id=" + Token(botId) + " kind=" + kind + " target=" + Token(target) + " at=" + Point(at);
        }

        /// <summary>bot が守る場所に着いた。</summary>
        /// <example><c>[Squad] arrived id=Bot_1 at=(1.00, 0.00, 10.00)</c></example>
        public static string Arrived(string botId, Vector3 at)
        {
            return Tag + " arrived id=" + Token(botId) + " at=" + Point(at);
        }

        /// <summary>bot が指示された敵を狙う目標にした。</summary>
        /// <example><c>[Squad] engage id=Bot_1 target=Dummy_0</c></example>
        public static string Engage(string botId, string target)
        {
            return Tag + " engage id=" + Token(botId) + " target=" + Token(target);
        }

        /// <summary>bot の指示が終わった (reason は理由の名前。狙う敵が倒れた・自分が倒れた など)。next は次の指示 (無ければ None)。</summary>
        /// <example><c>[Squad] done id=Bot_1 kind=Attack reason=TargetGone next=Follow</c></example>
        public static string Done(string botId, BotErrandKind kind, string reason, BotErrandKind next)
        {
            return Tag + " done id=" + Token(botId) + " kind=" + kind + " reason=" + Token(reason) + " next=" + next;
        }

        /// <summary>HUD で指示の印が画面に出たか、画面の中と端を行き来した。</summary>
        /// <example><c>[Squad] screen kind=Hold onScreen=true edge=None at=(0.50, 0.62)</c></example>
        public static string Screen(BotErrandKind kind, PingScreenPlacement placement)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} screen kind={1} onScreen={2} edge={3} at=({4:0.00}, {5:0.00})",
                Tag,
                kind,
                placement.IsOnScreen ? "true" : "false",
                placement.Edge,
                placement.X,
                placement.Y);
        }

        /// <summary>HUD に指示の印を出し始めた。view は出す部品の名前。</summary>
        /// <example><c>[Squad] view=SquadOrderMarkerView</c></example>
        public static string View(string view)
        {
            return Tag + " view=" + Token(view);
        }

        private static string Point(Vector3 point)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.00}, {1:0.00}, {2:0.00})", point.X, point.Y, point.Z);
        }

        // 名前に空白があってもログの区切りが崩れないよう、空白は _ に、空なら none にする。
        private static string Token(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "none";
            }

            var chars = text.Trim().ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (char.IsWhiteSpace(chars[i]))
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }
    }
}
