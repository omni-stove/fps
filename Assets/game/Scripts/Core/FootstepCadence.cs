using System;

namespace Shooter.Core
{
    // 位置の差から動きを測るので、自分で動かすキャラ・NavMesh で動く敵・ネットワークで補間して表示する他人のどれにも同じ規則で使える。
    public sealed class FootstepCadence
    {
        public const float DefaultWalkStride = 2.2f;

        // 歩幅を速さの比ほどは伸ばさず、ダッシュの足音の間隔を歩きより短くする。
        public const float DefaultSprintStride = 2.6f;

        public const float DefaultCrouchStride = 1.5f;

        public const float DefaultWalkVolume = 0.6f;
        public const float DefaultSprintVolume = 0.85f;
        public const float DefaultCrouchVolume = 0.2f;

        public const float StillSpeed = CharacterPose.StillSpeed;

        // 高所からの落下 (約 45 m/s) が 0.1 秒の引っかかりで動く距離より大きくしてある。
        // 速さ (距離 ÷ フレームの秒数) で判定しないのは、ネット対戦の自分のキャラが tick (1/60 秒) ぶんの移動を 1 フレームでまとめて行い、
        // フレームレートが高いほど計算上の速さが大きくなるため。
        public const float TeleportDistance = 5f;

        // 0 から始めず途中から始めるのは、歩き出してすぐ最初の足音が鳴るようにするため。
        public const float FirstStepProgress = 0.5f;

        // ネット対戦の自分のキャラは tick ごとにしか動かず、tick の進まないフレームは動きが 0 になるので、1 フレーム止まっただけでは戻さない。
        public const float StopSeconds = 0.15f;

        // 坂を下るときなどの接地の揺れ (1 フレームぶんの重力程度) を着地にしないための値で、
        // 1.2m のジャンプの着地 (約 7 m/s) より十分小さい。
        public const float DefaultMinLandingFallSpeed = 2.5f;

        public const float DefaultHardLandingFallSpeed = 12f;

        public const float DefaultMinLandingVolume = 0.35f;

        // 補間の揺れで歩きの速さを少し超えてもダッシュにしないための余裕。
        public const float SprintSpeedRatio = 1.1f;

        private float _progress = FirstStepProgress;

        // 生成直後の最初の接地は空中から降りたのか分からないので、着地にしないよう null から始める。
        private bool? _wasGrounded;
        private float _airFallSpeed;
        private float _stillSeconds;
        private float _secondsSinceVerticalMove;

        public FootstepCadence(
            float walkStride = DefaultWalkStride,
            float sprintStride = DefaultSprintStride,
            float crouchStride = DefaultCrouchStride,
            float walkVolume = DefaultWalkVolume,
            float sprintVolume = DefaultSprintVolume,
            float crouchVolume = DefaultCrouchVolume,
            float minLandingFallSpeed = DefaultMinLandingFallSpeed,
            float hardLandingFallSpeed = DefaultHardLandingFallSpeed,
            float minLandingVolume = DefaultMinLandingVolume)
        {
            WalkStride = RequirePositive(walkStride, nameof(walkStride));
            SprintStride = RequirePositive(sprintStride, nameof(sprintStride));
            CrouchStride = RequirePositive(crouchStride, nameof(crouchStride));
            WalkVolume = RequireVolume(walkVolume, nameof(walkVolume));
            SprintVolume = RequireVolume(sprintVolume, nameof(sprintVolume));
            CrouchVolume = RequireVolume(crouchVolume, nameof(crouchVolume));
            MinLandingFallSpeed = RequirePositive(minLandingFallSpeed, nameof(minLandingFallSpeed));
            HardLandingFallSpeed = RequirePositive(hardLandingFallSpeed, nameof(hardLandingFallSpeed));
            if (!(hardLandingFallSpeed > minLandingFallSpeed))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(hardLandingFallSpeed), hardLandingFallSpeed, nameof(hardLandingFallSpeed) + " must be greater than " + nameof(minLandingFallSpeed));
            }

