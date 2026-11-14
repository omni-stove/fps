using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetworkPlayerMotor"/> の 1 tick 分の移動。計算は Core の <see cref="CharacterMotorSimulation"/> に任せ、
    /// 当たりは CharacterController と Unity の物理ではなく <c>.fpsmap</c> の世界 (<see cref="IWorldCollision"/>) で取る。
    /// .NET のサーバーと同じコードと同じ形で動かし、予測・再シミュレーション・サーバーの答えを揃えるため。
    /// </summary>
    /// <remarks>
    /// Transform・CharacterController の寸法・目・見た目は、計算の結果を書き写すだけにする (<see cref="NetStance"/>)。
    /// 書き写した後に Physics.SyncTransforms を呼ぶのは、射撃や爆発など Unity の物理で本体のコライダーを探す側が同じフレームで新しい位置を見るため。
    /// </remarks>
    public sealed class NetMotorSimulation
    {
        public const string ReasonDowned = CharacterMotorSimulation.ReasonDowned;

        private readonly Transform _root;
        private readonly CharacterMotorSimulation _core;
        private readonly DodgeMotion _dodge;

        /// <param name="controller">見た目を書き写す本体。移動には使わない。</param>
        /// <param name="motor">移動量の計算。</param>
        /// <param name="stance">controller の姿勢。寸法は Core の体にも使う。</param>
        /// <param name="world">当たりを取る世界。</param>
        /// <param name="mover">カプセルの移動の設定 (坂・段差・skin・レイヤー)。</param>
        /// <param name="tickSeconds">1 tick の長さ (秒)。正の有限値。</param>
        /// <param name="killPlaneY">この高さより下に落ちたら <see cref="SpawnPosition"/> に戻す。</param>
        public NetMotorSimulation(
            CharacterController controller,
            PlayerMotor motor,
            NetStance stance,
            IWorldCollision world,
            CapsuleMoverSpec mover,
            float tickSeconds,
            float killPlaneY)
        {
            _root = controller != null ? controller.transform : throw new ArgumentNullException(nameof(controller));
            if (motor == null)
            {
                throw new ArgumentNullException(nameof(motor));
            }

            Stance = stance ?? throw new ArgumentNullException(nameof(stance));
            Body = new CharacterBody(world, mover, stance.Geometry);
            _core = new CharacterMotorSimulation(Body, motor, tickSeconds, killPlaneY, ProneSpec.Default);
            _dodge = new DodgeMotion(motor.Spec);
            SpawnPosition = _root.position;
        }

        public NetStance Stance { get; }

        /// <summary>Core の体。位置は最後に動かした (置いた) 状態にある。</summary>
        public CharacterBody Body { get; }

        public MotorSpec Spec => _core.Spec;

        public float TickSeconds => _core.TickSeconds;

        public float KillPlaneY => _core.KillPlaneY;

        public Vector3 SpawnPosition
        {
            get => NetMath.ToUnity(_core.SpawnPosition);
            set => _core.SpawnPosition = NetMath.ToNumerics(value);
        }

        public float BodyRadius => Body.Radius;

        public float DodgeDistance => _dodge.Distance;

        public float DodgeSeconds => _dodge.Seconds;

        public float DodgeCooldownSeconds => _dodge.CooldownSeconds;

        public VaultProbeResult LastVaultProbe => _core.LastVaultProbe;

        /// <summary>つかまれるはしご。サーバーと同じ並び (<see cref="NetMapWorld.Ladders"/>) を渡す。</summary>
        public System.Collections.Generic.IReadOnlyList<MapLadder> Ladders
        {
            get => _core.Ladders;
            set => _core.Ladders = value;
        }

        public static Vector3 YawForward(float yaw)
        {
            return NetMath.ToUnity(CharacterMotorSimulation.YawForward(yaw));
        }

        public MotorState Step(MotorState state, MotorInput input)
        {
            var next = MotorState.FromCore(_core.Step(state.ToCore(), input.ToCore()));
            Show(next.Position, next.Stance);
            return next;
        }

        public void Place(MotorState state)
        {
            _core.Place(state.ToCore());
            Show(state.Position, state.Stance);
        }

        /// <summary>体が今の位置で立ち上がれるか。持ち主のしゃがみの自動解除 (<see cref="CrouchInput"/>) がサーバーと同じ答えを使うため。</summary>
        public bool HasHeadroomToStand()
        {
            return Body.HasHeadroomToStand();
        }

        private void Show(Vector3 position, PlayerStance stance)
        {
            if (Stance.Stance != stance)
            {
                Stance.Apply(stance);
            }

            if (_root.position.Equals(position))
            {
                return;
            }

            _root.position = position;
            Physics.SyncTransforms();
        }
    }
}
