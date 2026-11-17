using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // プレイヤーの射撃と敵の射撃で同じダメージの流れ (Raycast → Hitbox → DamageCalculator → Damageable) を通すよう、ここにまとめる。
    public static class Hitscan
    {
        // これを超える数のコライダーが一直線に並ぶと遠いものを取りこぼす。
        public const int MaxHits = ShotCast.MaxHits;
        private static readonly List<INearMissTarget> _nearMissTargets = new List<INearMissTarget>();

        // 相手の OnNearMiss の後に出す。受け皿を持たない仕組み (音・カメラの揺れなど) が、どの弾がどこをかすめたかだけ聞けるように。
        public static event Action<HitscanNearMiss> NearMissed;

        public static IReadOnlyList<INearMissTarget> NearMissTargets => _nearMissTargets;

        public static bool RegisterNearMissTarget(INearMissTarget target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (_nearMissTargets.Contains(target))
            {
                return false;
            }

            _nearMissTargets.Add(target);
            return true;
        }

        public static bool UnregisterNearMissTarget(INearMissTarget target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            return _nearMissTargets.Remove(target);
        }

        // Fire の中だけでなく公開するのは、弾を届ける IShotDelivery (プレイヤー・敵・タレットの射撃) からも同じ判定を通すため。
        // 弾が当たった相手 (struck が相手の子孫) はニアミスではなく被弾として扱うので数えない。
        public static int ReportNearMisses(Vector3 origin, Vector3 direction, float travelled, Transform shooterRoot, Collider struck)
        {
            if (direction.sqrMagnitude < 1e-12f)
            {
                throw new ArgumentException("direction must not be zero", nameof(direction));
            }

            if (!(travelled >= 0f) || float.IsInfinity(travelled))
            {
                throw new ArgumentOutOfRangeException(nameof(travelled), travelled, "travelled must be non-negative and finite");
            }

            if (_nearMissTargets.Count == 0)
            {
                return 0;
            }

            // 受け取り側が登録を外したり足したりしても、この弾で調べる相手が変わらないよう写しを回す。
            var targets = _nearMissTargets.ToArray();
            var shooter = shooterRoot != null ? shooterRoot.gameObject : null;
            var count = 0;
            foreach (var target in targets)
            {
                // 消された MonoBehaviour が登録を外さずに残っていたら、ここで外す。
                if (target is UnityEngine.Object unityObject && unityObject == null)
                {
                    _nearMissTargets.Remove(target);
                    continue;
                }

                if (!TryMeasureNearMiss(target, origin, direction, travelled, shooterRoot, shooter, struck, out var nearMiss))
                {
                    continue;
                }

                count++;
                Deliver(target, nearMiss);
            }

            return count;
        }

        private static bool TryMeasureNearMiss(
            INearMissTarget target,
            Vector3 origin,
            Vector3 direction,
            float travelled,
            Transform shooterRoot,
            GameObject shooter,
            Collider struck,
            out HitscanNearMiss nearMiss)
        {
            nearMiss = default;
            var root = target.Root;
            if (root == null)
            {
                return false;
            }

            if (shooterRoot != null && root.IsChildOf(shooterRoot))
            {
                return false;
            }

            if (struck != null && struck.transform.IsChildOf(root))
            {
                return false;
            }

            var radius = target.NearMissRadius;
            if (!(radius > 0f) || !target.TryGetNearMissCenter(out var center))
            {
                return false;
            }

            if (!NearMiss.TryDistance(origin.ToNumerics(), direction.ToNumerics(), travelled, center.ToNumerics(), out var distance, out var along)
                || !(distance < radius))
            {
                return false;
            }

            var closest = origin + (direction.normalized * along);
            nearMiss = new HitscanNearMiss(target, shooter, origin, direction.normalized, closest, distance);
            return true;
        }

        // 受け取り側が投げても弾の処理 (ダメージ) を止めず、ほかの相手にも知らせる。
        private static void Deliver(INearMissTarget target, HitscanNearMiss nearMiss)
        {
            try
            {
                target.OnNearMiss(nearMiss);
            }
            catch (Exception e)
            {
                Debug.LogException(e, target as UnityEngine.Object);
            }

            var handlers = NearMissed;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<HitscanNearMiss>)handler)(nearMiss);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        // ドメインの再読み込みを切った Play で、前の Play の相手と購読が残らないようにする。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetNearMisses()
        {
            _nearMissTargets.Clear();
            NearMissed = null;
        }

        public static bool TryFirstHit(Vector3 origin, Vector3 direction, float maxDistance, Transform shooterRoot, out RaycastHit hit)
        {
            return ShotCast.TryFirst(origin, direction, maxDistance, Physics.DefaultRaycastLayers, shooterRoot, out hit);
        }

        // CombatEvents.HitConfirmed は撃った本人のクライアントで出すもので、ここを使う敵には要らないので出さない。
        public static HitscanResult Fire(
            Vector3 origin,
            Vector3 direction,
            float range,
            int baseDamage,
            DamageFalloff falloff,
            Transform shooterRoot,
            float explosionRadius = 0f,
            string weaponId = null)
        {
            if (baseDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseDamage), baseDamage, "baseDamage must not be negative");
            }

            if (falloff == null)
            {
                throw new ArgumentNullException(nameof(falloff));
            }

            // 建造物に当たったときだけ使う値でも、当たり方によって例外になったりならなかったりしないよう先に検査する。
            if (!(explosionRadius >= 0f) || float.IsInfinity(explosionRadius))
            {
                throw new ArgumentOutOfRangeException(nameof(explosionRadius), explosionRadius, "explosionRadius must be a non-negative finite number");
            }

            // 射程の検査を ShotCast と同じ例外にするため、RoundSpec に渡す前に見る。
            if (!(range > 0f) || float.IsInfinity(range))
            {
                throw new ArgumentOutOfRangeException(nameof(range), range, "range must be positive and finite");
            }

            var request = new ShotRequest(
                shooterRoot != null ? shooterRoot.gameObject : null,
                shooterRoot,
                shooterRoot != null ? shooterRoot.position : (Vector3?)null,
                new RoundSpec(range: range),
                new ImpactSpec(baseDamage, falloff, explosionRadius),
                weaponId,
                Physics.DefaultRaycastLayers);

            // 向きは正規化しない。0 の向きを ShotCast に ArgumentException で弾かせ、ShotFired にも渡された向きのまま出すため。
            return Fire(request, new PlannedRound(origin.ToNumerics(), direction.ToNumerics(), 0));
        }

        public static HitscanResult Fire(ShotRequest request, PlannedRound round)
        {
            var outcome = ShotDeliveries.Deliver(request, round);
            if (!outcome.Hit)
            {
                return HitscanResult.Miss;
            }

            if (outcome.Owner == null || !outcome.WasAlive)
            {
                return new HitscanResult(true, outcome.RaycastHit, outcome.Hitbox, 0);
            }

            Debug.Log(
                $"{Damageable.LogTag} hit zone={outcome.Hitbox.Zone} dmg={outcome.Damage} id={outcome.Owner.Id} hp={outcome.Owner.Health.Current}",
                outcome.Hitbox);
            return new HitscanResult(true, outcome.RaycastHit, outcome.Hitbox, outcome.Damage);
        }

        public static bool HasLineOfSight(Vector3 eye, Damageable target, float maxDistance, Transform viewerRoot)
        {
            if (!(maxDistance > 0f) || float.IsInfinity(maxDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(maxDistance), maxDistance, "maxDistance must be positive and finite");
            }

            if (target == null || target.IsDead)
            {
                return false;
            }

            var toTarget = target.AimPoint - eye;
            var distance = toTarget.magnitude;
            if (distance > maxDistance)
            {
                return false;
            }

            if (distance < 1e-4f)
            {
                return true;
            }

            // 狙う点は相手のコライダーの内側にあるので、通常は相手の表面に当たる。何にも当たらなければ遮るものは無い。
            if (!TryFirstHit(eye, toTarget, distance, viewerRoot, out var hit))
            {
                return true;
            }

            return hit.collider.transform.IsChildOf(target.transform);
        }
    }

    public readonly struct HitscanNearMiss
    {
        public INearMissTarget Target { get; }

        // 撃った者が分からない弾 (shooterRoot が null) もあるので null を許す。
        public GameObject Shooter { get; }

        public Vector3 Origin { get; }

        public Vector3 Direction { get; }

        public Vector3 ClosestPoint { get; }

        public float Distance { get; }

        public HitscanNearMiss(INearMissTarget target, GameObject shooter, Vector3 origin, Vector3 direction, Vector3 closestPoint, float distance)
        {
            Target = target;
            Shooter = shooter;
            Origin = origin;
            Direction = direction;
            ClosestPoint = closestPoint;
            Distance = distance;
        }
    }

    public readonly struct HitscanResult
    {
        public static HitscanResult Miss => default;

        public bool Hit { get; }

        public RaycastHit RaycastHit { get; }

        public Hitbox Hitbox { get; }

        public int Damage { get; }

        public HitscanResult(bool hit, RaycastHit raycastHit, Hitbox hitbox, int damage)
        {
            Hit = hit;
            RaycastHit = raycastHit;
            Hitbox = hitbox;
            Damage = damage;
        }
    }
}