            MinLandingVolume = RequireVolume(minLandingVolume, nameof(minLandingVolume));
        }

        public float WalkStride { get; }
        public float SprintStride { get; }
        public float CrouchStride { get; }
        public float WalkVolume { get; }
        public float SprintVolume { get; }
        public float CrouchVolume { get; }
        public float MinLandingFallSpeed { get; }
        public float HardLandingFallSpeed { get; }
        public float MinLandingVolume { get; }

        public float Progress => _progress;

        // しゃがんだままダッシュはしないので、しゃがみをダッシュより先に見る。
        public float StrideFor(bool crouching, bool sprinting)
        {
            if (crouching)
            {
                return CrouchStride;
            }

            return sprinting ? SprintStride : WalkStride;
        }

        public float VolumeFor(bool crouching, bool sprinting)
        {
            if (crouching)
            {
                return CrouchVolume;
            }

            return sprinting ? SprintVolume : WalkVolume;
        }

        public float IntervalFor(float horizontalSpeed, bool crouching, bool sprinting)
        {
            if (!(horizontalSpeed >= 0f) || float.IsInfinity(horizontalSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(horizontalSpeed), horizontalSpeed, "horizontalSpeed must be finite and not negative");
            }

            if (horizontalSpeed < StillSpeed)
            {
                return float.PositiveInfinity;
            }

            return StrideFor(crouching, sprinting) / horizontalSpeed;
        }

        public float LandingVolume(float fallSpeed)
        {
            if (float.IsNaN(fallSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(fallSpeed), fallSpeed, "fallSpeed must not be NaN");
            }

            if (fallSpeed < MinLandingFallSpeed)
            {
                return 0f;
            }

            var t = (fallSpeed - MinLandingFallSpeed) / (HardLandingFallSpeed - MinLandingFallSpeed);
            return MinLandingVolume + ((1f - MinLandingVolume) * Math.Min(t, 1f));
        }

        public static bool IsSprintingBySpeed(float horizontalSpeed, float walkSpeed)
        {
            if (!(horizontalSpeed >= 0f) || float.IsInfinity(horizontalSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(horizontalSpeed), horizontalSpeed, "horizontalSpeed must be finite and not negative");
            }

            RequirePositive(walkSpeed, nameof(walkSpeed));
            return horizontalSpeed > walkSpeed * SprintSpeedRatio;
        }

        public FootstepCue Advance(float horizontalDistance, float verticalDistance, float deltaSeconds, bool grounded, bool crouching, bool sprinting)
        {
            if (!(horizontalDistance >= 0f) || float.IsInfinity(horizontalDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(horizontalDistance), horizontalDistance, "horizontalDistance must be finite and not negative");
            }

            if (float.IsNaN(verticalDistance) || float.IsInfinity(verticalDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(verticalDistance), verticalDistance, "verticalDistance must be finite");
            }

            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            if (deltaSeconds == 0f)
            {
                return FootstepCue.None;
            }

            var distance = Math.Sqrt((horizontalDistance * (double)horizontalDistance) + (verticalDistance * (double)verticalDistance));
            if (distance > TeleportDistance)
            {
                Reset();
                _wasGrounded = grounded;
                return FootstepCue.None;
            }

            // tick ごとにしか動かないキャラ (ネット対戦の自分) は、tick の無いフレームで縦の動きが 0 になり、tick のフレームで
            // 1 tick ぶんまとめて動く。このフレームの秒数だけで割ると速さが大きく出るので、空中で前に縦に動いてからの秒数で割る。
            // 接地中は数えない (止まって立っている間の秒数で、次の落下の速さが小さく出るため)
            var fallSeconds = _secondsSinceVerticalMove + deltaSeconds;
            var fallSpeed = verticalDistance != 0f ? Math.Max(0f, -verticalDistance / fallSeconds) : 0f;
            _secondsSinceVerticalMove = grounded || verticalDistance != 0f ? 0f : fallSeconds;

            if (!grounded)
            {
                // 縦に動かなかったフレームで上書きすると、直前の落下の速さが消える
                if (verticalDistance != 0f)
                {
                    _airFallSpeed = fallSpeed;
                }

                _wasGrounded = false;
                return FootstepCue.None;
            }

            var landed = _wasGrounded == false;
            _wasGrounded = true;
            if (landed)
            {
                // 地面に当たったフレームは途中で止まるので、その前の空中のフレームの速さも見る
                var landingSpeed = Math.Max(fallSpeed, _airFallSpeed);
                _airFallSpeed = 0f;
                var landingVolume = LandingVolume(landingSpeed);
                if (landingVolume > 0f)
                {
                    // 着地音と足音を重ねないよう、次の足音まで歩幅をまるごと 1 つ待つ。
                    _progress = 0f;
                    return new FootstepCue(FootstepCueKind.Landing, landingVolume);
                }
            }

            if (horizontalDistance / deltaSeconds < StillSpeed)
            {
                _stillSeconds += deltaSeconds;
                if (_stillSeconds >= StopSeconds)
                {
                    _progress = FirstStepProgress;
                }

                return FootstepCue.None;
            }

            _stillSeconds = 0f;
            _progress += horizontalDistance / StrideFor(crouching, sprinting);
            if (_progress < 1f)
            {
                return FootstepCue.None;
            }

            // 引っかかりで 1 フレームが長くても、鳴らすのは 1 回だけにして端数を次へ持ち越す
            _progress %= 1f;
            return new FootstepCue(FootstepCueKind.Footstep, VolumeFor(crouching, sprinting));
        }

        public void Reset()
        {
            _progress = FirstStepProgress;
            _wasGrounded = null;
            _airFallSpeed = 0f;
            _stillSeconds = 0f;
            _secondsSinceVerticalMove = 0f;
        }

        private static float RequirePositive(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite and positive");
            }

            return value;
        }

        private static float RequireVolume(float value, string name)
        {
            if (!(value >= 0f && value <= 1f))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be within [0, 1]");
            }

            return value;
        }
    }
}
