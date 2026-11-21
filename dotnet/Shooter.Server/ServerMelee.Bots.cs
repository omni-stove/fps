using System;
using Shooter.Core;

namespace Shooter.Server
{
    // Bot は入力のメッセージを送らないので、Bot のモジュールが判断 (MeleeEnemyBehavior) の決めた時に人と同じ検査を通して振らせる
    internal sealed partial class ServerMelee
    {
        internal bool IsOn => _combat.Rules.Rules?.Actions.Melee == Melee.On;

        // 判断と同じ振りを読ませるため、Bot の振りの時間はここで持ったものを渡す (振っている間は撃たず、その場で振り終える)
        internal MeleeSwing SwingOf(int playerId)
        {
            return GetOrAdd(playerId).Swing;
        }

        // 断ったときは人の入力と違って理由をログに出さない。判断は届く間ずっと振りたがるので、毎 tick の警告になる
        internal bool BotSwing(int playerId)
        {
            if (!BotRoster.IsBotId(playerId) || _context == null || Check(playerId) != MeleeRefusal.None)
            {
                return false;
            }

            Begin(GetOrAdd(playerId));
            return true;
        }

        // 盾持ちの Bot の殴り。振りの時間は Bot が持つ盾の振り (ShieldEnemyBehavior の MeleeSwing) で進めるので、ここは当たりの瞬間に相手を選んで与えるだけにする。
        // 盾は Bot の攻撃そのものなので、人の近接を切るルール (Melee) では止めない
        internal bool BotBash(int playerId, MeleeSpec spec)
        {
            ArgumentNullException.ThrowIfNull(spec);
            if (!BotRoster.IsBotId(playerId) || _context == null || !_combat.IsHittable(playerId))
            {
                return false;
            }

            Strike(playerId, spec, ServerCombat.BotBodyWeaponId);
            return true;
        }
    }
}
