using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 地雷が有効になってから敵に踏まれて爆発することと、C4 を面に貼って起爆の要求で爆発させることを、同じホストで確かめる
    public sealed partial class ServerDeployablesTests
    {
        private const string _mineId = "deploy-mine";
        private const string _chargeId = "deploy-remote-charge";

        // 体の位置は体の中心 (足元から 1.1 m 上) なので、この位置の人のカプセルは Alpha の前の地雷 (x=-3) から 0.28 m に触れる
        private static readonly Vector3 _onTheMine = new Vector3(-2.5f, 1.6f, 0f);

        // 爆風の届かない所。持ち主を自分の爆発から外しておくのに使う
        private static readonly Vector3 _farAway = new Vector3(-12f, 1.6f, 0f);

        // Alpha の持ち主 (x=-5) の手前の床
        private static readonly Vector3 _chargeOnTheFloor = new Vector3(-4f, 0.5f, 0f);

        [Test]
        public void APlacedMineArmsAndBlowsUpUnderTheEnemyWhoStepsOnIt()
        {
            Start(Explosives());
            var kills = new List<KillFeedEntry>();
            _combat.PlayerKilled += kills.Add;
            Join(1);
            Join(2);
            Step(0.2f);

            Deploy(1, _mineId, _alphaFront, 90f);

            var placed = Rows(2);
            Assert.That(placed, Has.Count.EqualTo(1), _log.ToString());
            Assert.That(placed[0].Kind, Is.EqualTo(DeployableKind.Mine));
            Assert.That(placed[0].TypeId, Is.EqualTo(_mineId));
            Assert.That(placed[0].Health, Is.EqualTo(MineSpec.DefaultMaxHealth));
            Assert.That(placed[0].Position.Y, Is.EqualTo(0.5f).Within(1e-4f), "it lies on the floor");
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-mine placed serial=1"));
            Assert.That(_log.ToString(), Does.Contain("[Mine] owner=1 kind=deploy-mine serial=1 arming seconds=1.5 trigger=1.0 dmg=150 radius=4.0"));

            Step(2f);

            Assert.That(_log.ToString(), Does.Contain("[Mine] owner=1 kind=deploy-mine serial=1 armed"));
            Assert.That(_log.ToString(), Does.Not.Contain("triggered"), "no enemy is near the mine");
            Assert.That(Rows(1), Has.Count.EqualTo(1));

            Teleport(1, _farAway);
            Teleport(2, _onTheMine);
            Step(0.1f);

            Assert.That(_log.ToString(), Does.Contain("[Mine] owner=1 kind=deploy-mine serial=1 triggered by=2 at=(-3.00, 0.60, 0.00)"));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-mine serial=1 removed reason=Detonated"));
            Assert.That(Rows(1), Is.Empty);
            Assert.That(_rules.Session.IsAlive(2), Is.False, "the full blast (150) is more than the enemy's health");
            Assert.That(_rules.Session.GetHealth(1), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), "the owner is out of the blast");
            Assert.That(kills, Has.Count.EqualTo(1));
            Assert.That(kills[0].KillerId, Is.EqualTo(1));
            Assert.That(kills[0].VictimId, Is.EqualTo(2));
            Assert.That(kills[0].WeaponId, Is.EqualTo(_mineId));
            var blasts = _transport.Read<ExplosionMessage>(2, m => new ExplosionMessage { Center = m.Center, ThrowerId = m.ThrowerId, WeaponId = m.WeaponId });
            Assert.That(blasts, Has.Count.EqualTo(1));
            Assert.That(blasts[0].ThrowerId, Is.EqualTo(1));
            Assert.That(blasts[0].WeaponId, Is.EqualTo(_mineId));
            Assert.That(blasts[0].Center.Y, Is.EqualTo(0.5f + ServerDeployables.BlastLift).Within(1e-4f));
        }

        [Test]
        public void AMineWaitsUntilItIsArmedBeforeAnEnemyCanSetItOff()
        {
            Start(Explosives());
            Join(1);
            Join(2);
            Step(0.2f);
            Deploy(1, _mineId, _alphaFront, 90f);
            Teleport(1, _farAway);
            Teleport(2, _onTheMine);

            Step(1f);

            Assert.That(_log.ToString(), Does.Not.Contain("triggered"), "the mine arms after 1.5 seconds");
            Assert.That(Rows(1), Has.Count.EqualTo(1));

            Step(1f);

            Assert.That(_log.ToString(), Does.Contain("[Mine] owner=1 kind=deploy-mine serial=1 triggered by=2"));
            Assert.That(Rows(1), Is.Empty);
        }

        [Test]
        public void TheOwnerAndAnAllyDoNotSetOffTheMine()
        {
            Start(Explosives());
            Join(1);
            Join(2);
            Join(3);
            Step(0.2f);
            Deploy(1, _mineId, _alphaFront, 90f);
            Step(2f);

            Teleport(1, _onTheMine);
            Teleport(3, new Vector3(-3.5f, 1.6f, 0f));
            Step(0.5f);

            Assert.That(_log.ToString(), Does.Not.Contain("triggered"));
            Assert.That(Rows(1), Has.Count.EqualTo(1));
            Assert.That(_rules.Session.GetHealth(1), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_rules.Session.GetHealth(3), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
        }

        [Test]
        public void AMineValueFromTheCatalogIsUsed()
        {
            var mine = new MineSpec(0f, 3f, new ExplosionSpec(new ExplosionDamage(10, 2f, 0.5f)));
            Start(Explosives(mine));
            Join(1);
            Join(2);
            Step(0.2f);
            Deploy(1, _mineId, _alphaFront, 90f);
            Teleport(1, _farAway);

            // 踏む距離を 3 m に広げたので、体の面が地雷 (x=-3) から 1.6 m ほど離れていても起きる
            Teleport(2, new Vector3(-1f, 1.6f, 0f));
            Step(0.1f);

            Assert.That(_log.ToString(), Does.Contain("[Mine] owner=1 kind=deploy-mine serial=1 arming seconds=0.0 trigger=3.0 dmg=10 radius=2.0"));
            Assert.That(_log.ToString(), Does.Contain("[Mine] owner=1 kind=deploy-mine serial=1 triggered by=2"));
            Assert.That(_rules.Session.IsAlive(2), Is.True, "the blast deals at most 10");
            Assert.That(_rules.Session.GetHealth(2), Is.LessThan(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_rules.Session.GetHealth(2), Is.GreaterThanOrEqualTo(NetMatchSettings.DefaultMaxHealth - 10));
        }

        [Test]
        public void ARemoteChargeSticksToTheFloorAndBlowsUpWhenTheOwnerDetonatesIt()
        {
            Start(Explosives());
            var kills = new List<KillFeedEntry>();
            _combat.PlayerKilled += kills.Add;
            Join(1);
            Join(2);
            Step(0.2f);

            Deploy(1, _chargeId, _chargeOnTheFloor, 90f);

            var placed = Rows(2);
            Assert.That(placed, Has.Count.EqualTo(1), _log.ToString());
            Assert.That(placed[0].Kind, Is.EqualTo(DeployableKind.RemoteCharge));
            Assert.That(placed[0].TypeId, Is.EqualTo(_chargeId));
            Assert.That(placed[0].Health, Is.EqualTo(RemoteChargeSpec.DefaultMaxHealth));
            Assert.That(placed[0].Position.X, Is.EqualTo(-4f).Within(1e-3f));
            Assert.That(placed[0].Position.Y, Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(_log.ToString(), Does.Contain("[RemoteCharge] owner=1 kind=deploy-remote-charge serial=1 attached to=collider:1"));

            Step(1f);
            Assert.That(Rows(1), Has.Count.EqualTo(1), "a charge waits for the owner's detonation");

            Teleport(1, _farAway);
            Teleport(2, new Vector3(-3f, 1.6f, 0f));
            Detonate(1, _chargeId);

            Assert.That(_log.ToString(), Does.Contain("[RemoteCharge] owner=1 kind=deploy-remote-charge detonating count=1"));
            Assert.That(_log.ToString(), Does.Contain("[RemoteCharge] owner=1 kind=deploy-remote-charge serial=1 detonated at=(-4.00, 0.60, 0.00)"));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-remote-charge serial=1 removed reason=Detonated"));
            Assert.That(Rows(1), Is.Empty);
            Assert.That(_rules.Session.IsAlive(2), Is.False);
            Assert.That(kills, Has.Count.EqualTo(1));
            Assert.That(kills[0].KillerId, Is.EqualTo(1));
            Assert.That(kills[0].WeaponId, Is.EqualTo(_chargeId));
            var blasts = _transport.Read<ExplosionMessage>(1, m => new ExplosionMessage { WeaponId = m.WeaponId });
            Assert.That(blasts, Has.Count.EqualTo(1));
            Assert.That(blasts[0].WeaponId, Is.EqualTo(_chargeId));
        }

        [Test]
        public void EveryChargeOfTheOwnerBlowsUpAtOnceAndTheLimitRefusesAThird()
        {
            Start(Explosives());
            Join(1);
            Join(2);
            Step(0.2f);

            Assert.That(_deployables.Place(1, _chargeId, _chargeOnTheFloor, 90f), Is.EqualTo(ServerDeployRefusal.None));
            Assert.That(_deployables.Place(1, _chargeId, new Vector3(-4f, 0.5f, 1f), 90f), Is.EqualTo(ServerDeployRefusal.None));
            Assert.That(
                _deployables.Place(1, _chargeId, new Vector3(-4f, 0.5f, -1f), 90f),
                Is.EqualTo(ServerDeployRefusal.LimitReached),
                "a charge does not replace the oldest; the third is refused");
            Assert.That(Rows(1), Has.Count.EqualTo(2));

            Teleport(1, _farAway);
            Assert.That(_deployables.Detonate(1, _chargeId), Is.EqualTo(ServerDeployRefusal.None));

            Assert.That(_log.ToString(), Does.Contain("[RemoteCharge] owner=1 kind=deploy-remote-charge detonating count=2"));
            Assert.That(_log.ToString(), Does.Contain("[RemoteCharge] owner=1 kind=deploy-remote-charge serial=1 detonated"));
            Assert.That(_log.ToString(), Does.Contain("[RemoteCharge] owner=1 kind=deploy-remote-charge serial=2 detonated"));
            Assert.That(_log.ToString(), Does.Not.Contain("removed reason=Destroyed"), "the second charge is not broken by the first blast");
            Assert.That(Rows(1), Is.Empty);
            Assert.That(_transport.Read<ExplosionMessage>(2, m => new ExplosionMessage()), Has.Count.EqualTo(2));

            Assert.That(_deployables.Place(1, _chargeId, new Vector3(-12f, 0.5f, 1f), 90f), Is.EqualTo(ServerDeployRefusal.None), "the limit is free again");
        }

        [Test]
        public void ADetonationWithNothingToBlowUpIsRefused()
        {
            Start(Explosives());
            Join(1);
            Join(2);
            Step(0.2f);

            Assert.That(_deployables.Detonate(1, _chargeId), Is.EqualTo(ServerDeployRefusal.NothingToDetonate));
            Assert.That(_deployables.Detonate(1, _mineId), Is.EqualTo(ServerDeployRefusal.NotDetonatable));
            Assert.That(_deployables.Detonate(1, "deploy-unknown"), Is.EqualTo(ServerDeployRefusal.UnknownType));

            Deploy(1, _chargeId, _chargeOnTheFloor, 90f);
            Assert.That(_deployables.Detonate(2, _chargeId), Is.EqualTo(ServerDeployRefusal.NothingToDetonate), "only the owner detonates the charge");
            Assert.That(Rows(1), Has.Count.EqualTo(1));
        }

        [Test]
        public void ADetonationIsRefusedWhenAbilitiesAreOff()
        {
            Start(Explosives(), abilities: Abilities.Off);
            Join(1);
            Join(2);
            Step(0.2f);

            Assert.That(_deployables.Detonate(1, _chargeId), Is.EqualTo(ServerDeployRefusal.RuleOff));
        }

        [Test]
        public void ARemoteChargeSticksToAWallAndBlowsUpOffItsFace()
        {
            Start(Explosives(), wall: true);
            Join(1);
            Join(2);
            Step(0.2f);

            // 段 (x -3.5〜-2.5、y 0.5〜2.5、z 0〜1) の持ち主へ向いた面
            Assert.That(_deployables.Place(1, _chargeId, new Vector3(-3.5f, 1.2f, 0.5f), 90f), Is.EqualTo(ServerDeployRefusal.None), _log.ToString());

            var placed = Rows(1);
            Assert.That(placed[0].Position.X, Is.EqualTo(-3.5f).Within(1e-3f));
            Assert.That(placed[0].Position.Y, Is.EqualTo(1.2f).Within(1e-3f));
            Assert.That(_log.ToString(), Does.Contain("attached to=collider:3"));

            Teleport(1, _farAway);
            Assert.That(_deployables.Detonate(1, _chargeId), Is.EqualTo(ServerDeployRefusal.None));
            Assert.That(_log.ToString(), Does.Contain("serial=1 detonated at=(-3.60, 1.20, 0.50)"), "the blast starts off the face, toward the owner's side");
        }

        [Test]
        public void ARemoteChargeNeedsASurfaceInSightAndInReach()
        {
            Start(Explosives(), wall: true);
            Join(1);
            Join(2);
            Step(0.2f);

            Assert.That(_deployables.Place(1, _chargeId, new Vector3(-4f, 2.5f, 0f), 90f), Is.EqualTo(ServerDeployRefusal.NoGround), "nothing to stick to");
            Assert.That(
                _deployables.Place(1, _chargeId, new Vector3(-2.5f, 1.2f, 0.5f), 90f),
                Is.EqualTo(ServerDeployRefusal.OutOfReach),
                "the far face of the step is behind its near face");
            Assert.That(_deployables.Place(1, _chargeId, new Vector3(5f, 0.5f, 0f), 90f), Is.EqualTo(ServerDeployRefusal.OutOfReach), "too far");
            Assert.That(Rows(1), Is.Empty);
        }

        [Test]
        public void ARemoteChargeIsNotStuckOnAnotherDeployable()
        {
            var free = new AbilitySpec(SnapshotMessage.MaxDeployables + 1, 0f, 0f, 0f);
            Start(new DeployCatalog(new[]
            {
                new DeployType(_chargeId, DeployRole.RemoteCharge, DeployType.DefaultDeploy(DeployRole.RemoteCharge), ability: free, remoteCharge: RemoteChargeSpec.Default),
                new DeployType(_shieldId, DeployRole.Shield, DeployType.DefaultDeploy(DeployRole.Shield), ability: free),
            }));
            Join(1);
            Join(2);
            Step(0.2f);
            Deploy(1, _shieldId, _alphaFront, 90f);

            // 盾 (x=-3 に 90 度で立つ) の向こうの床。体の中心から引いた線は先に盾に当たる
            Assert.That(_deployables.Place(1, _chargeId, new Vector3(-2.5f, 0.5f, 0f), 90f), Is.EqualTo(ServerDeployRefusal.Blocked));
            Assert.That(Rows(1), Has.Count.EqualTo(1));
        }

        // 先頭の 2 つが選んでいない人の枠に入るので、地雷と C4 を先に並べる
        private static DeployCatalog Explosives(MineSpec mine = null)
        {
            var free = new AbilitySpec(SnapshotMessage.MaxDeployables + 1, 0f, 0f, 0f);
            return new DeployCatalog(new[]
            {
                new DeployType(_mineId, DeployRole.Mine, DeployType.DefaultDeploy(DeployRole.Mine), ability: free, mine: mine ?? MineSpec.Default),
                new DeployType(
                    _chargeId, DeployRole.RemoteCharge, DeployType.DefaultDeploy(DeployRole.RemoteCharge), ability: free, remoteCharge: RemoteChargeSpec.Default),
            });
        }

        private void Teleport(int playerId, Vector3 position)
        {
            _context.Bodies.Teleport(playerId, position, 0f, _host.TickCount);
        }

        private void Detonate(int playerId, string typeId)
        {
            _host.OnMessage(new ConnectionId((ulong)playerId), new DetonateRequestMessage { TypeId = typeId });
        }
    }
}
