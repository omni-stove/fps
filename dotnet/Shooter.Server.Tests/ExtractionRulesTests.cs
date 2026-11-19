using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;
using RaidOutcome = Shooter.Core.ExtractionOutcome;
using StashOutcome = Shooter.Protocol.ExtractionOutcome;

namespace Shooter.Server.Tests
{
    // 移動・ルール・戦闘・持ち物・箱と Extraction の目的を同じホストで動かし、持ち込み・拾う・脱出・倒れる・持ち物庫のファイルを確かめる
    public sealed class ExtractionRulesTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const int _seed = 1419;
        private const float _holdSeconds = 1f;
        private const string _herbId = "0d3c6f1a-5b2e-4c7d-8a9f-1e2b3c4d5e6f";
        private const string _rifleItemId = "4d5e6f7a-8b9c-4d0e-9f2a-3b4c5d6e7f8a";
        private const string _handgunItemId = "5e6f7a8b-9c0d-4e1f-8a3b-4c5d6e7f8a9b";
        private const string _shotgunItemId = "8b9c0d1e-2f3a-4b4c-9d6e-7f8a9b0c1d2e";
        private const string _shotgunId = "9c0d1e2f-3a4b-4c5d-8e7f-8a9b0c1d2e3f";
        private const string _supplies = "supplies";
        private const string _armory = "armory";
        private const string _north = "north";
        private const string _south = "south";
        private const int _suppliesBox = ServerLoot.FirstItemId;
        private const int _armoryBox = ServerLoot.FirstItemId + 1;
        private static readonly DateTime _time = new DateTime(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Vector3 _alphaSpawn = new Vector3(-5f, 1.6f, 0f);
        private static readonly Vector3 _bravoSpawn = new Vector3(5f, 1.6f, 0f);
        private static readonly Vector3 _suppliesAt = new Vector3(-5f, 0f, 10f);
        private static readonly Vector3 _armoryAt = new Vector3(5f, 0f, 10f);
        private static readonly Vector3 _northAt = new Vector3(0f, 0f, 25f);
        private static readonly Vector3 _southAt = new Vector3(0f, 0f, -25f);
        private static readonly int[] _pointIds = { 0, 1 };
        private static readonly string[] _device2Files = { "device2.json" };
        private static readonly Vector3 _standing = new Vector3(0f, 1.6f, 0f);
        private static readonly WeaponCategory[] _firstSlot = { WeaponCategory.AssaultRifle, WeaponCategory.Handgun, WeaponCategory.Shotgun };
        private static readonly WeaponCategory[] _secondSlot = { WeaponCategory.Handgun, WeaponCategory.AssaultRifle, WeaponCategory.Shotgun };

        private StringWriter _log;
        private StringWriter _json;
        private TempDirectory _directory;
        private RecordingTransport _transport;
        private ServerStashStore _store;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private MatchRulesModule _rules;
        private ServerCombat _combat;
        private ServerInventories _inventories;
        private ServerLoot _loot;
        private ExtractionRules _extraction;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _directory = new TempDirectory();
            _transport = new RecordingTransport();
            Assert.That(ServerStashStore.TryOpen(Path.Combine(_directory.Path, "stashes"), _log, out _store, out var error), Is.True, error);
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
            _json.Dispose();
            _directory.Dispose();
        }

        [Test]
        public void BringingInTakesTheChosenWeaponsThatAreInTheStashOutOfIt()
        {
            Start();
            Join(1);
            Join(2);

            var raid = _extraction.RaidOf(1);
            Assert.That(raid, Is.Not.Null, _log.ToString());
            Assert.That(raid.Outcome, Is.EqualTo(RaidOutcome.Raiding));
            Assert.That(raid.BroughtIn, Is.EqualTo(new[] { new ItemCount(_rifleItemId, 1) }));
            Assert.That(_extraction.StashOf(1).CountOf(_rifleItemId), Is.Zero, "the rifle left the stash");
            Assert.That(_combat.WeaponsOf(1).Specs.Select(s => s.Id), Is.EqualTo(new[] { TestRules.RifleId }), "the handgun is not in the stash");
            Assert.That(SavedStacks("device1"), Is.Empty, "the stash file no longer holds the rifle");

            var path = _store.PathOf("device1");
            var log = _log.ToString();
            Assert.That(log, Does.Contain("[Stash] load player=1 account=device1 stacks=1 new=" + path));
            Assert.That(log, Does.Contain("[Extraction] player=1 weapon=" + TestRules.HandgunId + " is not in the stash; left behind"));
            Assert.That(log, Does.Contain("[Extraction] started points=" + _north + " raid=120.0s open-after=0.0s hold=1.0s brought=" + _rifleItemId + "x1 player=1")
                .Or.Contain("[Extraction] started points=" + _south + " raid=120.0s open-after=0.0s hold=1.0s brought=" + _rifleItemId + "x1 player=1"));
            Assert.That(log, Does.Contain("[Stash] saved player=1 account=device1 stacks=0 to=" + path));

            var state = States(1).Last();
            Assert.That(state.Outcome, Is.EqualTo(StashOutcome.None));
            Assert.That(state.Stash.Count, Is.Zero);
            Assert.That(Items(state.BroughtIn), Is.EqualTo(new[] { (_rifleItemId, 1) }));
            Assert.That(Items(state.Carried), Is.EqualTo(new[] { (_rifleItemId, 1) }));
        }

