using System.Globalization;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // ビーコンを置き、倒されて戻るときにルールの RespawnAnchor の順でビーコンの前へ出ること・回数・壊れたときを確かめる
    public sealed partial class ServerDeployablesTests
    {
        private static readonly RespawnAnchor[] _beaconFirst = { RespawnAnchor.Beacon, RespawnAnchor.Base };

        // _alphaFront (-3, 0.5, 0) に 90 度で置くと前は +X なので、既定の 1 m 前の (-2, 0.5, 0) に出る
        private static readonly Vector3 _beaconSpawn = new Vector3(-2f, 0.5f, 0f);

        private static readonly float _respawnWait = NetMatchSettings.DefaultRespawnSeconds + 0.2f;

        [Test]
        public void APlacedBeaconIsListedAsABeaconAndArmedWithItsUses()
        {
            Start(BeaconCatalog(), respawnAnchors: _beaconFirst);
            Join(1);
            Join(2);
            Step(0.2f);

            Deploy(1, _beaconId, _alphaFront, 90f);

            var rows = Rows(2);
            Assert.That(rows, Has.Count.EqualTo(1), _log.ToString());
            Assert.That(rows[0].Kind, Is.EqualTo(DeployableKind.Beacon));
            Assert.That(rows[0].TypeId, Is.EqualTo(_beaconId));
            Assert.That(rows[0].Team, Is.EqualTo(Team.Alpha));
            Assert.That(rows[0].OwnerId, Is.EqualTo(1));
            Assert.That(rows[0].Yaw, Is.EqualTo(90f));
            Assert.That(rows[0].Health, Is.EqualTo(BeaconSpec.DefaultMaxHealth));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] net types=1 usable=1 from=memory"));
            Assert.That(
                _log.ToString(),
                Does.Contain("[Beacon] owner=1 kind=deploy-beacon serial=1 armed uses=3 side=" + SideText(Team.Alpha)));
            Assert.That(_deployables.BeaconUsesLeft(1), Is.EqualTo(BeaconSpec.DefaultUses));
        }

        [Test]
        public void TheOwnerKilledAfterPlacingABeaconComesBackInFrontOfIt()
        {
            Start(BeaconCatalog(), respawnAnchors: _beaconFirst);
            Join(1);
            Join(2);
            Step(0.2f);
            Deploy(1, _beaconId, _alphaFront, 90f);

            _combat.DealDamage(2, 1, NetMatchSettings.DefaultMaxHealth, TestRules.RifleId, HitZone.Body, null);
            Step(_respawnWait);

            Assert.That(_rules.Session.IsAlive(1), Is.True);
            Assert.That(_context.Bodies.TryGet(1, out var body), Is.True);
            Assert.That(body.Position.X, Is.EqualTo(_beaconSpawn.X).Within(1e-3f), _log.ToString());
            Assert.That(body.Position.Z, Is.EqualTo(_beaconSpawn.Z).Within(1e-3f));
            Assert.That(body.Yaw, Is.EqualTo(90f).Within(1e-3f), "faces the way the beacon faces");
            Assert.That(_log.ToString(), Does.Contain("[Beacon] owner=1 kind=deploy-beacon serial=1 used by=1 at=(-2.00, 0.50, 0.00) uses=2/3"));
            Assert.That(_log.ToString(), Does.Contain("[Match] spawn player=1 at=(-2.00, 0.50, 0.00) from=Beacon"));
            Assert.That(_deployables.BeaconUsesLeft(1), Is.EqualTo(2));
            Assert.That(Rows(1), Has.Count.EqualTo(1), "a beacon with uses left stays");
        }

        [Test]
        public void ATeammateUsesTheBeaconButAnEnemyGoesBackToTheirBase()
        {
            Start(BeaconCatalog(), respawnAnchors: _beaconFirst);
            Join(1);
            Join(2);
            Join(3);
            Step(0.2f);
            Assert.That(_rules.Session.GetTeam(3), Is.EqualTo(Team.Alpha));
            Deploy(1, _beaconId, _alphaFront, 90f);

            _combat.DealDamage(2, 3, NetMatchSettings.DefaultMaxHealth, TestRules.RifleId, HitZone.Body, null);
            _combat.DealDamage(1, 2, NetMatchSettings.DefaultMaxHealth, TestRules.RifleId, HitZone.Body, null);
            Step(_respawnWait);

            Assert.That(_log.ToString(), Does.Contain("[Beacon] owner=1 kind=deploy-beacon serial=1 used by=3 at=(-2.00, 0.50, 0.00) uses=2/3"));
            Assert.That(_log.ToString(), Does.Contain("[Match] spawn player=3 at=(-2.00, 0.50, 0.00) from=Beacon"));
            Assert.That(_log.ToString(), Does.Contain("[Match] spawn player=2 at=(5.00, 1.60, 0.00) from=Base"), "the enemy's beacon is not theirs to use");
            Assert.That(_log.ToString(), Does.Not.Contain("used by=2"));
            Assert.That(_deployables.BeaconUsesLeft(1), Is.EqualTo(2));
        }

        [Test]
        public void TheLastUseRemovesTheBeaconAndTheNextRespawnIsAtTheBase()
        {
            Start(BeaconCatalog(new BeaconSpec(1, BeaconSpec.DefaultSpawnDistance)), respawnAnchors: _beaconFirst);
            Join(1);
            Join(2);
            Step(0.2f);
            Deploy(1, _beaconId, _alphaFront, 90f);

            _combat.DealDamage(2, 1, NetMatchSettings.DefaultMaxHealth, TestRules.RifleId, HitZone.Body, null);
            Step(_respawnWait);

            Assert.That(_log.ToString(), Does.Contain("[Beacon] owner=1 kind=deploy-beacon serial=1 used by=1 at=(-2.00, 0.50, 0.00) uses=0/1"));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-beacon serial=1 removed reason=Expended"));
            Assert.That(Rows(1), Is.Empty);

            _combat.DealDamage(2, 1, NetMatchSettings.DefaultMaxHealth, TestRules.RifleId, HitZone.Body, null);
            Step(_respawnWait);

            Assert.That(_log.ToString(), Does.Contain("[Match] spawn player=1 at=(-5.00, 1.60, 0.00) from=Base"));
            Assert.That(_context.Bodies.TryGet(1, out var body), Is.True);
            Assert.That(body.Position.X, Is.EqualTo(_alphaSpawn.X).Within(1e-3f));
        }

        [Test]
        public void ADestroyedBeaconNoLongerBringsTheOwnerBack()
        {
            Start(BeaconCatalog(), respawnAnchors: _beaconFirst);
            Join(1);
            Join(2);
            Step(0.2f);
            Deploy(1, _beaconId, _alphaFront, 90f);

            Assert.That(_deployables.Damage(1, 2, BeaconSpec.DefaultMaxHealth), Is.True);
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-beacon serial=1 removed reason=Destroyed"));

            _combat.DealDamage(2, 1, NetMatchSettings.DefaultMaxHealth, TestRules.RifleId, HitZone.Body, null);
            Step(_respawnWait);

            Assert.That(_log.ToString(), Does.Contain("[Match] spawn player=1 at=(-5.00, 1.60, 0.00) from=Base"));
            Assert.That(_log.ToString(), Does.Not.Contain(" used by="));
        }

        [Test]
        public void RulesWithoutTheBeaconAnchorRespawnAtTheBaseAndKeepTheUses()
        {
            Start(BeaconCatalog());
            Join(1);
            Join(2);
            Step(0.2f);
            Deploy(1, _beaconId, _alphaFront, 90f);
            Assert.That(Rows(1), Has.Count.EqualTo(1), _log.ToString());

            _combat.DealDamage(2, 1, NetMatchSettings.DefaultMaxHealth, TestRules.RifleId, HitZone.Body, null);
            Step(_respawnWait);

            Assert.That(_log.ToString(), Does.Contain("[Match] spawn player=1 at=(-5.00, 1.60, 0.00) from=Base"));
            Assert.That(_deployables.BeaconUsesLeft(1), Is.EqualTo(BeaconSpec.DefaultUses));
        }

        // ラウンドの頭の並べ直し (Elimination・SearchAndDestroy の RespawnAll) は倒されて戻るのではないので、ビーコンを使わない
        [Test]
        public void PuttingEveryoneBackAtTheRoundHeadDoesNotUseTheBeacon()
        {
            Start(BeaconCatalog(), respawnAnchors: _beaconFirst);
            Join(1);
            Join(2);
            Step(0.2f);
            Deploy(1, _beaconId, _alphaFront, 90f);
            _combat.DealDamage(2, 1, NetMatchSettings.DefaultMaxHealth, TestRules.RifleId, HitZone.Body, null);
            var before = _log.ToString().Length;

            _rules.Session.RespawnAll();

            var written = _log.ToString().Substring(before);
            Assert.That(written, Does.Contain("[Match] spawn player=1 at=(-5.00, 1.60, 0.00)" + System.Environment.NewLine));
            Assert.That(written, Does.Not.Contain("from="), "only a respawn after a death names its anchor");
            Assert.That(_log.ToString(), Does.Not.Contain(" used by="));
            Assert.That(_deployables.BeaconUsesLeft(1), Is.EqualTo(BeaconSpec.DefaultUses));
        }

        [Test]
        public void WithoutDeployablesTheBeaconAnchorFallsBackToTheBase()
        {
            Start(BeaconCatalog(), respawnAnchors: _beaconFirst);
            Join(1);
            Join(2);
            Step(0.2f);

            _combat.DealDamage(2, 1, NetMatchSettings.DefaultMaxHealth, TestRules.RifleId, HitZone.Body, null);
            Step(_respawnWait);

            Assert.That(_log.ToString(), Does.Contain("[Match] spawn player=1 at=(-5.00, 1.60, 0.00) from=Base"));
        }

        private static DeployCatalog BeaconCatalog(BeaconSpec beacon = null)
        {
            return new DeployCatalog(new[]
            {
                new DeployType(_beaconId, DeployRole.Beacon, DeployType.DefaultDeploy(DeployRole.Beacon), beacon: beacon ?? BeaconSpec.Default),
            });
        }

        private static string SideText(Team team)
        {
            return Side.FromTeam(team).Id.ToString(CultureInfo.InvariantCulture);
        }
    }
}
