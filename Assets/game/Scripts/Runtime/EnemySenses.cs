using System;
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
        /// 1 tick 分の知覚を作る。target が無いか死んでいれば Target は null。
        /// 視線は sightRange 以内のときだけ目の高さから Raycast で調べる (それより遠ければ Raycast しない)。
        /// 位置はどちらも足元 (地面の高さ) で渡し、巡回地点との距離の比較と揃える。
        /// </summary>
        /// <param name="selfGround">敵の足元。</param>
        /// <param name="eye">敵の目の位置。</param>
        /// <param name="target">目標。null 可。</param>
        /// <param name="sightRange">視認距離。0 より大きい有限値。</param>
        /// <param name="selfRoot">敵自身。自分のコライダーは視線を遮らない。</param>
        public static EnemyPerception Perceive(
            Vector3 selfGround,
            Vector3 eye,
            Damageable target,
            float sightRange,
            Transform selfRoot)
        {
            if (!(sightRange > 0f) || float.IsInfinity(sightRange))
            {
                throw new ArgumentOutOfRangeException(nameof(sightRange), sightRange, "sightRange must be positive and finite");
            }

            if (target == null || target.IsDead)
            {
                return new EnemyPerception(selfGround.ToNumerics(), null, false, false, null);
            }

            var targetGround = target.GroundPoint;
            var inRange = Vector3.Distance(selfGround, targetGround) <= sightRange;

            // 距離の判定は足元同士で済ませたので、Raycast は目から狙う点まで (高さの差の分だけ長い) を通す。
            var lineOfSight = inRange
                && Hitscan.HasLineOfSight(eye, target, Vector3.Distance(eye, target.AimPoint) + 0.01f, selfRoot);

            // 音の発生源はまだ無い (プレイヤーの射撃音などは後続で渡す)。
            return new EnemyPerception(selfGround.ToNumerics(), targetGround.ToNumerics(), lineOfSight, false, null);
        }
    }
}