        [Test]
        public void AReturningAccountBringsInFromItsSavedStash()
        {
            Save("device1", new StashEntry(_handgunItemId, 1), new StashEntry(_herbId, 3));
            Start();
            Join(1);
            Join(2);

            Assert.That(_extraction.RaidOf(1).BroughtIn, Is.EqualTo(new[] { new ItemCount(_handgunItemId, 1) }));
            Assert.That(_combat.WeaponsOf(1).Specs.Select(s => s.Id), Is.EqualTo(new[] { TestRules.HandgunId }), "the starting rifle is only for a new stash");
            Assert.That(SavedStacks("device1"), Is.EqualTo(new[] { (_herbId, 3) }), "what is not brought in stays in the stash");
            Assert.That(_log.ToString(), Does.Contain("[Stash] load player=1 account=device1 stacks=2 from=" + _store.PathOf("device1")));
        }

        [Test]
        public void TheNextRaidBringsInTheWeaponsChosenWithALoadoutRequest()
        {
            Save("device1", new StashEntry(_shotgunItemId, 1), new StashEntry(_handgunItemId, 1), new StashEntry(_herbId, 3));
            Start();
            Join(1);
            Join(2);
            Step(0.2f);
            var request = new LoadoutRequestMessage();
            request.WeaponIds.Add(_shotgunId);
            request.WeaponIds.Add(TestRules.HandgunId);
            _host.OnMessage(new ConnectionId(1), request);

            Assert.That(_extraction.RaidOf(1).BroughtIn, Is.EqualTo(new[] { new ItemCount(_handgunItemId, 1) }), "the choice waits for the next raid");
            _rules.ApplyDamage(1, 2, 1000);
            Extract(1);
            Assert.That(_rules.Session.State, Is.EqualTo(GameModeState.Ended), _log.ToString());
            Step(NetMatchSettings.DefaultRestartDelaySeconds + 0.5f);

            var raid = _extraction.RaidOf(1);
            Assert.That(raid.Outcome, Is.EqualTo(RaidOutcome.Raiding), _log.ToString());
            Assert.That(raid.BroughtIn, Is.EqualTo(new[] { new ItemCount(_shotgunItemId, 1), new ItemCount(_handgunItemId, 1) }));
            Assert.That(_combat.WeaponsOf(1).Specs.Select(s => s.Id), Is.EqualTo(new[] { _shotgunId, TestRules.HandgunId }));
            Assert.That(SavedStacks("device1"), Is.EqualTo(new[] { (_herbId, 3) }));
        }

        [Test]
        public void ExtractingPutsWhatWasCarriedIntoTheStashAndSavesIt()
        {
            Start();
            Join(1);
            Join(2);
            Step(0.2f);
            var herbs = OpenSupplies(1);
            var raid = _extraction.RaidOf(1);
            Assert.That(raid.Carried, Is.EquivalentTo(new[] { new ItemCount(_rifleItemId, 1), new ItemCount(_herbId, herbs) }), _log.ToString());
            Assert.That(_inventories.InventoryOf(1).CountOf(_herbId), Is.EqualTo(herbs + 2), "the two starting herbs are in the inventory too");

            Extract(1);

            Assert.That(raid.Outcome, Is.EqualTo(RaidOutcome.Extracted), _log.ToString());
            var stash = _extraction.StashOf(1);
            Assert.That(stash.CountOf(_herbId), Is.EqualTo(herbs), "the starting herbs were not picked in the raid, so they stay behind");
            Assert.That(stash.CountOf(_rifleItemId), Is.EqualTo(1));
            Assert.That(SavedCount("device1", _herbId), Is.EqualTo(herbs));
            Assert.That(SavedCount("device1", _rifleItemId), Is.EqualTo(1));
            Assert.That(_combat.WeaponsOf(1).Items, Is.Empty, "the player leaves the raid with nothing in hand");
            Assert.That(_inventories.InventoryOf(1).CountOf(_herbId), Is.Zero);
            Assert.That(_extraction.RaidOf(2).Outcome, Is.EqualTo(RaidOutcome.Raiding), "the other raid goes on");
            Assert.That(_rules.Session.State, Is.EqualTo(GameModeState.Playing));

            var log = _log.ToString();
            Assert.That(log, Does.Contain("[Extraction] ended outcome=Extracted"));
            Assert.That(log, Does.Contain(" player=1 account=device1"));
            Assert.That(log, Does.Contain("[Loot] stow owner=1 weapons=1 items=" + (herbs + 2)));
            Assert.That(log, Does.Contain("[Stash] saved player=1 account=device1 stacks="));

            var state = States(1).Last();
            Assert.That(state.Outcome, Is.EqualTo(StashOutcome.Extracted));
            Assert.That(Items(state.Stash), Is.EquivalentTo(new[] { (_rifleItemId, 1), (_herbId, herbs) }));
            Assert.That(state.Carried.Count, Is.Zero);
            Assert.That(_extraction.IsWinningPlayer(1, Team.Alpha, null), Is.True);
            Assert.That(_extraction.Interact(1, _armoryBox), Is.EqualTo(InteractVerdict.Refused(InteractRefusal.NotInteractable)), "the raid is over");
        }

