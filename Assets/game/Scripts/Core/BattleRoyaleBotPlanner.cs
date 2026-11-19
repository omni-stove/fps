using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    public static class BattleRoyaleBotPlanner
    {
        public const float DefaultSearchRadius = 25f;

        // 次の円の縁で止まると押し出されるので、中心から半径のこの割合の所まで入る。
        public const float SafeRadiusFraction = 0.5f;

        // 今の円の外にいるときと武器が無いときは、戦っていても先にする。外ではダメージを受け、武器が無ければ撃てないので。
        // 武器を探すときだけ searchRadius を見ないのも、撃てないままでいさせないため。
        public static BotErrand Plan(
            Vector3 position,
            ZoneState? zone,
            bool armed,
            IReadOnlyList<BotPickupOption> pickups,
            IReadOnlyList<Vector3> downedSquadmates,
            float searchRadius)
        {
            return Plan(position, zone, armed, pickups, downedSquadmates, searchRadius, Array.Empty<Vector3>(), 0f);
        }

        // 補給の箱は武器を拾った後、次の円へ向かう前に取りに行く。箱は次の円の中に落ちるので、向かえば円の中にも入る。
        // 次の円の外に残った古い箱は、行くと円の外で足止めされるので、エリアがあるときは次の円の中の箱だけを選ぶ。
        public static BotErrand Plan(
            Vector3 position,
            ZoneState? zone,
            bool armed,
            IReadOnlyList<BotPickupOption> pickups,
            IReadOnlyList<Vector3> downedSquadmates,
            float searchRadius,
            IReadOnlyList<Vector3> crates,
            float crateSearchRadius)
        {
            if (crates == null)
            {
                throw new ArgumentNullException(nameof(crates));
            }

            if (!(crateSearchRadius >= 0f) || float.IsInfinity(crateSearchRadius))
            {
                throw new ArgumentOutOfRangeException(nameof(crateSearchRadius), crateSearchRadius, "crateSearchRadius must be a non-negative finite number");
            }

            if (pickups == null)
            {
                throw new ArgumentNullException(nameof(pickups));
            }

            if (downedSquadmates == null)
            {
                throw new ArgumentNullException(nameof(downedSquadmates));
            }

            if (!(searchRadius >= 0f) || float.IsInfinity(searchRadius))
            {
                throw new ArgumentOutOfRangeException(nameof(searchRadius), searchRadius, "searchRadius must be a non-negative finite number");
            }

            if (zone.HasValue && !InCurrent(zone.Value, position))
            {
                return new BotErrand(BotErrandKind.EnterZone, IntoTarget(zone.Value, position), -1, true);
            }

            var squadmate = Nearest(downedSquadmates, position, zone, searchRadius);
            if (squadmate >= 0)
            {
                return new BotErrand(BotErrandKind.Revive, downedSquadmates[squadmate], squadmate, false);
            }

            if (!armed)
            {
                var weapon = NearestPickup(pickups, position, zone, float.PositiveInfinity, weaponsOnly: true);
                if (weapon >= 0)
                {
                    return new BotErrand(BotErrandKind.Collect, pickups[weapon].Position, weapon, true);
                }
            }

            var crate = NearestCrate(crates, position, zone, crateSearchRadius);
            if (crate >= 0)
            {
                return new BotErrand(BotErrandKind.OpenCrate, crates[crate], crate, false);
            }

            if (zone.HasValue && !InTarget(zone.Value, position))
            {
                return new BotErrand(BotErrandKind.EnterZone, IntoTarget(zone.Value, position), -1, false);
            }

            var pickup = NearestPickup(pickups, position, zone, searchRadius, weaponsOnly: false);
            return pickup >= 0
                ? new BotErrand(BotErrandKind.Collect, pickups[pickup].Position, pickup, false)
                : BotErrand.None;
        }

        public static bool InCurrent(ZoneState zone, Vector3 position) => zone.Contains(Flat(position));

        public static bool InTarget(ZoneState zone, Vector3 position)
        {
            var flat = Flat(position);
            return IsFinite(flat) && Vector2.DistanceSquared(flat, zone.TargetCenter) <= zone.TargetRadius * zone.TargetRadius;
        }

        public static Vector3 IntoTarget(ZoneState zone, Vector3 position)
        {
            var flat = Flat(position);
            var center = zone.TargetCenter;
            var reach = zone.TargetRadius * SafeRadiusFraction;
            var offset = flat - center;
            var distance = offset.Length();
            if (!IsFinite(flat) || !(distance > reach))
            {
                return IsFinite(flat) ? position : new Vector3(center.X, 0f, center.Y);
            }

            var point = center + (offset * (reach / distance));
            return new Vector3(point.X, position.Y, point.Y);
        }

        private static int Nearest(IReadOnlyList<Vector3> points, Vector3 from, ZoneState? zone, float radius)
        {
            var best = -1;
            var bestDistance = float.PositiveInfinity;
            var limit = radius * radius;
            for (var i = 0; i < points.Count; i++)
            {
                if (zone.HasValue && !InCurrent(zone.Value, points[i]))
                {
                    continue;
                }

                var distance = Vector2.DistanceSquared(Flat(from), Flat(points[i]));
                if (distance <= limit && distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static int NearestCrate(IReadOnlyList<Vector3> crates, Vector3 from, ZoneState? zone, float radius)
        {
            var best = -1;
            var bestDistance = float.PositiveInfinity;
            var limit = radius * radius;
            for (var i = 0; i < crates.Count; i++)
            {
                if (zone.HasValue && !(InCurrent(zone.Value, crates[i]) && InTarget(zone.Value, crates[i])))
                {
                    continue;
                }

                var distance = Vector2.DistanceSquared(Flat(from), Flat(crates[i]));
                if (distance <= limit && distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static int NearestPickup(IReadOnlyList<BotPickupOption> pickups, Vector3 from, ZoneState? zone, float radius, bool weaponsOnly)
        {
            var best = -1;
            var bestDistance = float.PositiveInfinity;
            var limit = float.IsPositiveInfinity(radius) ? float.PositiveInfinity : radius * radius;
            for (var i = 0; i < pickups.Count; i++)
            {
                var option = pickups[i];
                if ((weaponsOnly && !option.GivesWeapon) || (zone.HasValue && !InCurrent(zone.Value, option.Position)))
                {
                    continue;
                }

                var distance = Vector2.DistanceSquared(Flat(from), Flat(option.Position));
                if (distance <= limit && distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static Vector2 Flat(Vector3 position) => new Vector2(position.X, position.Z);

        private static bool IsFinite(Vector2 value) =>
            !float.IsNaN(value.X) && !float.IsNaN(value.Y) && !float.IsInfinity(value.X) && !float.IsInfinity(value.Y);
    }
}
