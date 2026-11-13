using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// サーバーで動かす Bot の、<see cref="EnemyBrain"/> の判断から人と同じ入力への変換。
    /// 判断と今の位置・向き・経路から、<see cref="NetworkPlayerMotor"/> に渡す <see cref="MotorInput"/> と、
    /// <see cref="NetTeamDeathmatch.ServerFire"/> に渡す射撃の始点と向きを作る。
    /// 旋回の上限・照準の誤差・撃つ条件は <see cref="BotDriver"/> が決め、ここでは Unity の型との変換だけを行う。
    /// </summary>
    public sealed class BotInputPlanner
    {
        private readonly BotDriver _driver;

        // 経路を毎 tick 新しいリストにしないよう使い回す。
        private readonly List<System.Numerics.Vector3> _path = new List<System.Numerics.Vector3>();

        /// <param name="spec">設定。</param>
        /// <param name="random">照準の誤差の乱数。テストで固定できるよう外から渡す。</param>
        public BotInputPlanner(BotInputSpec spec, System.Random random)
        {
            _driver = new BotDriver(spec, random);
        }

        public BotInputSpec Spec => _driver.Spec;

        /// <summary>照準の誤差を捨て、次に狙うときに引き直す。リスポーンしたときに呼ぶ。</summary>
        public void ResetAim()
        {
            _driver.ResetAim();
        }

        /// <summary>時間を deltaSeconds 進め、この tick の入力を返す。</summary>
        /// <param name="deltaSeconds">前の tick からの経過秒。普通は <see cref="NetworkPlayerMotor.TickSeconds"/>。</param>
        /// <param name="decision">この tick の判断。</param>
        /// <param name="position">本体の位置。</param>
        /// <param name="eye">目の位置 (<see cref="NetPlayer.EyePosition"/>)。射撃の始点になる。</param>
        /// <param name="yaw">今の向き (Y 軸まわり、ラジアン)。<see cref="MotorInput.Yaw"/> と同じ単位。</param>
        /// <param name="pitch">今の上下の向き (度、正が下)。前の tick の <see cref="BotInput.Pitch"/>。</param>
        /// <param name="corners">MoveTo までの NavMesh の経路の角の点 (NavMeshPath.corners)。null か空ならまっすぐ進む。</param>
        /// <exception cref="ArgumentOutOfRangeException">deltaSeconds・位置・向きが範囲外か有限でないとき。</exception>
        /// <exception cref="ArgumentException">判断か経路の点が有限でないとき。</exception>
        public BotInput Plan(
            float deltaSeconds, EnemyDecision decision, Vector3 position, Vector3 eye, float yaw, float pitch, IReadOnlyList<Vector3> corners)
        {
            _path.Clear();
            if (corners != null)
            {
                for (var i = 0; i < corners.Count; i++)
                {
                    _path.Add(corners[i].ToNumerics());
                }
            }

            var plan = _driver.Plan(
                deltaSeconds, decision, position.ToNumerics(), eye.ToNumerics(), yaw * Mathf.Rad2Deg, pitch, _path);
            var motor = new MotorInput(
                plan.Move.ToUnity(), plan.Yaw * Mathf.Deg2Rad, plan.Sprint, false, plan.Crouch);
            return new BotInput(motor, plan.Pitch, plan.Fire, plan.FireOrigin.ToUnity(), plan.FireDirection.ToUnity());
        }
    }
}
