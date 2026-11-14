using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ヒットスキャン (Raycast) の共通処理。射撃と視線判定の両方で使い、プレイヤーの射撃と敵の射撃で
    /// 同じダメージの流れ (Raycast → <see cref="Hitbox"/> → <see cref="DamageCalculator"/> → <see cref="Damageable"/>) を通す。
    /// 撃った本人のコライダー (shooterRoot の子孫) とトリガーは無視する。
    /// </summary>
    public static class Hitscan
    {
        /// <summary>1 回の Raycast で調べる命中の上限。これを超える数のコライダーが一直線に並ぶと遠いものを取りこぼす。</summary>
        public const int MaxHits = 32;

        private static readonly RaycastHit[] _buffer = new RaycastHit[MaxHits];
        private static readonly List<INearMissTarget> _nearMissTargets = new List<INearMissTarget>();

        // 相手の OnNearMiss の後に出す。受け皿を持たない仕組み (音・カメラの揺れなど) が、どの弾がどこをかすめたかだけ聞けるように。
        public static event Action<HitscanNearMiss> NearMissed;

        public static IReadOnlyList<INearMissTarget> NearMissTargets => _nearMissTargets;

        /// <exception cref="ArgumentNullException">target が null。</exception>
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

        /// <exception cref="ArgumentNullException">target が null。</exception>
        public static bool UnregisterNearMissTarget(INearMissTarget target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            return _nearMissTargets.Remove(target);
        }

        // Fire の中だけでなく公開するのは、Raycast を自前で行う WeaponController (プレイヤーの射撃) からも同じ判定を通すため。
        // 弾が当たった相手 (struck が相手の子孫) はニアミスではなく被弾として扱うので数えない。
        /// <param name="travelled">弾が進んだ距離 (当たったならそこまで、外れたなら射程)。</param>
        /// <exception cref="ArgumentException">direction が 0。</exception>
        /// <exception cref="ArgumentOutOfRangeException">travelled が 0 以上の有限値でない。</exception>
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

        /// <summary>
        /// origin から direction へ maxDistance まで Raycast し、shooterRoot の子孫とトリガーと、
        /// shooterRoot の弾を通す置いた物 (<see cref="Deployable.LetsThrough(Collider, GameObject)"/>) を除いた最も近い命中を返す。
        /// </summary>
        /// <param name="origin">始点。</param>
        /// <param name="direction">向き。長さは問わないが 0 ではないこと。</param>
        /// <param name="maxDistance">距離。0 より大きい有限値。</param>
        /// <param name="shooterRoot">無視するコライダーの根。null なら何も無視しない。</param>
        /// <param name="hit">最も近い命中。</param>
        public static bool TryFirstHit(Vector3 origin, Vector3 direction, float maxDistance, Transform shooterRoot, out RaycastHit hit)
        {
            if (direction.sqrMagnitude < 1e-12f)
            {
                throw new ArgumentException("direction must not be zero", nameof(direction));
            }

            if (!(maxDistance > 0f) || float.IsInfinity(maxDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(maxDistance), maxDistance, "maxDistance must be positive and finite");
            }

            var count = Physics.RaycastNonAlloc(
                origin,
                direction.normalized,
                _buffer,
                maxDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            hit = default;
            var found = false;
            for (var i = 0; i < count; i++)
            {
                var candidate = _buffer[i];
                if (shooterRoot != null && candidate.collider.transform.IsChildOf(shooterRoot))
                {
                    continue;
                }

                if (shooterRoot != null && Deployable.LetsThrough(candidate.collider, shooterRoot.gameObject))
                {
                    continue;
                }

                if (!found || candidate.distance < hit.distance)
                {
                    hit = candidate;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// 1 発撃つ。最初に当たったコライダーに <see cref="Hitbox"/> があれば <see cref="Hitbox.ApplyHit"/> でダメージを与え、
        /// <c>[Combat] hit zone=.. dmg=.. id=.. hp=..</c> をログに出す。
        /// 壁など Hitbox の無いものに当たるか、持ち主がいないか既に死んでいれば、そこで止まりダメージは 0。
        /// 構えた盾 (<see cref="BulletShield"/>) に当たったときも、そこで止まりダメージは 0 で、建造物への被害も出さない。
        /// 置いた物 (<see cref="Deployable"/>) は、撃った者の弾を通すものなら素通りし、通さないものならそこで止まって耐久で受ける
        /// (<see cref="Deployable.TryStopBullet"/>。結果の <see cref="HitscanResult.Damage"/> は 0)。
        /// Hitbox の無いものに当たったときは <see cref="StructureDamage.Apply"/> に baseDamage と explosionRadius を渡す
        /// (結果の <see cref="HitscanResult.Damage"/> には含めない)。
        /// </summary>
        /// <param name="origin">銃口 (目) の位置。</param>
        /// <param name="direction">撃つ向き。0 ではないこと。</param>
        /// <param name="range">射程。0 より大きい有限値。</param>
        /// <param name="baseDamage">武器の基本ダメージ。0 以上。</param>
        /// <param name="falloff">距離減衰の設定。</param>
        /// <param name="shooterRoot">
        /// 撃った本人。自分のコライダーには当たらない。null でなければ、その位置を撃った者の位置として
        /// <see cref="Damageable.Damaged"/> に渡す。
        /// </param>
        /// <param name="explosionRadius">建造物への被害を広げる半径 (m)。0 以上の有限値。0 なら当たった物だけ。</param>
        /// <param name="weaponId">
        /// 撃った武器の Id。<see cref="CombatEvents.ShotFired"/> と、倒したときの <see cref="CombatEvents.Killed"/> に渡す。null なら空文字。
        /// </param>
        /// <remarks>
        /// 値の検査を通ったら <see cref="CombatEvents.ShotFired"/> と、弾の線の近くにいた相手への <see cref="NearMissed"/>
        /// (<see cref="ReportNearMisses"/>)、何かに当たったら <see cref="CombatEvents.Impact"/> を出す。
        /// <see cref="CombatEvents.HitConfirmed"/> は出さない (撃った本人のクライアントで出すもので、ここを使う敵には要らない)。
        /// </remarks>
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

            // TryFirstHit が向きと射程を検査するので、撃ったことはその後に知らせる (例外になった弾を撃ったことにしない)。
            var found = TryFirstHit(origin, direction, range, shooterRoot, out var hit);
            var shooter = shooterRoot != null ? shooterRoot.gameObject : null;
            CombatEvents.RaiseShotFired(shooter, origin, direction, weaponId);
            ReportNearMisses(origin, direction, found ? hit.distance : range, shooterRoot, found ? hit.collider : null);
            if (!found)
            {
                return HitscanResult.Miss;
            }

            CombatEvents.RaiseImpact(hit.point, hit.normal, hit.collider);
            if (BulletShield.TryBlock(hit.collider, baseDamage, shooterRoot != null ? shooterRoot.position : (Vector3?)null, shooter))
            {
                return new HitscanResult(true, hit, null, 0);
            }

            if (Deployable.TryStopBullet(
                hit.collider, baseDamage, hit.distance, falloff, shooterRoot != null ? shooterRoot.position : (Vector3?)null, direction, shooter))
            {
                return new HitscanResult(true, hit, null, 0);
            }

            var hitbox = Hitbox.FindFor(hit.collider);
            if (hitbox == null)
            {
                // 崩れた破片で倒したときに撃った者の名前でキルフィードに出せるよう、建物にも帰属を渡す。
                using (CombatEvents.Attribute(shooter, weaponId))
                {
                    StructureDamage.Apply(hit.collider, hit.point, baseDamage, explosionRadius);
                }

                return new HitscanResult(true, hit, null, 0);
            }

            var owner = hitbox.Owner;
            if (owner == null)
            {
                Debug.LogError($"{Damageable.LogTag} Hitbox '{hitbox.name}' has no Damageable owner; the hit is ignored", hitbox);
                return new HitscanResult(true, hit, hitbox, 0);
            }

            if (owner.IsDead)
            {
                return new HitscanResult(true, hit, hitbox, 0);
            }

            // 撃たれた側 (敵) が撃った方向へ向かえるよう、撃った者の位置を渡す。
            Vector3? sourcePosition = shooterRoot != null ? shooterRoot.position : (Vector3?)null;
            int damage;
            using (CombatEvents.Attribute(shooter, weaponId))
            {
                damage = hitbox.ApplyHit(baseDamage, hit.distance, falloff, sourcePosition, direction);
            }

            Debug.Log($"{Damageable.LogTag} hit zone={hitbox.Zone} dmg={damage} id={owner.Id} hp={owner.Health.Current}", hitbox);
            return new HitscanResult(true, hit, hitbox, damage);
        }

        /// <summary>
        /// eye から target の狙う点 (<see cref="Damageable.AimPoint"/>) まで遮るものが無いか調べる。
        /// 間に target 以外のコライダーがあれば false (見る側の弾を通す置いた物は数えない)。target が死んでいるか maxDistance より遠ければ false。
        /// </summary>
        /// <param name="eye">目の位置。</param>
        /// <param name="target">見たい相手。null なら false。</param>
        /// <param name="maxDistance">見える距離。0 より大きい有限値。</param>
        /// <param name="viewerRoot">見る側。自分のコライダーは遮るものに数えない。</param>
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

    /// <summary><see cref="Hitscan.Fire"/> の結果。</summary>
    public readonly struct HitscanResult
    {
        /// <summary>何にも当たらなかった結果。</summary>
        public static HitscanResult Miss => default;

        /// <summary>何かに当たったか (壁を含む)。</summary>
        public bool Hit { get; }

        /// <summary>最初に当たった命中。<see cref="Hit"/> が false なら既定値。</summary>
        public RaycastHit RaycastHit { get; }

        /// <summary>当たった部位。Hitbox の無いものに当たったか外れたなら null。</summary>
        public Hitbox Hitbox { get; }

        /// <summary>与えたダメージ。</summary>
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
