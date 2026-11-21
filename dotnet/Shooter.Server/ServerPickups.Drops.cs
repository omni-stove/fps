using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    internal sealed partial class ServerPickups
    {
        // 倒れた人の足元に、手に持っていた武器 (そのときの弾数と付けた物のまま) と予備弾を落とす。並べ方はオフラインの DropOnDeath と同じ。
        // 武器は倒れた人から取り上げない。出直すときにロードアウトから持ち直すので、取り上げても次の命には響かず、
        // 倒れた後の持ち物を見る部品 (Extraction の持ち帰り・BR の箱) の数え方を変えないため
        internal IReadOnlyList<int> DropFor(int playerId)
        {
            RequireStarted();
            var dropped = new List<int>();
            if (!_drops.DropsAnything)
            {
                return dropped;
            }

            // 箱を開けて拾うモード (BR・Extraction) は倒れた人の持ち物を箱 (ServerLoot) に入れるので、同じ物を二重に落とさない。
            // オフラインの BattleRoyaleRunner が DropOnDeath の落とし物を空にするのと同じ
            if (_rules.Objective?.UsesLoot == true)
            {
                _log.WriteLine(ServerPickupsLog.NoDrops(playerId, "objective drops a death box"));
                return dropped;
            }

            if (!_context.Bodies.TryGet(playerId, out var body) || !body.HasSpawned)
            {
                _log.WriteLine(ServerPickupsLog.NoDrops(playerId, "no body"));
                return dropped;
            }

            var ground = body.Position + new Vector3(0f, body.Shape.BottomY, 0f);
            var contents = new List<(PickupSpec Spec, bool Touch, ServerHeldWeapon Carried)>(2);
            if (_drops.DropsWeapon)
            {
                var held = _combat.WeaponsOf(playerId).CurrentHeld();
                if (held != null)
                {
                    contents.Add((DroppedWeaponSpec(held), false, held));
                }
            }

            if (_drops.ReserveAmmo > 0)
            {
                contents.Add((PickupSpec.ReserveAmmo(_drops.ReserveAmmo), true, null));
            }

            for (var i = 0; i < contents.Count; i++)
            {
                var (spec, touch, carried) = contents[i];
                dropped.Add(AddDrop(spec, ground + SpreadOffset(i, contents.Count, _drops.SpreadRadius), touch, playerId, carried));
            }

            return dropped;
        }

        // 2 つ以上落とすときは、重ならないよう足元の周りの円に等間隔で並べる (オフラインの DropOnDeath.SpreadOffset と同じ)
        internal static Vector3 SpreadOffset(int index, int count, float radius)
        {
            if (count <= 1 || radius <= 0f)
            {
                return Vector3.Zero;
            }

            var angle = index * MathF.PI * 2f / count;
            return new Vector3(MathF.Cos(angle) * radius, 0f, MathF.Sin(angle) * radius);
        }

        private void HandlePlayerKilled(KillFeedEntry entry)
        {
            DropFor(entry.VictimId);
        }

        // 持ち替えで手放した武器は、拾った物から拾った人の方へ少しずらして置く (オフラインの Pickup.DropWeapon と同じ)
        private void DropSwapped(int playerId, Spot from, ServerHeldWeapon replaced)
        {
            var position = from.Position;
            if (_context.Bodies.TryGet(playerId, out var body))
            {
                var toward = body.Position - position;
                toward.Y = 0f;
                var length = toward.Length();
                if (length > 1e-3f)
                {
                    position += toward / length * MathF.Min(DroppedWeaponOffset, length);
                }
            }

            AddDrop(DroppedWeaponSpec(replaced), position, false, playerId, replaced);
        }

        private int AddDrop(PickupSpec spec, Vector3 position, bool touch, int ownerId, ServerHeldWeapon carried)
        {
            var expiresAt = _drops.LifetimeSeconds > 0f ? _now + _drops.LifetimeSeconds : (float?)null;
            var spot = new Spot(NextItemId(), spec, position, _drops.Reach, touch, null, expiresAt, ownerId, carried, null);
            _spots.Add(spot);
            _log.WriteLine(ServerPickupsLog.Dropped(spot.ItemId, ownerId, spec, position));
            return spot.ItemId;
        }

        // 同じ武器を持つ人が拾えば、落とした武器の弾をすべて予備弾に足す (オフラインの PickupContents.ForDroppedWeapon と同じ)
        private static PickupSpec DroppedWeaponSpec(ServerHeldWeapon held)
        {
            var ammo = (int)Math.Min((long)held.AmmoInMagazine + held.ReserveAmmo, int.MaxValue);
            return PickupSpec.Weapon(held.Spec.Id, ammo);
        }
    }
}
