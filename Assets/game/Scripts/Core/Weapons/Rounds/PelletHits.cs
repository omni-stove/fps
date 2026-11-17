using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class PelletHits<TTarget>
    {
        // 1 回の射撃で当たる相手はペレットの数より少ないので、辞書にせず並びを順に探す。並びなら知らせる順も当たった順に揃う。
        private readonly List<PelletHit<TTarget>> _hits = new List<PelletHit<TTarget>>();
        private readonly IEqualityComparer<TTarget> _comparer;

        public PelletHits(IEqualityComparer<TTarget> comparer = null)
        {
            _comparer = comparer ?? EqualityComparer<TTarget>.Default;
        }

        public int Count => _hits.Count;

        public PelletHit<TTarget> this[int index] => _hits[index];

        public void Add(TTarget target, int damage, HitZone zone, DamageOutcome outcome, Vector3? point = null)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "damage must not be negative");
            }

            if (!Enum.IsDefined(typeof(HitZone), zone))
            {
                throw new ArgumentOutOfRangeException(nameof(zone), zone, "zone is not a defined HitZone");
            }

            if (!Enum.IsDefined(typeof(DamageOutcome), outcome))
            {
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "outcome is not a defined DamageOutcome");
            }

            // 与えなかった弾は、ばらばらに撃ったときも知らせていないので、まとめた知らせにも入れない。
            if (outcome == DamageOutcome.Ignored)
            {
                return;
            }

            for (var i = 0; i < _hits.Count; i++)
            {
                if (_comparer.Equals(_hits[i].Target, target))
                {
                    _hits[i] = _hits[i].With(damage, zone, outcome, point);
                    return;
                }
            }

            _hits.Add(new PelletHit<TTarget>(target, damage, zone, outcome, 1, point));
        }

        public void Clear()
        {
            _hits.Clear();
        }
    }

    public readonly struct PelletHit<TTarget>
    {
        public PelletHit(TTarget target, int damage, HitZone zone, DamageOutcome outcome, int pellets, Vector3? point = null)
        {
            Point = point;
            Target = target;
            Damage = damage;
            Zone = zone;
            Outcome = outcome;
            Pellets = pellets;
        }

        public TTarget Target { get; }

        public int Damage { get; }

        public HitZone Zone { get; }

        public DamageOutcome Outcome { get; }

        public int Pellets { get; }

        public Vector3? Point { get; }

        public HitConfirm ToHitConfirm()
        {
            return new HitConfirm(Damage, Zone, Outcome == DamageOutcome.Killed, Outcome == DamageOutcome.Downed, Point);
        }

        internal PelletHit<TTarget> With(int damage, HitZone zone, DamageOutcome outcome, Vector3? point)
        {
            // 足したダメージが int を超えても負にせず、上限で止める。
            var total = (long)Damage + damage;
            return new PelletHit<TTarget>(
                Target,
                total > int.MaxValue ? int.MaxValue : (int)total,
                Rank(zone) > Rank(Zone) ? zone : Zone,
                Rank(outcome) > Rank(Outcome) ? outcome : Outcome,
                Pellets + 1,
                Point ?? point);
        }

        // ヒットマーカーは部位と結果を 1 つしか出せないので、どれか 1 発でも起きたら目立つ方 (弱点・頭、倒した・ダウン) を見せる。
        private static int Rank(HitZone zone)
        {
            switch (zone)
            {
                case HitZone.WeakPoint:
                    return 3;
                case HitZone.Head:
                    return 2;
                case HitZone.Body:
                    return 1;
                default:
                    return 0;
            }
        }

        private static int Rank(DamageOutcome outcome)
        {
            switch (outcome)
            {
                case DamageOutcome.Killed:
                    return 3;
                case DamageOutcome.Downed:
                    return 2;
                case DamageOutcome.Damaged:
                    return 1;
                default:
                    return 0;
            }
        }
    }
}
