using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // コンポーネントごとに値を持たせると、オフライン (PlayerController) とネット対戦 (NetworkPlayerMotor) で歩く速さなどが食い違うので、
    // 両方が同じアセット (割り当てが無ければ入口 GameContent の Motor、それも無ければ Resources の ResourcePath、それも無ければ MotorSpec.Default) を読む。
    [CreateAssetMenu(fileName = "MotorSettings", menuName = "Shooter/Motor Settings")]
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

        [Header("カバー・はしご")]
        [SerializeField, Min(0f), Tooltip("カバーに入れる、体の中心から壁までの水平距離の上限 (m)")]
        private float _coverMaxEnterDistance = MotorSpec.DefaultCoverMaxEnterDistance;

        [SerializeField, Range(0f, 180f), Tooltip("カバーに入るときの、向いている方向と壁へ向かう方向のなす角の上限 (度)")]
        private float _coverMaxEnterAngleDegrees = MotorSpec.DefaultCoverMaxEnterAngleDegrees;

        [SerializeField, Min(0f), Tooltip("上端がこの高さ (足元から、m) 未満の壁を低い壁としてしゃがんで隠れる。以上なら高い壁で、端から横に出て覗く")]
        private float _coverLowWallHeight = MotorSpec.DefaultCoverLowWallHeight;

        [SerializeField, Min(0f), Tooltip("高い壁の端から覗くときに横へ出る距離 (m)")]
        private float _coverPeekSideDistance = MotorSpec.DefaultCoverPeekSideDistance;

        [SerializeField, Min(0.01f), Tooltip("カバーに入るとき、壁へ寄せるのにかける秒数")]
        private float _coverEnterSeconds = MotorSpec.DefaultCoverEnterSeconds;

        [SerializeField, Min(0f), Tooltip("カバー中に後ろの入力をこれだけ (秒) 続けると出る")]
        private float _coverBackHoldSeconds = MotorSpec.DefaultCoverBackHoldSeconds;

        [SerializeField, Min(0.01f), Tooltip("高い壁の端から横に出る・戻るのにかける秒数")]
        private float _coverPeekSlideSeconds = MotorSpec.DefaultCoverPeekSlideSeconds;

        [SerializeField, Min(0f), Tooltip("はしごの上の端からつかまったときの足元の、上の足場からの深さ (m)。前からつかまれるのもこの深さより下まで")]
        private float _ladderTopEntryDepth = MotorSpec.DefaultLadderTopEntryDepth;

        [SerializeField, Range(-1f, 1f), Tooltip("動いている向きとはしごへ向かう向きのなす角の余弦がこれ以上なら、はしごへ進んでいるとみなしてつかまる")]
        private float _ladderMinApproachDot = MotorSpec.DefaultLadderMinApproachDot;

        [SerializeField, Min(0f), Tooltip("水平の速さ (m/s) がこれ未満なら、はしごへ進んでいるとみなさない")]
        private float _ladderMinApproachSpeed = MotorSpec.DefaultLadderMinApproachSpeed;

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

        [Header("リーン・空中のジャンプ")]
        [SerializeField, Min(0f), Tooltip("左右へ傾け切ったときに目を横へずらす距離 (m)")]
        private float _leanSideOffset = MotorSpec.DefaultLeanSideOffset;

        [SerializeField, Range(0f, 90f), Tooltip("左右へ傾け切ったときの体の角度 (度)")]
        private float _leanAngleDegrees = MotorSpec.DefaultLeanAngleDegrees;

        [SerializeField, Min(0.01f), Tooltip("傾きが 1 秒に進む量 (傾け切りが 1)")]
        private float _leanSpeed = MotorSpec.DefaultLeanSpeed;

        [SerializeField, Min(0), Tooltip("着地するまでに空中で跳べる回数。0 なら空中では跳べない")]
        private int _airJumps = MotorSpec.DefaultAirJumps;

        [SerializeField, Min(0f), Tooltip("空中で跳んだときの上向きの速さ (m/s)")]
        private float _airJumpSpeed = MotorSpec.DefaultAirJumpSpeed;

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

        public float CoverMaxEnterDistance
        {
            get => _coverMaxEnterDistance;
            set => _coverMaxEnterDistance = value;
        }

        public float CoverMaxEnterAngleDegrees
        {
            get => _coverMaxEnterAngleDegrees;
            set => _coverMaxEnterAngleDegrees = value;
        }

        public float CoverLowWallHeight
        {
            get => _coverLowWallHeight;
            set => _coverLowWallHeight = value;
        }

        public float CoverPeekSideDistance
        {
            get => _coverPeekSideDistance;
            set => _coverPeekSideDistance = value;
        }

        public float CoverEnterSeconds
        {
            get => _coverEnterSeconds;
            set => _coverEnterSeconds = value;
        }

        public float CoverBackHoldSeconds
        {
            get => _coverBackHoldSeconds;
            set => _coverBackHoldSeconds = value;
        }

        public float CoverPeekSlideSeconds
        {
            get => _coverPeekSlideSeconds;
            set => _coverPeekSlideSeconds = value;
        }

        public float LadderTopEntryDepth
        {
            get => _ladderTopEntryDepth;
            set => _ladderTopEntryDepth = value;
        }

        public float LadderMinApproachDot
        {
            get => _ladderMinApproachDot;
            set => _ladderMinApproachDot = value;
        }

        public float LadderMinApproachSpeed
        {
            get => _ladderMinApproachSpeed;
            set => _ladderMinApproachSpeed = value;
        }

        public float LeanSideOffset
        {
            get => _leanSideOffset;
            set => _leanSideOffset = value;
        }

        public float LeanAngleDegrees
        {
            get => _leanAngleDegrees;
            set => _leanAngleDegrees = value;
        }

        public float LeanSpeed
        {
            get => _leanSpeed;
            set => _leanSpeed = value;
        }

        public int AirJumps
        {
            get => _airJumps;
            set => _airJumps = value;
        }

        public float AirJumpSpeed
        {
            get => _airJumpSpeed;
            set => _airJumpSpeed = value;
        }

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
                _vaultMaxDepth,
                _coverMaxEnterDistance,
                _coverMaxEnterAngleDegrees,
                _coverLowWallHeight,
                _coverPeekSideDistance,
                _coverEnterSeconds,
                _coverBackHoldSeconds,
                _coverPeekSlideSeconds,
                _ladderTopEntryDepth,
                _ladderMinApproachDot,
                _ladderMinApproachSpeed,
                _leanSideOffset,
                _leanAngleDegrees,
                _leanSpeed,
                _airJumps,
                _airJumpSpeed);
        }

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
            _coverMaxEnterDistance = MotorSpec.DefaultCoverMaxEnterDistance;
            _coverMaxEnterAngleDegrees = MotorSpec.DefaultCoverMaxEnterAngleDegrees;
            _coverLowWallHeight = MotorSpec.DefaultCoverLowWallHeight;
            _coverPeekSideDistance = MotorSpec.DefaultCoverPeekSideDistance;
            _coverEnterSeconds = MotorSpec.DefaultCoverEnterSeconds;
            _coverBackHoldSeconds = MotorSpec.DefaultCoverBackHoldSeconds;
            _coverPeekSlideSeconds = MotorSpec.DefaultCoverPeekSlideSeconds;
            _ladderTopEntryDepth = MotorSpec.DefaultLadderTopEntryDepth;
            _ladderMinApproachDot = MotorSpec.DefaultLadderMinApproachDot;
            _ladderMinApproachSpeed = MotorSpec.DefaultLadderMinApproachSpeed;
            _leanSideOffset = MotorSpec.DefaultLeanSideOffset;
            _leanAngleDegrees = MotorSpec.DefaultLeanAngleDegrees;
            _leanSpeed = MotorSpec.DefaultLeanSpeed;
            _airJumps = MotorSpec.DefaultAirJumps;
            _airJumpSpeed = MotorSpec.DefaultAirJumpSpeed;
            Prone = ProneSpec.Default;
            WalkDirection = MoveDirectionScale.Uniform;
            SprintDirection = MoveDirectionScale.Uniform;
            CarryLoad = CarryLoadSpec.Default;
        }
    }
}
