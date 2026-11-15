using System;
using System.Collections.Generic;
using System.Numerics;
using Fps.Core;

namespace Fps.Server
{
    /// <summary>
    /// ヒットスキャンの弾 1 本を、撃った人が見ていた tick まで巻き戻した対象と世界 (<see cref="ServerHitboxes"/>) に通し、弾の区間に分ける。
    /// 区間は当たりを多くても 1 つと、その当たりまでに弾が弱った分のダメージの倍率を持ち、<see cref="Fps.Protocol.ShotMessage"/> の弾の並びと着弾の処理にそのまま渡す。
    /// </summary>
    internal sealed partial class ServerShotTrace
    {
        private readonly ServerHitboxes _hitboxes;
        private readonly List<ShotHit> _hits = new List<ShotHit>();

        public ServerShotTrace(ServerHitboxes hitboxes)
        {
            _hitboxes = hitboxes ?? throw new ArgumentNullException(nameof(hitboxes));
        }

        /// <summary>planned を round の太さ・射程で通し、区間を segments の後ろに足す。外れても区間を 1 つ足す。</summary>
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

    /// <summary><see cref="ServerShotTrace"/> が分けた弾の区間 1 本。</summary>
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

        /// <summary><see cref="HasHit"/> が false なら使わない。</summary>
        public ShotHit Hit { get; }

        /// <summary>この区間の当たりに与えるダメージに掛ける倍率 (手前の物を抜けた・面で跳ねた分だけ弱る)。</summary>
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
