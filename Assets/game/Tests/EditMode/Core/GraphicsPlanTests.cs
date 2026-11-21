using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class GraphicsPlanTests
    {
        private static readonly ScreenResolution _display = new ScreenResolution(2560, 1440);

        [Test]
        public void TheDefaultKeepsTheProjectLevelTheWindowAndThePlatformFrameRate()
        {
            var plan = Plan(GraphicsSettings.Default, projectQualityLevel: 1);

            Assert.That(plan.QualityLevel, Is.EqualTo(1));
            Assert.That(plan.QualityClamped, Is.False);
            Assert.That(plan.VSyncCount, Is.EqualTo(0));
            Assert.That(plan.TargetFrameRate, Is.EqualTo(GraphicsPlan.PlatformFrameRate));
            Assert.That(plan.SetsResolution, Is.False, "the window is left as the player started it");
        }

        [Test]
        public void TheChosenValuesGoInAsTheyAre()
        {
            var settings = new GraphicsSettings(new ScreenResolution(1280, 720), 0, true, 120);

            var plan = Plan(settings, projectQualityLevel: 1);

            Assert.That(plan.QualityLevel, Is.EqualTo(0));
            Assert.That(plan.VSyncCount, Is.EqualTo(1));
            Assert.That(plan.TargetFrameRate, Is.EqualTo(120));
            Assert.That(plan.SetsResolution, Is.True);
            Assert.That(plan.Resolution, Is.EqualTo(new ScreenResolution(1280, 720)));
        }

        [Test]
        public void ALevelPastTheProjectsLevelsIsLoweredToTheHighest()
        {
            var plan = Plan(GraphicsSettings.Default.WithQualityLevel(5), projectQualityLevel: 0);

            Assert.That(plan.QualityLevel, Is.EqualTo(1));
            Assert.That(plan.QualityClamped, Is.True);
        }

        [Test]
        public void WithoutQualityLevelsTheQualityIsLeftAlone()
        {
            var plan = GraphicsPlan.For(GraphicsSettings.Default.WithQualityLevel(2), false, 0, 0, 60, _display, false);

            Assert.That(plan.QualityLevel, Is.EqualTo(GraphicsPlan.KeepQualityLevel));
            Assert.That(plan.QualityClamped, Is.False);
        }

        [Test]
        public void GoingBackToTheDisplayResolutionResizesOnlyAfterAChange()
        {
            Assert.That(Plan(GraphicsSettings.Default, 0, resolutionOverridden: false).SetsResolution, Is.False);

            var back = Plan(GraphicsSettings.Default, 0, resolutionOverridden: true);
            Assert.That(back.SetsResolution, Is.True);
            Assert.That(back.Resolution, Is.EqualTo(_display));

            var unknownDisplay = GraphicsPlan.For(GraphicsSettings.Default, false, 2, 0, 60, ScreenResolution.Native, true);
            Assert.That(unknownDisplay.SetsResolution, Is.False, "nothing to go back to when the display size is unknown");
        }

        [TestCase(0, 60)]
        [TestCase(30, 30)]
        [TestCase(144, 60)]
        public void AHeadlessClientRunsAtTheDataRateOrTheLowerPlayerLimit(int limit, int expected)
        {
            var settings = new GraphicsSettings(new ScreenResolution(1920, 1080), 1, true, limit);

            var plan = GraphicsPlan.For(settings, true, 2, 0, 60, _display, true);

            Assert.That(plan.TargetFrameRate, Is.EqualTo(expected));
            Assert.That(plan.VSyncCount, Is.EqualTo(0), "there is no display to wait for");
            Assert.That(plan.QualityLevel, Is.EqualTo(GraphicsPlan.KeepQualityLevel));
            Assert.That(plan.SetsResolution, Is.False);
        }

        [Test]
        public void TheHeadlessRateComesFromTheCaller()
        {
            var plan = GraphicsPlan.For(GraphicsSettings.Default, true, 2, 0, 20, _display, false);

            Assert.That(plan.TargetFrameRate, Is.EqualTo(20));
        }

        [Test]
        public void BadInputsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => GraphicsPlan.For(null, false, 2, 0, 60, _display, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => GraphicsPlan.For(GraphicsSettings.Default, false, -1, 0, 60, _display, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => GraphicsPlan.For(GraphicsSettings.Default, true, 2, 0, 0, _display, false));
        }

        private static GraphicsPlan Plan(GraphicsSettings settings, int projectQualityLevel, bool resolutionOverridden = false)
        {
            return GraphicsPlan.For(settings, false, 2, projectQualityLevel, 60, _display, resolutionOverridden);
        }
    }
}
