using System;

namespace Fps.Core
{
    /// <summary>
    /// キャラ 1 体の足音と着地音をいつ・どの大きさで鳴らすかを決める。毎フレーム、そのフレームに動いた距離と接地・しゃがみ・ダッシュの
    /// 状態を <see cref="Advance"/> に渡すと、鳴らす音 (<see cref="FootstepCue"/>) を返す。位置の差から動きを測るので、
    /// 自分で動かすキャラ・NavMesh で動く敵・ネットワークで補間して表示する他人のどれにも同じ規則で使える。
    /// <list type="bullet">
    /// <item>足音: 接地して <see cref="StillSpeed"/> 以上で動いている間、歩幅 (<see cref="StrideFor"/>) ぶん進むたびに 1 回。
    /// 歩幅は しゃがみ &lt; 歩き &lt; ダッシュ の順に長いが、速さの比ほどは伸ばさないので、ダッシュは歩きより間隔
    /// (<see cref="IntervalFor"/>) が短い。音量はしゃがみが小さく、ダッシュが大きい (<see cref="VolumeFor"/>)。</item>
    /// <item><see cref="StopSeconds"/> より長く止まってから歩き出すと、歩幅の <see cref="FirstStepProgress"/> だけ進んだところで最初の足音を鳴らす。</item>
    /// <item>着地: 空中から接地に変わり、そのときの落下の速さが <see cref="MinLandingFallSpeed"/> 以上なら 1 回 (<see cref="LandingVolume"/>)。
    /// 着地音を鳴らした後は、次の足音まで歩幅をまるごと 1 つ待つ (着地音と足音を重ねない)。
    /// 坂や段差で一瞬だけ接地が切れたような小さな落下は着地にせず、歩幅の進みもそのまま保つ。</item>
    /// <item>1 フレームに <see cref="TeleportDistance"/> を超えて動いたら瞬間移動 (リスポーン・補間の飛び) と見なし、
    /// 鳴らさずに歩幅の進みを最初からにする。</item>
    /// <item>生成直後の最初の接地は、空中から降りたのか分からないので着地にしない。</item>
    /// </list>
    /// </summary>
    public sealed class FootstepCadence
    {
        /// <summary>立って歩くときの歩幅 (m)。歩きの 5 m/s で 0.44 秒ごと。</summary>
        public const float DefaultWalkStride = 2.2f;

        /// <summary>ダッシュ中の歩幅 (m)。ダッシュの 8 m/s で 0.325 秒ごと。</summary>
        public const float DefaultSprintStride = 2.6f;

        /// <summary>しゃがみ中の歩幅 (m)。しゃがみの 2.5 m/s で 0.6 秒ごと。</summary>
        public const float DefaultCrouchStride = 1.5f;

        public const float DefaultWalkVolume = 0.6f;
        public const float DefaultSprintVolume = 0.85f;
        public const float DefaultCrouchVolume = 0.2f;

        /// <summary>これ未満の水平の速さ (m/s) は止まっていると見なす (<see cref="CharacterPose.StillSpeed"/> と同じ)。</summary>
        public const float StillSpeed = CharacterPose.StillSpeed;

        /// <summary>
        /// 1 フレームの動きがこの距離 (m) を超えたら瞬間移動と見なす。高所からの落下 (約 45 m/s) が 0.1 秒の引っかかりで動く距離より大きくしてある。
        /// 速さ (距離 ÷ フレームの秒数) で判定しないのは、ネット対戦の自分のキャラが tick (1/60 秒) ぶんの移動を 1 フレームでまとめて行い、
        /// フレームレートが高いほど計算上の速さが大きくなるため。
        /// </summary>
        public const float TeleportDistance = 5f;

        /// <summary>止まってから歩き出したときの、歩幅の進み (0〜1)。歩き出してすぐ最初の足音が鳴るようにする。</summary>
        public const float FirstStepProgress = 0.5f;

        /// <summary>
        /// この秒数より長く止まっていたら、歩幅の進みを <see cref="FirstStepProgress"/> に戻す。
        /// ネット対戦の自分のキャラは tick ごとにしか動かず、tick の進まないフレームは動きが 0 になるので、1 フレーム止まっただけでは戻さない。
        /// </summary>
        public const float StopSeconds = 0.15f;

        /// <summary>
        /// 着地音を鳴らす最小の落下の速さ (m/s)。坂を下るときなどの接地の揺れ (1 フレームぶんの重力程度) を着地にしないための値で、
        /// 1.2m のジャンプの着地 (約 7 m/s) より十分小さい。
        /// </summary>
        public const float MinLandingFallSpeed = 2.5f;

        /// <summary>着地音が最大の音量になる落下の速さ (m/s)。</summary>
        public const float HardLandingFallSpeed = 12f;

        /// <summary><see cref="MinLandingFallSpeed"/> ちょうどで着地したときの音量。</summary>
        public const float MinLandingVolume = 0.35f;

        /// <summary>
        /// ダッシュしているかが分からないキャラ (ネットワークの他人) で、歩きの速さの何倍を超えたらダッシュと見なすか
        /// (<see cref="IsSprintingBySpeed"/>)。補間の揺れで歩きの速さを少し超えてもダッシュにしないための余裕。
        /// </summary>
        public const float SprintSpeedRatio = 1.1f;

