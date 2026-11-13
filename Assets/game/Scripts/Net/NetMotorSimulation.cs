using System;
using Fps.Core;
using Fps.Runtime;
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
    /// <para>
    /// カバーはオフラインの <see cref="CoverController"/> と同じ Core の <see cref="CoverState"/> で進める。
    /// 状態 (<see cref="MotorState.Cover"/>) から <see cref="CoverState.Restore"/> で戻してから入力 (<see cref="MotorInput.Cover"/>・<see cref="MotorInput.Aim"/>) で
    /// 1 tick 進めるので、巻き戻して再シミュレーションしても同じ答えになる。カバー中は移動を壁沿いの横移動 (しゃがみの速さ) にし、ダッシュとジャンプを止め、
    /// 低い壁ではしゃがませる (覗いている間は立たせる)。寄せている間は壁へ寄せ、高い壁の端から覗いている間は横に出る。
    /// オフラインと違い、寄せるときに向きは回さない (向きはオーナーの視点操作が決めて入力に乗せるもので、シミュレーションからは書けないため)。
    /// </para>
    /// </remarks>
    public sealed class NetMotorSimulation
    {
        /// <summary>ダウンしてカバーから出た。</summary>
        public const string ReasonDowned = "downed";

        private readonly CharacterController _controller;
        private readonly PlayerMotor _motor;
        private readonly NetCoverSensor _sensor;

        // 状態から毎 tick 戻して使う作業用。tick をまたいで中身を持ち越さない
        private readonly CoverState _cover = new CoverState();

        /// <param name="controller">動かす CharacterController。</param>
        /// <param name="motor">移動量の計算。</param>
        /// <param name="stance">controller の姿勢。</param>
        /// <param name="tickSeconds">1 tick の長さ (秒)。正の有限値。</param>
        /// <param name="killPlaneY">この高さより下に落ちたら <see cref="SpawnPosition"/> に戻す。</param>
        public NetMotorSimulation(CharacterController controller, PlayerMotor motor, NetStance stance, float tickSeconds, float killPlaneY)
            : this(controller, motor, stance, tickSeconds, killPlaneY, Physics.DefaultRaycastLayers)
        {
        }

        /// <param name="controller">動かす CharacterController。</param>
        /// <param name="motor">移動量の計算。</param>
        /// <param name="stance">controller の姿勢。</param>
        /// <param name="tickSeconds">1 tick の長さ (秒)。正の有限値。</param>
        /// <param name="killPlaneY">この高さより下に落ちたら <see cref="SpawnPosition"/> に戻す。</param>
        /// <param name="coverWallMask">カバーにする壁として当たるレイヤー。</param>
        public NetMotorSimulation(
            CharacterController controller, PlayerMotor motor, NetStance stance, float tickSeconds, float killPlaneY, LayerMask coverWallMask)
        {
            _controller = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
            _motor = motor ?? throw new ArgumentNullException(nameof(motor));
            Stance = stance ?? throw new ArgumentNullException(nameof(stance));
            _sensor = new NetCoverSensor(controller, coverWallMask);

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

        /// <summary>移動の設定 (落下ダメージの閾値を含む)。</summary>
        public MotorSpec Spec => _motor.Spec;

        public float TickSeconds { get; }

        public float KillPlaneY { get; }

        /// <summary><see cref="KillPlaneY"/> より下に落ちたときに戻す位置。</summary>
        public Vector3 SpawnPosition { get; set; }

        /// <summary>カバーの壁を調べる部品。体の半径もここから測る。</summary>
        public NetCoverSensor CoverSensor => _sensor;

        /// <summary>
        /// 状態から 1 tick 進める。CharacterController を実際に動かすので、終わったとき本体は結果の位置と姿勢にある。
        /// ダウン中の入力 (<see cref="MotorInput.Downed"/>) は <see cref="MotorInput.Restrained"/> にしてから動かす (重力は効く)。
        /// ダウンするとカバーから出る。
        /// </summary>
        public MotorState Step(MotorState state, MotorInput input)
        {
            input = input.Restrained();
            Place(state);

            var cover = StepCover(state.Cover, input);
            var move = input.Move;
            var sprint = input.Sprint;
            var jump = input.Jump;
            var wantsCrouch = input.Crouch;
            if (cover.IsInCover)
            {
                sprint = false;
                jump = false;
                if (cover.Height == CoverHeight.Low)
                {
                    wantsCrouch = !cover.IsPeeking;
                }
            }

            var crouched = ResolveCrouch(state.IsCrouched, wantsCrouch);
            if (cover.IsInCover)
            {
                // 寄せている途中と覗いている間は動かさない
                move = cover.Phase == CoverPhase.In ? AlongWall(cover.Normal, input.Move.x, input.Yaw, crouched) : Vector2.zero;
            }

            _motor.SetVelocity(NetMath.ToNumerics(state.Velocity));
            var displacement = _motor.Step(
                NetMath.ToNumerics(move), input.Yaw, sprint, crouched, jump, state.IsGrounded, TickSeconds);
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
                cover = NetCoverState.Out;
                Place(new MotorState(position, velocity, grounded, false));
            }

            return new MotorState(position, velocity, grounded, crouched, cover);
        }

        /// <summary>yaw (ラジアン) の向きの水平の単位ベクトル。<see cref="PlayerMotor"/> の前と同じ向き。</summary>
        public static Vector3 YawForward(float yaw)
        {
            return new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        }

        // カバーを 1 tick 進め、寄せる・覗くための横移動を CharacterController に当てる。本体は Place(state) で前の tick の位置にある
        private NetCoverState StepCover(NetCoverState previous, MotorInput input)
        {
            if (!previous.IsInCover && !input.Cover)
            {
                return previous;
            }

            _cover.Restore(previous.Snapshot);
            if (input.Downed)
            {
                _cover.Release(ReasonDowned);
                return new NetCoverState(_cover.Capture(), 0f, CoverEdge.None, 0f);
            }

            var position = _controller.transform.position;
            var appliedPeek = previous.AppliedPeek;
            CoverProbeResult probe;
            float topY;
            if (previous.IsInCover)
            {
                // 端から横に出ている間は、出る前の位置で壁を調べる (出た先にはもう壁が無い)
                var into = -previous.Normal;
                var right = RightAlong(previous.Normal);
                probe = _sensor.Probe(position - (right * appliedPeek), into, into, out topY);
            }
            else
            {
                var forward = YawForward(input.Yaw);
                probe = _sensor.Probe(position, forward, forward, out topY);
            }

            var progressBefore = _cover.EnterProgress;
            var transition = _cover.Step(input.Cover, input.Aim, NetMath.ToNumerics(input.Move), probe, TickSeconds);
            if (!_cover.IsInCover)
            {
                // 横に出たまま出ても戻さない (撃ち合いの最中に勝手に動かさない。CoverController と同じ)
                return new NetCoverState(_cover.Capture(), 0f, CoverEdge.None, 0f);
            }

            var normal = NetMath.ToUnity(_cover.Normal);
            if (transition == CoverTransition.Enter)
            {
                appliedPeek = 0f;
            }
            else if (_cover.Phase == CoverPhase.Entering || (progressBefore < 1f && _cover.EnterProgress >= 1f))
            {
                SlideIn(normal, probe.Distance, progressBefore, _cover.EnterProgress);
            }

            appliedPeek = SlidePeek(normal, appliedPeek, _cover.PeekOffset);
            return new NetCoverState(_cover.Capture(), topY, probe.Edges, appliedPeek);
        }

        // 寄せ終わる時刻までの残りを等分して進むので、最後の tick でちょうど目標の距離になる
        private void SlideIn(Vector3 normal, float distance, float progressBefore, float progressAfter)
        {
            if (progressBefore >= 1f)
            {
                return;
            }

            var fraction = Mathf.Clamp01((progressAfter - progressBefore) / (1f - progressBefore));
            if (fraction <= 0f)
            {
                return;
            }

            var gap = distance - CoverProbe.HoldDistance(_sensor.BodyRadius);
            _controller.Move(-normal * (gap * fraction));
        }

        // 横に障害物があれば出られた分だけを覚え、戻るときに出た分だけ戻す
        private float SlidePeek(Vector3 normal, float applied, float target)
        {
            if (Mathf.Approximately(target, applied))
            {
                return applied;
            }

            var speed = CoverProbe.PeekSideDistance / CoverController.PeekSlideSeconds;
            var step = Mathf.MoveTowards(applied, target, speed * TickSeconds) - applied;
            var right = RightAlong(normal);
            var body = _controller.transform;
            var before = body.position;
            _controller.Move(right * step);
            return applied + Vector3.Dot(body.position - before, right);
        }

        // 壁沿いの横の入力を、yaw で回す前の移動入力 (X 右・Y 前) にする。立ったまま動く (高い壁) ときもしゃがみの速さにする
        private Vector2 AlongWall(Vector3 normal, float side, float yaw, bool crouched)
        {
            var along = RightAlong(normal) * Mathf.Clamp(side, -1f, 1f);
            var forward = YawForward(yaw);
            var right = new Vector3(forward.z, 0f, -forward.x);
            var local = new Vector2(Vector3.Dot(along, right), Vector3.Dot(along, forward));
            return local * SpeedScale(crouched);
        }

        private float SpeedScale(bool crouched)
        {
            var spec = _motor.Spec;
            if (crouched)
            {
                return 1f;
            }

            return spec.WalkSpeed > 0f ? Mathf.Min(1f, spec.CrouchSpeed / spec.WalkSpeed) : 0f;
        }

        private static Vector3 RightAlong(Vector3 normal)
        {
            return NetMath.ToUnity(CoverProbe.RightAlong(NetMath.ToNumerics(normal)));
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
