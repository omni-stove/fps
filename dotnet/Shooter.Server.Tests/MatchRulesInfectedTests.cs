using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // InfectedRules は ServerObjectiveFactory にまだ登録していないので、MatchRulesModule の目的の選び方を差し替えて試合を通す
    public sealed class MatchRulesInfectedTests
    {
        private const float _tickSeconds = 1f / 60f;
        private static readonly int[] _firstInfected = { 1 };
        private static readonly DateTime _time = new DateTime(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc);

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private MatchRulesModule _module;
        private long _tick;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
            _tick = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _module?.Stop();
            _transport.Dispose();
            _log.Dispose();
            _json.Dispose();
        }

        // 確認方法の筋書き: 3 人入ると 1 人が感染者になり、倒された人が感染側に移り、生存者がいなくなると感染者が勝つ
        [Test]
        public void ThreePlayersGetOneInfectedAndTheFallenTurnUntilTheInfectedWin()
        {
            Start(Settings());
            Join(1);
            Join(2);
            Join(3);
            Assert.That(TeamOf(1), Is.EqualTo(Team.Alpha));
            Assert.That(TeamOf(2), Is.EqualTo(Team.Bravo));
            Assert.That(TeamOf(3), Is.EqualTo(Team.Alpha));
            var rules = (InfectedRules)_module.Objective;
            Assert.That(_log.ToString(), Does.Contain("[Infected] net initialInfected=1 startDelay=1 roundSeconds=30 respawn=0.5"));
            Step();

            var snapshot = Match();
            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.Infected));
            Assert.That(snapshot.AlphaPoints, Is.EqualTo(3), "everyone starts as a survivor");
            Assert.That(snapshot.BravoPoints, Is.Zero);
            Assert.That(snapshot.ObjectiveRemainingSeconds, Is.EqualTo(1));
            Assert.That(snapshot.RemainingSeconds, Is.EqualTo(30), "the time comes from the Infected round, not the unreachable Session limit");
            Assert.That(Row(2).Team, Is.EqualTo(Team.Alpha), "a survivor is shown on the survivors' side whatever its match team");
            Assert.That(_module.ApplyDamage(1, 2, 100), Is.Not.EqualTo(DamageOutcome.Killed), "survivors are allies before the outbreak");
            Assert.That(_module.Session.GetHealth(2), Is.EqualTo(100));

            Advance(1.1f);

            Assert.That(rules.Infected.HasBrokenOut, Is.True);
            Assert.That(rules.Infected.FirstInfectedIds, Is.EqualTo(_firstInfected));
            Assert.That(_log.ToString(), Does.Contain(InfectedLog.Outbreak(_firstInfected)));
            snapshot = Match();
            Assert.That(snapshot.AlphaPoints, Is.EqualTo(2));
            Assert.That(snapshot.BravoPoints, Is.EqualTo(1));
            Assert.That(snapshot.ObjectiveRemainingSeconds, Is.Zero, "0 marks the outbreak");
            Assert.That(Row(1).Team, Is.EqualTo(Team.Bravo), "the first infected is shown on the infected side though it joined Alpha");
            Assert.That(_module.AreHostile(1, 3), Is.True, "the infected and a survivor of the same match team fight");
            Assert.That(_module.AreHostile(2, 3), Is.False, "survivors of different match teams are allies");

            Assert.That(_module.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));

            Assert.That(rules.Infected.IsInfected(2), Is.True);
            Assert.That(_log.ToString(), Does.Contain("infected turn player=2 by=1"));
            snapshot = Match();
            Assert.That(snapshot.AlphaPoints, Is.EqualTo(1));
            Assert.That(snapshot.BravoPoints, Is.EqualTo(2));
            Assert.That(snapshot.BravoScore, Is.EqualTo(1), "an infection scores for the infected");
            Assert.That(Row(2).Team, Is.EqualTo(Team.Bravo));
            Assert.That(_log.ToString(), Does.Contain("[Infected] score survivors=0 infected=1"));

            Assert.That(_module.ApplyDamage(1, 3, 100), Is.EqualTo(DamageOutcome.Killed), "the same match team does not shield a survivor");
            Step();

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            snapshot = Match();
            Assert.That(snapshot.HasWinner, Is.True);
            Assert.That(snapshot.Winner, Is.EqualTo(Team.Bravo), "the infected win when no survivor is left");
            Assert.That(snapshot.BravoScore, Is.EqualTo(2));
            Assert.That(rules.IsWinningPlayer(1, Team.Alpha, Team.Bravo), Is.True, "player 1 joined Alpha but won as infected");
            Assert.That(rules.IsWinningPlayer(3, Team.Alpha, Team.Bravo), Is.True);
            Assert.That(_log.ToString(), Does.Contain("[Infected] state=Ended survivors=0 infected=3 scoreSurvivors=0 scoreInfected=2"));
            Assert.That(_log.ToString(), Does.Contain(" winner=infected"));
        }

        [Test]
        public void TheSurvivorsWinWhenTheTimeRunsOut()
        {
            Start(Settings(roundSeconds: 3f));
            Join(1);
            Join(2);
            Join(3);
            Advance(1.1f);
            var rules = (InfectedRules)_module.Objective;
            Assert.That(rules.Infected.InfectedCount, Is.EqualTo(1));

            Advance(2.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            var snapshot = Match();
            Assert.That(snapshot.HasWinner, Is.True);
            Assert.That(snapshot.Winner, Is.EqualTo(Team.Alpha), "survivors are Alpha");
            Assert.That(snapshot.RemainingSeconds, Is.Zero);
            Assert.That(snapshot.AlphaScore, Is.EqualTo(InfectedSpec.DefaultScorePerSurvivedRound));
            Assert.That(rules.IsWinningPlayer(2, Team.Bravo, Team.Alpha), Is.True, "player 2 joined Bravo but survived");
            Assert.That(rules.IsWinningPlayer(1, Team.Alpha, Team.Alpha), Is.False, "player 1 joined Alpha but was infected");
            Assert.That(_log.ToString(), Does.Contain(" winner=survivor"));
        }

        [Test]
        public void ASurvivorWhoDiesWithoutAKillerAlsoTurns()
        {
            Start(Settings());
            Join(1);
            Join(2);
            Join(3);
            Advance(1.1f);
            var rules = (InfectedRules)_module.Objective;

            Assert.That(_module.ApplyDamage(null, 3, 100), Is.EqualTo(DamageOutcome.Killed));

            Assert.That(rules.Infected.IsInfected(3), Is.True);
            Assert.That(_log.ToString(), Does.Contain("infected turn player=3 by=" + InfectedLog.Nobody));
            Assert.That(Match().BravoScore, Is.Zero, "no one scores for a fall");
        }

        [Test]
        public void APlayerWhoJoinsAfterTheOutbreakIsInfected()
        {
            Start(Settings());
            Join(1);
            Join(2);
            Advance(1.1f);
            var rules = (InfectedRules)_module.Objective;

            Join(3);

            Assert.That(rules.Infected.IsInfected(3), Is.True);
            Assert.That(_log.ToString(), Does.Contain("[Infected] joined player=3 side=infected"));
            Assert.That(_log.ToString(), Does.Contain("[Infected] joined player=1 side=survivor"));
        }

        [Test]
        public void TheLastSurvivorLeavingEndsTheMatchForTheInfected()
        {
            Start(Settings());
            Join(1);
            Join(2);
            Advance(1.1f);

            Leave(2);
            Step();

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(Match().Winner, Is.EqualTo(Team.Bravo));
        }

        [Test]
        public void TheNextMatchStartsEveryoneAsASurvivorAgain()
        {
            Start(Settings());
            Join(1);
            Join(2);
            Advance(1.1f);
            Assert.That(_module.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            Step();
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));

            Advance(1.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            var rules = (InfectedRules)_module.Objective;
            Assert.That(rules.Infected.HasBrokenOut, Is.False);
            Assert.That(rules.Infected.InfectedCount, Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("[Infected] restart"));
            var snapshot = Match();
            Assert.That(snapshot.AlphaPoints, Is.EqualTo(2));
            Assert.That(snapshot.BravoScore, Is.Zero);
            Assert.That(Row(1).Team, Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void TheFirstInfectedGetsTheInfectedHealthAndSpeedAtOnce()
        {
            Start(Settings(maxHealth: 175, speedMultiplier: 1.25f));
            Join(1);
            Join(2);
            Join(3);
            Assert.That(_module.Session.GetMaxHealth(1), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(Body(1).MoveSpeedMultiplier, Is.EqualTo(1f), "everyone moves as a survivor before the outbreak");

            Advance(1.1f);

            var session = _module.Session;
            Assert.That(session.GetMaxHealth(1), Is.EqualTo(175));
            Assert.That(session.GetHealth(1), Is.EqualTo(175), "the first infected starts full, as offline respawns them");
            Assert.That(Body(1).MoveSpeedMultiplier, Is.EqualTo(1.25f));
            Assert.That(session.GetMaxHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), "survivors keep the shared cap");
            Assert.That(Body(2).MoveSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(_log.ToString(), Does.Contain("[Infected] infected body player=1 maxHealth=175 speedMultiplier=1.25"));

            Assert.That(_module.ApplyDamage(2, 1, 100), Is.EqualTo(DamageOutcome.Damaged), "the infected outlasts a hit that fells a survivor");
            Assert.That(session.GetHealth(1), Is.EqualTo(75));
        }

        [Test]
        public void AFallenSurvivorComesBackWithTheInfectedBody()
        {
            Start(Settings());
            Join(1);
            Join(2);
            Join(3);
            Advance(1.1f);

            Assert.That(_module.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));

            var session = _module.Session;
            Assert.That(session.IsAlive(2), Is.False);
            Assert.That(session.GetMaxHealth(2), Is.EqualTo(InfectedSpec.DefaultInfectedMaxHealth));
            Assert.That(Body(2).MoveSpeedMultiplier, Is.EqualTo(InfectedSpec.DefaultInfectedSpeedMultiplier));

            Advance(0.6f);

            Assert.That(session.IsAlive(2), Is.True);
            Assert.That(session.GetHealth(2), Is.EqualTo(InfectedSpec.DefaultInfectedMaxHealth));
        }

        [Test]
        public void TheNextMatchGivesEveryoneTheSurvivorBodyBack()
        {
            Start(Settings());
            Join(1);
            Join(2);
            Advance(1.1f);
            Assert.That(_module.Session.GetMaxHealth(1), Is.EqualTo(InfectedSpec.DefaultInfectedMaxHealth));
            Assert.That(_module.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            Step();
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));

            Advance(1.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            var session = _module.Session;
            foreach (var id in new[] { 1, 2 })
            {
                Assert.That(session.GetMaxHealth(id), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
                Assert.That(session.GetHealth(id), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
                Assert.That(Body(id).MoveSpeedMultiplier, Is.EqualTo(1f));
            }

            Assert.That(_log.ToString(), Does.Contain("[Infected] survivor bodies restored players=1,2"));
        }

        [Test]
        public void StoppingTheObjectiveGivesTheSpeedBack()
        {
            Start(Settings());
            Join(1);
            Join(2);
            Advance(1.1f);
            var body = Body(1);
            Assert.That(body.MoveSpeedMultiplier, Is.EqualTo(InfectedSpec.DefaultInfectedSpeedMultiplier));

            _module.Stop();
            _module = null;

            Assert.That(body.MoveSpeedMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void ThePlayerWhoJoinsInfectedGetsTheBodyAndTheWeapon()
        {
            Start(Settings(weaponId: TestRules.HandgunId));
            Join(1);
            Join(2);
            Advance(1.1f);
            var rearmed = new List<int>();
            _module.WeaponsChanged += rearmed.Add;

            Join(3);

            Assert.That(_module.Session.GetHealth(3), Is.EqualTo(InfectedSpec.DefaultInfectedMaxHealth), "joined at full health, then raised to the infected cap");
            Assert.That(Body(3).MoveSpeedMultiplier, Is.EqualTo(InfectedSpec.DefaultInfectedSpeedMultiplier));
            Assert.That(_module.WeaponsFor(3).Select(w => w.Id), Is.EqualTo(new[] { TestRules.HandgunId }));
            Assert.That(rearmed, Is.Empty, "the weapon is not swapped inside the join");

            Step();

            Assert.That(rearmed, Is.EqualTo(new[] { 3 }));
            Assert.That(_log.ToString(), Does.Contain("[Infected] rearm player=3 weapon=" + TestRules.HandgunId));
        }

        [Test]
        public void TheInfectedHoldOnlyTheInfectedWeaponAndTheFirstInfectedSwapsAtOnce()
        {
            Start(Settings(weaponId: TestRules.HandgunId));
            Join(1);
            Join(2);
            Join(3);
            Step();
            Assert.That(_module.InfectedWeapon.Id, Is.EqualTo(TestRules.HandgunId));
            Assert.That(_log.ToString(), Does.Contain("[Infected] infected weapon=" + TestRules.HandgunId + " rules=Infected"));
            Assert.That(_module.WeaponsFor(1).Count, Is.EqualTo(2), "survivors keep the loadout");
            var rearmed = new List<int>();
            _module.WeaponsChanged += rearmed.Add;

            Advance(1.1f);

            Assert.That(rearmed, Is.EqualTo(_firstInfected), "the first infected is standing, so it swaps without a respawn");
            Assert.That(_module.WeaponsFor(1).Select(w => w.Id), Is.EqualTo(new[] { TestRules.HandgunId }));
            Assert.That(_module.WeaponsFor(2).Count, Is.EqualTo(2));

            Assert.That(_module.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            Step();

            Assert.That(rearmed, Is.EqualTo(_firstInfected), "a fallen survivor gets the weapon when it respawns, not while down");
            Assert.That(_module.WeaponsFor(2).Select(w => w.Id), Is.EqualTo(new[] { TestRules.HandgunId }));
        }

        [Test]
        public void WithoutAnInfectedWeaponTheInfectedKeepTheLoadout()
        {
            Start(Settings());
            Join(1);
            Join(2);
            var rearmed = 0;
            _module.WeaponsChanged += _ => rearmed++;

            Advance(1.1f);

            Assert.That(_module.InfectedWeapon, Is.Null);
            Assert.That(((InfectedRules)_module.Objective).IsInfected(1), Is.True);
            Assert.That(_module.WeaponsFor(1).Count, Is.EqualTo(2));
            Assert.That(rearmed, Is.Zero);
        }

        [Test]
        public void AnInfectedWeaponOutsideTheLoadoutAndCatalogStopsTheStart()
        {
            var e = Assert.Throws<InvalidOperationException>(() => Start(Settings(weaponId: "claws")));

            Assert.That(e.Message, Is.EqualTo(
                "cannot start the Infected match: the infected weapon claws is not in the loadout or the catalog of rules Infected"));
            _module = null;
        }

        private static NetMatchSettings Settings(
            float roundSeconds = 30f,
            int maxHealth = InfectedSpec.DefaultInfectedMaxHealth,
            float speedMultiplier = InfectedSpec.DefaultInfectedSpeedMultiplier,
            string weaponId = InfectedSpec.DefaultInfectedWeaponId)
        {
            return new NetMatchSettings(
                restartDelaySeconds: 1f,
                infected: new InfectedSpec(
                    initialInfected: 1,
                    startDelaySeconds: 1f,
                    roundSeconds: roundSeconds,
                    infectedMaxHealth: maxHealth,
                    infectedSpeedMultiplier: speedMultiplier,
                    infectedRespawnSeconds: 0.5f,
                    infectedWeaponId: weaponId));
        }

        private ServerBody Body(int id)
        {
            Assert.That(_context.Bodies.TryGet(id, out var body), Is.True);
            return body;
        }

        private static GameRules Rules()
        {
            return GameRules.Default
                .WithActions(ActionRules.Default.WithDowned(Downed.Off).WithPing(Ping.Off))
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f)
                .WithObjective(MatchObjective.Infected);
        }

        // 最初の感染者は残っている生存者の先頭 (Id の小さい人) にする
        private void Start(NetMatchSettings settings)
        {
            var current = TestMaps.Data("NetTest", TestRules.Points());
            var events = new ServerEventWriter(true, _json, _log);
            _match = new ServerMatch(settings.RestartDelaySeconds, _log, events, () => _time);
            _match.UseMaps(current, null);
            _context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                current,
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                TestRules.Data(Rules(), settings, name: "Infected"));
            var module = new MatchRulesModule((objective, s, host) => objective == MatchObjective.Infected
                ? new InfectedRules(s, host, count => 0)
                : ServerObjectiveFactory.Create(objective, s, host));
            module.Start(_context);
            _module = module;
        }

        private void Join(int id)
        {
            _match.Join(new ConnectionId((ulong)id), "device" + id);
        }

        private void Leave(int id)
        {
            _match.Leave(new ConnectionId((ulong)id));
        }

        private Team TeamOf(int id)
        {
            Assert.That(_match.TryGetPlayer(id, out var player), Is.True);
            return player.Team;
        }

        private MatchSnapshot Match()
        {
            var snapshot = default(MatchSnapshot);
            _module.FillMatch(ref snapshot);
            return snapshot;
        }

        private PlayerSnapshot Row(int id)
        {
            var row = default(PlayerSnapshot);
            Assert.That(_module.FillPlayer(id, ref row), Is.True);
            return row;
        }

        private void Step()
        {
            _match.Tick(_tickSeconds);
            _module.Tick(++_tick, _tickSeconds);
        }

        private void Advance(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                Step();
            }
        }
    }
}