        private float _progress = FirstStepProgress;
        private bool? _wasGrounded;
        private float _airFallSpeed;
        private float _stillSeconds;
        private float _secondsSinceVerticalMove;

        /// <param name="walkStride">立って歩くときの歩幅 (m)。0 より大きい有限値。</param>
        /// <param name="sprintStride">ダッシュ中の歩幅 (m)。0 より大きい有限値。</param>
        /// <param name="crouchStride">しゃがみ中の歩幅 (m)。0 より大きい有限値。</param>
        /// <param name="walkVolume">歩きの足音の音量。[0, 1]。</param>
        /// <param name="sprintVolume">ダッシュの足音の音量。[0, 1]。</param>
        /// <param name="crouchVolume">しゃがみの足音の音量。[0, 1]。</param>
        public FootstepCadence(
            float walkStride = DefaultWalkStride,
            float sprintStride = DefaultSprintStride,
            float crouchStride = DefaultCrouchStride,
            float walkVolume = DefaultWalkVolume,
            float sprintVolume = DefaultSprintVolume,
            float crouchVolume = DefaultCrouchVolume)
        {
            WalkStride = RequirePositive(walkStride, nameof(walkStride));
            SprintStride = RequirePositive(sprintStride, nameof(sprintStride));
            CrouchStride = RequirePositive(crouchStride, nameof(crouchStride));
            WalkVolume = RequireVolume(walkVolume, nameof(walkVolume));
            SprintVolume = RequireVolume(sprintVolume, nameof(sprintVolume));
            CrouchVolume = RequireVolume(crouchVolume, nameof(crouchVolume));
        }

        public float WalkStride { get; }
        public float SprintStride { get; }
        public float CrouchStride { get; }
        public float WalkVolume { get; }
        public float SprintVolume { get; }
        public float CrouchVolume { get; }

        /// <summary>次の足音までの歩幅の進み ([0, 1))。</summary>
        public float Progress => _progress;

        /// <summary>
        /// 状態に合う歩幅 (m)。しゃがみはダッシュより優先する (しゃがんだままダッシュはしない)。
        /// </summary>
        public float StrideFor(bool crouching, bool sprinting)
        {
            if (crouching)
            {
                return CrouchStride;
            }

            return sprinting ? SprintStride : WalkStride;
        }

        /// <summary>状態に合う足音の音量。しゃがみはダッシュより優先する。</summary>
        public float VolumeFor(bool crouching, bool sprinting)
        {
            if (crouching)
            {
                return CrouchVolume;
            }

            return sprinting ? SprintVolume : WalkVolume;
        }

        /// <summary>
        /// horizontalSpeed (m/s) で動き続けたときの足音の間隔 (秒)。止まっている (<see cref="StillSpeed"/> 未満) なら正の無限大。
        /// </summary>
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

        /// <summary>
        /// 落下の速さ fallSpeed (m/s、下向きを正) で着地したときの着地音の音量。<see cref="MinLandingFallSpeed"/> 未満なら 0 (鳴らさない)。
        /// そこから <see cref="HardLandingFallSpeed"/> まで <see cref="MinLandingVolume"/> から 1 へ直線で上げ、それより速くても 1。
        /// </summary>
        public static float LandingVolume(float fallSpeed)
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

        /// <summary>
        /// ダッシュしているかが同期されていないキャラで、水平の速さからダッシュ中かを推す。
        /// 歩きの速さの <see cref="SprintSpeedRatio"/> 倍を超えていればダッシュ中と見なす (回避の転がりもダッシュに数える)。
        /// </summary>
        /// <param name="horizontalSpeed">水平の速さ (m/s)。0 以上の有限値。</param>
        /// <param name="walkSpeed">そのキャラの歩きの速さ (m/s)。0 より大きい有限値。</param>
        public static bool IsSprintingBySpeed(float horizontalSpeed, float walkSpeed)
        {
            if (!(horizontalSpeed >= 0f) || float.IsInfinity(horizontalSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(horizontalSpeed), horizontalSpeed, "horizontalSpeed must be finite and not negative");
            }

            RequirePositive(walkSpeed, nameof(walkSpeed));
            return horizontalSpeed > walkSpeed * SprintSpeedRatio;
        }

        /// <summary>
        /// 1 フレームぶん進め、鳴らす音を返す。1 フレームで鳴らすのは多くて 1 つ (着地が足音より優先)。
        /// </summary>
        /// <param name="horizontalDistance">このフレームに水平に動いた距離 (m)。0 以上の有限値。</param>
        /// <param name="verticalDistance">このフレームに縦に動いた距離 (m、上が正)。有限値。</param>
        /// <param name="deltaSeconds">このフレームの秒数。0 以上の有限値。0 なら何も変えずに <see cref="FootstepCue.None"/>。</param>
        /// <param name="grounded">このフレームの終わりに接地しているか。</param>
        /// <param name="crouching">しゃがんでいるか。</param>
        /// <param name="sprinting">ダッシュしているか。</param>
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

        /// <summary>
        /// 生成直後と同じ状態に戻す (歩幅の進みを <see cref="FirstStepProgress"/> に、接地の履歴を無しに)。
        /// リスポーンや有効にし直したときに呼ぶ。
        /// </summary>
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
