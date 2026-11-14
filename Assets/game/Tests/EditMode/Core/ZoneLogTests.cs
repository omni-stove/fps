using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ZoneLogTests
    {
        private static ZoneSchedule Schedule()
        {
            return new ZoneSchedule(
                new Vector2(1f, 2f),
                60f,
                new[] { new ZonePhase(30f, 20f, 35f, 1f), new ZonePhase(10f, 5f, 35f, 2.5f) },
                7);
        }

        [Test]
        public void StartedWritesTheInitialCircleAndSeed()
        {
            Assert.That(
                ZoneLog.Started(Schedule()),
                Is.EqualTo("[Zone] start center=(1.00, 2.00) radius=60.00 phases=2 seed=7 total=65.0"));
        }

        [Test]
        public void StageWritesTheOneBasedPhaseAndTheNextCircle()
        {
            var schedule = Schedule();
            var next = schedule.TargetCenter(0);
            var expectedNext = "(" + next.X.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                + ", " + next.Y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ")";

            Assert.That(
                ZoneLog.Stage(schedule.Evaluate(12f), schedule.PhaseCount),
                Is.EqualTo("[Zone] phase=1/2 stage=Waiting radius=60.00 center=(1.00, 2.00) next_radius=35.00 next_center="
                    + expectedNext + " dps=1.0 in=18.0"));
        }

        [Test]
        public void ClosedStageOmitsTheNextCircle()
        {
            var schedule = Schedule();
            var line = ZoneLog.Stage(schedule.Evaluate(100f), schedule.PhaseCount);

            Assert.That(line, Does.StartWith("[Zone] phase=2/2 stage=Closed radius=35.00 center=("));
            Assert.That(line, Does.EndWith(" dps=2.5"));
            Assert.That(line, Does.Not.Contain("next_"));
            Assert.That(line, Does.Not.Contain(" in="));
        }

        [Test]
        public void TargetLinesKeepValuesWithoutSpaces()
        {
            Assert.That(ZoneLog.Outside("Enemy 1"), Is.EqualTo("[Zone] outside target=Enemy_1"));
            Assert.That(ZoneLog.Inside(" "), Is.EqualTo("[Zone] inside target=-"));
            Assert.That(ZoneLog.Inside(null), Is.EqualTo("[Zone] inside target=-"));
            Assert.That(ZoneLog.Stopped(), Is.EqualTo("[Zone] stop"));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            var state = Schedule().Evaluate(0f);

            Assert.Throws<ArgumentNullException>(() => ZoneLog.Started(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => ZoneLog.Stage(state, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ZoneLog.Stage(Schedule().Evaluate(100f), 1));
        }

        [Test]
        public void HudLineShowsTheTimeUntilTheNextShrink()
        {
            var schedule = Schedule();

            Assert.That(ZoneHudText.Line(schedule.Evaluate(17.5f)), Is.EqualTo(new LocalizedText("hud.zone.waiting", "00:13")));
            Assert.That(GameText.Resolve(ZoneHudText.Line(schedule.Evaluate(17.5f))), Is.EqualTo("エリア縮小まで 00:13"));
            Assert.That(GameText.Resolve(ZoneHudText.Line(schedule.Evaluate(40f))), Is.EqualTo("エリア縮小中 00:10"));
            Assert.That(GameText.Resolve(ZoneHudText.Line(schedule.Evaluate(100f))), Is.EqualTo("最終エリア"));
        }
    }
}
