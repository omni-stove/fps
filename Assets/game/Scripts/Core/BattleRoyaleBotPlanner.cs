using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// バトルロイヤルの bot が戦いのほかにする用事 (<see cref="BotErrand"/>) を決める。状態は持たず、同じ入力なら同じ用事を返す。
    /// 位置は水平面だけで比べ、高さは見ない。
    /// </summary>
    /// <remarks>
    /// 上から順に、最初に当てはまったものを返す。
    /// <list type="number">
    /// <item>今の円の外にいれば、次の円の中へ移る (戦っていても優先する。外にいるとダメージを受けるので)。</item>
    /// <item>今の円の中で searchRadius 以内に、ダウンした分隊の味方がいれば、いちばん近い人を起こしに行く。</item>
    /// <item>武器を持っていなければ、今の円の中でいちばん近い、武器の増える物を取りに行く (遠くても行き、戦っていても優先する。撃てないので)。</item>
    /// <item>次の円の外にいれば、次の円の中へ移る。</item>
    /// <item>今の円の中で searchRadius 以内に拾える物があれば、いちばん近い物を取りに行く。</item>
    /// </list>
    /// 次の円へ移るときは、次の円の中心から半径の <see cref="SafeRadiusFraction"/> 倍の所 (bot のいる側) を目指し、縁で止まって押し出されないようにする。
    /// 距離が同じ候補は番号の小さいほうを選ぶ。
    /// </remarks>
    public static class BattleRoyaleBotPlanner
    {
        /// <summary>ダウンした味方や拾える物を探す既定の半径 (m)。</summary>
        public const float DefaultSearchRadius = 25f;

        /// <summary>次の円へ移るとき、中心から半径のどこまで入るか。</summary>
        public const float SafeRadiusFraction = 0.5f;

        /// <param name="position">bot の足元。</param>
        /// <param name="zone">縮むエリアの今の様子。エリアが無ければ null (どこでも円の中として扱う)。</param>
        /// <param name="armed">武器を持っていれば true。</param>
        /// <param name="pickups">bot が拾えると判定した物。拾っても何も変わらない物は入れない。</param>
        /// <param name="downedSquadmates">ダウンしていて起こせる分隊の味方の足元。</param>
        /// <param name="searchRadius">ダウンした味方と拾える物を探す半径 (m)。0 以上の有限値。</param>
        public static BotErrand Plan(
            Vector3 position,
            ZoneState? zone,
            bool armed,
            IReadOnlyList<BotPickupOption> pickups,
            IReadOnlyList<Vector3> downedSquadmates,
            float searchRadius)
        {
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

            if (zone.HasValue && !InTarget(zone.Value, position))
            {
                return new BotErrand(BotErrandKind.EnterZone, IntoTarget(zone.Value, position), -1, false);
            }

            var pickup = NearestPickup(pickups, position, zone, searchRadius, weaponsOnly: false);
            return pickup >= 0
                ? new BotErrand(BotErrandKind.Collect, pickups[pickup].Position, pickup, false)
                : BotErrand.None;
        }

        /// <summary>position が今の円の中 (境界を含む) なら true。</summary>
        public static bool InCurrent(ZoneState zone, Vector3 position) => zone.Contains(Flat(position));

        /// <summary>position が次の円の中 (境界を含む) なら true。<see cref="ZoneStage.Closed"/> では今の円と同じ。</summary>
        public static bool InTarget(ZoneState zone, Vector3 position)
        {
            var flat = Flat(position);
            return IsFinite(flat) && Vector2.DistanceSquared(flat, zone.TargetCenter) <= zone.TargetRadius * zone.TargetRadius;
        }

        /// <summary>次の円の中心から、position の側へ半径の <see cref="SafeRadiusFraction"/> 倍の所。すでにそれより内側なら position。</summary>
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
