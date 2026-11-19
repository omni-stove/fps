using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 人の弾と爆発が置いた物に当たる・止まる・通り抜けるを、戦闘の当たり判定を通して確かめる
    public sealed partial class ServerDeployablesTests
    {
        // 盾は Alpha の前 (x=-3) に 90 度で立てるので、+X を向いた面は x=-2.9、高さは床の上面 0.5 から 1.9 まで
        private static readonly Vector3 _onTheShield = new Vector3(-3f, 1.2f, 0f);

        private int _shotIndex;

        [Test]
        public void AnEnemyShotStopsAtTheShieldAndWearsItDown()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);
            Deploy(1, _shieldId, _alphaFront, 90f);
            Assert.That(Rows(1), Has.Count.EqualTo(1), _log.ToString());

            FireAt(2, _onTheShield);

            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-shield serial=1 blocked dmg=25 hp=275 shooter=2"));
            Assert.That(Rows(1)[0].Health, Is.EqualTo(DeploySpec.DefaultMaxHealth - 25));
            Assert.That(_rules.Session.GetHealth(1), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), "the owner behind the shield is not hit");
            Assert.That(
                _transport.Read<HitConfirmMessage>(2, m => new HitConfirmMessage { Damage = m.Damage }),
                Is.Empty,
                "a shot stopped by a deployable shows no hit marker, as offline");
        }

        [Test]
        public void TheHitboxesStopAnEnemyShotAtTheShieldFaceAndLetTheOwnersShotThrough()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);
            Deploy(1, _shieldId, _alphaFront, 90f);
            var hitboxes = _combat.Hitboxes;
            var tick = hitboxes.LastRecordedTick;

            Assert.That(hitboxes.Raycast(new Vector3(6f, 1.2f, 0f), -Vector3.UnitX, 20f, tick, 2, _combat.IsHittable, out var enemy), Is.True);
            Assert.That(enemy.HitDeployable, Is.True);
            Assert.That(enemy.DeployableId, Is.EqualTo(1));
            Assert.That(enemy.HitTarget, Is.False);
            Assert.That(enemy.Distance, Is.EqualTo(8.9f).Within(1e-3f));
            Assert.That(enemy.Normal.X, Is.EqualTo(1f).Within(1e-4f), "the face toward the shooter");
            Assert.That(enemy.Point.X, Is.EqualTo(-2.9f).Within(1e-3f));

            Assert.That(hitboxes.Raycast(new Vector3(-6f, 1.2f, 0f), Vector3.UnitX, 20f, tick, 1, _combat.IsHittable, out var own), Is.True);
            Assert.That(own.HitDeployable, Is.False, "the owner's shot passes their own shield");
            Assert.That(own.HitTarget, Is.True);
            Assert.That(own.TargetId, Is.EqualTo(2));

            Assert.That(
                hitboxes.Raycast(new Vector3(-3f, 1.2f, 0f), Vector3.UnitX, 20f, tick, 2, _combat.IsHittable, out var inside) && inside.HitDeployable,
                Is.False,
                "a shot from inside the shield's box is not stopped by it");
            Assert.That(
                hitboxes.RaycastIgnoringDeployables(new Vector3(6f, 1.2f, 0f), -Vector3.UnitX, 20f, tick, 2, _combat.IsHittable, out var ignoring),
                Is.True);
            Assert.That(ignoring.HitDeployable, Is.False);
            Assert.That(ignoring.TargetId, Is.EqualTo(1));
        }

        [Test]
        public void AnEnemyExplosionWearsTheShieldDownAndTheShieldShieldsItsOwner()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);
            Deploy(1, _shieldId, _alphaFront, 90f);

            // 盾の面まで 1.9 m・持ち主の体まで 4 m ほど。どちらも全ダメージの半径 5 m の内にある
            var explosion = new ExplosionSpec(new ExplosionDamage(100, 6f, 5f));
            _combat.Explode(new Vector3(-1f, 1.2f, 0f), explosion, 2, ServerCombat.GrenadeWeaponId);

            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-shield serial=1 blocked dmg=100 hp=200 shooter=2"));
            Assert.That(_log.ToString(), Does.Contain(" deployables=1"));
            Assert.That(Rows(1)[0].Health, Is.EqualTo(DeploySpec.DefaultMaxHealth - 100));
            Assert.That(_rules.Session.GetHealth(1), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), "the shield blocks the blast toward its owner");

            _combat.Explode(new Vector3(-1f, 1.2f, 0f), explosion, 1, ServerCombat.GrenadeWeaponId);

            Assert.That(Rows(1)[0].Health, Is.EqualTo(DeploySpec.DefaultMaxHealth - 100), "the owner's own blast passes their shield");
            Assert.That(_log.ToString(), Does.Contain(" deployables=0"));
        }

        [Test]
        public void ABlastThatBreaksTheShieldRemovesIt()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);
            Deploy(1, _shieldId, _alphaFront, 90f);

            _combat.Explode(new Vector3(-1f, 1.2f, 0f), new ExplosionSpec(new ExplosionDamage(1000, 6f, 5f)), 2, ServerCombat.GrenadeWeaponId);

            Assert.That(Rows(2), Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-shield serial=1 removed reason=Destroyed"));
        }

        private void FireAt(int shooter, Vector3 target)
        {
            Assert.That(_context.Bodies.TryGet(shooter, out var body), Is.True);
            var eye = body.EyePosition;
            _host.OnMessage(new ConnectionId((ulong)shooter), new FireRequestMessage
            {
                WeaponId = TestRules.RifleId,
                Origin = eye,
                Direction = target - eye,
                ClientTick = (int)_host.TickCount,
                InterpolationAlpha = 0f,
                ShotIndex = _shotIndex++,
                ShotGeneration = 0,
            });
        }
    }
}
