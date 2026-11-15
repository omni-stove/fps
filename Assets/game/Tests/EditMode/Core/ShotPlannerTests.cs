using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
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
        public void PelletsAreOneRoundAlongTheCenterForNow()
        {
            var shotgun = new RoundSpec(pelletCount: 8, pelletSpreadDegrees: 6f);
            var center = Vector3.Normalize(new Vector3(1f, 0f, 1f));

            Assert.That(PelletPattern.Count(shotgun), Is.EqualTo(1));
            Assert.That(PelletPattern.Direction(shotgun, center, 1, 2, 0), Is.EqualTo(center));
            Assert.That(ShotPlanner.Plan(shotgun, Vector3.Zero, center, 0f, 1, 2).Rounds.Count, Is.EqualTo(1));
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
        public void TheRoundHooksDoNothingForNow()
        {
            Assert.That(PenetrationRule.TryPassThrough(new PenetrationSpec(2, 0.5f, 0.3f), 0, "concrete", 0.1f, out var multiplier), Is.False);
            Assert.That(multiplier, Is.EqualTo(1f));

            var direction = Vector3.Normalize(new Vector3(1f, -0.1f, 0f));
            Assert.That(RicochetRule.TryBounce(new RicochetSpec(1, 20f, 0.5f), 0, direction, Vector3.UnitY, out var bounced, out multiplier), Is.False);
            Assert.That(bounced, Is.EqualTo(direction));
            Assert.That(multiplier, Is.EqualTo(1f));

            var velocity = new Vector3(0f, 0f, 30f);
            Assert.That(ProjectileSteering.Steer(new HomingSpec(1f, 10f, 90f), Vector3.Zero, velocity, Vector3.UnitX, 0.1f), Is.EqualTo(velocity));

            Assert.That(SprayCone.Contains(new SpraySpec(0.1f, 20f), 8f, Vector3.Zero, Vector3.UnitZ, new Vector3(0f, 0f, 2f)), Is.False);

            var beam = new BeamState(new BeamSpec(0.1f));
            beam.Start();
            Assert.That(beam.Tick(1f), Is.EqualTo(0));
            Assert.That(beam.IsFiring, Is.True);
            Assert.That(beam.FiringSeconds, Is.EqualTo(1f));
            beam.Stop();
            Assert.That(beam.IsFiring, Is.False);
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
