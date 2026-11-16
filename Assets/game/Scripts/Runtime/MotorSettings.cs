using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    // コンポーネントごとに値を持たせると、オフライン (PlayerController) とネット対戦 (NetworkPlayerMotor) で歩く速さなどが食い違うので、
    // 両方が同じアセット (割り当てが無ければ入口 GameContent の Motor、それも無ければ Resources の ResourcePath、それも無ければ MotorSpec.Default) を読む。
    [CreateAssetMenu(fileName = "MotorSettings", menuName = "Fps/Motor Settings")]
    public sealed class MotorSettings : ScriptableObject
    {
        public const string ResourcePath = "Movement/MotorSettings";

        [SerializeField, Min(0f), Tooltip("歩きの水平速度 (m/s)")]
        private float _walkSpeed = MotorSpec.DefaultWalkSpeed;

        [SerializeField, Min(0f), Tooltip("ダッシュ中の水平速度 (m/s)")]
        private float _sprintSpeed = MotorSpec.DefaultSprintSpeed;

        [SerializeField, Min(0f), Tooltip("しゃがみ中の水平速度 (m/s)。しゃがみ中はダッシュしない")]
        private float _crouchSpeed = MotorSpec.DefaultCrouchSpeed;

        [SerializeField, Min(0f), Tooltip("ジャンプで到達する高さ (m)")]
        private float _jumpHeight = MotorSpec.DefaultJumpHeight;

        [SerializeField, Min(0.01f), Tooltip("重力加速度の大きさ (m/s²)。下向きに適用する")]
        private float _gravity = MotorSpec.DefaultGravity;

        [SerializeField, Range(0f, 1f), Tooltip("空中で水平速度を入力方向へ寄せる 1 ステップあたりの割合")]
        private float _airControl = MotorSpec.DefaultAirControl;

        [SerializeField, Min(0f), Tooltip("ダッシュしていないとき (歩き・しゃがみ・伏せ) に前へ動く速さの倍率")]
        private float _walkForwardScale = MoveDirectionScale.DefaultForward;

        [SerializeField, Min(0f), Tooltip("ダッシュしていないときに後ろへ動く速さの倍率")]
        private float _walkBackwardScale = MoveDirectionScale.DefaultBackward;

        [SerializeField, Min(0f), Tooltip("ダッシュしていないときに左右へ動く速さの倍率")]
        private float _walkSideScale = MoveDirectionScale.DefaultSide;

        [SerializeField, Min(0f), Tooltip("ダッシュ中に前へ動く速さの倍率")]
        private float _sprintForwardScale = MoveDirectionScale.DefaultForward;

        [SerializeField, Min(0f), Tooltip("ダッシュ中に後ろへ動く速さの倍率")]
        private float _sprintBackwardScale = MoveDirectionScale.DefaultBackward;

        [SerializeField, Min(0f), Tooltip("ダッシュ中に左右へ動く速さの倍率")]
        private float _sprintSideScale = MoveDirectionScale.DefaultSide;

        public float WalkSpeed
        {
            get => _walkSpeed;
            set => _walkSpeed = value;
        }

        public float SprintSpeed
        {
            get => _sprintSpeed;
            set => _sprintSpeed = value;
        }

        public float CrouchSpeed
        {
            get => _crouchSpeed;
            set => _crouchSpeed = value;
        }

        public float JumpHeight
        {
            get => _jumpHeight;
            set => _jumpHeight = value;
        }

        public float Gravity
        {
            get => _gravity;
            set => _gravity = value;
        }

        public float AirControl
        {
            get => _airControl;
            set => _airControl = value;
        }

        /// <exception cref="ArgumentOutOfRangeException">取り出すときに、倍率が負か有限でない。</exception>
        public MoveDirectionScale WalkDirection
        {
            get => new MoveDirectionScale(_walkForwardScale, _walkBackwardScale, _walkSideScale);
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                _walkForwardScale = value.Forward;
                _walkBackwardScale = value.Backward;
                _walkSideScale = value.Side;
            }
        }

        /// <exception cref="ArgumentOutOfRangeException">取り出すときに、倍率が負か有限でない。</exception>
        public MoveDirectionScale SprintDirection
        {
            get => new MoveDirectionScale(_sprintForwardScale, _sprintBackwardScale, _sprintSideScale);
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                _sprintForwardScale = value.Forward;
                _sprintBackwardScale = value.Backward;
                _sprintSideScale = value.Side;
            }
        }

        public static MotorSettings Load()
        {
            var fromContent = GameContent.Pick(c => c.Motor);
            return fromContent != null ? fromContent : Resources.Load<MotorSettings>(ResourcePath);
        }

        public static MotorSpec Resolve(MotorSettings assigned)
        {
            var settings = assigned != null ? assigned : Load();
            return settings != null ? settings.CreateSpec() : MotorSpec.Default;
        }

        // 速さを表示や見た目に使うだけの側 (アニメーションのポーズなど) は、設定の誤りで止めずに既定の値で続ける。
        public static MotorSpec ResolveOrDefault(MotorSettings assigned)
        {
            try
            {
                return Resolve(assigned);
            }
            catch (ArgumentOutOfRangeException)
            {
                return MotorSpec.Default;
            }
        }

        public MotorSpec CreateSpec()
        {
            return new MotorSpec(
                _walkSpeed,
                _sprintSpeed,
                _jumpHeight,
                _gravity,
                _airControl,
                _crouchSpeed,
                walkDirection: WalkDirection,
                sprintDirection: SprintDirection);
        }

        public void ResetToDefaults()
        {
            _walkSpeed = MotorSpec.DefaultWalkSpeed;
            _sprintSpeed = MotorSpec.DefaultSprintSpeed;
            _crouchSpeed = MotorSpec.DefaultCrouchSpeed;
            _jumpHeight = MotorSpec.DefaultJumpHeight;
            _gravity = MotorSpec.DefaultGravity;
            _airControl = MotorSpec.DefaultAirControl;
            WalkDirection = MoveDirectionScale.Uniform;
            SprintDirection = MoveDirectionScale.Uniform;
        }
    }
}
