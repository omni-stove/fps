using System;
using System.IO;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class BattleRoyaleRulesTests
    {
        private const float _dropHeight = 50f;
        private const float _zoneRadius = 20f;

        // 最初の円 (中心 0,0,0・半径 20) の中に出現地点を 2 つ置く。体の高さは 1
        private static readonly Vector3 _alphaSpawn = new Vector3(-5f, 1f, 0f);
        private static readonly Vector3 _bravoSpawn = new Vector3(5f, 1f, 0f);
        private static readonly Vector3 _outside = new Vector3(50f, 1f, 0f);
        private static readonly Vector3 _inside = new Vector3(0f, 1f, 0f);
        private static readonly int[] _allFour = { 1, 2, 3, 4 };
        private static readonly int[] _firstOnly = { 1 };
        private static readonly int[] _firstTwice = { 1, 1 };
        private static readonly int[] _secondOnly = { 2 };

        private StringWriter _log;
        private MatchHost _host;
        private BattleRoyaleTestWorld _world;
        private BattleRoyaleRules _rules;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
        }

        [TearDown]
        public void TearDown()
        {
            _rules?.Stop();
            _log.Dispose();
        }

        [Test]
        public void TheSessionUsesDownsAndNeverEndsOrRespawnsOnItsOwn()
        {
            var rules = new BattleRoyaleRules(new MatchHost(_log, Points()), Spec(), new BattleRoyaleTestWorld(), 1);
            var settings = new NetMatchSettings(respawnSeconds: 7f);

            var spec = rules.CreateSessionSpec(GameRules.Default.WithActions(ActionRules.Default.WithDowned(Downed.Off)), settings);

            Assert.That(rules.Objective, Is.EqualTo(MatchObjective.BattleRoyale));
            Assert.That(spec.ScoreLimit, Is.EqualTo(int.MaxValue));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(float.MaxValue));
            Assert.That(spec.RespawnsByRound, Is.True, "only a redeploy brings a player back");
            Assert.That(spec.Rules.Actions.Downed, Is.EqualTo(Downed.Revive), "the mode always uses downs, as offline");
            Assert.That(rules.PlaysInRounds, Is.False);
            Assert.That(rules.KeepsMatchScore, Is.True);
            Assert.That(rules.HoldsFire, Is.False);
            Assert.That(rules.RemainingSeconds, Is.Zero, "without a time limit the zone decides");
            Assert.That(rules.DecideWinner(Team.Alpha), Is.Null, "a squad wins, not a team");
            Assert.That(rules.Zone, Is.Null);
            Assert.That(rules.BotRally, Is.Null);
            Assert.That(rules.Tick(true, 1f), Is.False, "nothing runs before Start");
        }

        [Test]
        public void PlayersAreSplitIntoSquadsAsTheyJoin()
        {
            Begin(Spec());

            _host.Join(1, Team.Alpha);
            _host.Join(2, Team.Bravo);
            _host.Join(3, Team.Alpha);
            _host.Join(4, Team.Alpha);
            _host.Join(5, Team.Bravo);

            var mode = _rules.BattleRoyale.Mode;
            Assert.That(mode.GetSide(1), Is.EqualTo(new Side(0)));
            Assert.That(mode.GetSide(2), Is.EqualTo(new Side(1)));
            Assert.That(mode.GetSide(3), Is.EqualTo(new Side(0)));
            Assert.That(mode.GetSide(4), Is.EqualTo(new Side(1)));
            Assert.That(mode.HasPlayer(5), Is.False, "two squads of two are full");
            Assert.That(_log.ToString(), Does.Contain("[BattleRoyale] squad player=1 squad=0"));
            Assert.That(_log.ToString(), Does.Contain("[BattleRoyale] squad player=4 squad=1"));
            Assert.That(_log.ToString(), Does.Contain("[BattleRoyale] squad player=5 squad=none (every squad is full; the player sits out this match)"));

            var row = new PlayerSnapshot { SquadId = 9 };
            _rules.FillPlayer(4, ref row);
            Assert.That(row.SquadId, Is.EqualTo(1));
            _rules.FillPlayer(5, ref row);
            Assert.That(row.SquadId, Is.Zero, "a row reused for a player outside the squads does not keep the last squad");

            Assert.That(_rules.AreHostile(1, Team.Alpha, 3, Team.Alpha), Is.False);
            Assert.That(_rules.AreHostile(1, Team.Alpha, 4, Team.Alpha), Is.True, "the same team in another squad");
            Assert.That(_rules.AreHostile(2, Team.Bravo, 4, Team.Alpha), Is.False, "another team in the same squad");
            Assert.That(_rules.AreHostile(5, Team.Bravo, 1, Team.Alpha), Is.False, "a player outside the squads is nobody's enemy");
            Assert.That(_rules.AreHostile(1, Team.Alpha, 1, Team.Alpha), Is.False);
            Assert.That(_rules.AreAllies(1, Team.Alpha, 3, Team.Alpha), Is.True);
            Assert.That(_rules.AreAllies(2, Team.Bravo, 4, Team.Alpha), Is.True, "another team in the same squad");
            Assert.That(_rules.AreAllies(1, Team.Alpha, 4, Team.Alpha), Is.False, "the same team in another squad");
            Assert.That(_rules.AreAllies(5, Team.Bravo, 1, Team.Alpha), Is.False, "a player outside the squads is nobody's ally");
            Assert.That(_rules.AreAllies(1, Team.Bravo, 5, Team.Bravo), Is.False, "a player outside the squads is nobody's ally");
            Assert.That(_rules.AreAllies(5, Team.Bravo, 5, Team.Bravo), Is.True);

            var snapshot = default(MatchSnapshot);
            _rules.FillMatch(ref snapshot);
            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.BattleRoyale));
            Assert.That(snapshot.SquadsRemaining, Is.EqualTo(2), "before the drop, the squads with players");
            Assert.That(snapshot.ZonePhase, Is.Zero, "the zone has not started");
            Assert.That(snapshot.ZoneRadius, Is.EqualTo(_zoneRadius));
        }

        [Test]
        public void SquadmatesCannotHurtEachOtherWhateverTheirTeams()
        {
            Begin(Spec());
            JoinFour();
            _host.Steps(1);
            var session = _host.Session;

            Assert.That(session.ApplyDamage(1, 3, 10), Is.EqualTo(DamageOutcome.Ignored), "1 and 3 share squad 0");
            Assert.That(session.ApplyDamage(2, 4, 10), Is.EqualTo(DamageOutcome.Ignored), "2 and 4 share squad 1 across teams");
            Assert.That(session.ApplyDamage(1, 2, 10), Is.EqualTo(DamageOutcome.Damaged));
            Assert.That(session.ApplyDamage(3, 4, 10), Is.EqualTo(DamageOutcome.Damaged), "3 and 4 share team Alpha but not a squad");
            Assert.That(session.GetHealth(3), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(session.GetHealth(4), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 10));
        }

        [Test]
        public void TheDropTakesTheWeaponsAndParachutesEveryoneFromTheMapHeight()
        {
            Begin(Spec());
            JoinFour();

            _host.Steps(1);

            Assert.That(_rules.BattleRoyale.IsLive, Is.True);
            Assert.That(_world.Disarmed.OrderBy(id => id), Is.EqualTo(_allFour));
            Assert.That(_world.Drops.Select(d => d.PlayerId).OrderBy(id => id), Is.EqualTo(_allFour));
            foreach (var (playerId, from) in _world.Drops)
            {
                Assert.That(_host.Bodies.TryGet(playerId, out var body), Is.True);
                Assert.That(from, Is.EqualTo(body.Position + new Vector3(0f, _dropHeight, 0f)), "dropped from above the spawn " + playerId);
            }

            var log = _log.ToString();
            Assert.That(log, Does.Contain("[BattleRoyale] net squads=2x2 timeLimit=none zone=(0.00,0.00,0.00) radius=20.0m phases=1 drop=50.0m"));
            Assert.That(log, Does.Contain("[BattleRoyale] begin squads=2 players=4"));
            Assert.That(log, Does.Contain("[Zone] start center=(0.00, 0.00) radius=20.00 phases=1"));
            Assert.That(log, Does.Contain("[Zone] phase=1/1 stage=Waiting radius=20.00"));
            Assert.That(log, Does.Contain("[BattleRoyale] drop player=1 height=50.0m from="));
        }

        [Test]
        public void AMapWithoutADropHeightStartsOnTheGround()
        {
            Begin(Spec(), Points(dropHeight: null));
            JoinFour();

            _host.Steps(1);

            Assert.That(_world.Disarmed, Has.Count.EqualTo(4), "the weapons are still taken");
            Assert.That(_world.Drops, Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[BattleRoyale] drop player=1 height=none"));
        }

        [Test]
        public void PlayersOutsideTheZoneTakeTheDamageOfTheStage()
        {
            Begin(Spec(zone: OnePhase(damagePerSecond: 10f)));
            JoinFour();
            _host.Steps(1);
            _host.Place(1, _outside);
            _host.Place(2, _inside);

            _host.Steps(65);

            var session = _host.Session;
            Assert.That(session.GetHealth(1), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 10), _log.ToString());
            Assert.That(session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_log.ToString(), Does.Contain("[Zone] outside target=1"));
            Assert.That(_log.ToString(), Does.Contain("[BattleRoyale] zone-damage player=1 damage=10 outcome=Damaged"));

            _host.Place(1, _inside);
            _host.Steps(60);

            Assert.That(session.GetHealth(1), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 10), "no damage back inside");
            Assert.That(_log.ToString(), Does.Contain("[Zone] inside target=1"));
        }

        [Test]
        public void ZoneDamageBelowOnePerHitCarriesOverAsOffline()
        {
            Begin(Spec(zone: OnePhase(damagePerSecond: 0.5f)));
            JoinFour();
            _host.Steps(1);
            _host.Place(1, _outside);

            _host.Steps(65);
            Assert.That(_host.Session.GetHealth(1), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), "half a point is owed, not dealt");

            _host.Steps(60);
            Assert.That(_host.Session.GetHealth(1), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 1));
        }

        [Test]
        public void SupplyDropsFallIntoTheNextCircle()
        {
            var drops = new SupplyDropSpec(
                new[] { new SupplyDropStage(3, 0f, 0.2f) }, "crate-table", dropHeight: 6f, fallSpeed: 6f, placeFraction: 0.8f);
            Begin(Spec(drops: drops));
            JoinFour();
            _host.Steps(30);

            Assert.That(_world.Crates, Is.Empty, "a crate takes a second to fall");
            Assert.That(_log.ToString(), Does.Contain("[SupplyDrop] stage phase=1 crates=3"));
            Assert.That(_log.ToString(), Does.Contain("[SupplyDrop] dropped crate=1 at="));

            _host.Steps(90);

            Assert.That(_world.Crates, Has.Count.EqualTo(3), _log.ToString());
            var zone = _rules.Zone.Value;
            Assert.That(zone.TargetRadius, Is.EqualTo(8f));
            foreach (var (position, tableId) in _world.Crates)
            {
                var flat = new Vector2(position.X, position.Z);
                Assert.That(Vector2.Distance(flat, zone.TargetCenter), Is.LessThanOrEqualTo((zone.TargetRadius * 0.8f) + 1e-3f), "inside the next circle");
                Assert.That(tableId, Is.EqualTo("crate-table"));
                var onFloor = Math.Abs(position.X) <= 10f && Math.Abs(position.Z) <= 10f;
                Assert.That(position.Y, Is.EqualTo(onFloor ? 0.5f : 0f).Within(1e-4f), "on the floor of the test map, or at the zone height off it");
            }

            Assert.That(_log.ToString(), Does.Contain("[SupplyDrop] landed crate=3 at="));
            Assert.That(_log.ToString(), Does.Contain("[BattleRoyale] crate=1 item=1001"));
        }

        [Test]
        public void ARedeployBringsAFallenPlayerBackOnlyAsOftenAsTheCharges()
        {
            Begin(Spec(redeploy: new RedeploySpec(1, 5, 1f)));
            JoinFour();
            _host.Steps(1);
            var session = _host.Session;

            KillFully(2, 1);

            Assert.That(session.IsAlive(1), Is.False);
            Assert.That(_rules.BattleRoyale.GetRedeploysLeft(1), Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("[Mode] redeploy player=1 verdict=Waiting phase=1 left=0 in=1.00"));
            _host.Steps(1);
            Assert.That(_world.DeathBoxes, Is.EqualTo(_firstOnly), "the fallen leave their things in a box");

            _host.Steps(65);

            Assert.That(session.IsAlive(1), Is.True, _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[BattleRoyale] redeployed player=1 left=0"));
            Assert.That(_world.Disarmed.Count(id => id == 1), Is.EqualTo(2), "the redeploy takes the respawn loadout away again");
            Assert.That(_world.Drops.Count(d => d.PlayerId == 1), Is.EqualTo(2), "the redeploy parachutes in again");

            KillFully(2, 1);
            _host.Steps(120);

            Assert.That(_log.ToString(), Does.Contain("[Mode] redeploy player=1 verdict=NoCharges phase=1 left=0"));
            Assert.That(session.IsAlive(1), Is.False, "no charge is left");
            Assert.That(_world.DeathBoxes, Is.EqualTo(_firstTwice));
        }

        [Test]
        public void TheLastSquadStandingWinsAndItsPlayersCountAsWinners()
        {
            Begin(Spec());
            _host.Join(1, Team.Alpha);
            _host.Join(2, Team.Bravo);
            _host.Steps(1);
            var snapshot = default(MatchSnapshot);
            _rules.FillMatch(ref snapshot);
            Assert.That(snapshot.SquadsRemaining, Is.EqualTo(2));
            Assert.That(snapshot.ZonePhase, Is.EqualTo(1));
            Assert.That(snapshot.ZoneRadius, Is.EqualTo(_zoneRadius));
            Assert.That(snapshot.ZonePhaseRemainingSeconds, Is.GreaterThan(0));

            Assert.That(_host.Session.ApplyDamage(1, 2, NetMatchSettings.DefaultMaxHealth), Is.EqualTo(DamageOutcome.Killed), "no squadmate is standing to revive 2");
            _host.Steps(1);

            Assert.That(_host.Ended, Is.True, _log.ToString());
            Assert.That(_host.Winner, Is.Null);
            Assert.That(_rules.IsWinningPlayer(1, Team.Alpha, null), Is.True);
            Assert.That(_rules.IsWinningPlayer(2, Team.Bravo, null), Is.False);
            Assert.That(_rules.BattleRoyale.GetPlacement(new Side(0)), Is.EqualTo(1));
            Assert.That(_rules.BattleRoyale.GetPlacement(new Side(1)), Is.EqualTo(2));
            Assert.That(_world.DeathBoxes, Is.EqualTo(_secondOnly));
            Assert.That(_log.ToString(), Does.Contain("[Mode] placements winner=0 1:squad=0 2:squad=1"));
            Assert.That(_log.ToString(), Does.Contain("[BattleRoyale] state=Ended winner=squad0 squads=2"));
            _rules.FillMatch(ref snapshot);
            Assert.That(snapshot.SquadsRemaining, Is.Zero);

            _host.Session.Restart();
            Assert.That(_log.ToString(), Does.Contain("[BattleRoyale] restart"));
            Assert.That(_rules.BattleRoyale.Mode.State, Is.EqualTo(GameModeState.Waiting));
            Assert.That(_rules.IsWinningPlayer(1, Team.Alpha, null), Is.False);
            _host.Steps(1);
            Assert.That(_rules.BattleRoyale.IsLive, Is.True, "the next match drops again");
        }

        [Test]
        public void ALateJoinerFillsAStandingSquadOrSitsOut()
        {
            Begin(Spec());
            _host.Join(1, Team.Alpha);
            _host.Join(2, Team.Bravo);
            _host.Join(3, Team.Alpha);
            _host.Steps(1);

            _host.Join(4, Team.Bravo);
            _host.Join(5, Team.Alpha);

            Assert.That(_rules.BattleRoyale.Mode.GetSide(4), Is.EqualTo(new Side(1)), "squad 0 is full");
            Assert.That(_world.Disarmed, Does.Contain(4));
            Assert.That(_world.Drops.Any(d => d.PlayerId == 4), Is.True);
            Assert.That(_rules.BattleRoyale.Mode.HasPlayer(5), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[BattleRoyale] squad player=5 squad=none (joined after the drop"));
            Assert.That(_rules.AreHostile(5, Team.Alpha, 1, Team.Alpha), Is.False, "a player outside the squads cannot hurt anyone");
            Assert.That(_rules.AreAllies(5, Team.Alpha, 1, Team.Alpha), Is.False, "nor count as anyone's squadmate");
        }

        [Test]
        public void APlayerOutsideTheSquadsNeitherKeepsASquadDownedNorRevivesIt()
        {
            Begin(Spec());
            JoinFour();
            _host.Join(5, Team.Bravo);
            _host.Steps(1);
            Assert.That(_rules.BattleRoyale.Mode.HasPlayer(5), Is.False);
            var session = _host.Session;
            Assert.That(session.AreAllies(5, 1), Is.False);
            Assert.That(session.AreAllies(2, 4), Is.True);

            Assert.That(session.ApplyDamage(1, 2, NetMatchSettings.DefaultMaxHealth), Is.EqualTo(DamageOutcome.Downed), "4 is standing in squad 1");
            Assert.That(session.TryRevive(5, 2, 60f), Is.False, "5 is not in squad 1");
            Assert.That(session.IsDowned(2), Is.True);
            Assert.That(session.ApplyDamage(1, 4, NetMatchSettings.DefaultMaxHealth), Is.EqualTo(DamageOutcome.Killed), "5 standing does not keep the last of squad 1 downed");
            Assert.That(session.IsAlive(2), Is.False, "the wiped squad's downed player goes down for good");
        }

        [Test]
        public void SquadmatesOpenBoxesOnlyWhileTheMatchIsLive()
        {
            Begin(Spec());
            JoinFour();

            Assert.That(_rules.Interact(1, 77).IsAccepted, Is.False, "before the drop");
            _host.Steps(1);

            Assert.That(_rules.Interact(1, 77).IsAccepted, Is.True);
            Assert.That(_rules.Interact(5, 77).Refusal, Is.EqualTo(InteractRefusal.CannotAct));
        }

        [Test]
        public void BotsHeadIntoTheZoneThenToAWeaponBoxAndOpenIt()
        {
            Begin(Spec());
            JoinFour();
            _host.Steps(1);
            _host.Place(1, _outside);

            var first = _rules.BotGoal(1);
            var zone = _rules.Zone.Value;
            Assert.That(first.HasValue, Is.True);
            Assert.That(Vector2.Distance(new Vector2(first.Value.X, first.Value.Z), zone.TargetCenter), Is.LessThanOrEqualTo(zone.TargetRadius));
            Assert.That(_rules.BotGoal(1), Is.EqualTo(first), "the same goal until it is planned again");
            Assert.That(_log.ToString(), Does.Contain("[Mode] errand player=1 kind=EnterZone"));

            var box = new Vector3(3f, 1f, 0f);
            _world.Boxes.Add(new ServerLootBox(77, ObjectiveItemKind.LootBox, box, 1.6f, true));
            _host.Place(1, _inside);
            _host.Steps(31);

            Assert.That(_rules.BotGoal(1), Is.EqualTo(box), "an unarmed bot goes for the weapon box");
            Assert.That(_log.ToString(), Does.Contain("[Mode] errand player=1 kind=Collect"));

            _host.Place(1, new Vector3(3.5f, 1f, 0f));
            _host.Steps(1);

            Assert.That(_world.Opened, Is.EqualTo(new[] { (1, 77) }), "only the bot that asked for a goal opens boxes by itself");
        }

        [Test]
        public void AMapWithoutAZoneIsRefusedWithTheReason()
        {
            var rules = new BattleRoyaleRules(new MatchHost(_log, Points()), Spec(), new BattleRoyaleTestWorld(), 1);
            var noZone = TestRules.Points();
            var map = TestMaps.Data("Empty", noZone);

            var check = Assert.Throws<InvalidOperationException>(() => rules.CheckMap(map, noZone));
            Assert.That(check.Message, Is.EqualTo(
                "cannot change to map Empty for the BattleRoyale match: it has no zone; "
                + "place a ShrinkingZone in the scene and export the map points with make export-server-rules"));
            var use = Assert.Throws<InvalidOperationException>(() => rules.UseMap(map, noZone));
            Assert.That(use.Message, Is.EqualTo(
                "cannot start the BattleRoyale match on map Empty: it has no zone; "
                + "place a ShrinkingZone in the scene and export the map points with make export-server-rules"));
            Assert.Throws<InvalidOperationException>(() => rules.Start(), "Start needs a map");

            var small = Points(zoneRadius: 5f);
            var tooSmall = Assert.Throws<InvalidOperationException>(() => rules.CheckMap(TestMaps.Data("Small", small), small));
            Assert.That(tooSmall.Message, Does.StartWith(
                "cannot change to map Small for the BattleRoyale match: the zone of radius 5 does not fit the zone phases of battle-royale.json: "));

            Assert.DoesNotThrow(() => rules.CheckMap(TestMaps.Data("Zoned", Points()), Points()));
        }

        private static BattleRoyaleSpec Spec(ZoneSpec zone = null, SupplyDropSpec drops = null, RedeploySpec redeploy = null)
        {
            return new BattleRoyaleSpec(
                2,
                2,
                0f,
                BattleRoyaleSpec.DefaultBotSearchRadius,
                0.5f,
                zone ?? OnePhase(damagePerSecond: 1f),
                drops ?? new SupplyDropSpec(Array.Empty<SupplyDropStage>()),
                redeploy ?? RedeploySpec.None);
        }

        // 縮み始めるまで 60 秒待つので、どのテストの間も円は最初の大きさのまま
        private static ZoneSpec OnePhase(float damagePerSecond)
        {
            return new ZoneSpec(new[] { new ZonePhase(60f, 10f, 8f, damagePerSecond) }, 1f, 3);
        }

        private static NetMapPoints Points(float? dropHeight = _dropHeight, float zoneRadius = _zoneRadius)
        {
            return new NetMapPoints(
                new[] { new SpawnPoint(_alphaSpawn, Team.Alpha), new SpawnPoint(_bravoSpawn, Team.Bravo) },
                Array.Empty<ControlPointArea>(),
                zone: new NetMapZoneCircle(Vector3.Zero, zoneRadius),
                dropHeight: dropHeight);
        }

        private static GameRules Plain()
        {
            return GameRules.Default
                .WithFriendlyFire(FriendlyFire.Off)
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f);
        }

        private void Begin(BattleRoyaleSpec spec, NetMapPoints points = null)
        {
            points ??= Points();
            _host = new MatchHost(_log, points);
            _world = new BattleRoyaleTestWorld(() => _host.Session);
            _rules = new BattleRoyaleRules(_host, spec, _world, 11);
            _rules.UseMap(TestMaps.Data("Zoned", points), points);
            _host.Begin(_rules, Plain(), NetMatchSettings.Default);
            _rules.Start();
        }

        // 1・3 が分隊 0、2・4 が分隊 1。4 はチームでは 1・3 と同じ Alpha
        private void JoinFour()
        {
            _host.Join(1, Team.Alpha);
            _host.Join(2, Team.Bravo);
            _host.Join(3, Team.Alpha);
            _host.Join(4, Team.Alpha);
        }

        // 分隊に立っている味方がいるとまずダウンするので、ダウン中にとどめを刺す
        private void KillFully(int attackerId, int victimId)
        {
            var session = _host.Session;
            Assert.That(session.ApplyDamage(attackerId, victimId, NetMatchSettings.DefaultMaxHealth), Is.EqualTo(DamageOutcome.Downed));
            Assert.That(session.ApplyDamage(attackerId, victimId, NetMatchSettings.DefaultMaxHealth), Is.EqualTo(DamageOutcome.Killed));
        }

        // MatchRulesModule が目的を回す順 (Session の Tick の後に目的の Tick、目的が終えたら DecideWinner の勝者で Session を Finish) と、
        // 味方の判定を目的の敵味方で決めさせる渡し方をここでなぞる (PayloadRulesTests と同じ)
        private sealed class MatchHost : IMatchObjectiveHost
        {
            private const float _tickSeconds = 1f / 60f;

            private readonly NetMapPoints _points;
            private IMatchObjectiveRules _rules;
            private long _tick;

            public MatchHost(TextWriter log, NetMapPoints points)
            {
                Log = log;
                _points = points;
                Bodies = new ServerBodies(NetMotorSettings.Default.Stance);
            }

            public TeamDeathmatchSession Session { get; private set; }

            public ServerBodies Bodies { get; }

            public TextWriter Log { get; }

            public bool Ended { get; private set; }

            public Team? Winner { get; private set; }

            public void Begin(IMatchObjectiveRules rules, GameRules gameRules, NetMatchSettings settings)
            {
                _rules = rules;
                TeamDeathmatchSession session = null;
                session = new TeamDeathmatchSession(
                    rules.CreateSessionSpec(gameRules, settings),
                    settings.MaxHealth,
                    _points.SpawnPoints,
                    new SpawnSelector(settings.MinEnemySpawnDistance),
                    PositionOf,
                    null,
                    settings.ReconnectGraceSeconds,
                    null,
                    (a, b) => rules.AreAllies(a, session.GetTeam(a), b, session.GetTeam(b)));
                Session = session;
                Session.Spawned += (playerId, point) => Bodies.Teleport(playerId, point.Position, 0f, _tick);
                Session.Killed += (killerId, victimId, _) => _rules.PlayerKilled(killerId, victimId);
                Session.Restarted += () =>
                {
                    Ended = false;
                    Winner = null;
                    _rules.MatchRestarted();
                };
                Session.Ended += winner =>
                {
                    Ended = true;
                    Winner = _rules.DecideWinner(winner);
                    _rules.MatchEnded();
                };
            }

            public void Join(int playerId, Team team)
            {
                Bodies.Add(playerId);
                Session.Join(playerId, "device" + playerId, team);
                _rules.PlayerJoined(playerId);
            }

            public void Place(int playerId, Vector3 position)
            {
                Assert.That(Bodies.TryGet(playerId, out var body), Is.True);
                body.SetPose(position, 0f, PlayerStance.Standing);
            }

            public void Steps(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    _tick++;
                    Session.Tick(_tickSeconds);
                    if (_rules.Tick(Session.State == GameModeState.Playing, _tickSeconds))
                    {
                        Session.Finish(_rules.DecideWinner(Session.Winner));
                    }
                }
            }

            public void SetMatchScores(int alpha, int bravo)
            {
            }

            public void ClearDowned()
            {
            }

            private Vector3 PositionOf(int playerId)
            {
                return Bodies.TryGet(playerId, out var body) ? body.Position : Vector3.Zero;
            }
        }
    }
}
