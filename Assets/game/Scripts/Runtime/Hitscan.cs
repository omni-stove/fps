using System;
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

        /// <summary>
        /// origin から direction へ maxDistance まで Raycast し、shooterRoot の子孫とトリガーを除いた最も近い命中を返す。
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
        /// </summary>
        /// <param name="origin">銃口 (目) の位置。</param>
        /// <param name="direction">撃つ向き。0 ではないこと。</param>
        /// <param name="range">射程。0 より大きい有限値。</param>
        /// <param name="baseDamage">武器の基本ダメージ。0 以上。</param>
        /// <param name="falloff">距離減衰の設定。</param>
        /// <param name="shooterRoot">撃った本人。自分のコライダーには当たらない。</param>
        public static HitscanResult Fire(
            Vector3 origin,
            Vector3 direction,
            float range,
            int baseDamage,
            DamageFalloff falloff,
            Transform shooterRoot)
        {
            if (baseDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseDamage), baseDamage, "baseDamage must not be negative");
            }

            if (falloff == null)
            {
                throw new ArgumentNullException(nameof(falloff));
            }

            if (!TryFirstHit(origin, direction, range, shooterRoot, out var hit))
            {
                return HitscanResult.Miss;
            }

            var hitbox = Hitbox.FindFor(hit.collider);
            if (hitbox == null)
            {
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

            var damage = hitbox.ApplyHit(baseDamage, hit.distance, falloff);
            Debug.Log($"{Damageable.LogTag} hit zone={hitbox.Zone} dmg={damage} id={owner.Id} hp={owner.Health.Current}", hitbox);
            return new HitscanResult(true, hit, hitbox, damage);
        }

        /// <summary>
        /// eye から target の狙う点 (<see cref="Damageable.AimPoint"/>) まで遮るものが無いか調べる。
        /// 間に target 以外のコライダーがあれば false。target が死んでいるか maxDistance より遠ければ false。
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
