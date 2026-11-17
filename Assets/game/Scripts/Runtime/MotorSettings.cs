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

        [Header("落下・回避・乗り越え・崖のぼり")]
        [SerializeField, Min(0f), Tooltip("着地したときにこの落下速さ (m/s) 以下ならダメージを受けない。落下ダメージのルールが On のときだけ使う")]
        private float _safeFallSpeed = MotorSpec.DefaultSafeFallSpeed;

        [SerializeField, Min(0f), Tooltip("着地したときにこの落下速さ (m/s) 以上なら倒れる。ダメージを受けない落下速さより大きくする")]
        private float _lethalFallSpeed = MotorSpec.DefaultLethalFallSpeed;

        [SerializeField, Min(0f), Tooltip("回避 1 回で動く水平距離 (m)")]
        private float _dodgeDistance = MotorSpec.DefaultDodgeDistance;

        [SerializeField, Min(0.01f), Tooltip("回避 1 回にかかる秒数")]
        private float _dodgeSeconds = MotorSpec.DefaultDodgeSeconds;

        [SerializeField, Min(0f), Tooltip("回避が終わってから次の回避を始められるまでの秒数")]
        private float _dodgeCooldownSeconds = MotorSpec.DefaultDodgeCooldownSeconds;

        [SerializeField, Min(0f), Tooltip("乗り越えられる障害物の、足元からの最大の高さ (m)")]
        private float _vaultMaxHeight = MotorSpec.DefaultVaultMaxHeight;

        [SerializeField, Min(0.01f), Tooltip("乗り越える速さ (m/s)。厚い台の上に乗る乗り越えもこの速さ")]
        private float _vaultSpeed = MotorSpec.DefaultVaultSpeed;

        [SerializeField, Min(0f), Tooltip("乗り越え・よじ登りが届く、体の中心から障害物の手前の面までの水平距離 (m)")]
        private float _vaultReach = MotorSpec.DefaultVaultReach;

        [SerializeField, Range(0f, 180f), Tooltip("乗り越え・よじ登りで、向いている方向と障害物へ向かう方向のなす角の上限 (度)")]
        private float _vaultMaxAngleDegrees = MotorSpec.DefaultVaultMaxAngleDegrees;

        [SerializeField, Min(0f), Tooltip("上端がこの高さ (足元から、m) 未満の障害物は段差として歩いて上がり、乗り越えない")]
        private float _vaultMinHeight = MotorSpec.DefaultVaultMinHeight;

        [SerializeField, Min(0f), Tooltip("乗り越えられる障害物の奥行きの上限 (m)。これより厚い物は台で、向こう側へ降りない")]
        private float _vaultMaxDepth = MotorSpec.DefaultVaultMaxDepth;

        [SerializeField, Min(0f), Tooltip("よじ登れる段の、足元からの最大の高さ (m)。乗り越えられる高さ以下の段は乗り越える")]
        private float _mantleMaxHeight = MotorSpec.DefaultMantleMaxHeight;

        [SerializeField, Min(0.01f), Tooltip("よじ登る速さ (m/s)")]
        private float _mantleSpeed = MotorSpec.DefaultMantleSpeed;

        [Header("伏せ")]
        [SerializeField, Min(0.01f), Tooltip("伏せたときのカプセルの高さ (m)。半径の 2 倍未満やしゃがみより高いときは合う高さに収める")]
        private float _proneHeight = ProneSpec.DefaultHeight;

        [SerializeField, Min(0f), Tooltip("伏せている間の水平速度 (m/s)")]
        private float _proneSpeed = ProneSpec.DefaultSpeed;

        [SerializeField, Range(1f, 90f), Tooltip("伏せている間に上下を向ける角度の上限 (度、水平から上下それぞれ)")]
        private float _proneMaxPitchDegrees = ProneSpec.DefaultMaxPitchDegrees;

        [SerializeField, Range(0f, 1f), Tooltip("伏せている間に弾の散らばりと照準の広がりに掛ける倍率")]
        private float _proneSpreadScale = ProneSpec.DefaultSpreadScale;

        [SerializeField, Min(0f), Tooltip("ダッシュ中に伏せたとき、飛び込んで前へ滑る水平距離 (m)")]
        private float _diveDistance = ProneSpec.DefaultDiveDistance;

        [SerializeField, Min(0.01f), Tooltip("飛び込みにかかる秒数")]
        private float _diveSeconds = ProneSpec.DefaultDiveSeconds;

        [Header("持ち物")]
        [SerializeField, Tooltip("重さを数える武器。Held は構えている武器だけ、Carried は持っている全部の合計、None は数えない")]
        private CarrySource _carryWeightSource = CarryLoadSpec.DefaultWeightSource;

        [SerializeField, Tooltip("武器ごとの移動の倍率を掛ける武器。Carried なら全部の積")]
        private CarrySource _carryMultiplierSource = CarryLoadSpec.DefaultMultiplierSource;

        [SerializeField, Min(0f), Tooltip("この重さ (kg) までは遅くならない")]
        private float _carryFreeWeight = CarryLoadSpec.DefaultFreeWeight;

        [SerializeField, Min(0f), Tooltip("遅くならない重さを超えた 1 kg ごとに速さの倍率から引く量。0 なら重さでは遅くならない")]
        private float _carrySpeedLossPerKg = CarryLoadSpec.DefaultSpeedLossPerKg;

        [SerializeField, Range(0f, 1f), Tooltip("重さで下がる速さの倍率の下限。武器ごとの倍率はこの後に掛ける")]
        private float _carryMinWeightMultiplier = CarryLoadSpec.DefaultMinWeightMultiplier;

        [SerializeField, Tooltip("持ち物の倍率を掛ける動き")]
        private CarryGaits _carryGaits = CarryLoadSpec.DefaultGaits;

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

        public float SafeFallSpeed
        {
            get => _safeFallSpeed;
            set => _safeFallSpeed = value;
        }

        public float LethalFallSpeed
        {
            get => _lethalFallSpeed;
            set => _lethalFallSpeed = value;
        }

        public float DodgeDistance
        {
            get => _dodgeDistance;
            set => _dodgeDistance = value;
        }

        public float DodgeSeconds
        {
            get => _dodgeSeconds;
            set => _dodgeSeconds = value;
        }

        public float DodgeCooldownSeconds
        {
            get => _dodgeCooldownSeconds;
            set => _dodgeCooldownSeconds = value;
        }

        public float VaultMaxHeight
        {
            get => _vaultMaxHeight;
            set => _vaultMaxHeight = value;
        }

        public float VaultSpeed
        {
            get => _vaultSpeed;
            set => _vaultSpeed = value;
        }

        public float VaultReach
        {
            get => _vaultReach;
            set => _vaultReach = value;
        }

        public float VaultMaxAngleDegrees
        {
            get => _vaultMaxAngleDegrees;
            set => _vaultMaxAngleDegrees = value;
        }

        public float VaultMinHeight
        {
            get => _vaultMinHeight;
            set => _vaultMinHeight = value;
        }

        public float VaultMaxDepth
        {
            get => _vaultMaxDepth;
            set => _vaultMaxDepth = value;
        }

        public float MantleMaxHeight
        {
            get => _mantleMaxHeight;
            set => _mantleMaxHeight = value;
        }

        public float MantleSpeed
        {
            get => _mantleSpeed;
            set => _mantleSpeed = value;
        }

        /// <exception cref="ArgumentOutOfRangeException">取り出すときに、値が範囲の外。</exception>
        public ProneSpec Prone
        {
            get => CreateProneSpec();
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                _proneHeight = value.Height;
                _proneSpeed = value.Speed;
                _proneMaxPitchDegrees = value.MaxPitchDegrees;
                _proneSpreadScale = value.SpreadScale;
                _diveDistance = value.DiveDistance;
                _diveSeconds = value.DiveSeconds;
            }
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

        /// <exception cref="ArgumentOutOfRangeException">取り出すときに、値が範囲の外。</exception>
        public CarryLoadSpec CarryLoad
        {
            get => new CarryLoadSpec(
                _carryWeightSource, _carryMultiplierSource, _carryFreeWeight, _carrySpeedLossPerKg, _carryMinWeightMultiplier, _carryGaits);
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                _carryWeightSource = value.WeightSource;
                _carryMultiplierSource = value.MultiplierSource;
                _carryFreeWeight = value.FreeWeight;
                _carrySpeedLossPerKg = value.SpeedLossPerKg;
                _carryMinWeightMultiplier = value.MinWeightMultiplier;
                _carryGaits = value.Gaits;
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

        /// <summary><see cref="Resolve"/> と同じ順で設定を探して伏せの調整値を作る。どれも無ければ <see cref="ProneSpec.Default"/>。</summary>
        /// <exception cref="ArgumentOutOfRangeException">伏せの値が範囲の外。</exception>
        public static ProneSpec ResolveProne(MotorSettings assigned)
        {
            var settings = assigned != null ? assigned : Load();
            return settings != null ? settings.CreateProneSpec() : ProneSpec.Default;
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
                _safeFallSpeed,
                _lethalFallSpeed,
                _dodgeDistance,
                _dodgeSeconds,
                _vaultMaxHeight,
                _mantleMaxHeight,
                _mantleSpeed,
                WalkDirection,
                SprintDirection,
                CarryLoad,
                _dodgeCooldownSeconds,
                _vaultSpeed,
                _vaultReach,
                _vaultMaxAngleDegrees,
                _vaultMinHeight,
                _vaultMaxDepth);
        }

        /// <exception cref="ArgumentOutOfRangeException">伏せの値が範囲の外。</exception>
        public ProneSpec CreateProneSpec()
        {
            return new ProneSpec(
                _proneHeight, _proneSpeed, _proneMaxPitchDegrees, _proneSpreadScale, _diveDistance, _diveSeconds);
        }

        public void ResetToDefaults()
        {
            _walkSpeed = MotorSpec.DefaultWalkSpeed;
            _sprintSpeed = MotorSpec.DefaultSprintSpeed;
            _crouchSpeed = MotorSpec.DefaultCrouchSpeed;
            _jumpHeight = MotorSpec.DefaultJumpHeight;
            _gravity = MotorSpec.DefaultGravity;
            _airControl = MotorSpec.DefaultAirControl;
            _safeFallSpeed = MotorSpec.DefaultSafeFallSpeed;
            _lethalFallSpeed = MotorSpec.DefaultLethalFallSpeed;
            _dodgeDistance = MotorSpec.DefaultDodgeDistance;
            _dodgeSeconds = MotorSpec.DefaultDodgeSeconds;
            _dodgeCooldownSeconds = MotorSpec.DefaultDodgeCooldownSeconds;
            _vaultMaxHeight = MotorSpec.DefaultVaultMaxHeight;
            _vaultSpeed = MotorSpec.DefaultVaultSpeed;
            _vaultReach = MotorSpec.DefaultVaultReach;
            _vaultMaxAngleDegrees = MotorSpec.DefaultVaultMaxAngleDegrees;
            _vaultMinHeight = MotorSpec.DefaultVaultMinHeight;
            _vaultMaxDepth = MotorSpec.DefaultVaultMaxDepth;
            _mantleMaxHeight = MotorSpec.DefaultMantleMaxHeight;
            _mantleSpeed = MotorSpec.DefaultMantleSpeed;
            Prone = ProneSpec.Default;
            WalkDirection = MoveDirectionScale.Uniform;
            SprintDirection = MoveDirectionScale.Uniform;
            CarryLoad = CarryLoadSpec.Default;
        }
    }
}
