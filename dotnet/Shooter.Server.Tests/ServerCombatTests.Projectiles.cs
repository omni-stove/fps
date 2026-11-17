using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 飛ぶ弾 (RoundDelivery.Projectile) をサーバーで tick ごとに進め、今の位置の相手と世界に当てる
    public sealed partial class ServerCombatTests
    {
        [Test]
        public void AProjectileMovesEachTickAndIsSentToTheOthers()
        {
            const float speed = 10f;
            Start(Rules(), rifle: ProjectileRifle(speed, 0f));
            Join(1);
            Join(2);
            Step(0.2f);
            var origin = Body(1).EyePosition;
            var direction = Vector3.Normalize(Body(2).Center - origin);

            FireAt(1, Body(2).Center);

            var spawned = _transport.Read<ProjectileSpawnedMessage>(2, Copy).Single();
            Assert.That(spawned.WeaponId, Is.EqualTo(TestRules.RifleId));
            Assert.That(spawned.HasShooter, Is.True);
            Assert.That(spawned.ShooterId, Is.EqualTo(1));
            Assert.That(spawned.Position, Is.EqualTo(origin));
            Assert.That(Vector3.Distance(spawned.Velocity, direction * speed), Is.LessThan(1e-4f));
            Assert.That(_transport.Read<ProjectileSpawnedMessage>(1, Copy), Is.Empty, "the shooter shows its own projectile without waiting");

            Tick();
            var first = OnlyProjectile(2);
            Tick();
            var second = OnlyProjectile(2);

            Assert.That(first.ProjectileId, Is.EqualTo(spawned.ProjectileId));
            Assert.That(first.Present, Is.EqualTo(ProjectileFields.All));
            Assert.That(first.HasShooter, Is.True);
            Assert.That(first.ShooterId, Is.EqualTo(1));
            Assert.That(Vector3.Distance(first.Position, origin + (direction * speed * _tickSeconds)), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(second.Position, origin + (direction * speed * 2f * _tickSeconds)), Is.LessThan(1e-4f));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), "the projectile has not arrived yet");
        }

        [Test]
        public void AProjectileHitsTheTargetDamagesItAndEnds()
        {
            Start(Rules(), rifle: ProjectileRifle(40f, 0f));
            Join(1);
            Join(2);
            Step(0.2f);

            FireAt(1, Body(2).Center);
            Step(0.5f);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 25), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[Net] hit attacker=1 victim=2 rewindTicks=0.00"));
            Assert.That(_log.ToString(), Does.Contain("[Combat] net projectile id=0 shooter=1 weapon=" + TestRules.RifleId + " end=HitTarget"));
            foreach (var receiver in new[] { 1, 2 })
            {
                var ended = _transport.Read<ProjectileEndedMessage>(receiver, Copy).Single();
                Assert.That(ended.ProjectileId, Is.EqualTo(0));
                Assert.That(ended.Reason, Is.EqualTo(ProjectileEndReason.HitCharacter));
            }

            Assert.That(Snapshot(2).Projectiles.Count, Is.Zero);
            Assert.That(_transport.Read<HitConfirmMessage>(1, m => new HitConfirmMessage { Damage = m.Damage }).Single().Damage, Is.EqualTo(25));
            Assert.That(_transport.Read<ShotMessage>(2, m => m), Is.Empty, "a projectile is shown by its own messages");
        }

        [Test]
        public void AKillByAProjectileIsCreditedToTheWeapon()
        {
            Start(Rules(), rifle: ProjectileRifle(40f, 0f, damage: NetMatchSettings.DefaultMaxHealth));
            Join(1);
            Join(2);
            Step(0.2f);

            FireAt(1, Body(2).Center);
            Step(0.5f);

            Assert.That(_rules.Session.IsAlive(2), Is.False, _log.ToString());
            var killed = _transport.Read<KilledMessage>(2, Copy).Single();
            Assert.That(killed.KillerId, Is.EqualTo(1));
            Assert.That(killed.WeaponId, Is.EqualTo(TestRules.RifleId));
        }

        [Test]
        public void AProjectileIsJudgedAgainstWhereTheTargetIsNotWhereTheShooterSawIt()
        {
            Start(Rules(), rifle: ProjectileRifle(5f, 0f));
            Join(1);
            Join(2);
            Step(0.2f);

            FireAt(1, Body(2).Center);
            Walk(2, new Vector2(0f, 1f), 60, InputButtons.Sprint);
            Step(_projectileLifetime);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), _log.ToString());
            Assert.That(_transport.Read<ProjectileEndedMessage>(2, Copy).Single().Reason, Is.EqualTo(ProjectileEndReason.Expired));
        }

        [Test]
        public void AWallStopsTheProjectile()
        {
            Start(Rules(), wall: true, rifle: ProjectileRifle(40f, 0f));
            Join(1);
            Join(2);
            Step(0.2f);

            FireAt(1, Body(2).Center);
            Step(0.5f);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            var ended = _transport.Read<ProjectileEndedMessage>(2, Copy).Single();
            Assert.That(ended.Reason, Is.EqualTo(ProjectileEndReason.HitWorld));
            Assert.That(ended.Position.X, Is.EqualTo(-0.25f).Within(1e-3f));
        }

        [Test]
        public void GravityPullsTheProjectileDown()
        {
            Start(Rules(), rifle: ProjectileRifle(10f, 1f));
            Join(1);
            Join(2);
            Step(0.2f);
            var eye = Body(1).EyePosition;

            Fire(1, eye, new Vector3(0f, 0f, 1f), (int)_host.TickCount);
            Step(0.5f);

            var row = OnlyProjectile(2);
            Assert.That(row.Position.Y, Is.LessThan(eye.Y - 1f));
            Assert.That(row.Velocity.Y, Is.LessThan(-4f));
        }

        [Test]
        public void AProjectileKeepsFlyingAfterItsShooterLeaves()
        {
            Start(Rules(), rifle: ProjectileRifle(10f, 0f));
            Join(1);
            Join(2);
            Step(0.2f);

            FireAt(1, Body(2).Center);
            Tick();
            var connection = new ConnectionId(1);
            _match.Leave(connection);
            _host.OnDisconnected(connection);

            var row = OnlyProjectile(2);
            Assert.That(row.HasShooter, Is.False);
            Step(1.5f);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 25), _log.ToString());
            Assert.That(_transport.Read<ProjectileEndedMessage>(2, Copy).Single().Reason, Is.EqualTo(ProjectileEndReason.HitCharacter));
        }

        [Test]
        public void ARocketFliesToWhereItHitsAndExplodesThere()
        {
            var explosion = new ExplosionSpec(new ExplosionDamage(60, 5f));
            Start(Rules(), rifle: ExplodingProjectileRifle(new ProjectileSpec(40f, 0f, _projectileLifetime), explosion));
            Join(1);
            Join(2);
            Step(0.2f);
            var victim = Body(2);
            var floor = new Vector3(victim.Position.X - 1.5f, 0.5f, 0f);

            FireAt(1, floor);
            Assert.That(_transport.Read<ExplosionMessage>(2, m => m), Is.Empty, "the rocket has not arrived yet");
            Step(0.5f);

            Assert.That(_transport.Read<ProjectileEndedMessage>(2, Copy).Single().Reason, Is.EqualTo(ProjectileEndReason.HitWorld));
            var center = floor + new Vector3(0f, ImpactResolver.DetonationStandoff, 0f);
            var sent = _transport.Read<ExplosionMessage>(2, m => new ExplosionMessage { Center = m.Center, WeaponId = m.WeaponId }).Single();
            Assert.That(sent.WeaponId, Is.EqualTo(TestRules.RifleId));
            Assert.That(Vector3.Distance(sent.Center, center), Is.LessThan(1e-3f));
            var expected = explosion.Damage.At(DistanceToBody(center, victim), false);
            Assert.That(expected, Is.GreaterThan(0), "the body is inside the radius");
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - expected), _log.ToString());
        }

        [Test]
        public void AGrenadeRoundBouncesOffTheFloorAndExplodesWhenItsLifetimeEnds()
        {
            const float lifetime = 1f;
            var explosion = new ExplosionSpec(new ExplosionDamage(60, 5f), 0, DetonationTrigger.OnLifetimeEnd);
            Start(Rules(), rifle: ExplodingProjectileRifle(new ProjectileSpec(10f, 1f, lifetime, WorldHitResponse.Bounce), explosion));
            Join(1);
            Join(2);
            Step(0.2f);
            var floor = new Vector3(Body(1).Position.X + 2f, 0.5f, 0f);

            FireAt(1, floor);
            Step(lifetime / 2f);

            Assert.That(_transport.Read<ProjectileEndedMessage>(2, Copy), Is.Empty, "a bounce does not end the round");
            Assert.That(_transport.Read<ExplosionMessage>(2, m => m), Is.Empty, "nor does it set the explosion off");

            Step(lifetime);

            var ended = _transport.Read<ProjectileEndedMessage>(2, Copy).Single();
            Assert.That(ended.Reason, Is.EqualTo(ProjectileEndReason.Expired));
            Assert.That(ended.Position.X, Is.GreaterThan(floor.X), "it flew on past the floor it bounced off");
            Assert.That(ended.Position.Y, Is.GreaterThan(0.5f), "it bounced up instead of going through the floor");
            var sent = _transport.Read<ExplosionMessage>(2, m => new ExplosionMessage { Center = m.Center, WeaponId = m.WeaponId }).Single();
            Assert.That(sent.WeaponId, Is.EqualTo(TestRules.RifleId));
            Assert.That(Vector3.Distance(sent.Center, ended.Position), Is.LessThan(1e-3f), "it explodes where its lifetime ran out");
            var expected = explosion.Damage.At(DistanceToBody(sent.Center, Body(2)), false);
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - expected), _log.ToString());
        }

        [Test]
        public void ARoundThatExplodesWhenItStopsExplodesWhereItStopped()
        {
            var explosion = new ExplosionSpec(new ExplosionDamage(60, 5f), 0, DetonationTrigger.OnStop);
            Start(Rules(), rifle: ExplodingProjectileRifle(new ProjectileSpec(40f, 0f, _projectileLifetime), explosion));
            Join(1);
            Join(2);
            Step(0.2f);
            var floor = new Vector3(Body(2).Position.X - 1.5f, 0.5f, 0f);

            FireAt(1, floor);
            Step(0.5f);

            var center = floor + new Vector3(0f, ImpactResolver.DetonationStandoff, 0f);
            var sent = _transport.Read<ExplosionMessage>(2, m => new ExplosionMessage { Center = m.Center }).Single();
            Assert.That(Vector3.Distance(sent.Center, center), Is.LessThan(1e-3f), _log.ToString());
        }

        [Test]
        public void ARoundThatExplodesOnlyAtTheEndOfItsLifetimeDoesNotExplodeWhenItHitsTheFloor()
        {
            var explosion = new ExplosionSpec(new ExplosionDamage(60, 5f), 0, DetonationTrigger.OnLifetimeEnd);
            Start(Rules(), rifle: ExplodingProjectileRifle(new ProjectileSpec(40f, 0f, _projectileLifetime), explosion));
            Join(1);
            Join(2);
            Step(0.2f);

            FireAt(1, new Vector3(Body(2).Position.X - 1.5f, 0.5f, 0f));
            Step(0.5f);

            Assert.That(_transport.Read<ProjectileEndedMessage>(2, Copy).Single().Reason, Is.EqualTo(ProjectileEndReason.HitWorld));
            Assert.That(_transport.Read<ExplosionMessage>(2, m => m), Is.Empty);
        }

        private const float _projectileLifetime = 3f;

        [Test]
        public void AChargedProjectileLeavesFasterByTheAcceptedCharge()
        {
            const float speed = 10f;
            var charge = new ChargeSpec(0.5f, 0f, 1f, fullChargeSpeedMultiplier: 3f);
            Start(Rules(), rifle: ProjectileRifle(speed, 0f, charge: charge));
            Join(1);
            Join(2);
            Step(1f);
            var eye = Body(1).EyePosition;

            Send(1, new FireRequestMessage
            {
                WeaponId = TestRules.RifleId,
                Origin = eye,
                Direction = Body(2).Center - eye,
                ClientTick = (int)_host.TickCount,
                ShotIndex = _shotIndex++,
                ChargeFraction = 1f,
            });

            var spawned = _transport.Read<ProjectileSpawnedMessage>(2, Copy).Single();
            Assert.That(spawned.Velocity.Length(), Is.EqualTo(speed * 3f).Within(1e-3f), _log.ToString());
        }

        private static WeaponSpec ProjectileRifle(float speed, float gravityMultiplier, int? damage = null, ChargeSpec charge = null)
        {
            var rifle = TestRules.Rifle();
            var round = new RoundSpec(
                RoundDelivery.Projectile,
                rifle.Round.Range,
                projectile: new ProjectileSpec(speed, gravityMultiplier, _projectileLifetime, WorldHitResponse.Stick));
            var impact = damage.HasValue ? new ImpactSpec(damage.Value, DamageFalloff.None, rifle.Impact.StructureRadius) : rifle.Impact;
            return new WeaponSpec(
                rifle.Id,
                rifle.NameKey,
                rifle.Category,
                charge != null ? new TriggerSpec(FireMode.Semi, rifle.FireInterval, charge: charge) : rifle.Trigger,
                rifle.Ammo,
                round,
                impact,
                rifle.Aim,
                rifle.AmmoTypes,
                rifle.ShotNoiseRadius);
        }

        private static WeaponSpec ExplodingProjectileRifle(ProjectileSpec projectile, ExplosionSpec explosion)
        {
            var rifle = TestRules.Rifle();
            var round = new RoundSpec(RoundDelivery.Projectile, rifle.Round.Range, projectile: projectile);
            var impact = new ImpactSpec(rifle.Impact.Damage, rifle.Impact.Falloff, rifle.Impact.StructureRadius, explosion);
            return new WeaponSpec(
                rifle.Id, rifle.NameKey, rifle.Category, rifle.Trigger, rifle.Ammo, round, impact, rifle.Aim, rifle.AmmoTypes, rifle.ShotNoiseRadius);
        }

        private static float DistanceToBody(Vector3 center, ServerBody body)
        {
            return ServerCombat.DistanceToCapsule(
                center, body.Position + new Vector3(0f, body.Shape.BottomY, 0f), body.Position + new Vector3(0f, body.Shape.TopY, 0f), body.Radius);
        }

        private ProjectileSnapshot OnlyProjectile(int receiverId)
        {
            var projectiles = Snapshot(receiverId).Projectiles;
            Assert.That(projectiles.Count, Is.EqualTo(1));
            return projectiles[0];
        }

        private static ProjectileSpawnedMessage Copy(ProjectileSpawnedMessage m)
        {
            return new ProjectileSpawnedMessage
            {
                ProjectileId = m.ProjectileId,
                WeaponId = m.WeaponId,
                HasShooter = m.HasShooter,
                ShooterId = m.ShooterId,
                Position = m.Position,
                Velocity = m.Velocity,
            };
        }

        private static ProjectileEndedMessage Copy(ProjectileEndedMessage m)
        {
            return new ProjectileEndedMessage { ProjectileId = m.ProjectileId, Reason = m.Reason, Position = m.Position };
        }
    }
}
