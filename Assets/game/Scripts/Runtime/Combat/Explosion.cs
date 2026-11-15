using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 1 回の爆発。<see cref="Detonate"/> は半径内の <see cref="Damageable"/> に <see cref="ExplosionDamage"/> を与え (中心から体の狙う点か頭の高さまでのどちらかの線が
    /// <see cref="Damageable"/> を持たない物に遮られていなければ)、<see cref="StructureDamage.ApplyAround"/> で建造物にも範囲で当てる。
    /// 演出は <see cref="PresentationCatalog"/> の爆発 (エフェクトと音) を出し、カタログか素材が無ければ出さない。
    /// </summary>
    public static class Explosion
    {
        /// <summary>爆発のエフェクトを消すまでの秒数。プレハブが自分で消えなかったときの後始末。</summary>
        public const float EffectLifetimeSeconds = 4f;

        /// <summary>
        /// 爆発の音が聞こえなくなる距離 (m)。<see cref="AudioSource.PlayClipAtPoint(AudioClip, Vector3)"/> が作る音源の既定の maxDistance に合わせ、
        /// 字幕 (<see cref="SubtitleFeed"/>) をこれより遠い人には出さない。
        /// </summary>
        public const float AudibleDistance = 500f;

        // 身をかがめて頭だけ出している相手にも当たるよう、体の上端の少し下へも線を引く。
        private const float TopProbeInset = 0.1f;

        /// <summary>
        /// center で爆発させる。半径内の生きている <see cref="Damageable"/> に遮られていなければダメージを与え
        /// (shooter の帰属で、撃った本人も巻き込む)、建造物に範囲で被害を与え、演出を出して <see cref="CombatEvents.RaiseExploded"/> で知らせる。
        /// </summary>
        public static GrenadeExplosion Detonate(Vector3 center, ExplosionSpec explosion, GameObject shooter, string weaponId)
        {
            if (explosion == null)
            {
                throw new ArgumentNullException(nameof(explosion));
            }

            var damage = explosion.Damage;
            var damaged = 0;
            var killed = 0;
            foreach (var target in FindTargets(center, damage.Radius))
            {
                if (TryDamage(target, center, damage, shooter, weaponId, out var killedTarget))
                {
                    damaged++;
                    if (killedTarget)
                    {
                        killed++;
                    }
                }
            }

            int structures;
            using (CombatEvents.Attribute(shooter, weaponId))
            {
                structures = StructureDamage.ApplyAround(center, explosion.StructureDamageAtCenter, damage.Radius);
            }

            var result = new GrenadeExplosion(center, damaged, killed, structures);
            Debug.Log(FormatLog(result));

            PlayEffects(center);
            CombatEvents.RaiseExploded(center, weaponId);
            return result;
        }

        /// <summary>爆発のログ。例: <c>[Grenade] exploded at=(1.0, 0.1, 9.5) damaged=2 killed=1 structures=0</c>。</summary>
        public static string FormatLog(GrenadeExplosion explosion)
        {
            var c = explosion.Center;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} exploded at=({1:0.0}, {2:0.0}, {3:0.0}) damaged={4} killed={5} structures={6}",
                GrenadeThrower.LogTag,
                c.x,
                c.y,
                c.z,
                explosion.Damaged,
                explosion.Killed,
                explosion.StructureHits);
        }

        /// <summary>
        /// center から target までの線が遮られているか。target 自身と、<see cref="Damageable"/> を持つ物 (人や標的) とトリガーは遮らない。
        /// target の狙う点と、体の上端の少し下の 2 本を引き、両方とも遮られていれば true。
        /// </summary>
        public static bool IsObstructed(Vector3 center, Damageable target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (!IsLineObstructed(center, target.AimPoint, target.transform))
            {
                return false;
            }

            if (!target.TryGetBounds(out var bounds))
            {
                return true;
            }

            var top = new Vector3(bounds.center.x, Mathf.Max(bounds.center.y, bounds.max.y - TopProbeInset), bounds.center.z);
            return IsLineObstructed(center, top, target.transform);
        }

        /// <summary>center から target の体 (コライダーを合わせた範囲) のいちばん近い点までの距離。コライダーが無ければ Transform の位置まで。</summary>
        public static float DistanceTo(Vector3 center, Damageable target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var closest = target.TryGetBounds(out var bounds) ? bounds.ClosestPoint(center) : target.transform.position;
            return Vector3.Distance(center, closest);
        }

        /// <summary>
        /// center に <see cref="PresentationCatalog"/> の爆発のエフェクトと音を出し、音を鳴らしたら字幕のために <see cref="SubtitleFeed.Raise"/> で知らせる。
        /// Play 中でない・専用サーバー・カタログか素材が無いときは何もしない。
        /// ネット対戦ではサーバーから爆発を受け取ったクライアント (<c>Fps.Net.NetCombatFeed</c>) も呼ぶ。
        /// </summary>
        public static void PlayEffects(Vector3 center)
        {
            // Edit Mode では時間差で消せず、専用サーバーには見る人がいない。
            if (!Application.isPlaying || NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                return;
            }

            var catalog = PresentationCatalog.Load();
            if (catalog == null)
            {
                return;
            }

            if (catalog.Explosion != null)
            {
                var effect = UnityEngine.Object.Instantiate(catalog.Explosion, center, Quaternion.identity);
                UnityEngine.Object.Destroy(effect, EffectLifetimeSeconds);
            }

            if (catalog.ExplosionSound != null)
            {
                AudioSource.PlayClipAtPoint(catalog.ExplosionSound, center);
                SubtitleFeed.Raise(SoundCueKind.Explosion, center, AudibleDistance);
            }
        }

        private static bool TryDamage(
            Damageable target,
            Vector3 center,
            ExplosionDamage explosion,
            GameObject shooter,
            string weaponId,
            out bool killedTarget)
        {
            killedTarget = false;
            if (target.IsDead)
            {
                return false;
            }

            var distance = DistanceTo(center, target);
            if (!explosion.Reaches(distance))
            {
                return false;
            }

            var obstructed = IsObstructed(center, target);
            var damage = explosion.At(distance, obstructed);
            if (damage <= 0)
            {
                if (obstructed)
                {
                    Debug.Log($"{GrenadeThrower.LogTag} blocked id={target.Id}", target);
                }

                return false;
            }

            // 倒れると体の当たり判定が隠され狙う点が動くので、向きはダメージの前に取る。
            var push = target.AimPoint - center;
            using (CombatEvents.Attribute(shooter, weaponId))
            {
                target.TakeDamage(damage, center, push, DamageKind.Explosion);
            }

            killedTarget = target.IsDead;
            Knockback.Apply(target, DamageKind.Explosion, push, explosion.StrengthAt(distance));
            Debug.Log(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} hit id={1} dmg={2} distance={3:0.0}",
                    GrenadeThrower.LogTag,
                    target.Id,
                    damage,
                    distance),
                target);

            // オフラインで投げるのは手元のプレイヤーだけなので、自分以外に当てたらその画面にヒットマーカーを出す。
            if (shooter != null && !target.transform.IsChildOf(shooter.transform))
            {
                var shooterBody = shooter.GetComponent<Damageable>();
                if (shooterBody != null && shooterBody.IsLocalPlayer)
                {
                    CombatEvents.RaiseHitConfirmed(new HitConfirm(damage, HitZone.Body, killedTarget, downed: false));
                }
            }

            return true;
        }

        /// <summary>燃える範囲 (AreaEffectZone) も爆発と同じ相手を範囲の中とみなすよう、ここで探す。</summary>
        internal static List<Damageable> FindTargets(Vector3 center, float radius)
        {
            var targets = new List<Damageable>();
            var seen = new HashSet<Damageable>();
            foreach (var collider in Physics.OverlapSphere(center, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var damageable = FindDamageable(collider);
                if (damageable != null && seen.Add(damageable))
                {
                    targets.Add(damageable);
                }
            }

            return targets;
        }

        private static Damageable FindDamageable(Collider collider)
        {
            if (collider == null)
            {
                return null;
            }

            var hitbox = Hitbox.FindFor(collider);
            if (hitbox != null && hitbox.Owner != null)
            {
                return hitbox.Owner;
            }

            var damageable = collider.GetComponentInParent<Damageable>();
            return damageable != null ? damageable : null;
        }

        private static bool IsLineObstructed(Vector3 from, Vector3 to, Transform target)
        {
            var offset = to - from;
            var length = offset.magnitude;
            if (length < 1e-4f)
            {
                return false;
            }

            var hits = Physics.RaycastAll(from, offset / length, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                var hitTransform = hit.collider.transform;
                if (hitTransform.IsChildOf(target) || FindDamageable(hit.collider) != null)
                {
                    continue;
                }

                return true;
            }

            return false;
        }
    }
}
