using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 移動・ルール・戦闘・設置物を同じホストで動かし、置く・撃つ・壊れる・消えるとスナップショットを確かめる
    public sealed partial class ServerDeployablesTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const string _turretId = "deploy-turret";
        private const string _shieldId = "deploy-shield";
        private const string _decoyId = "deploy-decoy";
        private static readonly DateTime _time = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);

        // 床の上面は y=0.5。Alpha (1 と 3) は x=-5、Bravo (2) は x=5 に出る
        private static readonly Vector3 _alphaSpawn = new Vector3(-5f, 1.6f, 0f);
        private static readonly Vector3 _bravoSpawn = new Vector3(5f, 1.6f, 0f);
        private static readonly Vector3 _alphaFront = new Vector3(-3f, 0.5f, 0f);
        private static readonly Vector3 _bravoFront = new Vector3(2f, 0.5f, 0f);

        private StringWriter _log;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private MatchRulesModule _rules;
        private ServerCombat _combat;
        private ServerDeployables _deployables;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _transport = new RecordingTransport();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null && _host.State == ServerHostState.Running)
            {
                _host.Stop();
            }

            _transport.Dispose();
            _log.Dispose();
        }

        [Test]
        public void APlacedTurretShootsTheEnemyAndIsListedInTheSnapshot()
        {
            Start(Catalog());
            var kills = new List<KillFeedEntry>();
            _combat.PlayerKilled += kills.Add;
            Join(1);
            Join(2);
            Step(0.2f);

            Deploy(1, _turretId, _alphaFront, 90f);

            var placed = Rows(1);
            Assert.That(placed, Has.Count.EqualTo(1), _log.ToString());
            Assert.That(placed[0].DeployableId, Is.EqualTo(1));
            Assert.That(placed[0].Kind, Is.EqualTo(DeployableKind.Turret));
            Assert.That(placed[0].TypeId, Is.EqualTo(_turretId));
            Assert.That(placed[0].Team, Is.EqualTo(Team.Alpha));
            Assert.That(placed[0].HasOwner, Is.True);
            Assert.That(placed[0].OwnerId, Is.EqualTo(1));
            Assert.That(placed[0].Position.X, Is.EqualTo(_alphaFront.X).Within(1e-4f));
            Assert.That(placed[0].Position.Y, Is.EqualTo(_alphaFront.Y).Within(1e-4f), "it stands on the floor under the requested point");
            Assert.That(placed[0].Position.Z, Is.EqualTo(_alphaFront.Z).Within(1e-4f));
            Assert.That(placed[0].Yaw, Is.EqualTo(90f));
            Assert.That(placed[0].Health, Is.EqualTo(TurretSpec.DefaultMaxHealth));
            Assert.That(placed[0].Present, Is.EqualTo(DeployableFields.All));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-turret placed serial=1 pos=(-3.00, 0.50, 0.00) active=1/1"));
            Assert.That(_log.ToString(), Does.Contain("[Turret] owner=1 kind=deploy-turret serial=1 armed weapon=" + TestRules.RifleId + " rounds=60 seconds=30.0 friendlyFire=Off"));

            Step(1f);

            Assert.That(_log.ToString(), Does.Contain("[Turret] owner=1 kind=deploy-turret serial=1 target=2"));
            Assert.That(_log.ToString(), Does.Contain("[Turret] owner=1 kind=deploy-turret serial=1 fire target=2 hit=2 dmg=25 rounds=59"));
            Assert.That(_log.ToString(), Does.Contain("[Combat] dealt attacker=1 victim=2 weapon=" + TestRules.RifleId + " zone=Body dmg=25 outcome=Damaged"));
            Assert.That(_log.ToString(), Does.Contain("[Turret] owner=1 kind=deploy-turret serial=1 killed id=2"));
            Assert.That(_rules.Session.IsAlive(2), Is.False);
            Assert.That(kills, Has.Count.EqualTo(1));
            Assert.That(kills[0].KillerId, Is.EqualTo(1));
            Assert.That(kills[0].VictimId, Is.EqualTo(2));
            Assert.That(kills[0].WeaponId, Is.EqualTo(TestRules.RifleId));
            Assert.That(_rules.Session.GetKills(1), Is.EqualTo(1));
            var confirms = _transport.Read<HitConfirmMessage>(1, m => new HitConfirmMessage { Damage = m.Damage, Killed = m.Killed });
            Assert.That(confirms, Has.Count.EqualTo(4), "the owner sees a hit marker for every turret hit");
            Assert.That(confirms[3].Killed, Is.True);
        }

        [Test]
        public void TheTurretBreaksAnEnemyShieldInTheWayAndTheShieldLeavesTheSnapshot()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);

            Deploy(2, _shieldId, _bravoFront, 90f);
            Deploy(1, _turretId, _alphaFront, 90f);
            var both = Rows(2);
            Assert.That(both, Has.Count.EqualTo(2), _log.ToString());
            Assert.That(both[0].Kind, Is.EqualTo(DeployableKind.Shield));
            Assert.That(both[0].Team, Is.EqualTo(Team.Bravo));
            Assert.That(both[0].Health, Is.EqualTo(DeploySpec.DefaultMaxHealth));
            Assert.That(both[1].Kind, Is.EqualTo(DeployableKind.Turret));

            Step(0.5f);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), "the shield takes the shots");
            Assert.That(Rows(2)[0].Health, Is.LessThan(DeploySpec.DefaultMaxHealth));
            Assert.That(_log.ToString(), Does.Contain("[Turret] owner=1 kind=deploy-turret serial=2 fire target=2 hit=deployable:1 dmg=25 rounds=59"));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=2 kind=deploy-shield serial=1 blocked dmg=25 hp=275 shooter=1"));

            Step(1.5f);

            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=2 kind=deploy-shield serial=1 blocked dmg=25 hp=0 shooter=1"));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=2 kind=deploy-shield serial=1 removed reason=Destroyed"));
            var left = Rows(2);
            Assert.That(left, Has.Count.EqualTo(1));
            Assert.That(left[0].DeployableId, Is.EqualTo(2));
            Assert.That(left[0].Kind, Is.EqualTo(DeployableKind.Turret));
            Assert.That(_rules.Session.GetHealth(2), Is.LessThan(NetMatchSettings.DefaultMaxHealth), "with the shield gone the turret hits the enemy");
        }

        [Test]
        public void ADestroyedTurretLeavesTheSnapshot()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);
            Deploy(1, _turretId, _alphaFront, 90f);
            Assert.That(Rows(2), Has.Count.EqualTo(1));

            Assert.That(_deployables.Damage(1, 2, 100), Is.True);
            Assert.That(Rows(2)[0].Health, Is.EqualTo(50));
            Assert.That(_deployables.Damage(1, 2, 100), Is.True);

            Assert.That(Rows(2), Is.Empty);
            Assert.That(_deployables.Count, Is.EqualTo(0));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-turret serial=1 removed reason=Destroyed"));
            Assert.That(_deployables.Damage(1, 2, 100), Is.False, "a removed turret takes no more damage");
        }

        [Test]
        public void ShotsOfTheOwnerAndTeammatesPassThrough()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Join(3);
            Step(0.2f);
            Deploy(1, _shieldId, _alphaFront, 90f);

            Assert.That(_deployables.Damage(1, 1, 50), Is.False);
            Assert.That(_deployables.Damage(1, 3, 50), Is.False);
            Assert.That(_deployables.Raycast(new Vector3(-6f, 1.5f, 0f), Vector3.UnitX, 10f, 3, out _, out _), Is.False);
            Assert.That(_deployables.Raycast(new Vector3(6f, 1.5f, 0f), -Vector3.UnitX, 20f, 2, out var id, out var distance), Is.True);
            Assert.That(id, Is.EqualTo(1));
            Assert.That(distance, Is.EqualTo(8.9f).Within(1e-3f), "the enemy's shot stops at the shield's face");
            Assert.That(_deployables.Damage(1, null, 50), Is.True, "damage without an attacker is not let through");
            Assert.That(Rows(1)[0].Health, Is.EqualTo(DeploySpec.DefaultMaxHealth - 50));
        }

        [Test]
        public void TheTurretDoesNotShootItsOwnerOrTeammates()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Join(3);
            Step(0.2f);
            Assert.That(_rules.Session.GetTeam(3), Is.EqualTo(Team.Alpha));

            // 敵が倒れて出直すまでの間は、狙える相手が持ち主と味方しかいない
            _combat.DealDamage(1, 2, NetMatchSettings.DefaultMaxHealth, TestRules.RifleId, HitZone.Body, null);
            Deploy(1, _turretId, _alphaFront, 270f);
            Step(1f);

            Assert.That(_rules.Session.GetHealth(1), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_rules.Session.GetHealth(3), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_log.ToString(), Does.Not.Contain("fire target="));
        }

        [Test]
        public void TheTurretRunsOutOfTimeAndLeaves()
        {
            Start(Catalog(turret: new TurretSpec(60, 0.5f, 12f, 270f, 5f)));
            Join(1);
            Join(2);
            Step(0.2f);
            Deploy(1, _turretId, _alphaFront, 90f);

            Step(0.6f);

            Assert.That(Rows(1), Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-turret serial=1 removed reason=Expired"));
        }

        [Test]
        public void TheTurretRunsOutOfRoundsAndLeaves()
        {
            Start(Catalog(turret: new TurretSpec(2, 30f, 12f, 270f, 5f)));
            Join(1);
            Join(2);
            Step(0.2f);
            Deploy(1, _turretId, _alphaFront, 90f);

            Step(0.5f);

            Assert.That(Rows(1), Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Turret] owner=1 kind=deploy-turret serial=1 fire target=2 hit=2 dmg=25 rounds=0"));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-turret serial=1 removed reason=Expended"));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 50));
        }

        [Test]
        public void ADecoyCallsAndExpires()
        {
            Start(Catalog(decoy: new DecoySpec(1f, 0.4f, 10f)));
            Join(1);
            Join(2);
            Step(0.2f);
            Deploy(1, _decoyId, _alphaFront, 0f);
            var row = Rows(1)[0];
            Assert.That(row.Kind, Is.EqualTo(DeployableKind.Decoy));
            Assert.That(row.Health, Is.EqualTo(DecoySpec.DefaultMaxHealth));

            Step(1.1f);

            var calls = _log.ToString().Split("[Decoy] owner=1 kind=deploy-decoy serial=1 call at=(-3.00, 0.50, 0.00)").Length - 1;
            Assert.That(calls, Is.EqualTo(3), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-decoy serial=1 removed reason=Expired"));
            Assert.That(Rows(1), Is.Empty);
        }

        [Test]
        public void PlacingMoreThanTheLimitReplacesTheOldest()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);

            Deploy(1, _shieldId, new Vector3(-3f, 0.5f, -2.5f), 90f);
            Deploy(1, _shieldId, new Vector3(-3f, 0.5f, 0f), 90f);
            Deploy(1, _shieldId, new Vector3(-3f, 0.5f, 2.5f), 90f);

            var rows = Rows(1);
            Assert.That(rows, Has.Count.EqualTo(2), _log.ToString());
            Assert.That(rows[0].DeployableId, Is.EqualTo(2));
            Assert.That(rows[1].DeployableId, Is.EqualTo(3));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-shield serial=1 removed reason=Replaced"));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-shield placed serial=3 pos=(-3.00, 0.50, 2.50) active=2/2"));
        }

        [Test]
        public void ARefusingTypeRefusesOverTheLimit()
        {
            var shield = new DeploySpec(1, DeployOverflow.Refuse, 300, 4f, 30f, 0.25f, 2f, 1.4f, 0.2f);
            Start(Catalog(shield: shield));
            Join(1);
            Join(2);
            Step(0.2f);

            Deploy(1, _shieldId, new Vector3(-3f, 0.5f, -2.5f), 90f);
            Assert.That(_deployables.Place(1, _shieldId, new Vector3(-3f, 0.5f, 2.5f), 90f), Is.EqualTo(ServerDeployRefusal.LimitReached));

            Assert.That(Rows(1), Has.Count.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-shield refused reason=Limit active=1/1"));
        }

        [Test]
        public void AFullServerRefusesANewOneButLetsAnOwnerReplaceTheirOldest()
        {
            // 足元 0.1 m 角の小さな盾を 0.3 m おきに 8x8 並べ、全員分の合計を SnapshotMessage.MaxDeployables (64) にする
            var shield = new DeploySpec(SnapshotMessage.MaxDeployables, DeployOverflow.ReplaceOldest, 300, 4f, 30f, 0.25f, 0.1f, 0.5f, 0.1f);
            Start(Catalog(shield: shield));
            Join(1);
            Join(2);
            Step(0.2f);

            for (var i = 0; i < SnapshotMessage.MaxDeployables; i++)
            {
                var position = new Vector3(-4f + (0.3f * (i % 8)), 0.5f, -1.05f + (0.3f * (i / 8)));
                Assert.That(_deployables.Place(1, _shieldId, position, 0f), Is.EqualTo(ServerDeployRefusal.None), _log.ToString());
            }

            Assert.That(_deployables.Place(1, _decoyId, new Vector3(-3f, 0.5f, -2f), 0f), Is.EqualTo(ServerDeployRefusal.LimitReached));
            Assert.That(Rows(1), Has.Count.EqualTo(64));

            Assert.That(_deployables.Place(1, _shieldId, new Vector3(-3f, 0.5f, 1.6f), 0f), Is.EqualTo(ServerDeployRefusal.None), _log.ToString());
            var rows = Rows(1);
            Assert.That(rows, Has.Count.EqualTo(64));
            Assert.That(rows[0].DeployableId, Is.EqualTo(2));
            Assert.That(rows[63].DeployableId, Is.EqualTo(65));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-shield serial=1 removed reason=Replaced"));
        }

        [Test]
        public void RequestsThatCannotBePlacedAreRefused()
        {
            Start(Catalog(), wall: true);
            Join(1);
            Join(2);
            Step(0.2f);

            Assert.That(_deployables.Place(1, "deploy-missing", _alphaFront, 0f), Is.EqualTo(ServerDeployRefusal.UnknownType));
            Assert.That(_deployables.Place(1, _shieldId, new Vector3(5f, 0.5f, 0f), 0f), Is.EqualTo(ServerDeployRefusal.OutOfReach));
            Assert.That(_deployables.Place(1, _shieldId, new Vector3(-3f, 3f, 0f), 0f), Is.EqualTo(ServerDeployRefusal.NoGround));
            Assert.That(_deployables.Place(1, _shieldId, new Vector3(-5f, 0.5f, 0f), 0f), Is.EqualTo(ServerDeployRefusal.Blocked), "the owner stands there");
            Assert.That(_deployables.Place(1, _shieldId, new Vector3(-2f, 0.5f, 0f), 90f), Is.EqualTo(ServerDeployRefusal.Blocked), "the beam is there");
            Assert.That(_deployables.Place(1, _shieldId, new Vector3(-3f, 2.5f, 0.3f), 0f), Is.EqualTo(ServerDeployRefusal.Uneven), "the shield hangs over the step");
            Assert.That(Rows(1), Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=- net refused reason=UnknownType"));
        }

        [Test]
        public void WithAbilitiesOffTheRuleRefusesAndNothingIsPlaced()
        {
            Start(Catalog(), abilities: Abilities.Off);
            Join(1);
            Join(2);
            Step(0.2f);

            Assert.That(_deployables.Place(1, _turretId, _alphaFront, 90f), Is.EqualTo(ServerDeployRefusal.RuleOff));
            Deploy(2, _shieldId, _bravoFront, 90f);

            Assert.That(Rows(1), Is.Empty);
            Assert.That(_deployables.Count, Is.EqualTo(0));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=- net refused reason=RuleOff"));
            Assert.That(_log.ToString(), Does.Not.Contain(" placed serial="));
        }

        [Test]
        public void WithAbilitiesOnTheSameRequestIsPlaced()
        {
            Start(Catalog(), abilities: Abilities.On);
            Join(1);
            Join(2);
            Step(0.2f);

            Assert.That(_deployables.Place(1, _turretId, _alphaFront, 90f), Is.EqualTo(ServerDeployRefusal.None), _log.ToString());

            var rows = Rows(1);
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0].TypeId, Is.EqualTo(_turretId));
            Assert.That(_log.ToString(), Does.Not.Contain("reason=RuleOff"));
        }

        [Test]
        public void ADeadPlayerCannotPlace()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);
            _combat.DealDamage(2, 1, NetMatchSettings.DefaultMaxHealth, TestRules.RifleId, HitZone.Body, null);

            Assert.That(_deployables.Place(1, _shieldId, _alphaFront, 90f), Is.EqualTo(ServerDeployRefusal.CannotAct));
            Assert.That(Rows(2), Is.Empty);
        }

        [Test]
        public void AnOwnerWhoLeavesTakesTheirDeployablesAlong()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);
            Deploy(1, _shieldId, _alphaFront, 90f);
            Deploy(2, _shieldId, _bravoFront, 90f);

            _match.Leave(new ConnectionId(1));
            _host.OnDisconnected(new ConnectionId(1));

            var rows = Rows(2);
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0].OwnerId, Is.EqualTo(2));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-shield serial=1 removed reason=Discarded"));
        }

        [Test]
        public void ATurretWhoseWeaponIsNotInTheRulesCannotBePlaced()
        {
            var turret = new DeployType(_turretId, DeployRole.Turret, DeployType.DefaultDeploy(DeployRole.Turret), TurretSpec.Default, "weapon-missing");
            Start(new DeployCatalog(new[] { turret }));
            Join(1);
            Join(2);
            Step(0.2f);

            Assert.That(_log.ToString(), Does.Contain("[Deploy] net kind=deploy-turret unusable: weapon weapon-missing is not in the rules TeamDeathmatch"));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] net types=1 usable=0 from=memory"));
            Assert.That(_deployables.Place(1, _turretId, _alphaFront, 90f), Is.EqualTo(ServerDeployRefusal.Unusable));
        }

        [Test]
        public void TheCatalogIsReadFromTheTuningDirectory()
        {
            using var directory = new TempDirectory();
            var tuning = Path.Combine(directory.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(tuning);
            File.WriteAllText(
                Path.Combine(tuning, ServerDeployables.CatalogFileName),
                "{\"types\": [{\"id\": \"deploy-shield\", \"role\": \"Shield\", \"maxHealth\": 120}]}");

            Assert.That(ServerDeployables.TryLoadCatalog(new[] { directory.Path }, out var catalog, out var path, out var error), Is.True, error);
            Assert.That(catalog.Count, Is.EqualTo(1));
            Assert.That(catalog.All[0].Deploy.MaxHealth, Is.EqualTo(120));
            Assert.That(path, Is.EqualTo(Path.GetFullPath(Path.Combine(tuning, ServerDeployables.CatalogFileName))));
        }

        [Test]
        public void AMissingCatalogPlacesNothingAndABrokenOneStopsTheServer()
        {
            using var empty = new TempDirectory();
            Assert.That(ServerDeployables.TryLoadCatalog(new[] { empty.Path }, out var none, out var nonePath, out var noneError), Is.True);
            Assert.That(none.Count, Is.EqualTo(0));
            Assert.That(nonePath, Is.Null);
            Assert.That(noneError, Is.Null);

            using var broken = new TempDirectory();
            var tuning = Path.Combine(broken.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(tuning);
            File.WriteAllText(Path.Combine(tuning, ServerDeployables.CatalogFileName), "{\"types\": [{\"id\": \"x\", \"role\": \"Mine\"}]}");

            Assert.That(ServerDeployables.TryLoadCatalog(new[] { broken.Path }, out var catalog, out _, out var error), Is.False);
            Assert.That(catalog.Count, Is.EqualTo(0));
            Assert.That(error, Does.StartWith("cannot read deployable tuning: ").And.Contain("types[0].role"));
        }

        private static DeployCatalog Catalog(TurretSpec turret = null, DecoySpec decoy = null, DeploySpec shield = null)
        {
            return new DeployCatalog(new[]
            {
                new DeployType(_turretId, DeployRole.Turret, DeployType.DefaultDeploy(DeployRole.Turret), turret ?? TurretSpec.Default, TestRules.RifleId),
                new DeployType(_shieldId, DeployRole.Shield, shield ?? DeployType.DefaultDeploy(DeployRole.Shield)),
                new DeployType(_decoyId, DeployRole.Decoy, DeployType.DefaultDeploy(DeployRole.Decoy), decoy: decoy ?? DecoySpec.Default),
            });
        }

        private static GameRules Rules(Abilities abilities)
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off).WithAbilities(abilities);
            return GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
        }

        // wall は Alpha の前 (x=-2) で床から浮いた梁 (y 1.0〜1.4) と、x=-3・z 0〜1 に置いた上面 2.5 の 1 m 角の段
        private void Start(DeployCatalog catalog, bool wall = false, Abilities abilities = Abilities.On)
        {
            var colliders = new List<WorldCollider>
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(60f, 0.5f, 60f), Quaternion.Identity),
            };
            if (wall)
            {
                colliders.Add(WorldCollider.Box(2, 0, WorldCollider.StaticGroup, new Vector3(-2f, 1.2f, 0f), new Vector3(0.3f, 0.2f, 2f), Quaternion.Identity));
                colliders.Add(WorldCollider.Box(3, 0, WorldCollider.StaticGroup, new Vector3(-3f, 1.5f, 0.5f), new Vector3(0.5f, 1f, 0.5f), Quaternion.Identity));
            }

            var points = new NetMapPoints(new[] { new SpawnPoint(_alphaSpawn, Team.Alpha), new SpawnPoint(_bravoSpawn, Team.Bravo) }, Array.Empty<ControlPointArea>());
            var map = new ServerMapData("NetTest", new WorldMap(colliders), TestMaps.NavMesh(), "memory", points);
            var data = new ServerRulesData("TeamDeathmatch", TestRules.File(Rules(abilities)), PingSpec.Default, "memory", null);
            _match = new ServerMatch(NetMatchSettings.DefaultRestartDelaySeconds, _log, new ServerEventWriter(false, TextWriter.Null, _log), () => _time);
            _context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                map,
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                data);
            var movement = new ServerMovement(ServerModules.MotorSettingsFrom(data));
            _rules = new MatchRulesModule();
            _combat = new ServerCombat(_rules, movement);
            _deployables = new ServerDeployables(_rules, _combat, catalog, "memory");
            _host = new ServerHost();
            _host.Register(movement);
            _host.Register(_rules);
            _host.Register(_combat);
            _host.Register(_deployables);
            _host.Start(_context);
        }

        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        private void Deploy(int playerId, string typeId, Vector3 position, float yaw)
        {
            _host.OnMessage(new ConnectionId((ulong)playerId), new DeployRequestMessage { TypeId = typeId, Position = position, Yaw = yaw });
        }

        private List<DeployableSnapshot> Rows(int receiverId)
        {
            var snapshot = new SnapshotMessage();
            Assert.That(_match.TryGetPlayer(receiverId, out var receiver), Is.True);
            _deployables.Contribute(snapshot, receiver);
            var rows = new List<DeployableSnapshot>();
            for (var i = 0; i < snapshot.Deployables.Count; i++)
            {
                rows.Add(snapshot.Deployables[i]);
            }

            return rows;
        }

        private void Step(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                _match.Tick(_tickSeconds);
                _host.Tick(_tickSeconds);
            }
        }
    }
}
