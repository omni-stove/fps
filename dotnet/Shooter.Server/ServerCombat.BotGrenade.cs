using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // Bot は投げる要求を送らないので、Bot のモジュールが人と同じ ThrowGrenade の検査を通して投げさせる
    internal sealed partial class ServerCombat
    {
        // 投げられないとき (切ってある・使い切った・投げている最中・空中の手榴弾が多すぎる) は null を返し、
        // 断った警告を人の要求のようにログへ出さない
        internal GrenadeSpec BotGrenade(int playerId)
        {
            if (!_grenadesEnabled || !BotRoster.IsBotId(playerId) || !_fighters.TryGetValue(playerId, out var fighter)
                || _grenades.Count >= SnapshotMessage.MaxGrenades || !_rules.CanFire(playerId))
            {
                return null;
            }

            return fighter.Grenades.FirstReadyExplosive(_now);
        }

        internal bool BotThrowGrenade(int playerId, Vector3 direction, GrenadeSpec spec)
        {
            if (spec == null || !ReferenceEquals(BotGrenade(playerId), spec))
            {
                return false;
            }

            var fighter = _fighters[playerId];
            var before = fighter.Grenades.RemainingOf(spec.Id);
            ThrowGrenade(fighter, direction, spec.Id);
            return fighter.Grenades.RemainingOf(spec.Id) < before;
        }
    }
}
