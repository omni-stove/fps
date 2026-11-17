using System;
using System.Numerics;
using Fps.Core;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    public sealed class ServerBotBrainAimTests
    {
        private static readonly Vector3 _eye = new Vector3(0f, 1.6f, 0f);

        [Test]
        public void TheDefaultBotFiresWhicheverWayItFaces()
        {
            var agent = Agent(NetBotSpec.Default);

            Assert.That(agent.IsFacing(_eye, new Vector3(0f, 0f, -1f), new Vector3(0f, 1.6f, 10f)), Is.True);
        }

        [Test]
        public void ABotFacingAwayFromTheTargetBeyondTheFacingToleranceDoesNotFire()
        {
            var agent = Agent(TuningJson.ParseBots("{\"facingToleranceDegrees\": 10}", "bots.json"));
            var target = new Vector3(0f, 1.6f, 10f);

            Assert.That(agent.IsFacing(_eye, Direction(5f), target), Is.True);
            Assert.That(agent.IsFacing(_eye, Direction(15f), target), Is.False);
        }

        [Test]
        public void TheFacingToleranceComparesOnlyTheHorizontalDirection()
        {
            var agent = Agent(TuningJson.ParseBots("{\"facingToleranceDegrees\": 1}", "bots.json"));

            Assert.That(agent.IsFacing(_eye, Vector3.Normalize(new Vector3(0f, -0.5f, 1f)), new Vector3(0f, 10f, 10f)), Is.True);
            Assert.That(agent.IsFacing(_eye, new Vector3(0f, 0f, 1f), null), Is.True);
            Assert.That(agent.IsFacing(_eye, new Vector3(0f, 0f, 1f), new Vector3(0f, 20f, 0f)), Is.True, "straight above");
        }

        [Test]
        public void TheDefaultBotShootsStraightAlongItsAim()
        {
            var agent = Agent(NetBotSpec.Default);
            var direction = Vector3.Normalize(new Vector3(1f, 0f, 2f));

            var shot = agent.Scatter(_eye, direction, new Vector3(0f, 1.6f, 20f));

            Assert.That(Vector3.Distance(shot, direction), Is.LessThan(1e-6f));
        }

        [Test]
        public void TheOpeningSpreadScattersTheFirstShotWithinItsCone()
        {
            var agent = Agent(TuningJson.ParseBots("{\"openingSpreadDegrees\": 5, \"openingShots\": 3}", "bots.json"));
            var direction = new Vector3(0f, 0f, 1f);
            var scattered = false;

            for (var i = 0; i < 20; i++)
            {
                var shot = agent.Scatter(_eye, direction, new Vector3(0f, 1.6f, 10f));
                var degrees = Math.Acos(Math.Min(1f, Vector3.Dot(shot, direction))) * 180.0 / Math.PI;
                Assert.That(degrees, Is.LessThanOrEqualTo(5.0 + 1e-3));
                scattered |= degrees > 0.01;
            }

            Assert.That(scattered, Is.True);
        }

        [Test]
        public void TheRangeSpreadScattersOnlyBeyondTheAttackRange()
        {
            var agent = Agent(TuningJson.ParseBots(
                "{\"attackRange\": 10, \"fireRange\": 30, \"rangeSpreadDegrees\": 4}", "bots.json"));
            var direction = new Vector3(0f, 0f, 1f);

            var near = agent.Scatter(_eye, direction, new Vector3(0f, 1.6f, 8f));
            var far = agent.Scatter(_eye, direction, new Vector3(0f, 1.6f, 30f));

            Assert.That(Vector3.Distance(near, direction), Is.LessThan(1e-6f));
            var degrees = Math.Acos(Math.Min(1f, Vector3.Dot(far, direction))) * 180.0 / Math.PI;
            Assert.That(degrees, Is.GreaterThan(0.0).And.LessThanOrEqualTo(4.0 + 1e-3));
        }

        private static Vector3 Direction(float degreesFromForward)
        {
            var radians = degreesFromForward * Math.PI / 180.0;
            return new Vector3((float)Math.Sin(radians), 0f, (float)Math.Cos(radians));
        }

        private static ServerBotAgent Agent(NetBotSpec spec)
        {
            var map = BotTestMaps.Flat(BotTestMaps.Points(new Vector3(-10f, 1.1f, -10f), new Vector3(10f, 1.1f, 10f)));
            var navigation = new ServerBotNavigation(
                map.Navigation, map.NavMesh.Links, map.Ladders.Ladders, map.World, _ => false, spec.NavSampleRadius, 1f);
            return new ServerBotAgent(1, spec, navigation, Array.Empty<Vector3>(), new Random(1), new SeeEverything());
        }

        private sealed class SeeEverything : ISightBlocker
        {
            public bool Blocks(Vector3 from, Vector3 to) => false;
        }
    }
}
