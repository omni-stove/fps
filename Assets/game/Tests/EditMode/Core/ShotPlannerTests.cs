using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ShotPlannerTests
    {
        [TestCase(0f, 0, 0)]
        [TestCase(3f, 7, 12)]
        [TestCase(90f, -4, int.MaxValue)]
        public void TodaysWeaponsFireOneRoundAlongTheSpreadDirection(float coneDegrees, int playerId, int shotIndex)
        {
            var origin = new Vector3(1f, 2f, 3f);
            var aim = new Vector3(0.2f, -0.1f, 1f);

            var plan = ShotPlanner.Plan(RoundSpec.Default, origin, aim, coneDegrees, playerId, shotIndex);

            Assert.That(plan.Round, Is.SameAs(RoundSpec.Default));
            Assert.That(plan.ShotIndex, Is.EqualTo(shotIndex));
            Assert.That(plan.Rounds.Count, Is.EqualTo(1));
            Assert.That(plan.Rounds[0].Origin, Is.EqualTo(origin));
            Assert.That(plan.Rounds[0].Index, Is.EqualTo(0));
            Assert.That(plan.Rounds[0].Direction, Is.EqualTo(SpreadPattern.Direction(aim, coneDegrees, playerId, shotIndex)));
        }

        [Test]
        public void AShotgunPlansOneRoundPerPelletAroundTheSpreadCenter()
        {
            var shotgun = new RoundSpec(pelletCount: 8, pelletSpreadDegrees: 6f);
            var origin = new Vector3(1f, 2f, 3f);
            var aim = new Vector3(1f, 0f, 1f);

            var plan = ShotPlanner.Plan(shotgun, origin, aim, 4f, 1, 2);

            var center = SpreadPattern.Direction(aim, 4f, 1, 2);
            Assert.That(plan.Rounds.Count, Is.EqualTo(8));
            for (var i = 0; i < plan.Rounds.Count; i++)
            {
                Assert.That(plan.Rounds[i].Index, Is.EqualTo(i));
                Assert.That(plan.Rounds[i].Origin, Is.EqualTo(origin));
                Assert.That(plan.Rounds[i].Direction, Is.EqualTo(PelletPattern.Direction(shotgun, center, 1, 2, i)));
            }
        }

        [Test]
        public void ThePelletStreamsDifferFromTheOtherStreams()
        {
            var streams = new[]
            {
                SpreadPattern.PolarStream,
                SpreadPattern.AzimuthStream,
                SpreadPattern.RecoilYawStream,
                SpreadPattern.BlindFirePolarStream,
                SpreadPattern.BlindFireAzimuthStream,
                SpreadPattern.PelletPolarStream,
                SpreadPattern.PelletAzimuthStream,
            };

            Assert.That(streams, Is.Unique);
        }

        [Test]
        public void RejectsInvalidInput()
        {
            Assert.Throws<ArgumentNullException>(() => ShotPlanner.Plan(null, Vector3.Zero, Vector3.UnitZ, 0f, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ShotPlanner.Plan(RoundSpec.Default, new Vector3(float.NaN, 0f, 0f), Vector3.UnitZ, 0f, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ShotPlanner.Plan(RoundSpec.Default, Vector3.Zero, Vector3.Zero, 0f, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ShotPlanner.Plan(RoundSpec.Default, Vector3.Zero, Vector3.UnitZ, 91f, 0, 0));
            Assert.Throws<ArgumentException>(() => new ShotPlan(RoundSpec.Default, 0, Array.Empty<PlannedRound>()));
            Assert.Throws<ArgumentNullException>(() => new BeamState(null));
        }
    }
}
