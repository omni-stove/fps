using Shooter.Core;

namespace Shooter.Runtime
{
    // EnemyPerception は位置しか持たないので、Target を別の相手 (囮など) に差し替えた contributor は、撃つ相手と発見ゲージが見る相手を
    // ここから EnemyAgent に知らせる。知らせないと、知覚は囮を見ているのに弾は元の目標へ飛ぶ。
    public interface IPerceivedTargetContributor : IPerceptionContributor
    {
        Damageable PerceivedTarget { get; }
    }
}