        [Test]
        public void DyingLosesWhatWasCarriedIntoADeathBox()
        {
            Start();
            Join(1);
            Join(2);
            Step(0.2f);
            var herbs = OpenSupplies(1);

            _rules.ApplyDamage(2, 1, 1000);

            var raid = _extraction.RaidOf(1);
            Assert.That(raid.Outcome, Is.EqualTo(RaidOutcome.Died), _log.ToString());
            Assert.That(raid.Stored, Is.Empty);
            Assert.That(raid.Lost, Is.EquivalentTo(new[] { new ItemCount(_rifleItemId, 1), new ItemCount(_herbId, herbs) }));
            Assert.That(_extraction.StashOf(1).Stacks, Is.Empty);
            Assert.That(SavedStacks("device1"), Is.Empty);

            var deathBox = ServerLoot.FirstItemId + 2;
            Assert.That(_loot.TryGetContents(deathBox, out var weapons, out var items), Is.True, _log.ToString());
            Assert.That(weapons.Select(w => w.Id), Is.EqualTo(new[] { TestRules.RifleId }));
            Assert.That(items, Is.EqualTo(new[] { new ItemCount(_herbId, herbs + 2) }));
            Assert.That(_log.ToString(), Does.Contain("[Extraction] ended outcome=Died"));
            Assert.That(States(1).Last().Outcome, Is.EqualTo(StashOutcome.Killed));
            Assert.That(_extraction.IsWinningPlayer(1, Team.Alpha, null), Is.False);

            // 箱は倒れた所 (補給の箱のそば) に残り、開けた人の出撃が持ち帰る物に数える
            MoveTo(2, _suppliesAt + new Vector3(0.5f, 1.6f, 1f));
            Assert.That(_loot.Open(2, deathBox).IsAccepted, Is.True, _log.ToString());
            Assert.That(_extraction.RaidOf(2).Carried, Does.Contain(new ItemCount(_herbId, herbs + 2)));
        }

        [Test]
        public void AClosedPointDoesNotExtract()
        {
            Start();
            Join(1);
            Join(2);
            Step(0.2f);
            var open = _extraction.OpenPointIds.Single();
            var closed = open == _north ? _south : _north;

            MoveTo(1, CenterOf(closed) + _standing);
            Step(_holdSeconds * 3f);

            var raid = _extraction.RaidOf(1);
            Assert.That(raid.Outcome, Is.EqualTo(RaidOutcome.Raiding));
            Assert.That(raid.HoldingPointId, Is.Null);
            var snapshot = Contribute(1);
            Assert.That(snapshot.Owner.HasExtractionPoint, Is.False);
            var rows = Rows(snapshot).Where(r => r.Kind == ObjectiveItemKind.ExtractionPoint).ToList();
            Assert.That(rows.Select(r => r.ItemId), Is.EqualTo(_pointIds), "the points are numbered in id order (north, south)");
            Assert.That(rows.Single(r => r.ItemId == IndexOf(closed)).Opened, Is.False);
            Assert.That(rows.Single(r => r.ItemId == IndexOf(open)).Opened, Is.True);
            Assert.That(rows[0].Position, Is.EqualTo(_northAt));
            Assert.That(rows[0].Radius, Is.EqualTo(3f));

            MoveTo(1, CenterOf(open) + _standing);
            Step(_holdSeconds / 2f);

            snapshot = Contribute(1);
            Assert.That(raid.HoldingPointId, Is.EqualTo(open));
            Assert.That(snapshot.Owner.HasExtractionPoint, Is.True);
            Assert.That(snapshot.Owner.ExtractionPointId, Is.EqualTo(IndexOf(open)));
            Assert.That(snapshot.Owner.ExtractionProgress, Is.InRange(0.3f, 0.7f));
            Assert.That(Contribute(2).Owner.HasExtractionPoint, Is.False, "the wait is shown to its owner only");
        }

