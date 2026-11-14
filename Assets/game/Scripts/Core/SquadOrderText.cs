using System;

namespace Fps.Core
{
    /// <summary>
    /// 分隊への指示で画面に出す文字。bot の応答の字幕 (「Bot_1: 了解、そこを守る」) と、HUD の指示の印の札。
    /// 語は表 (Resources/Localization/&lt;言語&gt;/squad.json) から <see cref="GameText"/> で今の言語で引く。
    /// </summary>
    public static class SquadOrderText
    {
        /// <summary>応答の字幕の 1 行。{0} が話す bot の名前、{1} が応答。</summary>
        public const string LineKey = "squad.reply.line";

        /// <summary>狙っていた敵が倒れ、次の指示に移ったときの応答。</summary>
        public const string TargetDownKey = "squad.reply.targetDown";

        /// <summary>kind の指示を受けたときの応答の鍵。</summary>
        /// <exception cref="ArgumentOutOfRangeException">kind が指示 (<see cref="BotErrand.IsOrderKind"/>) でない。</exception>
        public static string ReplyKey(BotErrandKind kind)
        {
            RequireOrder(kind);
            return "squad.reply." + kind;
        }

        /// <summary>HUD の kind の指示の印に添える札の鍵。</summary>
        /// <exception cref="ArgumentOutOfRangeException">kind が指示でない。</exception>
        public static string MarkerKey(BotErrandKind kind)
        {
            RequireOrder(kind);
            return "squad.marker." + kind;
        }

        /// <summary>speaker が kind の指示を受けたときの字幕 (例: 「Bot_1: 了解、そこを守る」)。</summary>
        public static string Reply(string speaker, BotErrandKind kind)
        {
            return Line(speaker, ReplyKey(kind));
        }

        /// <summary>speaker の、表の replyKey の応答の字幕。</summary>
        /// <exception cref="ArgumentNullException">replyKey が null。</exception>
        public static string Line(string speaker, string replyKey)
        {
            if (replyKey == null)
            {
                throw new ArgumentNullException(nameof(replyKey));
            }

            return GameText.Format(LineKey, string.IsNullOrWhiteSpace(speaker) ? "?" : speaker.Trim(), GameText.Get(replyKey));
        }

        /// <summary>HUD の kind の指示の印に添える札 (例: 「守れ」)。</summary>
        public static string Marker(BotErrandKind kind)
        {
            return GameText.Get(MarkerKey(kind));
        }

        private static void RequireOrder(BotErrandKind kind)
        {
            if (!BotErrand.IsOrderKind(kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind must be a squad order (Hold, Attack or Follow)");
            }
        }
    }
}
