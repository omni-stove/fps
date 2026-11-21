using System;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class HordeRulesTests
    {
        private const float _tickSeconds = 1f / 60f;
        private const int _bot = BotRoster.FirstBotPlayerId;

        // 1 波目は 2 体、同時に 2 体まで。休憩 2 秒
        private static readonly HordeSettings _settings = new HordeSettings(
            startingCurrency: 30, killReward: 10, killRewardPerWave: 5, waveCount: 2, restSeconds: 2f, firstEnemyCount: 2,
            enemiesPerWave: 1f, maxAlive: 2, corpseSeconds: 1f, repairCostPerPiece: 4, buildCost: 25);

        private StringWriter _log;
        private MatchHost _host;
        private HordeRules _rules;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _host = new MatchHost(_log, TestRules.Points());
        }

        [TearDown]
        public void TearDown()
        {
            _rules?.Stop();
            _log.Dispose();
        }

        [Test]
        public void TheObjectiveDecidesTheWavesAndKeepsTheSessionFromEndingOrRespawningFirst()
        {
            var settings = new NetMatchSettings(respawnSeconds: 6f, horde: _settings);
            _rules = new HordeRules(settings, _host);

            Assert.That(_rules.Objective, Is.EqualTo(MatchObjective.Horde));
            Assert.That(_rules.PlaysInRounds, Is.False);
            Assert.That(_rules.KeepsMatchScore, Is.True);
            Assert.That(_rules.HoldsFire, Is.False);
            Assert.That(_rules.RemainingSeconds, Is.EqualTo(0f));
            Assert.That(_rules.CurrentRoundPhase(), Is.Null);
            Assert.That(_rules.FixedTeam(false), Is.EqualTo((Team?)Team.Alpha), "players defend");
            Assert.That(_rules.FixedTeam(true), Is.EqualTo((Team?)Team.Bravo), "bots are the enemies");
            Assert.That(_rules.BotsWanted, Is.EqualTo(0), "no bots before the session");
            Assert.That(_rules.Schedule.Phase, Is.EqualTo(WavePhase.Waiting));
            Assert.That(_rules.Economy.Currency, Is.EqualTo(30));
            var spec = _rules.CreateSessionSpec(GameRules.Default, settings);
            Assert.That(spec.ScoreLimit, Is.EqualTo(int.MaxValue));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(float.MaxValue));
            Assert.That(spec.RespawnSeconds, Is.EqualTo(6f));
            Assert.That(spec.RespawnsByRound, Is.True, "the defenders come back between waves, not by the session");
        }

        [Test]
        public void AMapWithoutSpawnPointsForTheEnemiesIsRefused()
        {
            _rules = new HordeRules(new NetMatchSettings(horde: _settings), _host);
            var points = new NetMapPoints(new[] { new SpawnPoint(new Vector3(0f, 1f, 0f), Team.Alpha) }, Array.Empty<ControlPointArea>());
            var map = BotTestMaps.Flat(points);

            var e = Assert.Throws<InvalidOperationException>(() => _rules.CheckMap(map, points));

            Assert.That(e.Message, Does.StartWith("cannot start the Horde match on map "));
            Assert.That(e.Message, Does.Contain("spawn points for both the defenders (Alpha) and the enemies (Bravo)"));
            Assert.DoesNotThrow(() => _rules.CheckMap(map, TestRules.Points()));
            var shared = new NetMapPoints(new[] { new SpawnPoint(new Vector3(0f, 1f, 0f), null) }, Array.Empty<ControlPointArea>());
            Assert.DoesNotThrow(() => _rules.CheckMap(map, shared), "a spawn point of no team serves both");
        }

        [Test]
        public void ADefenderStartsTheWavesAndAsksForTheirEnemies()
        {
            Begin();
            _host.Join(1, Team.Alpha);

            _host.Steps(1);

            Assert.That(_rules.Schedule.Phase, Is.EqualTo(WavePhase.Fighting));
            Assert.That(_rules.Schedule.WaveNumber, Is.EqualTo(1));
            Assert.That(_rules.BotsWanted, Is.EqualTo(2));
            Assert.That(_log.ToString(), Does.Contain("[Horde] net waves=2 rest=2.0s firstEnemies=2"));
            Assert.That(_log.ToString(), Does.Contain("[Mode] wave start number=1/2 enemies=2"));

            _host.Join(_bot, Team.Bravo);
            _host.Join(_bot + 1, Team.Bravo);

            Assert.That(_rules.Schedule.Alive, Is.EqualTo(2));
            Assert.That(_rules.BotsWanted, Is.EqualTo(2), "the two enemies are kept");
            Assert.That(_rules.BotDropOrder(_bot), Is.EqualTo(2));
            Assert.That(_log.ToString(), Does.Contain("[Horde] enemy player=" + _bot + " wave=1 alive=1 spawned=1/2"));
        }

        [Test]
        public void ABotBeyondTheWaveIsNotCountedAndIsDroppedFirst()
        {
            Begin();
            _host.Join(1, Team.Alpha);
            _host.Steps(1);
            _host.Join(_bot, Team.Bravo);
            _host.Join(_bot + 1, Team.Bravo);

            _host.Join(_bot + 2, Team.Bravo);

            Assert.That(_rules.Schedule.Spawned, Is.EqualTo(2));
            Assert.That(_rules.BotDropOrder(_bot + 2), Is.Zero);
            Assert.That(_rules.BotsWanted, Is.EqualTo(2), "the spare bot is taken out");
            Assert.That(_log.ToString(), Does.Contain("[Horde] spare player=" + (_bot + 2) + " phase=Fighting; removing it"));
        }

        [Test]
        public void AKilledEnemyPaysTheWaveRewardAndItsBodyIsDroppedAfterTheCorpseSeconds()
        {
            BeginFighting();

            _host.Session.ApplyDamage(1, _bot, 1000);

            Assert.That(_rules.Economy.Currency, Is.EqualTo(40));
            Assert.That(_rules.Schedule.Killed, Is.EqualTo(1));
            Assert.That(_rules.BotDropOrder(_bot), Is.EqualTo(1), "a body is kept for a while");
            Assert.That(_host.Scores, Is.EqualTo((1, 0)));
            Assert.That(_log.ToString(), Does.Contain("[Mode] currency +10 wave=1 total=40"));
            Assert.That(_log.ToString(), Does.Contain("[Horde] enemy killed player=" + _bot + " by=1 wave=1 remaining=1"));

            _host.Steps(61);

            Assert.That(_rules.BotDropOrder(_bot), Is.Zero, "the body is taken away");
            Assert.That(_rules.BotsWanted, Is.EqualTo(1), "one enemy still stands and the first wave has no more to send");
        }

        [Test]
        public void AnEnemyThatLeavesIsCountedAsKilledWithoutAReward()
        {
            BeginFighting();

            _host.Leave(_bot);

            Assert.That(_rules.Schedule.Killed, Is.EqualTo(1));
            Assert.That(_rules.Economy.Currency, Is.EqualTo(30));
            Assert.That(_log.ToString(), Does.Contain("[Horde] enemy left player=" + _bot + " wave=1 remaining=1; counted as killed without a reward"));
        }

        [Test]
        public void WipingOutTheLastWaveWinsAndLosingEveryDefenderLoses()
        {
            BeginFighting();
            _host.Session.ApplyDamage(1, _bot, 1000);
            _host.Session.ApplyDamage(1, _bot + 1, 1000);
            Assert.That(_rules.Schedule.Phase, Is.EqualTo(WavePhase.Resting));

            _host.Steps(130);

            Assert.That(_rules.Schedule.WaveNumber, Is.EqualTo(2));
            Assert.That(_rules.BotsWanted, Is.EqualTo(2), "the bodies are gone and the second wave sends two at a time");

            _host.Join(_bot + 2, Team.Bravo);
            _host.Session.ApplyDamage(_bot + 2, 1, 1000);
            _host.Steps(1);

            Assert.That(_host.Ended, Is.True);
            Assert.That(_host.Winner, Is.EqualTo((Team?)Team.Bravo));
            Assert.That(_log.ToString(), Does.Contain("[Mode] horde defeat wave=2/2"));
        }

        [Test]
        public void TheSnapshotCarriesTheWaveTheCurrencyAndTheRest()
        {
            BeginFighting();
            _host.Session.ApplyDamage(1, _bot, 1000);
            _host.Session.ApplyDamage(1, _bot + 1, 1000);
            _host.Steps(30);
            var snapshot = new MatchSnapshot();

            _rules.FillMatch(ref snapshot);

            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.Horde));
            Assert.That(snapshot.AlphaPoints, Is.EqualTo(50), "the currency");
            Assert.That(snapshot.BravoPoints, Is.Zero, "no enemy is left in the wave");
            Assert.That(snapshot.RoundNumber, Is.EqualTo(1));
            Assert.That(snapshot.RoundsToWin, Is.EqualTo(2));
            Assert.That(snapshot.NextWaveSeconds, Is.EqualTo(2), "1.5 seconds of rest are shown as 2");
            Assert.That(snapshot.ObjectiveRemainingSeconds, Is.Zero, "under a second has passed");
            Assert.That(snapshot.AlphaScore, Is.EqualTo(2));
            Assert.That(snapshot.BravoScore, Is.Zero);
        }

        [Test]
        public void ARestartForgetsTheWavesAndTheBots()
        {
            BeginFighting();
            _host.Session.ApplyDamage(1, _bot, 1000);

            _rules.MatchRestarted();

            Assert.That(_rules.Schedule.Phase, Is.EqualTo(WavePhase.Waiting));
            Assert.That(_rules.Economy.Currency, Is.EqualTo(30));
            Assert.That(_rules.BotDropOrder(_bot), Is.Zero);
            Assert.That(_rules.BotDropOrder(_bot + 1), Is.Zero, "a standing enemy of the last match is not counted");
            Assert.That(_host.Scores, Is.EqualTo((0, 0)));
            Assert.That(_log.ToString(), Does.Contain("[Horde] restart"));
        }

        [Test]
        public void FortifyingPaysBetweenWavesOnly()
        {
            var fortifications = new FakeFortifications { Target = new HordeFortificationTarget(3, "Wall-North", HordeFortificationAction.Repair, 2) };
            BeginFighting(fortifications);

            Assert.That(_rules.Fortify(1), Is.EqualTo(HordePurchaseVerdict.WaveInProgress));
            Assert.That(fortifications.Restored, Is.Zero);

            _host.Session.ApplyDamage(1, _bot, 1000);
            _host.Session.ApplyDamage(1, _bot + 1, 1000);

            Assert.That(_rules.Fortify(1), Is.EqualTo(HordePurchaseVerdict.Ok));
            Assert.That(fortifications.Restored, Is.EqualTo(1));
            Assert.That(fortifications.LastRadius, Is.EqualTo(HordeSpec.DefaultInteractRadius));
            Assert.That(_rules.Economy.Currency, Is.EqualTo(50 - 8), "two pieces at four each");
            Assert.That(_log.ToString(), Does.Contain("[Mode] repair structure=Wall-North pieces=2 cost=8 currency=42"));
            Assert.That(_rules.Fortify(_bot), Is.EqualTo(HordePurchaseVerdict.NothingToDo), "bots do not buy");

            fortifications.Target = new HordeFortificationTarget(4, "Barricade-NE", HordeFortificationAction.Build, 2);
            Assert.That(_rules.Fortify(1), Is.EqualTo(HordePurchaseVerdict.Ok));
            Assert.That(_rules.Economy.Currency, Is.EqualTo(42 - 25));

            Assert.That(_rules.Fortify(1), Is.EqualTo(HordePurchaseVerdict.NotEnoughCurrency));
            Assert.That(_log.ToString(), Does.Contain("[Mode] purchase-denied action=build structure=Barricade-NE reason=NotEnoughCurrency cost=25 currency=17"));

            fortifications.Target = null;
            Assert.That(_rules.Fortify(1), Is.EqualTo(HordePurchaseVerdict.NothingToDo));
        }

        [Test]
        public void WithoutFortificationsNothingCanBeFortified()
        {
            Begin();
            _host.Join(1, Team.Alpha);

            Assert.That(_rules.Fortify(1), Is.EqualTo(HordePurchaseVerdict.NothingToDo));
            Assert.That(_rules.Fortify(99), Is.EqualTo(HordePurchaseVerdict.NothingToDo), "not in the match");
            Assert.That(_rules.Offer(1), Is.Null);
        }

        [Test]
        public void TheOfferShowsTheCostAndWhetherItCanBePaidNow()
        {
            var fortifications = new FakeFortifications { Target = new HordeFortificationTarget(3, "Wall-North", HordeFortificationAction.Repair, 2) };
            BeginFighting(fortifications);

            var during = _rules.Offer(1);

            Assert.That(during.HasValue, Is.True);
            Assert.That(during.Value.FortificationId, Is.EqualTo(3));
            Assert.That(during.Value.NameKey, Is.EqualTo("Wall-North"));
            Assert.That(during.Value.Action, Is.EqualTo(HordeFortificationAction.Repair));
            Assert.That(during.Value.Cost, Is.EqualTo(8), "two pieces at four each");
            Assert.That(during.Value.Verdict, Is.EqualTo(HordePurchaseVerdict.WaveInProgress));
            Assert.That(_rules.Offer(_bot), Is.Null, "bots are not offered anything");
            Assert.That(_rules.Offer(99), Is.Null, "not in the match");

            _host.Session.ApplyDamage(1, _bot, 1000);
            _host.Session.ApplyDamage(1, _bot + 1, 1000);
            Assert.That(_rules.Offer(1).Value.Verdict, Is.EqualTo(HordePurchaseVerdict.Ok));

            fortifications.Target = new HordeFortificationTarget(4, "Barricade-NE", HordeFortificationAction.Build, 2);
            var build = _rules.Offer(1).Value;
            Assert.That(build.Cost, Is.EqualTo(25));
            Assert.That(build.Verdict, Is.EqualTo(HordePurchaseVerdict.Ok), "50 is enough");

            fortifications.Target = new HordeFortificationTarget(4, "Barricade-NE", HordeFortificationAction.Build, 0);
            Assert.That(_rules.Offer(1).Value.Verdict, Is.EqualTo(HordePurchaseVerdict.NothingToDo), "everything left is blocked by bodies");

            fortifications.Target = null;
            Assert.That(_rules.Offer(1), Is.Null);
        }

        [Test]
        public void TheOfferIsNotMadeToTheFallen()
        {
            var fortifications = new FakeFortifications { Target = new HordeFortificationTarget(3, "Wall-North", HordeFortificationAction.Repair, 2) };
            BeginFighting(fortifications);

            _host.Session.ApplyDamage(_bot, 1, 1000);

            Assert.That(_rules.Offer(1), Is.Null);
            Assert.That(_rules.Fortify(1), Is.EqualTo(HordePurchaseVerdict.NothingToDo));
        }

        [Test]
        public void TheOfferIsWrittenOnlyForItsReceiver()
        {
            var fortifications = new FakeFortifications { Target = new HordeFortificationTarget(3, "Wall-North", HordeFortificationAction.Repair, 2) };
            BeginFighting(fortifications);
            var snapshot = new SnapshotMessage();
            snapshot.Clear();

            _rules.Contribute(snapshot, new ServerPlayer(new ConnectionId(1), 1, Team.Alpha, "k1"));
            Assert.That(snapshot.Owner.HordeOffer.Value.FortificationId, Is.EqualTo(3));

            _rules.Contribute(snapshot, new ServerPlayer(new ConnectionId(2), _bot, Team.Bravo, "k2"));
            Assert.That(snapshot.Owner.HordeOffer, Is.Null, "a stale offer is not left for the next receiver");

            snapshot.Owner.HordeOffer = _rules.Offer(1);
            _rules.Contribute(snapshot, null);
            Assert.That(snapshot.Owner.HordeOffer, Is.Null);
        }

        [Test]
        public void TheFortificationsAreReadiedOnStartRestartAndEveryTick()
        {
            var fortifications = new FakeFortifications();
            Begin(fortifications);

            Assert.That(fortifications.Prepared, Is.EqualTo(1), "on start");
            _host.Steps(3);
            Assert.That(fortifications.Ticks, Is.EqualTo(3));

            _rules.MatchRestarted();
            Assert.That(fortifications.Prepared, Is.EqualTo(2), "a new match starts from the map's fortifications");

            _rules.UseMap(BotTestMaps.Flat(TestRules.Points()), TestRules.Points());
            Assert.That(fortifications.Prepared, Is.EqualTo(3), "so does a new map");
        }

        [Test]
        public void HordeWinDecidesTheSameWayOnlineAndOffline()
        {
            Assert.That(HordeWin.Decide(WavePhase.Completed, true, Side.Alpha, Side.Bravo), Is.EqualTo((Side?)Side.Alpha),
                "the last enemy and the last defender falling together is a win");
            Assert.That(HordeWin.Decide(WavePhase.Fighting, true, Side.Alpha, Side.Bravo), Is.EqualTo((Side?)Side.Bravo));
            Assert.That(HordeWin.Decide(WavePhase.Resting, false, Side.Alpha, Side.Bravo), Is.Null);
            Assert.Throws<ArgumentException>(() => HordeWin.Decide(WavePhase.Fighting, false, Side.Alpha, Side.Alpha));
        }

        private void Begin(IHordeFortifications fortifications = null)
        {
            var settings = new NetMatchSettings(horde: _settings);
            _rules = new HordeRules(settings, _host, fortifications);
            var actions = ActionRules.Default.WithDowned(Downed.Off);
            _host.Begin(_rules, GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f), settings);
            _rules.Start();
        }

        // 守る側 1 人と、1 波目の敵 2 体が立っている
        private void BeginFighting(IHordeFortifications fortifications = null)
        {
            Begin(fortifications);
            _host.Join(1, Team.Alpha);
            _host.Steps(1);
            _host.Join(_bot, Team.Bravo);
            _host.Join(_bot + 1, Team.Bravo);
        }

        private sealed class FakeFortifications : IHordeFortifications
        {
            public HordeFortificationTarget? Target { get; set; }

            public int Restored { get; private set; }

            public float LastRadius { get; private set; }

            public int Prepared { get; private set; }

            public int Ticks { get; private set; }

            public void Prepare()
            {
                Prepared++;
            }

            public void Tick()
            {
                Ticks++;
            }

            public HordeFortificationTarget? FindInReach(Vector3 position, float radius)
            {
                LastRadius = radius;
                return Target;
            }

            public int Restore(int fortificationId, HordeFortificationAction action)
            {
                Restored++;
                return Target.HasValue && Target.Value.Id == fortificationId ? Target.Value.RestorablePieces : 0;
            }
        }

        private sealed class MatchHost : IMatchObjectiveHost
        {
            private readonly NetMapPoints _points;
            private HordeRules _rules;
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

            public (int Alpha, int Bravo) Scores { get; private set; }

            public void Begin(HordeRules rules, GameRules gameRules, NetMatchSettings settings)
            {
                _rules = rules;
                Session = new TeamDeathmatchSession(
                    rules.CreateSessionSpec(gameRules, settings),
                    settings.MaxHealth,
                    _points.SpawnPoints,
                    new SpawnSelector(settings.MinEnemySpawnDistance),
                    PositionOf,
                    null,
                    settings.ReconnectGraceSeconds);
                Session.Spawned += (playerId, point) => Bodies.Teleport(playerId, point.Position, 0f, _tick);
                Session.Killed += (killerId, victimId, _) => _rules.PlayerKilled(killerId, victimId);
                Session.Restarted += () => _rules.MatchRestarted();
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
                Session.Join(playerId, BotRoster.IsBotId(playerId) ? null : "device" + playerId, team);
                _rules.PlayerJoined(playerId);
            }

            public void Leave(int playerId)
            {
                _rules.PlayerLeft(playerId);
                Session.Leave(playerId);
                Bodies.Remove(playerId);
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
                Scores = (alpha, bravo);
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
