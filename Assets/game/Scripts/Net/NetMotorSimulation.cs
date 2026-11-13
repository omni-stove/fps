using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetworkPlayerMotor"/> の 1 tick 分の移動。状態から姿勢を決め、<see cref="PlayerMotor"/> の変位を CharacterController で動かす。
    /// 予測・再シミュレーション・サーバーで同じインスタンスの同じ処理を通すので、同じ状態と入力からは同じ結果になる。
    /// </summary>
    /// <remarks>
    /// しゃがみは押している間だけ。離しても立ったカプセルが頭上に重なれば (<see cref="NetStance.HasHeadroomToStand"/>) しゃがんだまま。
    /// 姿勢の判断も状態 (<see cref="MotorState.IsCrouched"/>) と入力だけから行い、Transform に残った前の tick の姿勢には頼らない
    /// (巻き戻しで状態が飛んでも同じ答えにするため)。
    /// </remarks>
    public sealed class NetMotorSimulation
    {
        private readonly CharacterController _controller;
        private readonly PlayerMotor _motor;

        /// <param name="controller">動かす CharacterController。</param>
        /// <param name="motor">移動量の計算。</param>
        /// <param name="stance">controller の姿勢。</param>
        /// <param name="tickSeconds">1 tick の長さ (秒)。正の有限値。</param>
        /// <param name="killPlaneY">この高さより下に落ちたら <see cref="SpawnPosition"/> に戻す。</param>
        public NetMotorSimulation(CharacterController controller, PlayerMotor motor, NetStance stance, float tickSeconds, float killPlaneY)
        {
            _controller = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
            _motor = motor ?? throw new ArgumentNullException(nameof(motor));
            Stance = stance ?? throw new ArgumentNullException(nameof(stance));

            if (!(tickSeconds > 0f) || float.IsInfinity(tickSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(tickSeconds), tickSeconds, "tickSeconds must be positive and finite");
            }

            if (float.IsNaN(killPlaneY))
            {
                throw new ArgumentOutOfRangeException(nameof(killPlaneY), killPlaneY, "killPlaneY must not be NaN");
            }

            TickSeconds = tickSeconds;
            KillPlaneY = killPlaneY;
            SpawnPosition = controller.transform.position;
        }

        /// <summary>姿勢。</summary>
        public NetStance Stance { get; }

        public float TickSeconds { get; }

        public float KillPlaneY { get; }

        /// <summary><see cref="KillPlaneY"/> より下に落ちたときに戻す位置。</summary>
        public Vector3 SpawnPosition { get; set; }

        /// <summary>
        /// 状態から 1 tick 進める。CharacterController を実際に動かすので、終わったとき本体は結果の位置と姿勢にある。
        /// </summary>
        public MotorState Step(MotorState state, MotorInput input)
        {
            Place(state);
            var crouched = ResolveCrouch(state.IsCrouched, input.Crouch);

            _motor.SetVelocity(NetMath.ToNumerics(state.Velocity));
            var displacement = _motor.Step(
                NetMath.ToNumerics(input.Move), input.Yaw, input.Sprint, crouched, input.Jump, state.IsGrounded, TickSeconds);
            _controller.Move(NetMath.ToUnity(displacement));

            var position = _controller.transform.position;
            var velocity = NetMath.ToUnity(_motor.Velocity);
            var grounded = _controller.isGrounded;

            if (position.y < KillPlaneY)
            {
                position = SpawnPosition;
                velocity = Vector3.zero;
                grounded = false;
                crouched = false;
                Place(new MotorState(position, velocity, grounded, false));
            }

            return new MotorState(position, velocity, grounded, crouched);
        }

        /// <summary>本体を位置と姿勢へ置き、CharacterController がその位置から動くよう物理側にも反映する。</summary>
        public void Place(MotorState state)
        {
            // 同じ姿勢の寸法を毎 tick 当て直すと CharacterController の形状を作り直すことになるので、変わるときだけ当てる
            if (Stance.IsCrouched != state.IsCrouched)
            {
                Stance.Apply(state.IsCrouched);
            }

            PlaceAt(state.Position);
        }

        /// <summary>本体を位置へ置き、CharacterController がその位置から動くよう物理側にも反映する。</summary>
        public void PlaceAt(Vector3 position)
        {
            var body = _controller.transform;
            if (body.position.Equals(position))
            {
                return;
            }

            body.position = position;
            Physics.SyncTransforms();
        }

        private bool ResolveCrouch(bool crouched, bool wantsCrouch)
        {
            if (wantsCrouch == crouched)
            {
                return crouched;
            }

            if (!wantsCrouch && !Stance.HasHeadroomToStand())
            {
                return true;
            }

            Stance.Apply(wantsCrouch);
            return wantsCrouch;
        }
    }
}
