using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class DeployPlacementTests
    {
        private static readonly DeploySpec _spec = new DeploySpec(2, DeployOverflow.ReplaceOldest, 300, 4f, 30f, 0.25f, 2f, 1.4f, 0.2f);

        [Test]
        public void SpecRejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeploySpec(0, DeployOverflow.ReplaceOldest, 1, 1f, 30f, 0f, 1f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeploySpec(1, (DeployOverflow)99, 1, 1f, 30f, 0f, 1f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeploySpec(1, DeployOverflow.Refuse, 0, 1f, 30f, 0f, 1f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeploySpec(1, DeployOverflow.Refuse, 1, 0f, 30f, 0f, 1f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeploySpec(1, DeployOverflow.Refuse, 1, float.PositiveInfinity, 30f, 0f, 1f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeploySpec(1, DeployOverflow.Refuse, 1, 1f, -1f, 0f, 1f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeploySpec(1, DeployOverflow.Refuse, 1, 1f, 90f, 0f, 1f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeploySpec(1, DeployOverflow.Refuse, 1, 1f, 30f, -0.1f, 1f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeploySpec(1, DeployOverflow.Refuse, 1, 1f, 30f, 0f, 0f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeploySpec(1, DeployOverflow.Refuse, 1, 1f, 30f, 0f, 1f, float.NaN, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeploySpec(1, DeployOverflow.Refuse, 1, 1f, 30f, 0f, 1f, 1f, -1f));
        }

        [Test]
        public void TheDefaultSpecUsesTheDefaults()
        {
            var spec = DeploySpec.Default;

            Assert.That(spec.MaxActive, Is.EqualTo(DeploySpec.DefaultMaxActive));
            Assert.That(spec.Overflow, Is.EqualTo(DeploySpec.DefaultOverflow));
            Assert.That(spec.MaxHealth, Is.EqualTo(DeploySpec.DefaultMaxHealth));
            Assert.That(spec.MaxRange, Is.EqualTo(DeploySpec.DefaultMaxRange));
            Assert.That(spec.MaxSlopeDegrees, Is.EqualTo(DeploySpec.DefaultMaxSlopeDegrees));
            Assert.That(spec.MaxStepHeight, Is.EqualTo(DeploySpec.DefaultMaxStepHeight));
            Assert.That(spec.Width, Is.EqualTo(DeploySpec.DefaultWidth));
            Assert.That(spec.Height, Is.EqualTo(DeploySpec.DefaultHeight));
            Assert.That(spec.Depth, Is.EqualTo(DeploySpec.DefaultDepth));
        }

        [Test]
        public void FlatClearGroundIsOk()
        {
            var survey = new DeploySurvey(Vector3.UnitY, 0f, 0f, true, false);

            Assert.That(DeployPlacement.Judge(_spec, survey), Is.EqualTo(DeployVerdict.Ok));
        }

        [Test]
        public void NoGroundComesFirst()
        {
            Assert.That(DeployPlacement.Judge(_spec, DeploySurvey.NoGround), Is.EqualTo(DeployVerdict.NoGround));
        }

        [Test]
        public void SlopesUpToTheLimitAreOkAndSteeperOnesAreNot()
        {
            Assert.That(DeployPlacement.Judge(_spec, Flat(Tilted(29f))), Is.EqualTo(DeployVerdict.Ok));
            Assert.That(DeployPlacement.Judge(_spec, Flat(Tilted(31f))), Is.EqualTo(DeployVerdict.TooSteep));
            Assert.That(DeployPlacement.Judge(_spec, Flat(Vector3.UnitX)), Is.EqualTo(DeployVerdict.TooSteep), "a wall");
            Assert.That(DeployPlacement.Judge(_spec, Flat(-Vector3.UnitY)), Is.EqualTo(DeployVerdict.TooSteep), "a ceiling");
        }

        [Test]
        public void SteepnessWinsOverUnevenAndBlocked()
        {
            var survey = new DeploySurvey(Vector3.UnitX, -1f, 1f, false, true);

            Assert.That(DeployPlacement.Judge(_spec, survey), Is.EqualTo(DeployVerdict.TooSteep));
        }

        [Test]
        public void CornersOffTheGroundOrBeyondTheStepAreUneven()
        {
            Assert.That(DeployPlacement.Judge(_spec, new DeploySurvey(Vector3.UnitY, 0f, 0f, false, false)), Is.EqualTo(DeployVerdict.Uneven));
            Assert.That(DeployPlacement.Judge(_spec, new DeploySurvey(Vector3.UnitY, -0.3f, 0f, true, false)), Is.EqualTo(DeployVerdict.Uneven), "a ledge");
            Assert.That(DeployPlacement.Judge(_spec, new DeploySurvey(Vector3.UnitY, 0f, 0.3f, true, false)), Is.EqualTo(DeployVerdict.Uneven), "a step");
            Assert.That(DeployPlacement.Judge(_spec, new DeploySurvey(Vector3.UnitY, -0.25f, 0.25f, true, false)), Is.EqualTo(DeployVerdict.Ok), "within the step");
        }

        [Test]
        public void SomethingInTheWayIsBlocked()
        {
            Assert.That(DeployPlacement.Judge(_spec, new DeploySurvey(Vector3.UnitY, 0f, 0f, true, true)), Is.EqualTo(DeployVerdict.Blocked));
        }

        [Test]
        public void SlopeDegreesMeasuresFromStraightUp()
        {
            Assert.That(DeployPlacement.SlopeDegrees(Vector3.UnitY), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(DeployPlacement.SlopeDegrees(new Vector3(0f, 3f, 0f)), Is.EqualTo(0f).Within(1e-4f), "the length does not matter");
            Assert.That(DeployPlacement.SlopeDegrees(Tilted(45f)), Is.EqualTo(45f).Within(1e-3f));
            Assert.That(DeployPlacement.SlopeDegrees(Vector3.UnitZ), Is.EqualTo(90f).Within(1e-3f));
            Assert.That(DeployPlacement.SlopeDegrees(-Vector3.UnitY), Is.EqualTo(180f).Within(1e-3f));
            Assert.Throws<ArgumentOutOfRangeException>(() => DeployPlacement.SlopeDegrees(Vector3.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => DeployPlacement.SlopeDegrees(new Vector3(float.NaN, 1f, 0f)));
        }

        [Test]
        public void SurveyRejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeploySurvey(Vector3.Zero, 0f, 0f, true, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeploySurvey(Vector3.UnitY, float.NaN, 0f, true, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeploySurvey(Vector3.UnitY, 0f, float.PositiveInfinity, true, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeploySurvey(Vector3.UnitY, 0.2f, 0.1f, true, false));
            Assert.Throws<ArgumentNullException>(() => DeployPlacement.Judge(null, DeploySurvey.NoGround));
        }

        [Test]
        public void SurveyNormalizesTheNormal()
        {
            var survey = new DeploySurvey(new Vector3(0f, 2f, 0f), 0f, 0f, true, false);

            Assert.That(survey.HasGround, Is.True);
            Assert.That(survey.GroundNormal.Y, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(DeploySurvey.NoGround.HasGround, Is.False);
        }

        [Test]
        public void TheOwnerAndTheSameSideGetThrough()
        {
            Assert.That(DeployPlacement.LetsThrough(true, null, null), Is.True, "the owner");
            Assert.That(DeployPlacement.LetsThrough(true, Side.Alpha, Side.Bravo), Is.True, "the owner even if the side changed");
            Assert.That(DeployPlacement.LetsThrough(false, Side.Alpha, Side.Alpha), Is.True, "an ally");
            Assert.That(DeployPlacement.LetsThrough(false, Side.Alpha, Side.Bravo), Is.False, "an enemy");
            Assert.That(DeployPlacement.LetsThrough(false, Side.Alpha, null), Is.False, "a shooter without a side");
            Assert.That(DeployPlacement.LetsThrough(false, null, Side.Alpha), Is.False, "a shield without a side");
            Assert.That(DeployPlacement.LetsThrough(false, null, null), Is.False);
        }

        private static DeploySurvey Flat(Vector3 normal)
        {
            return new DeploySurvey(normal, 0f, 0f, true, false);
        }

        private static Vector3 Tilted(float degrees)
        {
            var radians = degrees * Math.PI / 180d;
            return new Vector3(0f, (float)Math.Cos(radians), (float)Math.Sin(radians));
        }
    }
}
