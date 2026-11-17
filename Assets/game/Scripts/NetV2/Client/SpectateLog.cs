using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    /// <summary>ネット対戦のクライアントの観戦 (<see cref="ClientSpectate"/>) のログの行。</summary>
    public static class SpectateLog
    {
        public const string Tag = "[Spectate]";

        /// <summary>観戦の状態が変わった。status が null なら観戦をやめた。</summary>
        public static string Changed(SpectateStatus? status)
        {
            return status.HasValue ? Tag + " net " + status.Value : Tag + " net stopped";
        }

        /// <summary>見る相手を替えた知らせを送った (sent が false なら、つながっていなくて送れなかった)。</summary>
        public static string Requested(SpectateStep step, bool sent)
        {
            return Tag + " net request step=" + step + (sent ? string.Empty : " (not connected; not sent)");
        }
    }
}
