using System;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="Damageable"/> を持たない本体 (ネット対戦のプレイヤー <c>Fps.Net.NetPlayer</c>) が、被弾を演出 (<see cref="CharacterAudio"/>) に知らせる。
    /// この画面で操作しているキャラの被弾は <see cref="CombatEvents.DamageTaken"/> で届くので、<see cref="Hurt"/> はそれ以外のキャラの分だけ出す。
    /// </summary>
    public interface ICharacterHurtSource
    {
        /// <summary>この画面で操作しているキャラか。true の間の被弾は <see cref="CombatEvents.DamageTaken"/> で届く。</summary>
        bool IsLocalPlayer { get; }

        /// <summary>
        /// この画面で操作していないキャラが生きている間にダメージを受けたとき (同期された体力が減ったとき)。減った量を渡す。
        /// </summary>
        event Action<int> Hurt;
    }
}
