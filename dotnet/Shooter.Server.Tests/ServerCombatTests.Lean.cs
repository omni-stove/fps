using System;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    public sealed partial class ServerCombatTests
    {
        // -X (撃つ人の方) を向くと、右は +Z
        private const float _facingAlpha = -MathF.PI / 2f;

        [Test]
        public void AShotAtTheLeanedHeadHitsAndAShotWhereTheHeadStoodMisses()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            var target = Body(2);
            var feet = Feet(target);
            target.SetPose(target.Position, _facingAlpha, PlayerStance.Standing, CharacterLadderState.NoLadder, 1f);
            Tick();

            FireAt(1, feet + UprightHeadEdge);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), _log.ToString());
            Assert.That(_log.ToString(), Does.Not.Contain("victim=2"));

            Step(0.2f);
            FireAt(1, feet + LeanedHead());

            Assert.That(_rules.Session.GetHealth(2), Is.LessThan(NetMatchSettings.DefaultMaxHealth), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[Net] hit attacker=1 victim=2 rewindTicks=0.00 zone=Head"));
        }

        [Test]
        public void TheShotWhereTheHeadStoodHitsTheHeadOfAnUprightTarget()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            var target = Body(2);
            target.SetPose(target.Position, _facingAlpha, PlayerStance.Standing, CharacterLadderState.NoLadder, 0f);
            Tick();

            FireAt(1, Feet(target) + UprightHeadEdge);

            Assert.That(_log.ToString(), Does.Contain("[Net] hit attacker=1 victim=2 rewindTicks=0.00 zone=Head"));
        }

        [Test]
        public void TheLeanIsJudgedAtTheTickTheShooterSaw()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            var target = Body(2);
            var feet = Feet(target);
            target.SetPose(target.Position, _facingAlpha, PlayerStance.Standing, CharacterLadderState.NoLadder, 1f);
            Tick();
            var seenTick = (int)_host.TickCount;

            target.SetPose(target.Position, _facingAlpha, PlayerStance.Standing, CharacterLadderState.NoLadder, 0f);
            for (var i = 0; i < 6; i++)
            {
                Tick();
            }

            FireAt(1, feet + LeanedHead());
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), "the target stands upright now");

            FireAt(1, feet + LeanedHead(), seenTick);

            Assert.That(_log.ToString(), Does.Contain("[Net] hit attacker=1 victim=2 rewindTicks=6.00 zone=Head"));
        }

        // 立った頭 (足元から 1.5 の球、半径 0.5) の、右へ傾けると離れる左の端
        private static Vector3 UprightHeadEdge => new Vector3(0f, 1.85f, -0.3f);

        // 右へ傾け切って目が +Z へ LeanSideOffset (0.4) ずれた体の、軸の上の足元から 1.9 の点。足元を軸に sin = 0.4 / 1.6 だけ回る。
        // 立った体の頭の球からは 0.5 より離れている
        private static Vector3 LeanedHead()
        {
            var offset = LeanStep.EyeOffset(1f, _facingAlpha, MotorSpec.Default);
            Assert.That(offset.Z, Is.EqualTo(0.4f).Within(1e-5f));
            return new Vector3(0f, 1.9f * 0.968246f, 1.9f * 0.25f);
        }

        private static Vector3 Feet(ServerBody body)
        {
            return body.Position + new Vector3(0f, body.Shape.CenterY - (body.Shape.Height / 2f), 0f);
        }
    }
}
