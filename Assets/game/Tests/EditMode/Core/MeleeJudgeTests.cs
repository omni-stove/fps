using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class MeleeJudgeTests
    {
        private static readonly Vector3 _origin = Vector3.Zero;
        private static readonly Vector3 _forward = new Vector3(0f, 0f, 1f);

        private static MeleeSpec Spec(float reach = 2f, float arc = 90f) => new MeleeSpec(50, reach, arc, 0.5f, 0.4f, 0.2f, 0.75f);

        [Test]
        public void APointAheadWithinReachIsInTheFan()
        {
            Assert.That(MeleeJudge.IsInFan(_origin, _forward, Spec(), new Vector3(0f, 0f, 1.5f), out var distance), Is.True);
            Assert.That(distance, Is.EqualTo(1.5f).Within(1e-5f));
        }

        [Test]
        public void APointBeyondReachIsNotInTheFan()
        {
            Assert.That(MeleeJudge.IsInFan(_origin, _forward, Spec(), new Vector3(0f, 0f, 2.5f), out _), Is.False);
        }

        [Test]
        public void HeightCountsTowardsTheReach()
        {
            Assert.That(MeleeJudge.IsInFan(_origin, _forward, Spec(), new Vector3(0f, 3f, 1f), out _), Is.False, "a target on a ledge above is out of reach");
        }

        [Test]
        public void APointBehindIsNotInTheFan()
        {
            Assert.That(MeleeJudge.IsInFan(_origin, _forward, Spec(), new Vector3(0f, 0f, -1f), out _), Is.False);
        }

        [Test]
        public void TheFanEdgeIsHalfTheArc()
        {
            var at40 = new Vector3((float)Math.Sin(40 * Math.PI / 180), 0f, (float)Math.Cos(40 * Math.PI / 180));
            var at50 = new Vector3((float)Math.Sin(50 * Math.PI / 180), 0f, (float)Math.Cos(50 * Math.PI / 180));
            Assert.That(MeleeJudge.IsInFan(_origin, _forward, Spec(), at40, out _), Is.True);
            Assert.That(MeleeJudge.IsInFan(_origin, _forward, Spec(), at50, out _), Is.False);
        }

        [Test]
        public void AFullCircleReachesBehind()
        {
            Assert.That(MeleeJudge.IsInFan(_origin, _forward, Spec(arc: 360f), new Vector3(0f, 0f, -1f), out _), Is.True);
        }

        [Test]
        public void APointOnTopOfTheAttackerCounts()
        {
            Assert.That(MeleeJudge.IsInFan(_origin, _forward, Spec(), new Vector3(0f, 0.5f, 0f), out _), Is.True);
        }

        [Test]
        public void AVerticalForwardHasNoFan()
        {
            Assert.That(MeleeJudge.IsInFan(_origin, new Vector3(0f, 1f, 0f), Spec(), new Vector3(0f, 0f, 1f), out _), Is.False);
            Assert.That(MeleeJudge.IsInFan(_origin, _forward, Spec(), new Vector3(float.NaN, 0f, 1f), out _), Is.False);
        }

        [Test]
        public void SelectNearestPicksTheClosestInTheFan()
        {
            var candidates = new List<Vector3>
            {
                new Vector3(0f, 0f, 1.8f),
                new Vector3(0f, 0f, -0.5f),
                new Vector3(0.3f, 0f, 1f),
                new Vector3(0f, 0f, 5f),
            };

            Assert.That(MeleeJudge.SelectNearest(_origin, _forward, Spec(), candidates), Is.EqualTo(2));
        }

        [Test]
        public void SelectNearestKeepsTheFirstOnATie()
        {
            var candidates = new List<Vector3> { new Vector3(-0.5f, 0f, 1f), new Vector3(0.5f, 0f, 1f) };
            Assert.That(MeleeJudge.SelectNearest(_origin, _forward, Spec(), candidates), Is.EqualTo(0));
        }

        [Test]
        public void SelectNearestReturnsMinusOneWhenNothingIsInTheFan()
        {
            var candidates = new List<Vector3> { new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, 4f) };
            Assert.That(MeleeJudge.SelectNearest(_origin, _forward, Spec(), candidates), Is.EqualTo(-1));
            Assert.That(MeleeJudge.SelectNearest(_origin, _forward, Spec(), new List<Vector3>()), Is.EqualTo(-1));
        }

        [Test]
        public void NullArgumentsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => MeleeJudge.IsInFan(_origin, _forward, null, _forward, out _));
            Assert.Throws<ArgumentNullException>(() => MeleeJudge.SelectNearest(_origin, _forward, Spec(), null));
            Assert.Throws<ArgumentNullException>(() => MeleeJudge.SelectNearest(_origin, _forward, null, new List<Vector3>()));
        }

        [Test]
        public void HitDirectionIsHorizontalTowardsTheTarget()
        {
            var direction = MeleeJudge.HitDirection(_origin, _forward, new Vector3(1f, 2f, 0f));
            Assert.That(direction.X, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(direction.Y, Is.EqualTo(0f));
            Assert.That(direction.Z, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void HitDirectionFallsBackToTheSwing()
        {
            var direction = MeleeJudge.HitDirection(_origin, new Vector3(0f, 0.5f, 2f), new Vector3(0f, 1f, 0f));
            Assert.That(direction.Z, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(direction.Y, Is.EqualTo(0f));
        }
    }
}
