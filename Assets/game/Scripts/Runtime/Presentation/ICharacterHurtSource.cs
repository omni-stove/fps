using System;

namespace Shooter.Runtime
{
    // この画面で操作しているキャラの被弾は CombatEvents.DamageTaken で届くので、Hurt はそれ以外のキャラの分だけ出す。
    public interface ICharacterHurtSource
    {
        bool IsLocalPlayer { get; }

        event Action<int> Hurt;
    }
}
