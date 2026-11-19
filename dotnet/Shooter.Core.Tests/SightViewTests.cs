using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class SightViewTests
    {
        private static readonly SightSpec _redDot = new SightSpec("redDot");
        private static readonly SightSpec _scope = new SightSpec("mildot", scopeOverlay: true, eyeViewInThirdPerson: true);

        [Test]
        public void TheDefaultSightHasNoReticleOrOverlay()
        {
            var sight = SightSpec.Default;

            Assert.That(sight.ReticleId, Is.Empty);
            Assert.That(sight.HasReticle, Is.False);
            Assert.That(sight.ScopeOverlay, Is.False);
            Assert.That(sight.EyeViewInThirdPerson, Is.False);
            Assert.That(sight.AimedInProgress, Is.EqualTo(0.9f));
            Assert.That(sight.IsDefault, Is.True);
            Assert.That(new SightSpec("  ").IsDefault, Is.True, "a blank reticle is no reticle");
            Assert.That(new SightSpec(" redDot ").ReticleId, Is.EqualTo("redDot"));
            Assert.That(new SightSpec(aimedInProgress: 0.5f).IsDefault, Is.False);
            Assert.That(AimSpec.Default.Sight, Is.SameAs(SightSpec.Default));
            Assert.That(sight.VisionMode, Is.EqualTo(VisionMode.None));
            Assert.That(new SightSpec(visionMode: VisionMode.Night).IsDefault, Is.False, "a night sight is a sight");
        }

        [Test]
        public void RejectsAnUndefinedVisionMode()
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => new SightSpec(visionMode: (VisionMode)9));

            Assert.That(e.ParamName, Is.EqualTo("visionMode"));
        }

        [Test]
        public void TheVisionModeHoldsOnlyWhileAimedIn()
        {
            var thermal = new SightSpec("thermal", scopeOverlay: true, visionMode: VisionMode.Thermal);

            Assert.That(SightView.Of(thermal, ViewMode.FirstPerson, 0.5f).VisionMode, Is.EqualTo(VisionMode.None), "not while still raising the gun");
            Assert.That(SightView.Of(thermal, ViewMode.FirstPerson, 1f).VisionMode, Is.EqualTo(VisionMode.Thermal));
            Assert.That(SightView.Of(thermal, ViewMode.ThirdPerson, 1f).VisionMode, Is.EqualTo(VisionMode.None), "over the shoulder the sight is not looked through");
            Assert.That(SightView.None.VisionMode, Is.EqualTo(VisionMode.None));

            var night = new SightSpec("thermal", scopeOverlay: true, visionMode: VisionMode.Night);
            Assert.That(SightView.Of(night, ViewMode.FirstPerson, 1f), Is.Not.EqualTo(SightView.Of(thermal, ViewMode.FirstPerson, 1f)));
            Assert.That(
                SightView.Of(thermal, ViewMode.FirstPerson, 1f),
                Is.EqualTo(SightView.Of(new SightSpec("thermal", scopeOverlay: true, visionMode: VisionMode.Thermal), ViewMode.FirstPerson, 1f)));
            Assert.That(
                SightView.Of(thermal, ViewMode.FirstPerson, 0.5f),
                Is.EqualTo(SightView.Of(new SightSpec("thermal", scopeOverlay: true), ViewMode.FirstPerson, 0.5f)),
                "before aiming in the vision mode does not tell two sights apart");
        }

        [TestCase(0f)]
        [TestCase(-0.1f)]
        [TestCase(1.01f)]
        [TestCase(float.NaN)]
        public void RejectsAnAimedInProgressOutsideZeroToOne(float progress)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => new SightSpec(aimedInProgress: progress));

            Assert.That(e.ParamName, Is.EqualTo("aimedInProgress"));
        }

        [Test]
        public void InFirstPersonTheReticleAppearsOnceAimedIn()
        {
            Assert.That(SightView.Of(_redDot, ViewMode.FirstPerson, 0.89f).ShowsReticle, Is.False);

            var view = SightView.Of(_redDot, ViewMode.FirstPerson, 0.9f);

            Assert.That(view.ThroughSight, Is.True);
            Assert.That(view.AimedIn, Is.True);
            Assert.That(view.ShowsReticle, Is.True);
            Assert.That(view.ReticleId, Is.EqualTo("redDot"));
            Assert.That(view.ScopeOverlay, Is.False);
            Assert.That(view.HidesViewmodel, Is.False, "a red dot is seen through, the gun stays on screen");
            Assert.That(view.EyeBlend, Is.Zero);
            Assert.That(view.HidesBody, Is.False);
        }

        [Test]
        public void AScopeCoversTheScreenAndHidesTheViewmodelOnceAimedIn()
        {
            var view = SightView.Of(_scope, ViewMode.FirstPerson, 1f);

            Assert.That(view.ScopeOverlay, Is.True);
            Assert.That(view.HidesViewmodel, Is.True);
            Assert.That(view.ShowsReticle, Is.True);
            Assert.That(view.ReticleId, Is.EqualTo("mildot"));
            Assert.That(SightView.Of(_scope, ViewMode.FirstPerson, 0.5f).ScopeOverlay, Is.False, "not while still raising the gun");
        }

        [Test]
        public void AScopeWithoutAReticleStillShowsTheDefaultOne()
        {
            var view = SightView.Of(new SightSpec(scopeOverlay: true), ViewMode.FirstPerson, 1f);

            Assert.That(view.ShowsReticle, Is.True);
            Assert.That(view.ReticleId, Is.Empty);
        }

        [Test]
        public void ThirdPersonAimsOverTheShoulderUnlessTheSightTakesTheEye()
        {
            var shoulder = SightView.Of(_redDot, ViewMode.ThirdPerson, 1f);

            Assert.That(shoulder.ThroughSight, Is.False);
            Assert.That(shoulder.AimedIn, Is.False);
            Assert.That(shoulder.ShowsReticle, Is.False);
            Assert.That(shoulder.EyeBlend, Is.Zero);

            var halfway = SightView.Of(_scope, ViewMode.ThirdPerson, 0.5f);

            Assert.That(halfway.ThroughSight, Is.True);
            Assert.That(halfway.EyeBlend, Is.EqualTo(0.5f));
            Assert.That(halfway.HidesBody, Is.True);
            Assert.That(halfway.ScopeOverlay, Is.False);

            var aimed = SightView.Of(_scope, ViewMode.ThirdPerson, 1f);

            Assert.That(aimed.EyeBlend, Is.EqualTo(1f));
            Assert.That(aimed.ScopeOverlay, Is.True);
            Assert.That(SightView.Of(_scope, ViewMode.ThirdPerson, 0f).HidesBody, Is.False, "at the hip the body is drawn");
        }

        [Test]
        public void ProgressIsClampedAndNullIsTheDefaultSight()
        {
            Assert.That(SightView.Of(_scope, ViewMode.ThirdPerson, 3f).EyeBlend, Is.EqualTo(1f));
            Assert.That(SightView.Of(_scope, ViewMode.ThirdPerson, -1f).EyeBlend, Is.Zero);
            Assert.That(SightView.Of(_scope, ViewMode.ThirdPerson, float.NaN).EyeBlend, Is.Zero);
            Assert.That(SightView.Of(null, ViewMode.FirstPerson, 1f), Is.EqualTo(SightView.Of(SightSpec.Default, ViewMode.FirstPerson, 1f)));
            Assert.That(SightView.Of(null, ViewMode.FirstPerson, 1f).ShowsReticle, Is.False);
            Assert.That(SightView.None.ReticleId, Is.Empty);
            Assert.That(SightView.None.ShowsReticle, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => SightView.Of(_scope, (ViewMode)99, 1f));
        }

        [Test]
        public void TheAdsEyeViewRuleOverridesTheSightOnlyInThirdPerson()
        {
            Assert.That(SightView.Of(_redDot, ViewMode.ThirdPerson, 1f, AdsEyeView.PerWeapon).EyeBlend, Is.Zero, "a red dot stays over the shoulder");
            Assert.That(SightView.Of(_scope, ViewMode.ThirdPerson, 1f, AdsEyeView.PerWeapon).EyeBlend, Is.EqualTo(1f), "the scope asks for the eye");

            var always = SightView.Of(_redDot, ViewMode.ThirdPerson, 0.5f, AdsEyeView.Always);
            Assert.That(always.EyeBlend, Is.EqualTo(0.5f));
            Assert.That(always.ThroughSight, Is.True);
            Assert.That(always.HidesBody, Is.True);
            Assert.That(always.AimedIn, Is.False, "not yet past aimedInProgress");
            Assert.That(SightView.Of(_redDot, ViewMode.ThirdPerson, 1f, AdsEyeView.Always).ReticleId, Is.EqualTo("redDot"));

            var never = SightView.Of(_scope, ViewMode.ThirdPerson, 1f, AdsEyeView.Never);
            Assert.That(never.EyeBlend, Is.Zero);
            Assert.That(never.ThroughSight, Is.False);
            Assert.That(never.ScopeOverlay, Is.False, "the scope is not looked through over the shoulder");

            Assert.That(SightView.Of(_scope, ViewMode.FirstPerson, 1f, AdsEyeView.Never), Is.EqualTo(SightView.Of(_scope, ViewMode.FirstPerson, 1f)), "first person always looks through the sight");
            Assert.That(SightView.Of(_redDot, ViewMode.FirstPerson, 1f, AdsEyeView.Always).EyeBlend, Is.Zero, "the camera is already at the eye");
            Assert.That(SightView.Of(_scope, ViewMode.ThirdPerson, 1f), Is.EqualTo(SightView.Of(_scope, ViewMode.ThirdPerson, 1f, AdsEyeView.PerWeapon)), "PerWeapon is the default");

            var e = Assert.Throws<ArgumentOutOfRangeException>(() => SightView.Of(_scope, ViewMode.ThirdPerson, 1f, (AdsEyeView)99));
            Assert.That(e.ParamName, Is.EqualTo("eyeViewRule"));
        }
    }
}
