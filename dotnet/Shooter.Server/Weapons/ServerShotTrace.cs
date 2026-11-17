using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    internal sealed partial class ServerShotTrace
    {
        private readonly ServerHitboxes _hitboxes;
        private readonly List<ShotHit> _hits = new List<ShotHit>();

        public ServerShotTrace(ServerHitboxes hitboxes)
        {
            _hitboxes = hitboxes ?? throw new ArgumentNullException(nameof(hitboxes));
        }

        public void Trace(RoundSpec round, PlannedRound planned, float tick, int shooterId, Func<int, bool> hittable, List<ShotTraceSegment> segments)
        {
            ArgumentNullException.ThrowIfNull(round);
            ArgumentNullException.ThrowIfNull(segments);
            var maxHits = 1;
            ChooseMaxHits(round, ref maxHits);
            _hits.Clear();
            if (!_hitboxes.Cast(planned.Origin, planned.Direction, round.Radius, round.Range, maxHits, tick, shooterId, hittable, _hits))
            {
                segments.Add(ShotTraceSegment.Miss(planned.Origin, planned.Direction));
                return;
            }

            // 抜けた先の区間は、手前の当たりから同じ向きに続ける
            var origin = planned.Origin;
            var first = segments.Count;
            var passed = 0;
            var damageMultiplier = 1f;
            var passedLast = false;
            foreach (var hit in _hits)
            {
                segments.Add(ShotTraceSegment.WithHit(origin, planned.Direction, hit, damageMultiplier));
                origin = hit.Point;
                passedLast = TryPassThrough(round, hit, planned.Direction, ref passed, ref damageMultiplier);
                if (!passedLast)
                {
                    break;
                }
            }

            // 最後の当たりを抜けた弾は、その先で何にも当たらずに射程まで飛ぶ
            if (passedLast)
            {
                segments.Add(ShotTraceSegment.Miss(origin, planned.Direction, damageMultiplier));
            }

            Ricochet(round, tick, shooterId, hittable, segments, first);
        }

        // 貫通する弾は 1 本の線で複数の当たりを拾う
        static partial void ChooseMaxHits(RoundSpec round, ref int maxHits);

        // 最後の区間が跳ねる面に当たったら、跳ねた先の区間を足す
        partial void Ricochet(RoundSpec round, float tick, int shooterId, Func<int, bool> hittable, List<ShotTraceSegment> segments, int first);
    }

    internal readonly struct ShotTraceSegment
    {
        private ShotTraceSegment(Vector3 origin, Vector3 direction, bool hasHit, ShotHit hit, float damageMultiplier, bool bounced)
        {
            Origin = origin;
            Direction = direction;
            HasHit = hasHit;
            Hit = hit;
            DamageMultiplier = damageMultiplier;
            Bounced = bounced;
        }

        public Vector3 Origin { get; }

        public Vector3 Direction { get; }

        public bool HasHit { get; }

        public ShotHit Hit { get; }

        public float DamageMultiplier { get; }

        // 跳ねた面は着弾の演出だけを出し、弾はそのまま次の区間へ進むので、着弾の効果を与えない。
        public bool Bounced { get; }

        public static ShotTraceSegment Miss(Vector3 origin, Vector3 direction, float damageMultiplier = 1f)
        {
            return new ShotTraceSegment(origin, direction, false, default, damageMultiplier, false);
        }

        public static ShotTraceSegment WithHit(Vector3 origin, Vector3 direction, ShotHit hit, float damageMultiplier = 1f)
        {
            return new ShotTraceSegment(origin, direction, true, hit, damageMultiplier, false);
        }

        public ShotTraceSegment AsBounced()
        {
            return new ShotTraceSegment(Origin, Direction, HasHit, Hit, DamageMultiplier, true);
        }
    }
}
