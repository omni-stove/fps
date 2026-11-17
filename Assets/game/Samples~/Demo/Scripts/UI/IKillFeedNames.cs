using Shooter.Core;

namespace Shooter.Runtime
{
    // Id の意味がオフライン (CombatEvents.OfflineId) とオンライン (プレイヤー Id) で違うので、それぞれが実装する。
    public interface IKillFeedNames
    {
        KillFeedLine Describe(KillFeedEntry entry);
    }
}
