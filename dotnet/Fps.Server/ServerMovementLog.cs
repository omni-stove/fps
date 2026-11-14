using System.Globalization;
using Fps.Core;

namespace Fps.Server
{
    /// <summary>Unity のサーバー (NetworkPlayerMotor) と同じ行にして、ログを見る手順と監視をそのまま使えるようにする。</summary>
    internal static class ServerMovementLog
    {
        public static string Stance(int playerId, int tick, bool crouched)
        {
            return Head("stance", playerId, tick) + " crouched=" + Bool(crouched);
        }

        public static string Cover(int playerId, int tick, CoverPhase phase, CoverHeight? height)
        {
            var log = Head("cover", playerId, tick) + " phase=" + phase;
            return phase == CoverPhase.Out || !height.HasValue ? log : log + " height=" + height.Value;
        }

        public static string Dodge(int playerId, int tick, bool dodging)
        {
            return Head("dodge", playerId, tick) + " dodging=" + Bool(dodging);
        }

        public static string Vault(int playerId, int tick, bool vaulting)
        {
            return Head("vault", playerId, tick) + " vaulting=" + Bool(vaulting);
        }

        private static string Head(string what, int playerId, int tick)
        {
            return "[Net] " + what + " owner=" + playerId.ToString(CultureInfo.InvariantCulture)
                + " tick=" + tick.ToString(CultureInfo.InvariantCulture);
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