        [Test]
        public void TheMatchEndsWhenEveryRaidIsOverAndTheNextStartsFromTheStash()
        {
            Start();
            Join(1);
            Join(2);
            Step(0.2f);
            _rules.ApplyDamage(1, 2, 1000);
            Assert.That(_rules.Session.State, Is.EqualTo(GameModeState.Playing), "one raid is still going");

            Extract(1);

            Assert.That(_rules.Session.State, Is.EqualTo(GameModeState.Ended), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[Extraction] every raid of the match is over raids=2"));
            Assert.That(_rules.Session.IsAlive(2), Is.False, "the fallen player is not brought back during the match");

            Step(NetMatchSettings.DefaultRestartDelaySeconds + 0.5f);

            Assert.That(_rules.Session.State, Is.EqualTo(GameModeState.Playing), _log.ToString());
            Assert.That(_extraction.ElapsedSeconds, Is.LessThan(1f));
            Assert.That(_extraction.RaidOf(1).Outcome, Is.EqualTo(RaidOutcome.Raiding));
            Assert.That(_extraction.RaidOf(1).BroughtIn, Is.EqualTo(new[] { new ItemCount(_rifleItemId, 1) }), "the rifle carried home goes out again");
            Assert.That(_extraction.RaidOf(2).BroughtIn, Is.Empty, "the rifle lost in the last raid is gone");
            Assert.That(_combat.WeaponsOf(2).Specs, Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Extraction] restart open="));
        }

        [Test]
        public void RunningOutOfRaidTimeEndsTheMatchAndLosesWhatWasCarried()
        {
            Start(new ExtractionSpec(raidSeconds: 3f, extractHoldSeconds: _holdSeconds, openPoints: 1, openAfterSeconds: 0f));
            Join(1);
            Join(2);

            Step(3.5f);

            Assert.That(_rules.Session.State, Is.EqualTo(GameModeState.Ended), _log.ToString());
            Assert.That(_extraction.RaidOf(1).Outcome, Is.EqualTo(RaidOutcome.TimedOut));
            Assert.That(_extraction.RaidOf(1).Lost, Is.EqualTo(new[] { new ItemCount(_rifleItemId, 1) }));
            Assert.That(SavedStacks("device1"), Is.Empty);
            Assert.That(_extraction.RemainingSeconds, Is.EqualTo(0f));
        }

        [Test]
        public void PointsOpenOnlyAfterTheOpeningTime()
        {
            Start(new ExtractionSpec(raidSeconds: 60f, extractHoldSeconds: _holdSeconds, openPoints: 2, openAfterSeconds: 5f));
            Join(1);
            Join(2);
            MoveTo(1, _northAt + _standing);

            Step(3f);
            Assert.That(_extraction.RaidOf(1).Outcome, Is.EqualTo(RaidOutcome.Raiding));
            Assert.That(Rows(Contribute(1)).Where(r => r.Kind == ObjectiveItemKind.ExtractionPoint).Select(r => r.Opened), Is.All.False);

            Step(5f);
            Assert.That(_extraction.RaidOf(1).Outcome, Is.EqualTo(RaidOutcome.Extracted), _log.ToString());
        }

        [Test]
        public void BotsAreSentToTheNearestBoxAndThenToAnOpenPoint()
        {
            Start();
            Join(1);
            Join(2);
            Step(0.2f);
            MoveTo(1, new Vector3(-5f, 1.6f, 2f));

            Assert.That(_extraction.BotGoal(1), Is.EqualTo(_suppliesAt));
            MoveTo(1, new Vector3(4f, 1.6f, 9f));
            Assert.That(_extraction.BotGoal(1), Is.EqualTo(_suppliesAt), "the same box stays the goal while it is closed");

            OpenSupplies(1);

            Assert.That(_extraction.BotGoal(1), Is.EqualTo(CenterOf(_extraction.OpenPointIds.Single())));
            Assert.That(_extraction.BotGoal(99), Is.Null, "a player who is not in the match has no goal");
        }

        [Test]
        public void StoppingCallsOffTheRaidAndReturnsWhatWasBroughtIn()
        {
            Start();
            Join(1);
            Join(2);
            Assert.That(SavedStacks("device1"), Is.Empty);

            _host.Stop();

            Assert.That(SavedStacks("device1"), Is.EqualTo(new[] { (_rifleItemId, 1) }));
            Assert.That(_log.ToString(), Does.Contain("[Extraction] raid player=1 called off returned=" + _rifleItemId + "x1"));
        }

        [Test]
        public void LeavingInTheRaidLosesWhatWasCarried()
        {
            Start();
            Join(1);
            Join(2);
            Step(0.2f);

            _match.Leave(new ConnectionId(1));

            Assert.That(SavedStacks("device1"), Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Extraction] ended outcome=Died"));
            Assert.That(_loot.TryGetContents(ServerLoot.FirstItemId + 2, out var weapons, out _), Is.True, _log.ToString());
            Assert.That(weapons.Select(w => w.Id), Is.EqualTo(new[] { TestRules.RifleId }));
        }

        [Test]
        public void DyingWithKeepCarriedOnDeathPutsWhatWasCarriedInTheStashAndLeavesNoDeathBox()
        {
            Start(new ExtractionSpec(raidSeconds: 120f, extractHoldSeconds: _holdSeconds, openPoints: 1, openAfterSeconds: 0f, keepCarriedOnDeath: true));
            Join(1);
            Join(2);
            Step(0.2f);

            _rules.ApplyDamage(2, 1, 1000);

            Assert.That(_extraction.StashOf(1).CountOf(_rifleItemId), Is.EqualTo(1), _log.ToString());
            Assert.That(_loot.TryGetContents(ServerLoot.FirstItemId + 2, out _, out _), Is.False, "the carried items are not left in a death box as well");
        }

        [Test]
        public void TheSessionLetsEveryoneHurtEveryoneAndDoesNotBringTheFallenBack()
        {
            Start();
            var spec = _extraction.CreateSessionSpec(GameRules.Default.WithFriendlyFire(FriendlyFire.Off), NetMatchSettings.Default);

            Assert.That(spec.Rules.FriendlyFire, Is.EqualTo(FriendlyFire.On));
            Assert.That(spec.Rules.Actions.Downed, Is.EqualTo(Downed.Off));
            Assert.That(spec.RespawnsByRound, Is.True);
            Assert.That(_extraction.AreHostile(1, Team.Alpha, 2, Team.Alpha), Is.True);
            Assert.That(_extraction.AreHostile(1, Team.Alpha, 1, Team.Alpha), Is.False);
            Assert.That(_extraction.DecideWinner(Team.Alpha), Is.Null);
        }

        [Test]
        public void AMapWithoutExtractionPointsIsRefused()
        {
            var rules = new MatchRulesModule();
            var combat = new ServerCombat(rules, new ServerMovement(ServerMotorSettings.Default));
            var loot = new ServerLoot(rules, combat, new ServerInventories(rules, combat));
            var extraction = new ExtractionRules(rules, rules, loot, _store);
            var map = TestMaps.Data("NetTest", TestRules.Points());

            var e = Assert.Throws<InvalidOperationException>(() => extraction.UseMap(map, map.Points));
            Assert.That(e.Message, Does.StartWith("cannot start the Extraction match on map NetTest: it has no extraction points"));
            Assert.Throws<InvalidOperationException>(() => extraction.CheckMap(map, map.Points));
            Assert.Throws<InvalidOperationException>(() => extraction.Start(), "Start needs a map");
        }

        [Test]
        public void TheRulesNeedAStashStore()
        {
            var rules = new MatchRulesModule();
            var combat = new ServerCombat(rules, new ServerMovement(ServerMotorSettings.Default));
            var loot = new ServerLoot(rules, combat, new ServerInventories(rules, combat));

            var e = Assert.Throws<ArgumentNullException>(() => new ExtractionRules(rules, rules, loot, null));
            Assert.That(e.Message, Does.Contain(ServerLaunch.StashDirectoryFlag));
            Assert.Throws<ArgumentNullException>(() => new ExtractionRules(null, rules, loot, _store));
            Assert.Throws<ArgumentNullException>(() => new ExtractionRules(rules, null, loot, _store));
            Assert.Throws<ArgumentNullException>(() => new ExtractionRules(rules, rules, null, _store));
        }

        [Test]
        public void ABotKeepsItsOwnStashFile()
        {
            Start();
            Join(1);
            var bot = BotRoster.FirstBotPlayerId;
            _match.JoinBot(bot);

            var account = ExtractionRules.BotAccountPrefix + bot.ToString(CultureInfo.InvariantCulture);
            Assert.That(_extraction.AccountOf(bot), Is.EqualTo(account));
            Assert.That(_extraction.RaidOf(bot).BroughtIn, Is.EqualTo(new[] { new ItemCount(_rifleItemId, 1) }));
            Assert.That(File.Exists(_store.PathOf(account)), Is.True, _log.ToString());
        }

        [Test]
        public void APlayerWithoutAKeyPlaysWithAStashThatIsNotSaved()
        {
            Start();
            Join(2);
            _match.Join(new ConnectionId(1), "");

            Assert.That(_extraction.AccountOf(1), Is.Null);
            Assert.That(_extraction.RaidOf(1).BroughtIn, Is.EqualTo(new[] { new ItemCount(_rifleItemId, 1) }));
            Assert.That(_log.ToString(), Does.Contain("[Stash] player=1 has no account key; the stash starts from the starting items and is not saved"));
            Assert.That(Directory.GetFiles(_store.Directory).Select(f => Path.GetFileName(f)), Is.EqualTo(_device2Files));
        }

        [Test]
        public void TheExampleRulesFilePlaysExtraction()
        {
            Assert.That(ServerRulesData.TryLoad("Extraction", RepositoryRoots(), out var data, out var error), Is.True, error);

            Assert.That(data.Rules.Objective, Is.EqualTo(MatchObjective.Extraction));
            Assert.That(data.File.Name, Is.EqualTo("Extraction"));
            Assert.That(data.Rules.FriendlyFire, Is.EqualTo(FriendlyFire.On), "every raider is an enemy");
            Assert.That(data.Rules.Actions.Melee, Is.EqualTo(Melee.On));
            Assert.That(data.ExtractionPath, Does.EndWith(ServerRulesData.ExtractionTuningFileName));
            Assert.That(data.StashPath, Does.EndWith(ServerRulesData.StashTuningFileName));
        }

        [Test]
        public void TheLaunchFlagTakesADirectory()
        {
            Assert.That(ServerLaunch.TryParse(Args("--stash-dir", "/var/stash"), _ => null, out var launch, out var error), Is.True, error);
            Assert.That(launch.StashDirectory, Is.EqualTo("/var/stash"));
            Assert.That(launch.StashProblem(new[] { MatchObjective.Extraction }), Is.Null);
            Assert.That(launch.WithOptions(launch.Options).StashDirectory, Is.EqualTo("/var/stash"));

            Assert.That(ServerLaunch.TryParse(Args("--stash-dir"), _ => null, out _, out error), Is.False);
            Assert.That(error, Is.EqualTo("--stash-dir requires a directory"));
            Assert.That(ServerLaunch.TryParse(Args("--stash-dir", "--private"), _ => null, out _, out error), Is.False);
            Assert.That(error, Is.EqualTo("--stash-dir requires a directory"));
            Assert.That(ServerLaunch.TryParse(Args("--stash-dir", "a", "--stash-dir", "b"), _ => null, out _, out error), Is.False);
            Assert.That(error, Is.EqualTo("--stash-dir is given more than once"));

            Assert.That(ServerLaunch.TryParse(Args(), _ => null, out launch, out error), Is.True, error);
            Assert.That(launch.StashDirectory, Is.Null);
            Assert.That(launch.StashProblem(new[] { MatchObjective.None, MatchObjective.Payload }), Is.Null);
            Assert.That(launch.StashProblem(new[] { MatchObjective.None, MatchObjective.Extraction }), Does.Contain("--stash-dir"));
        }

        [Test]
        public void AnExtractionServerWithoutAStashDirectoryRefusesToStart()
        {
            using var output = new StringWriter();
            using var errors = new StringWriter();
            WriteServerData(_directory.Path);

            var code = ServerProgram.Run(Args("-rules", "Extraction"), Array.Empty<IServerModule>(), Runtime(output, errors, TestClient.FreePort()));

            Assert.That(code, Is.EqualTo(ServerProgram.ExitMisconfigured));
            Assert.That(errors.ToString(), Does.StartWith("[Server] the Extraction rules keep each account's stash on disk; pass --stash-dir"));
            Assert.That(output.ToString(), Does.Not.Contain("listening"));
        }

        [Test]
        public void AnExtractionPlaylistWithoutAStashDirectoryRefusesToStart()
        {
            using var output = new StringWriter();
            using var errors = new StringWriter();
            WriteServerData(_directory.Path);
            TestRules.Write(_directory.Path, ServerLaunch.DefaultRules);
            var playlists = Path.Combine(_directory.Path, ServerMapData.DataDirectoryName, ServerRulesData.PlaylistsDirectoryName);
            Directory.CreateDirectory(playlists);
            File.WriteAllText(
                Path.Combine(playlists, "playlists.json"),
                @"{ ""playlists"": [ { ""id"": ""raid"", ""entries"": [ { ""scene"": ""NetTest"" }, { ""scene"": ""NetTest"", ""rules"": ""Extraction"" } ] } ] }");

            var code = ServerProgram.Run(Args("-playlist", "raid"), Array.Empty<IServerModule>(), Runtime(output, errors, TestClient.FreePort()));

            Assert.That(code, Is.EqualTo(ServerProgram.ExitMisconfigured), errors.ToString());
            Assert.That(errors.ToString(), Does.Contain("pass --stash-dir"));
        }

        [Test]
        [NonParallelizable]
        public void AnExtractionServerWithAStashDirectoryStartsAndCreatesIt()
        {
            using var output = new StringWriter();
            using var errors = new StringWriter();
            WriteServerData(_directory.Path);
            var stashes = Path.Combine(_directory.Path, "new-stashes");

            var code = ServerProgram.Run(Args("-rules", "Extraction", "--stash-dir", stashes), Array.Empty<IServerModule>(), Runtime(output, errors, TestClient.FreePort()));

            Assert.That(code, Is.EqualTo(ServerProgram.ExitOk), errors.ToString());
            Assert.That(Directory.Exists(stashes), Is.True);
            Assert.That(output.ToString(), Does.Contain("[Stash] directory=" + Path.GetFullPath(stashes)));
        }

        [Test]
        public void AStashDirectoryThatCannotBeMadeRefusesToStart()
        {
            using var output = new StringWriter();
            using var errors = new StringWriter();
            WriteServerData(_directory.Path);
            var file = Path.Combine(_directory.Path, "a-file");
            File.WriteAllText(file, "x");

            var code = ServerProgram.Run(Args("-rules", "Extraction", "--stash-dir", file), Array.Empty<IServerModule>(), Runtime(output, errors, TestClient.FreePort()));

            Assert.That(code, Is.EqualTo(ServerProgram.ExitMisconfigured));
            Assert.That(errors.ToString(), Does.StartWith("[Stash] cannot use the stash directory " + file + ": "));
        }

        // 薬草は 5 個で 1 山。2 個持って出撃する (出撃の外で配られる物)
        private static ItemCatalog Items()
        {
            return new ItemCatalog(
                new[]
                {
                    new ItemSpec(_herbId, "item.herb", maxStack: 5, weight: 0.1f, useHealth: 30),
                    new ItemSpec(_rifleItemId, "weapon.rifle", width: 4, height: 2, weaponId: TestRules.RifleId),
                    new ItemSpec(_handgunItemId, "weapon.handgun", width: 2, height: 1, weaponId: TestRules.HandgunId),
                    new ItemSpec(_shotgunItemId, "weapon.shotgun", width: 4, height: 2, weaponId: _shotgunId),
                },
                new InventoryRule(gridWidth: 8, gridHeight: 4),
                new[] { new ItemCount(_herbId, 2) });
        }

        // 補給の表は薬草を 2 回 (1-3 個ずつ)、武器庫の表は散弾銃を 1 つ
        private static LootSpec Loot()
        {
            return new LootSpec(new[]
            {
                new LootTable(_supplies, new[] { new LootEntry(_herbId, 1f, 1, 3) }, rolls: 2),
                new LootTable(_armory, new[] { new LootEntry(_shotgunItemId, 1f) }),
            });
        }

        // 新しい持ち物庫は小銃を 1 つ持つ
        private static StashSpec Stash()
        {
            return new StashSpec(capacity: 10, startingItems: new[] { new StashEntry(_rifleItemId, 1) });
        }

        private static ExtractionSpec Spec()
        {
            return new ExtractionSpec(raidSeconds: 120f, extractHoldSeconds: _holdSeconds, openPoints: 1, openAfterSeconds: 0f, keepCarriedOnDeath: false);
        }

        private static WeaponSpec Shotgun() => new WeaponSpec(
            _shotgunId, "weapon.shotgun", WeaponCategory.Shotgun, FireMode.Semi, 6, 24, 0.8f, 2.5f, 12, 30f, DamageFalloff.None);

        private static GameRules Rules()
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off);
            return GameRules.Default.WithObjective(MatchObjective.Extraction).WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f);
        }

        // 散弾銃はロードアウトに無く、武器の一覧にだけある
        private static NetMatchFile MatchFile()
        {
            return new NetMatchFile(
                "Extraction",
                Rules(),
                NetMatchSettings.Default,
                "DefaultLoadout",
                new LoadoutRule(new[] { _firstSlot, _secondSlot }),
                new[] { TestRules.Rifle(), TestRules.Handgun() },
                0.4f,
                "WeaponCatalog",
                new WeaponCatalog(new[] { TestRules.Rifle(), TestRules.Handgun(), Shotgun() }),
                null);
        }

        private static NetMapPoints Points()
        {
            return new NetMapPoints(
                new[] { new SpawnPoint(_alphaSpawn, Team.Alpha), new SpawnPoint(_bravoSpawn, Team.Bravo) },
                Array.Empty<ControlPointArea>(),
                lootContainers: new[]
                {
                    new NetMapLootContainer("crate-supplies", _supplies, _suppliesAt, 1.6f),
                    new NetMapLootContainer("crate-armory", _armory, _armoryAt, 1.6f),
                },
                extractionPoints: new[]
                {
                    // 書き出しの順に左右されず Id の順 (north, south) に並ぶことも確かめるため、逆に並べる
                    new NetMapExtractionPoint(_south, _southAt, 3f),
                    new NetMapExtractionPoint(_north, _northAt, 3f),
                });
        }

        private void Start(ExtractionSpec spec = null)
        {
            var colliders = new List<WorldCollider>
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(60f, 0.5f, 60f), Quaternion.Identity),
            };
            var map = new ServerMapData("NetTest", new WorldMap(colliders), TestMaps.NavMesh(), "memory", Points(), null);
            var data = new ServerRulesData(
                "Extraction", MatchFile(), PingSpec.Default, "memory", null, items: Items(), loot: Loot(), extraction: spec ?? Spec(), stash: Stash());
            _match = new ServerMatch(NetMatchSettings.DefaultRestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time);
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
            _rules = new MatchRulesModule((_, _, host) => _extraction = new ExtractionRules(host, _rules, _loot, _store, _seed));
            _combat = new ServerCombat(_rules, movement);
            _inventories = new ServerInventories(_rules, _combat);
            _loot = new ServerLoot(_rules, _combat, _inventories, _seed);
            _host = new ServerHost();
            _host.Register(movement);
            _host.Register(_rules);
            _host.Register(_combat);
            _host.Register(_inventories);
            _host.Register(_loot);
            _host.Start(_context);
        }

        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id.ToString(CultureInfo.InvariantCulture));
            _host.OnConnected(connection);
        }

        private void Save(string account, params StashEntry[] stacks)
        {
            File.WriteAllText(_store.PathOf(account), StashSave.ToJson(stacks));
        }

        private (string ItemId, int Count)[] SavedStacks(string account)
        {
            var stacks = StashSave.FromJson(File.ReadAllText(_store.PathOf(account)), out var problem);
            Assert.That(stacks, Is.Not.Null, problem);
            return stacks.Select(s => (s.ItemId, s.Count)).ToArray();
        }

        private int SavedCount(string account, string itemId)
        {
            return SavedStacks(account).Where(s => s.ItemId == itemId).Sum(s => s.Count);
        }

        // 補給の箱を開け、入った薬草の数を返す
        private int OpenSupplies(int id)
        {
            Assert.That(_loot.TryGetContents(_suppliesBox, out _, out var contents), Is.True, _log.ToString());
            MoveTo(id, _suppliesAt + new Vector3(0f, 1.6f, 1f));
            Assert.That(_loot.Open(id, _suppliesBox).IsAccepted, Is.True, _log.ToString());
            return contents.Sum(i => i.Count);
        }

        // 開いた地点に立ち、待ち切るまで進める
        private void Extract(int id)
        {
            MoveTo(id, CenterOf(_extraction.OpenPointIds.Single()) + _standing);
            Step(_holdSeconds + 0.3f);
        }

        private static Vector3 CenterOf(string pointId)
        {
            return pointId == _north ? _northAt : _southAt;
        }

        private static int IndexOf(string pointId)
        {
            return pointId == _north ? 0 : 1;
        }

        private void MoveTo(int id, Vector3 position)
        {
            _context.Bodies.Teleport(id, position, 0f, 0);
        }

        private SnapshotMessage Contribute(int id)
        {
            Assert.That(_match.TryGetPlayer(new ConnectionId((ulong)id), out var receiver), Is.True);
            var snapshot = new SnapshotMessage();
            _rules.Contribute(snapshot, receiver);
            _loot.Contribute(snapshot, receiver);
            return snapshot;
        }

        private static List<ObjectiveItemSnapshot> Rows(SnapshotMessage snapshot)
        {
            var rows = new List<ObjectiveItemSnapshot>();
            for (var i = 0; i < snapshot.ObjectiveItems.Count; i++)
            {
                rows.Add(snapshot.ObjectiveItems[i]);
            }

            return rows;
        }

        private List<StashStateMessage> States(int id)
        {
            return _transport.Read<StashStateMessage>(id, m =>
            {
                var copy = new StashStateMessage();
                copy.Stash.CopyFrom(m.Stash);
                copy.Carried.CopyFrom(m.Carried);
                copy.BroughtIn.CopyFrom(m.BroughtIn);
                copy.Outcome = m.Outcome;
                return copy;
            });
        }

        private static (string ItemId, int Count)[] Items(StashItemList list)
        {
            var items = new (string, int)[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                items[i] = (list.IdAt(i), list.CountAt(i));
            }

            return items;
        }

        private void Step(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                _match.Tick(_tickSeconds);
                _host.Tick(_tickSeconds);
            }
        }

        // 地点ファイルは Extraction に要る脱出地点を持つ
        private static void WriteServerData(string root)
        {
            TestMaps.Write(root, "NetTest", points: Points());
            TestRules.Write(root, "Extraction", TestRules.File(GameRules.Default.WithObjective(MatchObjective.Extraction), name: "Extraction"));
        }

        private ServerRuntime Runtime(StringWriter output, StringWriter errors, ushort port)
        {
            var environment = new Dictionary<string, string> { [ServerLaunch.PortVariable] = port.ToString(CultureInfo.InvariantCulture) };
            return new ServerRuntime(
                output,
                errors,
                name => environment.TryGetValue(name, out var value) ? value : null,
                new ImmediateStopSignal(),
                new[] { _directory.Path },
                new[] { _directory.Path },
                () => new UdpMatchTransport(),
                null,
                null);
        }

        private static string[] RepositoryRoots()
        {
            for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ServerMapData.DataDirectoryName, ServerRulesData.RulesDirectoryName)))
                {
                    return new[] { directory.FullName };
                }
            }

            Assert.Fail(ServerMapData.DataDirectoryName + " is not above " + TestContext.CurrentContext.TestDirectory);
            return null;
        }

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static string[] Args(params string[] args) => args;
    }
}
