using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class DecoyCoreTests
    {
        [Test]
        public void TheDefaultSpecIsValid()
        {
            var spec = DecoySpec.Default;

            Assert.That(spec.LifetimeSeconds, Is.EqualTo(DecoySpec.DefaultLifetimeSeconds));
            Assert.That(spec.CallIntervalSeconds, Is.EqualTo(DecoySpec.DefaultCallIntervalSeconds));
            Assert.That(spec.CallRadius, Is.EqualTo(DecoySpec.DefaultCallRadius));
        }

        [TestCase(0f, 1f, 1f)]
        [TestCase(float.PositiveInfinity, 1f, 1f)]
        [TestCase(float.NaN, 1f, 1f)]
        [TestCase(1f, 0f, 1f)]
        [TestCase(1f, float.NaN, 1f)]
        [TestCase(1f, 1f, -1f)]
        [TestCase(1f, 1f, float.PositiveInfinity)]
        [TestCase(1f, 1f, float.NaN)]
        public void InvalidSpecsAreRejected(float seconds, float interval, float radius)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DecoySpec(seconds, interval, radius));
        }

        [Test]
        public void AZeroCallRadiusIsAllowed()
        {
            Assert.That(new DecoySpec(1f, 1f, 0f).CallRadius, Is.Zero);
        }

        [Test]
        public void ItCallsWhenPlacedAndThenEveryInterval()
        {
            var state = new DecoyState(new DecoySpec(10f, 2f, 5f));

            Assert.That(state.Tick(0f), Is.EqualTo(1), "the first call comes on placing");
            Assert.That(state.Tick(1.5f), Is.Zero);
            Assert.That(state.Tick(0.5f), Is.EqualTo(1));
            Assert.That(state.Tick(4.5f), Is.EqualTo(2), "a long tick makes every call it crossed");
            Assert.That(state.Calls, Is.EqualTo(4));
            Assert.That(state.Over, Is.Null);
        }

        [Test]
        public void ItExpiresWithoutACallAtTheVeryEnd()
        {
            var state = new DecoyState(new DecoySpec(4f, 2f, 5f));

            Assert.That(state.Tick(3.9f), Is.EqualTo(2));
            Assert.That(state.SecondsLeft, Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(state.Tick(1f), Is.Zero, "no call at the moment it disappears");
            Assert.That(state.Over, Is.EqualTo(DeployRemoval.Expired));
            Assert.That(state.SecondsLeft, Is.Zero);
            Assert.That(state.Tick(1f), Is.Zero);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidTicksAreRejected(float delta)
        {
            var state = new DecoyState(DecoySpec.Default);

            Assert.Throws<ArgumentOutOfRangeException>(() => state.Tick(delta));
        }

        [Test]
        public void TheNearestSeenHostileDecoyIsChosen()
        {
            var decoys = new[]
            {
                new DecoyOption(true, new Vector3(0f, 0f, 6f), true, true, false),
                new DecoyOption(false, new Vector3(0f, 0f, 1f), true, true, false),
                new DecoyOption(true, new Vector3(0f, 0f, 2f), false, true, false),
                new DecoyOption(true, new Vector3(0f, 0f, 3f), true, false, true),
                new DecoyOption(true, new Vector3(0f, 0f, 4f), true, true, false),
            };

            Assert.That(DecoyLure.SelectSeen(Vector3.Zero, null, false, decoys), Is.EqualTo(4));
        }

        [Test]
        public void ASeenTargetNearerThanTheDecoyKeepsTheEnemysAttention()
        {
            var decoys = new[] { new DecoyOption(true, new Vector3(0f, 0f, 5f), true, true, false) };

            Assert.That(DecoyLure.SelectSeen(Vector3.Zero, new Vector3(0f, 0f, 4f), true, decoys), Is.EqualTo(-1));
            Assert.That(DecoyLure.SelectSeen(Vector3.Zero, new Vector3(0f, 0f, 5f), true, decoys), Is.EqualTo(-1), "a tie keeps the target");
            Assert.That(DecoyLure.SelectSeen(Vector3.Zero, new Vector3(0f, 0f, 6f), true, decoys), Is.Zero);
            Assert.That(DecoyLure.SelectSeen(Vector3.Zero, new Vector3(0f, 0f, 4f), false, decoys), Is.Zero, "an unseen target does not count");
        }

        [Test]
        public void OnlyAHeardHostileDecoyIsHeard()
        {
            var decoys = new[]
            {
                new DecoyOption(true, new Vector3(0f, 0f, 2f), true, true, false),
                new DecoyOption(false, new Vector3(0f, 0f, 1f), true, false, true),
                new DecoyOption(true, new Vector3(0f, 0f, 3f), true, false, true),
            };

            Assert.That(DecoyLure.SelectHeard(Vector3.Zero, decoys), Is.EqualTo(2));
            Assert.That(DecoyLure.SelectHeard(Vector3.Zero, Array.Empty<DecoyOption>()), Is.EqualTo(-1));
            Assert.Throws<ArgumentNullException>(() => DecoyLure.SelectHeard(Vector3.Zero, null));
        }

        [Test]
        public void PresentingShowsTheDecoyAsASeenTargetAndKeepsTheRest()
        {
            var noise = new Vector3(9f, 0f, 9f);
            var hit = new Vector3(-3f, 0f, 0f);
            var perception = new EnemyPerception(
                Vector3.Zero, new Vector3(0f, 0f, 20f), false, true, noise, true, hit, 0.5f, 0f, true, 0.7f);

            var decoy = new Vector3(1f, 0f, 2f);
            var presented = DecoyLure.Present(perception, decoy);

            Assert.That(presented.Target, Is.EqualTo(decoy));
            Assert.That(presented.HasLineOfSight, Is.True);
            Assert.That(presented.Awareness, Is.EqualTo(EnemyPerception.FullAwareness));
            Assert.That(presented.NoisePosition, Is.EqualTo(noise));
            Assert.That(presented.WasHit, Is.True);
            Assert.That(presented.HitSource, Is.EqualTo(hit));
            Assert.That(presented.HealthFraction, Is.EqualTo(0.5f));
            Assert.That(presented.IsSuppressed, Is.True);
            Assert.That(presented.SuppressionLevel, Is.EqualTo(0.7f));
        }

        [Test]
        public void HearingTheDecoyDoesNotDrownAnotherNoise()
        {
            var target = new Vector3(0f, 0f, 20f);
            var quiet = new EnemyPerception(Vector3.Zero, target, false, false, null);
            var decoy = new Vector3(1f, 0f, 2f);

            var heard = DecoyLure.Hear(quiet, decoy);
            Assert.That(heard.HeardNoise, Is.True);
            Assert.That(heard.NoisePosition, Is.EqualTo(decoy));
            Assert.That(heard.Target, Is.EqualTo(target), "hearing does not change the target");

            var shot = new EnemyPerception(Vector3.Zero, target, false, true, new Vector3(5f, 0f, 5f));
            Assert.That(DecoyLure.Hear(shot, decoy).NoisePosition, Is.EqualTo(new Vector3(5f, 0f, 5f)));
        }

        [Test]
        public void TheLogLinesNameTheOwnerTheKindAndTheEnemy()
        {
            Assert.That(
                DecoyLog.Armed("Player", "deploy-decoy", 2, 20f, 2f, 10f, Stealth.On),
                Is.EqualTo("[Decoy] owner=Player kind=deploy-decoy serial=2 armed seconds=20.0 interval=2.0 radius=10.0 stealth=On"));
            Assert.That(
                DecoyLog.Called("Player", "deploy-decoy", 2, new Vector3(1f, 0f, -2.5f)),
                Is.EqualTo("[Decoy] owner=Player kind=deploy-decoy serial=2 call at=(1.00, 0.00, -2.50)"));
            Assert.That(
                DecoyLog.Seen("guard", "Player", "deploy-decoy", 2),
                Is.EqualTo("[Decoy] enemy=guard sees owner=Player kind=deploy-decoy serial=2"));
            Assert.That(
                DecoyLog.Heard("guard", null, "deploy-decoy", 2, Vector3.Zero),
                Is.EqualTo("[Decoy] enemy=guard hears owner=- kind=deploy-decoy serial=2 at=(0.00, 0.00, 0.00)"));
            Assert.That(
                DecoyLog.Lost("guard", "Player", "deploy-decoy", 2, new Vector3(1f, 0f, 2f), true),
                Is.EqualTo("[Decoy] enemy=guard lost owner=Player kind=deploy-decoy serial=2 last=(1.00, 0.00, 2.00) gone=True"));
            Assert.That(DecoyLog.Seen("guard", "Player", "deploy-decoy", 2), Does.StartWith(DecoyLog.EnemyPrefix("guard")));
        }
    }
}
