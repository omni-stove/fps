using System;
using System.Linq;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    // 誘導する飛ぶ弾。届いたロックオンの相手を確かめ、確かめられた相手へだけ曲がる
    public sealed partial class ServerCombatTests
    {
        private const float _homingCone = 8f;

        [Test]
        public void AHomingRoundCurvesOntoTheLockedTarget()
        {
            Start(Rules(), rifle: HomingRifle());
            Join(1);
            Join(2);
            Step(0.2f);

            FireLocked(1, AimBeside(1, 2, 5f), lockTarget: 2);
            Step(2f);

            Assert.That(_log.ToString(), Does.Contain("[Combat] net lock-on shooter=1 target=2 accepted"));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 25), _log.ToString());
            Assert.That(_transport.Read<ProjectileEndedMessage>(2, Copy).Single().Reason, Is.EqualTo(ProjectileEndReason.HitCharacter));
        }

        [Test]
        public void WithoutALockTheSameShotFliesStraightPast()
        {
            Start(Rules(), rifle: HomingRifle());
            Join(1);
            Join(2);
            Step(0.2f);

            var eye = Body(1).EyePosition;
            Fire(1, eye, AimBeside(1, 2, 5f), (int)_host.TickCount);
            Step(_projectileLifetime + 0.2f);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), _log.ToString());
            Assert.That(_log.ToString(), Does.Not.Contain("net lock-on"));
        }

        [Test]
        public void ALockOnATargetOutsideTheConeIsRefusedAndTheRoundFliesStraight()
        {
            Start(Rules(), rifle: HomingRifle());
            Join(1);
            Join(2);
            Step(0.2f);

            FireLocked(1, AimBeside(1, 2, _homingCone + 4f), lockTarget: 2);
            Step(_projectileLifetime + 0.2f);

            Assert.That(_log.ToString(), Does.Contain("[Combat] net lock-on shooter=1 target=2 rejected reason=outside-cone"));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), _log.ToString());
        }

        [Test]
        public void ALockOnThroughAWallIsRefused()
        {
            Start(Rules(), wall: true, rifle: HomingRifle());
            Join(1);
            Join(2);
            Step(0.2f);

            FireLocked(1, Body(2).Center - Body(1).EyePosition, lockTarget: 2);

            Assert.That(_log.ToString(), Does.Contain("[Combat] net lock-on shooter=1 target=2 rejected reason=blocked"));
        }

        [TestCase(1, "self")]
        [TestCase(9, "not-hittable")]
        public void ALockOnTheShooterOrSomeoneNotInTheMatchIsRefused(int target, string reason)
        {
            Start(Rules(), rifle: HomingRifle());
            Join(1);
            Join(2);
            Step(0.2f);

            FireLocked(1, Body(2).Center - Body(1).EyePosition, lockTarget: target);

            Assert.That(_log.ToString(), Does.Contain("[Combat] net lock-on shooter=1 target=" + target + " rejected reason=" + reason));
        }

        [Test]
        public void ALockOnAWeaponThatDoesNotHomeIsIgnored()
        {
            Start(Rules(), rifle: ProjectileRifle(10f, 0f));
            Join(1);
            Join(2);
            Step(0.2f);

            FireLocked(1, AimBeside(1, 2, 5f), lockTarget: 2);
            Step(_projectileLifetime + 0.2f);

            Assert.That(_log.ToString(), Does.Not.Contain("net lock-on"));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
        }

        private static WeaponSpec HomingRifle()
        {
            var rifle = TestRules.Rifle();
            var round = new RoundSpec(
                RoundDelivery.Projectile,
                rifle.Round.Range,
                projectile: new ProjectileSpec(10f, 0f, _projectileLifetime),
                homing: new HomingSpec(1f, _homingCone, 180f));
            return new WeaponSpec(
                rifle.Id, rifle.NameKey, rifle.Category, rifle.Trigger, rifle.Ammo, round, rifle.Impact, rifle.Aim, rifle.AmmoTypes, rifle.ShotNoiseRadius);
        }

        // shooter の目から target の中心を、水平に degrees だけ外した向き。まっすぐ飛ぶ弾は当たらない
        private Vector3 AimBeside(int shooter, int target, float degrees)
        {
            var toTarget = Vector3.Normalize(Body(target).Center - Body(shooter).EyePosition);
            var radians = degrees * MathF.PI / 180f;
            return Vector3.Transform(toTarget, Quaternion.CreateFromAxisAngle(Vector3.UnitY, radians));
        }

        private void FireLocked(int shooter, Vector3 direction, int lockTarget)
        {
            Send(shooter, new FireRequestMessage
            {
                WeaponId = TestRules.RifleId,
                Origin = Body(shooter).EyePosition,
                Direction = direction,
                ClientTick = (int)_host.TickCount,
                InterpolationAlpha = 0f,
                ShotIndex = _shotIndex++,
                ShotGeneration = 0,
                HasLockTarget = true,
                LockTargetId = lockTarget,
            });
        }
    }
}
