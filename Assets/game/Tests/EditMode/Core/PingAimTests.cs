using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class PingAimTests
    {
        private static readonly Vector3 _origin = Vector3.Zero;
        private static readonly Vector3 _forward = new Vector3(0f, 0f, 1f);
        private static readonly PingSpec _spec = new PingSpec(maxDistance: 50f, enemyAimToleranceDegrees: 5f);

        [Test]
        public void AnEnemyUnderTheAimIsMarked()
        {
            var candidates = new[] { new PingCandidate(new Vector3(0f, 0f, 10f), true, true) };

            var hit = PingAim.Resolve(_spec, _origin, _forward, new Vector3(0f, 0f, 9.5f), 0, candidates);

            Assert.That(hit.HasTarget, Is.True);
            Assert.That(hit.Kind, Is.EqualTo(PingKind.Enemy));
            Assert.That(hit.CandidateIndex, Is.EqualTo(0));
            Assert.That(hit.Position, Is.EqualTo(candidates[0].Position), "the mark goes on the enemy, not on the surface the ray hit");
        }

        [Test]
        public void AVisibleEnemyNearTheAimIsMarkedEvenIfTheRayMissesIt()
        {
            // 10m 先で 0.5m ずれた敵は約 2.9 度で、許容の 5 度に入る。
            var candidates = new[]
            {
                new PingCandidate(new Vector3(2f, 0f, 10f), true, true),
                new PingCandidate(new Vector3(0.5f, 0f, 10f), true, true),
            };

            var hit = PingAim.Resolve(_spec, _origin, _forward, new Vector3(0f, 0f, 30f), -1, candidates);

            Assert.That(hit.Kind, Is.EqualTo(PingKind.Enemy));
            Assert.That(hit.CandidateIndex, Is.EqualTo(1));
        }

        [Test]
        public void HiddenDeadOrFarEnemiesNearTheAimAreNotMarked()
        {
            var surface = new Vector3(0f, 0f, 5f);
            var candidates = new[]
            {
                new PingCandidate(new Vector3(0.1f, 0f, 10f), true, false),
                new PingCandidate(new Vector3(0.1f, 0f, 10f), false, true),
                new PingCandidate(new Vector3(0f, 0f, 60f), true, true),
                new PingCandidate(new Vector3(5f, 0f, 10f), true, true),
            };

            var hit = PingAim.Resolve(_spec, _origin, _forward, surface, -1, candidates);

            Assert.That(hit.Kind, Is.EqualTo(PingKind.Location));
            Assert.That(hit.Position, Is.EqualTo(surface));
            Assert.That(hit.CandidateIndex, Is.EqualTo(-1));
        }

        [Test]
        public void ADeadEnemyUnderTheAimMarksTheSurfaceInstead()
        {
            var surface = new Vector3(0f, 0f, 10f);
            var candidates = new[] { new PingCandidate(surface, false, true) };

            var hit = PingAim.Resolve(_spec, _origin, _forward, surface, 0, candidates);

            Assert.That(hit.Kind, Is.EqualTo(PingKind.Location));
        }

        [Test]
        public void TheClosestAngleWinsAndThenTheNearerEnemy()
        {
            var candidates = new[]
            {
                new PingCandidate(new Vector3(0f, 0f, 20f), true, true),
                new PingCandidate(new Vector3(0f, 0f, 8f), true, true),
            };

            var hit = PingAim.Resolve(_spec, _origin, _forward, null, -1, candidates);

            Assert.That(hit.CandidateIndex, Is.EqualTo(1));
        }

        [Test]
        public void NothingWithinRangeMeansNoTarget()
        {
            var none = PingAim.Resolve(_spec, _origin, _forward, null, -1, Array.Empty<PingCandidate>());
            var far = PingAim.Resolve(_spec, _origin, _forward, new Vector3(0f, 0f, 51f), -1, Array.Empty<PingCandidate>());

            Assert.That(none.HasTarget, Is.False);
            Assert.That(none.CandidateIndex, Is.EqualTo(-1));
            Assert.That(far.HasTarget, Is.False);
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            var candidates = Array.Empty<PingCandidate>();

            Assert.Throws<ArgumentNullException>(() => PingAim.Resolve(null, _origin, _forward, null, -1, candidates));
            Assert.Throws<ArgumentNullException>(() => PingAim.Resolve(_spec, _origin, _forward, null, -1, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => PingAim.Resolve(_spec, new Vector3(float.NaN, 0f, 0f), _forward, null, -1, candidates));
            Assert.Throws<ArgumentOutOfRangeException>(() => PingAim.Resolve(_spec, _origin, Vector3.Zero, null, -1, candidates));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PingAim.Resolve(_spec, _origin, _forward, new Vector3(float.PositiveInfinity, 0f, 0f), -1, candidates));
            Assert.Throws<ArgumentOutOfRangeException>(() => PingAim.Resolve(_spec, _origin, _forward, null, 0, candidates));
            Assert.Throws<ArgumentOutOfRangeException>(() => PingAim.Resolve(_spec, _origin, _forward, null, -2, candidates));
        }
    }
}
