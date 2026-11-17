using System;

namespace Shooter.Core
{
    /// <summary>
    /// 覗き込んだときに何を通して見るか。照準の見た目 (<see cref="ReticleId"/>)、画面を覆うスコープの縁 (<see cref="ScopeOverlay"/>)、
    /// 三人称でも目から覗くか (<see cref="EyeViewInThirdPerson"/>)、どこまで構えたら覗いたことにするか (<see cref="AimedInProgress"/>)。
    /// 武器 (<see cref="AimSpec.Sight"/>) が持ち、照準器のアタッチメント (<see cref="AttachmentSpec.Sight"/>) が付け替える。
    /// 画面への出し方は <see cref="SightView"/> が決める。
    /// </summary>
    public sealed class SightSpec
    {
        public const float DefaultAimedInProgress = 0.9f;

        public static readonly SightSpec Default = new SightSpec();

        /// <param name="reticleId">照準の見た目のキー。空なら照準器の照準を出さない (スコープの縁を出すときは既定の十字)。</param>
        /// <param name="scopeOverlay">構えきったら画面をスコープの縁で覆い、一人称の武器を隠す。</param>
        /// <param name="eyeViewInThirdPerson">三人称でも、構えるにつれてカメラを肩越しから目へ寄せる。</param>
        /// <param name="aimedInProgress">覗き込みの進み具合 (0 より大きく 1 以下) がこれ以上で、照準と縁を出す。</param>
        public SightSpec(
            string reticleId = null,
            bool scopeOverlay = false,
            bool eyeViewInThirdPerson = false,
            float aimedInProgress = DefaultAimedInProgress)
        {
            ReticleId = SpecGuard.ReferenceId(reticleId);
            ScopeOverlay = scopeOverlay;
            EyeViewInThirdPerson = eyeViewInThirdPerson;
            if (!(aimedInProgress > 0f && aimedInProgress <= 1f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(aimedInProgress), aimedInProgress, nameof(aimedInProgress) + " must be greater than 0 and at most 1");
            }

            AimedInProgress = aimedInProgress;
        }

        public string ReticleId { get; }

        public bool HasReticle => ReticleId.Length > 0;

        public bool ScopeOverlay { get; }

        public bool EyeViewInThirdPerson { get; }

        public float AimedInProgress { get; }

        public bool IsDefault =>
            !HasReticle && !ScopeOverlay && !EyeViewInThirdPerson && AimedInProgress == DefaultAimedInProgress;
    }
}
