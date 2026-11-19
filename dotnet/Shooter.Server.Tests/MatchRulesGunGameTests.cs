using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // MatchRulesModule が Gun Game の段の武器を持たせ、キルの武器を目的に渡し、段の上がった人を持ち替えさせることを確かめる
    public sealed class MatchRulesGunGameTests
    {
        private const float _tickSeconds = 1f / 60f;
        private static readonly DateTime _time = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);
        private static readonly int[] _firstPlayer = { 1 };

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
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

        [Test]
        public void EveryoneHoldsOnlyTheFirstTierWeaponAndTheChosenLoadoutIsIgnored()
        {
            Start(TestRules.RifleId, TestRules.HandgunId);
            Join(1);
            Join(2);

            Assert.That(_module.WeaponsFor(1).Select(w => w.Id), Is.EqualTo(new[] { TestRules.RifleId }));
            Assert.That(_module.WeaponsFor(2).Select(w => w.Id), Is.EqualTo(new[] { TestRules.RifleId }));
            Assert.That(_module.GunGame, Is.Not.Null);
        }

        [Test]
        public void AKillWithTheTierWeaponClimbsAndRearmsTheKillerOnTheNextTick()
        {
            Start(TestRules.RifleId, TestRules.HandgunId);
            Join(1);
            Join(2);
            Step();
            var rearmed = new System.Collections.Generic.List<int>();
            _module.WeaponsChanged += rearmed.Add;

            var outcome = _module.ApplyDamage(1, 2, NetMatchSettings.DefaultMaxHealth, new KillShot(1, 2, TestRules.RifleId, HitZone.Body));

            Assert.That(outcome, Is.EqualTo(DamageOutcome.Killed));
            Assert.That(_module.GunGame.GetTier(1), Is.EqualTo(1));
            Assert.That(rearmed, Is.Empty, "the killer is not rearmed in the middle of the kill");
            Assert.That(_module.WeaponsFor(1).Select(w => w.Id), Is.EqualTo(new[] { TestRules.HandgunId }));

            Step();

            Assert.That(rearmed, Is.EqualTo(_firstPlayer));
            Assert.That(_log.ToString(), Does.Contain("[Mode] gungame rearm player=1 weapon=" + TestRules.HandgunId));
            Step();
            Assert.That(rearmed, Is.EqualTo(_firstPlayer), "a rearm is told once");
        }

        [Test]
        public void TheSnapshotCarriesEachPlayersTier()
        {
            Start(TestRules.RifleId, TestRules.HandgunId);
            Join(1);
            Join(2);
            Step();
            _module.ApplyDamage(1, 2, NetMatchSettings.DefaultMaxHealth, new KillShot(1, 2, TestRules.RifleId, HitZone.Body));
            var snapshot = new SnapshotMessage();
            ref var first = ref snapshot.Players.Add();
            first.PlayerId = 1;
            ref var second = ref snapshot.Players.Add();
            second.PlayerId = 2;

            _module.Contribute(snapshot, null);

            Assert.That(snapshot.Match.Objective, Is.EqualTo(MatchObjective.GunGame));
            Assert.That(snapshot.Players[0].GunGameTier, Is.EqualTo(1));
            Assert.That(snapshot.Players[1].GunGameTier, Is.Zero);
        }

        [Test]
        public void ATierWeaponOutsideTheLoadoutAndCatalogStopsTheStart()
        {
            var e = Assert.Throws<InvalidOperationException>(() => Start(TestRules.RifleId, "missing-weapon"));

            Assert.That(e.Message, Is.EqualTo(
                "cannot start the GunGame match: tier 2 weapon missing-weapon is not in the loadout or the catalog of rules TeamDeathmatch"));
            _module = null;
        }

        [Test]
        public void OtherObjectivesKeepTheLoadoutAndNeverRearm()
        {
            Start(TestRules.RifleId, TestRules.HandgunId, MatchObjective.None);
            Join(1);
            Join(2);
            Step();
            var rearmed = 0;
            _module.WeaponsChanged += _ => rearmed++;

            _module.ApplyDamage(1, 2, NetMatchSettings.DefaultMaxHealth, new KillShot(1, 2, TestRules.RifleId, HitZone.Body));
            Step();

            Assert.That(_module.GunGame, Is.Null);
            Assert.That(_module.WeaponsFor(1).Count, Is.EqualTo(2), "the default loadout has both weapons");
            Assert.That(rearmed, Is.Zero);
        }

        [Test]
        public void AtTimeUpTheMatchEndsWithTheHighestTierPlayersTeamAsTheWinner()
        {
            Start(TestRules.RifleId, TestRules.HandgunId, timeLimitSeconds: 2f);
            Join(1);
            Join(2);
            Step();
            Assert.That(_module.Session.State, Is.EqualTo(GameModeState.Playing));
            _module.ApplyDamage(2, 1, NetMatchSettings.DefaultMaxHealth, new KillShot(2, 1, TestRules.RifleId, HitZone.Body));
            Assert.That(_module.GunGame.GetTier(2), Is.EqualTo(1));
            var leaderTeam = _module.Session.GetTeam(2);

            StepFor(2.1f);

            Assert.That(_module.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            var snapshot = new SnapshotMessage();
            _module.Contribute(snapshot, null);
            Assert.That(snapshot.Match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(snapshot.Match.HasWinner, Is.True);
            Assert.That(snapshot.Match.Winner, Is.EqualTo(leaderTeam));
            Assert.That(_log.ToString(), Does.Contain("[Mode] gungame ended by=time winner=2 team=" + leaderTeam));
        }

        [Test]
        public void AtTimeUpWithEveryoneOnTheSameTierTheMatchIsADraw()
        {
            Start(TestRules.RifleId, TestRules.HandgunId, timeLimitSeconds: 2f);
            Join(1);
            Join(2);
            Step();

            StepFor(2.1f);

            Assert.That(_module.Session.State, Is.EqualTo(GameModeState.Ended));
            var snapshot = new SnapshotMessage();
            _module.Contribute(snapshot, null);
            Assert.That(snapshot.Match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(snapshot.Match.HasWinner, Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Mode] gungame ended by=time winner=draw team=-"));
        }

        private void Start(
            string firstTier, string secondTier, MatchObjective objective = MatchObjective.GunGame, float timeLimitSeconds = 600f)
        {
            var settings = new NetMatchSettings(gunGame: new GunGameSpec(timeLimitSeconds, 0.5f, new[] { firstTier, secondTier }, Array.Empty<string>(), true));
            var rules = GameRules.Default
                .WithActions(ActionRules.Default.WithDowned(Downed.Off))
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f)
                .WithObjective(objective);
            _match = new ServerMatch(settings.RestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time);
            var context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                TestMaps.Data("NetTest", TestRules.Points()),
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                TestRules.Data(rules, settings));
            _module = new MatchRulesModule();
            _module.Start(context);
        }

        private void Join(int id)
        {
            _match.Join(new ConnectionId((ulong)id), "device" + id);
        }

        private void Step()
        {
            _tick++;
            _match.Tick(_tickSeconds);
            _module.Tick(_tick, _tickSeconds);
        }

        private void StepFor(float seconds)
        {
            var steps = (int)Math.Ceiling(seconds / _tickSeconds);
            for (var i = 0; i < steps; i++)
            {
                Step();
            }
        }
    }
}
