using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class RiotShieldSpecTests
    {
        [Test]
        public void TheDefaultsAreTheDefaultConstants()
        {
            var spec = RiotShieldSpec.Default;

            Assert.That(spec.BlockArcDegrees, Is.EqualTo(RiotShieldSpec.DefaultBlockArcDegrees));
            Assert.That(spec.Width, Is.EqualTo(RiotShieldSpec.DefaultWidth));
            Assert.That(spec.Height, Is.EqualTo(RiotShieldSpec.DefaultHeight));
            Assert.That(spec.MoveSpeedMultiplier, Is.EqualTo(RiotShieldSpec.DefaultMoveSpeedMultiplier));
            Assert.That(spec.Bash, Is.SameAs(RiotShieldSpec.DefaultBash));
            Assert.That(spec.Bash.Damage, Is.EqualTo(RiotShieldSpec.DefaultBashDamage));
        }

        [TestCase(0f, 0.6f, 1.2f, 0.8f, "blockArcDegrees")]
        [TestCase(361f, 0.6f, 1.2f, 0.8f, "blockArcDegrees")]
        [TestCase(float.NaN, 0.6f, 1.2f, 0.8f, "blockArcDegrees")]
        [TestCase(120f, 0f, 1.2f, 0.8f, "width")]
        [TestCase(120f, float.PositiveInfinity, 1.2f, 0.8f, "width")]
        [TestCase(120f, 0.6f, -1f, 0.8f, "height")]
        [TestCase(120f, 0.6f, 1.2f, 0f, "moveSpeedMultiplier")]
        [TestCase(120f, 0.6f, 1.2f, 1.1f, "moveSpeedMultiplier")]
        public void BadValuesAreRefusedByName(float arc, float width, float height, float move, string param)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => new RiotShieldSpec(arc, width, height, move));

            Assert.That(e.ParamName, Is.EqualTo(param));
        }

        // 盾は +Z を向く。止める扇は撃った者の水平の向きで見るので、正面の撃ち手は止まり、真後ろの撃ち手は通る。
        [Test]
        public void TheBlockArcCoversTheFrontAndNotTheBack()
        {
            var spec = new RiotShieldSpec(blockArcDegrees: 120f);
            var shield = Vector3.Zero;
            var forward = Vector3.UnitZ;

            Assert.That(MeleeJudge.IsWithinArc(shield, forward, spec.BlockArcDegrees, new Vector3(0f, 1f, 10f)), Is.True, "in front");
            Assert.That(MeleeJudge.IsWithinArc(shield, forward, spec.BlockArcDegrees, new Vector3(5f, 0f, 5f)), Is.True, "45 degrees off");
            Assert.That(MeleeJudge.IsWithinArc(shield, forward, spec.BlockArcDegrees, new Vector3(10f, 0f, 0f)), Is.False, "beside, outside 60 degrees");
            Assert.That(MeleeJudge.IsWithinArc(shield, forward, spec.BlockArcDegrees, new Vector3(0f, 0f, -10f)), Is.False, "behind");
        }

        [Test]
        public void AFullCircleBlocksFromAnySide()
        {
            var spec = new RiotShieldSpec(blockArcDegrees: RiotShieldSpec.MaxBlockArcDegrees);

            Assert.That(MeleeJudge.IsWithinArc(Vector3.Zero, Vector3.UnitZ, spec.BlockArcDegrees, new Vector3(0f, 0f, -10f)), Is.True);
        }

        // 盾で殴るのは近接攻撃と同じ振り (MeleeSwing) と当たりの選び方 (MeleeJudge) に盾の値を渡すこと。
        [Test]
        public void TheBashSwingsWithTheShieldValuesAndHitsOnlyAhead()
        {
            var bash = new MeleeSpec(45, 1.5f, 90f, 0.6f, 0.5f, 0.25f, 0f);
            var spec = new RiotShieldSpec(bash: bash);
            var swing = new MeleeSwing(spec.Bash);

            Assert.That(swing.TryStart(), Is.True);
            Assert.That(swing.Tick(0.2f), Is.False);
            Assert.That(swing.Tick(0.1f), Is.True, "it hits once the bash impact time has passed");
            Assert.That(swing.CurrentSpec.Damage, Is.EqualTo(45));

            var ahead = new[] { new Vector3(0f, 0f, 1f) };
            var behind = new[] { new Vector3(0f, 0f, -1f) };
            var outOfReach = new[] { new Vector3(0f, 0f, 2f) };
            Assert.That(MeleeJudge.SelectNearest(Vector3.Zero, Vector3.UnitZ, spec.Bash, ahead), Is.EqualTo(0));
            Assert.That(MeleeJudge.SelectNearest(Vector3.Zero, Vector3.UnitZ, spec.Bash, behind), Is.EqualTo(-1));
            Assert.That(MeleeJudge.SelectNearest(Vector3.Zero, Vector3.UnitZ, spec.Bash, outOfReach), Is.EqualTo(-1));
        }
    }
}
