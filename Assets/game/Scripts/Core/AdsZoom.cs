using System;

namespace Fps.Core
{
    /// <summary>
    /// 覗き込み (ADS) のズーム。腰だめ (<see cref="Progress"/> = 0) から覗き込み切った状態 (1) までの進み具合を
    /// <see cref="Step"/> で時間に沿って動かし、そこからカメラの視野角 (<see cref="FieldOfView(float, float)"/>) と
    /// 視点操作の感度の倍率 (<see cref="SensitivityMultiplier"/>) を出す。視野角はすべて縦の視野角 (度) で扱う。
    /// </summary>
    public sealed class AdsZoom
    {
        /// <summary>武器が ADS の視野角を決めていないときに使う、覗き込み切ったときの視野角 (度)。</summary>
        public const float DefaultFieldOfView = 50f;

        /// <summary>腰だめから覗き込み切るまで (と戻るまで) の既定の秒数。</summary>
        public const float DefaultTransitionSeconds = 0.15f;

        /// <summary>視野角の下限 (この値自体は受け付けない)。0° と 180° では透視投影ができない。</summary>
        public const float MinFieldOfView = 0f;

        /// <summary>視野角の上限 (この値自体は受け付けない)。</summary>
        public const float MaxFieldOfView = 180f;

        /// <param name="transitionSeconds">腰だめから覗き込み切るまでの秒数。0 以上の有限値。0 なら即座に切り替わる。</param>
        public AdsZoom(float transitionSeconds = DefaultTransitionSeconds)
        {
            if (!(transitionSeconds >= 0f) || float.IsInfinity(transitionSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(transitionSeconds), transitionSeconds, "transitionSeconds must be a non-negative finite number");
            }

            TransitionSeconds = transitionSeconds;
        }

        /// <summary>腰だめから覗き込み切るまでの秒数。</summary>
        public float TransitionSeconds { get; }

        /// <summary>最後の <see cref="Step"/> で覗き込もうとしていたか。</summary>
        public bool IsAiming { get; private set; }

        /// <summary>覗き込みの進み具合。0 が腰だめ、1 が覗き込み切った状態。</summary>
        public float Progress { get; private set; }

        /// <summary>
        /// deltaSeconds だけ時間を進める。aiming なら 1 へ、そうでなければ 0 へ、<see cref="TransitionSeconds"/> で端から端まで動く速さで寄せ、
        /// 進めた後の <see cref="Progress"/> を返す。途中で向きが変わっても今の位置からそのまま戻る。
        /// </summary>
        /// <param name="aiming">覗き込むか。</param>
        /// <param name="deltaSeconds">進める秒数。0 以上の有限値。</param>
        public float Step(bool aiming, float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            IsAiming = aiming;
            var target = aiming ? 1f : 0f;
            if (TransitionSeconds <= 0f)
            {
                Progress = target;
                return Progress;
            }

            var step = deltaSeconds / TransitionSeconds;
            Progress = aiming ? Math.Min(1f, Progress + step) : Math.Max(0f, Progress - step);
            return Progress;
        }

        /// <summary>腰だめの状態に即座に戻す (視点を止めたときなど)。</summary>
        public void Reset()
        {
            IsAiming = false;
            Progress = 0f;
        }

        /// <summary>今の <see cref="Progress"/> でのカメラの視野角 (度)。</summary>
        /// <param name="hipFieldOfView">腰だめの視野角 (度)。</param>
        /// <param name="adsFieldOfView">覗き込み切ったときの視野角 (度)。</param>
        public float FieldOfView(float hipFieldOfView, float adsFieldOfView)
        {
            return Interpolate(hipFieldOfView, adsFieldOfView, Progress);
        }

        /// <summary>
        /// 進み具合 progress (0〜1 に丸める) での視野角 (度)。動き始めと止まり際が急にならないよう、進み具合を smoothstep で緩めてから補間する。
        /// </summary>
        public static float Interpolate(float hipFieldOfView, float adsFieldOfView, float progress)
        {
            ValidateFieldOfView(hipFieldOfView, nameof(hipFieldOfView));
            ValidateFieldOfView(adsFieldOfView, nameof(adsFieldOfView));
            if (float.IsNaN(progress))
            {
                throw new ArgumentOutOfRangeException(nameof(progress), progress, "progress must be a number");
            }

            var t = Math.Max(0f, Math.Min(1f, progress));
            var eased = t * t * (3f - (2f * t));
            return hipFieldOfView + ((adsFieldOfView - hipFieldOfView) * eased);
        }

        /// <summary>
        /// 視野角が hipFieldOfView から fieldOfView に変わったときに感度へ掛ける倍率。
        /// 画面上の同じ距離を動かすのに同じ手の動きで済むよう、半視野角の正接の比 (tan(fov/2) / tan(hip/2)) にする。
        /// 視野角が等しければ 1、狭まれば 1 未満。
        /// </summary>
        public static float SensitivityMultiplier(float hipFieldOfView, float fieldOfView)
        {
            ValidateFieldOfView(hipFieldOfView, nameof(hipFieldOfView));
            ValidateFieldOfView(fieldOfView, nameof(fieldOfView));
            if (hipFieldOfView == fieldOfView)
            {
                return 1f;
            }

            var hip = Math.Tan(hipFieldOfView * Math.PI / 360d);
            var current = Math.Tan(fieldOfView * Math.PI / 360d);
            return (float)(current / hip);
        }

        /// <summary>value が視野角として使えるか (<see cref="MinFieldOfView"/> を超え <see cref="MaxFieldOfView"/> 未満)。</summary>
        public static bool IsValidFieldOfView(float value)
        {
            return value > MinFieldOfView && value < MaxFieldOfView;
        }

        /// <summary>value が視野角として使えなければ ArgumentOutOfRangeException を投げ、使えればそのまま返す。</summary>
        public static float ValidateFieldOfView(float value, string paramName)
        {
            if (!IsValidFieldOfView(value))
            {
                throw new ArgumentOutOfRangeException(
                    paramName, value, "field of view must be greater than 0 and less than 180 degrees");
            }

            return value;
        }
    }
}
