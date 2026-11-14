using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 敵の知覚 (<see cref="EnemyPerception"/>) をシーンから組み立てる。判断は <see cref="EnemyBrain"/> が行い、
    /// ここでは位置と視線 (Raycast) を測るだけ。
    /// </summary>
    public static class EnemySenses
    {
        /// <summary>
        /// self の陣営から見た、いちばん近い生きている敵を members から選ぶ。体力 (<see cref="Damageable"/>) の無い相手と self 自身は選ばない。
        /// 同じ陣営の誰かが印を付けた敵 (<see cref="PingController.IsMarkedFor"/>) がいれば、その中でいちばん近いものを先に選ぶ。
        /// いなければ null。距離は足元 (<see cref="SideMember.GroundPosition"/>) 同士で測り、判定は <see cref="HostileSelector"/> に任せる。
        /// </summary>
        /// <param name="self">狙う側。</param>
        /// <param name="members">候補 (シーンの <see cref="SideMember"/> など)。null の要素は飛ばす。</param>
        public static SideMember SelectHostile(SideMember self, IReadOnlyList<SideMember> members)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }

            if (members == null)
            {
                throw new ArgumentNullException(nameof(members));
            }

            var targets = new List<SideMember>(members.Count);
            List<SideMember> marked = null;
            foreach (var member in members)
            {
                if (member != null && member != self && member.Damageable != null)
                {
                    targets.Add(member);
                    if (PingController.IsMarkedFor(self.Side, member.Damageable))
                    {
                        (marked ?? (marked = new List<SideMember>())).Add(member);
                    }
                }
            }

            // 印の付いた敵が全員倒れているか同じ陣営なら、ふだんどおりいちばん近い敵を選ぶ。
            var preferred = marked != null ? SideMember.NearestHostile(self.Side, self.GroundPosition, marked) : null;
            return preferred != null ? preferred : SideMember.NearestHostile(self.Side, self.GroundPosition, targets);
        }

        // 置いたタレットは SideMember を持たない (持たせるとモードの参加者になる) ので、陣営で選んだ相手やタグの相手と並べて、
        // より近い敵のタレットがあればそちらを返す。同じ距離なら current を選ぶ。
        public static Damageable PreferNearerTurret(
            GameObject self, Side? selfSide, Vector3 selfGround, Damageable current, Vector3 currentGround, IReadOnlyList<Turret> turrets)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }

            if (turrets == null)
            {
                throw new ArgumentNullException(nameof(turrets));
            }

            var choices = new List<Damageable>(turrets.Count + 1);
            var options = new List<TargetOption>(turrets.Count + 1);
            if (current != null)
            {
                choices.Add(current);
                options.Add(new TargetOption(true, currentGround.ToNumerics(), !current.IsDead));
            }

            foreach (var turret in turrets)
            {
                var damageable = turret != null && turret.IsArmed ? turret.Damageable : null;
                if (damageable == null)
                {
                    continue;
                }

                var owner = turret.Owner;
                var isOwner = owner != null && self.transform.IsChildOf(owner.transform);
                choices.Add(damageable);
                options.Add(new TargetOption(
                    TurretTargeting.IsHostile(isOwner, turret.Side, selfSide, true),
                    damageable.GroundPoint.ToNumerics(),
                    !damageable.IsDead));
            }

            var index = TurretTargeting.Nearest(selfGround.ToNumerics(), options);
            return index >= 0 ? choices[index] : current;
        }

        /// <summary>
        /// 1 tick 分の知覚を作る。target が無いか死んでいれば Target は null。
        /// 視線は sightRange 以内のときだけ目の高さから Raycast で調べる (それより遠ければ Raycast しない)。
        /// 位置はどちらも足元 (地面の高さ) で渡し、巡回地点との距離の比較と揃える。
        /// </summary>
        /// <param name="selfGround">敵の足元。</param>
        /// <param name="eye">敵の目の位置。</param>
        /// <param name="target">目標。null 可。</param>
        /// <param name="sightRange">視認距離。0 より大きい有限値。</param>
        /// <param name="selfRoot">敵自身。自分のコライダーは視線を遮らない。</param>
        /// <param name="noisePosition">
        /// この tick に聞いた物音の位置 (撃たれたときの撃った者の位置など)。null なら物音は無い。
        /// </param>
        /// <param name="wasHit">前の知覚からこの知覚までに撃たれたか。</param>
        /// <param name="hitSource">撃った者の位置。分からなければ null。wasHit が false なら使わない。</param>
        /// <param name="healthFraction">今の体力の割合。0 以上 1 以下。</param>
        public static EnemyPerception Perceive(
            Vector3 selfGround,
            Vector3 eye,
            Damageable target,
            float sightRange,
            Transform selfRoot,
            Vector3? noisePosition = null,
            bool wasHit = false,
            Vector3? hitSource = null,
            float healthFraction = 1f)
        {
            if (!(sightRange > 0f) || float.IsInfinity(sightRange))
            {
                throw new ArgumentOutOfRangeException(nameof(sightRange), sightRange, "sightRange must be positive and finite");
            }

            if (!(healthFraction >= 0f) || healthFraction > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(healthFraction), healthFraction, "healthFraction must be between 0 and 1");
            }

            var heardNoise = noisePosition.HasValue;
            System.Numerics.Vector3? noise = heardNoise ? noisePosition.Value.ToNumerics() : (System.Numerics.Vector3?)null;
            System.Numerics.Vector3? source = wasHit && hitSource.HasValue ? hitSource.Value.ToNumerics() : (System.Numerics.Vector3?)null;

            if (target == null || target.IsDead)
            {
                return new EnemyPerception(selfGround.ToNumerics(), null, false, heardNoise, noise, wasHit, source, healthFraction);
            }

            var targetGround = target.GroundPoint;
            var inRange = Vector3.Distance(selfGround, targetGround) <= sightRange;

            // 距離の判定は足元同士で済ませたので、Raycast は目から狙う点まで (高さの差の分だけ長い) を通す。
            var lineOfSight = inRange
                && Hitscan.HasLineOfSight(eye, target, Vector3.Distance(eye, target.AimPoint) + 0.01f, selfRoot);

            return new EnemyPerception(
                selfGround.ToNumerics(), targetGround.ToNumerics(), lineOfSight, heardNoise, noise, wasHit, source, healthFraction);
        }
    }
}
