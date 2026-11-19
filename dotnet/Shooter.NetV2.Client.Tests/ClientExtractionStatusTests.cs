using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;
using RaidOutcome = Shooter.Core.ExtractionOutcome;
using StashOutcome = Shooter.Protocol.ExtractionOutcome;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientExtractionStatusTests
    {
        public const string Ammo = "5b0c55a4-8f0e-4a43-9c55-0b1f3ad1e001";
        public const string Bandage = "5b0c55a4-8f0e-4a43-9c55-0b1f3ad1e002";

        private const int _self = 3;
        private const int _point = 7;
        private const int _box = 8;
        private const string _north = "north";

        private static readonly string[] _northOnly = { _north };

        private ClientExtractionStatus _status;

        [SetUp]
        public void SetUp()
        {
            _status = new ClientExtractionStatus(_self, Spec());
        }

        [Test]
        public void HoldingAnOpenPointIsTheSameAsTheOfflineSession()
        {
            var stash = StashWithAmmo(10);
            var offline = Begin(stash);
            offline.Pick(Bandage, 2);
            offline.Tick(100f, null);
            offline.Tick(3f, _north);
            Assert.That(offline.HoldingPointId, Is.EqualTo(_north));

            _status.Receive(Message(stash, offline.Carried, StashOutcome.None));
            var snapshot = Snapshot(remaining: (int)offline.RemainingSeconds, pointOpen: true);
            snapshot.HasOwner = true;
            snapshot.Owner.HasExtractionPoint = true;
            snapshot.Owner.ExtractionPointId = _point;
            snapshot.Owner.ExtractionProgress = offline.HoldProgress;
            var changed = _status.Apply(snapshot);

            Assert.That(changed, Is.True);
            Assert.That(_status.IsActive, Is.True);
            Assert.That(_status.Outcome, Is.EqualTo(offline.Outcome));
            Assert.That(_status.RemainingSeconds, Is.EqualTo(offline.RemainingSeconds));
            Assert.That(_status.ElapsedSeconds, Is.EqualTo(offline.ElapsedSeconds));
            Assert.That(_status.PointsOpen, Is.EqualTo(offline.PointsOpen));
            Assert.That(_status.SecondsUntilOpen, Is.EqualTo(offline.SecondsUntilOpen));
            Assert.That(_status.OpenPointCount, Is.EqualTo(offline.OpenPointIds.Count));
            Assert.That(_status.HoldProgress, Is.EqualTo((float?)offline.HoldProgress));
            Assert.That(_status.HoldRemainingSeconds, Is.EqualTo(offline.Spec.ExtractHoldSeconds - offline.HoldSeconds));
            Assert.That(_status.StandingOnClosedPoint, Is.False, "holding an open point");
            AssertSameItems(_status.Carried, offline.Carried);
            Assert.That(_status.Stored, Is.Empty);
            Assert.That(_status.Lost, Is.Empty);
            Assert.That(_status.OwnSide, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(_status.IsEnded, Is.False);
        }

        [Test]
        public void BeforeThePointsOpenTheCountdownIsTheSameAsTheOfflineSession()
        {
            var stash = StashWithAmmo(10);
            var offline = Begin(stash);
            offline.Tick(30f, _north);

            _status.Apply(Snapshot(remaining: (int)offline.RemainingSeconds, pointOpen: false));

            Assert.That(_status.PointsOpen, Is.EqualTo(offline.PointsOpen));
            Assert.That(_status.PointsOpen, Is.False);
            Assert.That(_status.SecondsUntilOpen, Is.EqualTo(offline.SecondsUntilOpen));
            Assert.That(_status.OpenPointCount, Is.Zero, "the server opens none yet");
            Assert.That(_status.StandingOnClosedPoint, Is.True, "the local player stands on the closed point");
            Assert.That(_status.HoldProgress, Is.Null);
        }

        [Test]
        public void ExtractingStoresWhatTheOfflineSessionStores()
        {
            AssertEndLike(capacity: 10, end: session => session.Tick(Spec().ExtractHoldSeconds, _north), StashOutcome.Extracted);
        }

        [Test]
        public void AFullStashLosesWhatTheOfflineSessionLoses()
        {
            AssertEndLike(capacity: 1, end: session => session.Tick(Spec().ExtractHoldSeconds, _north), StashOutcome.Extracted);
        }

        [Test]
        public void DyingLosesWhatTheOfflineSessionLoses()
        {
            AssertEndLike(capacity: 10, end: session => session.Die(), StashOutcome.Killed);
        }

        [Test]
        public void TimingOutLosesWhatTheOfflineSessionLoses()
        {
            AssertEndLike(capacity: 10, end: session => session.Tick(Spec().RaidSeconds, null), StashOutcome.TimedOut);
        }

        [Test]
        public void AnEndWithoutTheRaidBeforeItKnowsNothingWasStoredOrLost()
        {
            var stash = new StashStateMessage { Outcome = StashOutcome.Extracted };
            stash.Stash.Add(Ammo, 30);

            Assert.That(_status.Receive(stash), Is.True);

            Assert.That(_status.Outcome, Is.EqualTo(RaidOutcome.Extracted));
            Assert.That(_status.Stored, Is.Empty);
            Assert.That(_status.Lost, Is.Empty);
        }

        [Test]
        public void ANewRaidForgetsTheLastResult()
        {
            var raiding = new StashStateMessage();
            raiding.Carried.Add(Ammo, 5);
            _status.Receive(raiding);
            _status.Receive(new StashStateMessage { Outcome = StashOutcome.Killed });
            Assert.That(_status.Lost, Has.Count.EqualTo(1));

            _status.Receive(new StashStateMessage());

            Assert.That(_status.Outcome, Is.EqualTo(RaidOutcome.Raiding));
            Assert.That(_status.Lost, Is.Empty);
            Assert.That(_status.Stored, Is.Empty);
        }

        [Test]
        public void ALootBoxWithinItsRadiusIsInReach()
        {
            var snapshot = Snapshot(remaining: 500, pointOpen: true);
            AddItem(snapshot, _box, ObjectiveItemKind.LootBox, new Vector3(1f, 0f, 0f), opened: false, radius: 2f);

            _status.Apply(snapshot);

            Assert.That(_status.LootInReach, Is.True);

            var opened = Snapshot(remaining: 500, pointOpen: true);
            AddItem(opened, _box, ObjectiveItemKind.LootBox, new Vector3(1f, 0f, 0f), opened: true, radius: 2f);
            _status.Apply(opened);

            Assert.That(_status.LootInReach, Is.False, "an opened box has nothing left to take");
        }

        [Test]
        public void ADeadPlayerReachesNothing()
        {
            var snapshot = Snapshot(remaining: 500, pointOpen: false, health: 0);
            AddItem(snapshot, _box, ObjectiveItemKind.LootBox, new Vector3(1f, 0f, 0f), opened: false, radius: 2f);

            _status.Apply(snapshot);

            Assert.That(_status.LootInReach, Is.False);
            Assert.That(_status.StandingOnClosedPoint, Is.False);
        }

        [Test]
        public void TheSameValuesKeepTheShownValues()
        {
            Assert.That(_status.Apply(Snapshot(remaining: 500, pointOpen: true)), Is.True);
            Assert.That(_status.Apply(Snapshot(remaining: 500, pointOpen: true)), Is.False);
            Assert.That(_status.Apply(Snapshot(remaining: 499, pointOpen: true)), Is.True);
        }

        [Test]
        public void AnotherObjectiveClearsTheValues()
        {
            _status.Apply(Snapshot(remaining: 500, pointOpen: true));
            var snapshot = Snapshot(remaining: 500, pointOpen: true);
            snapshot.Match.Objective = MatchObjective.BattleRoyale;

            Assert.That(_status.Apply(snapshot), Is.True);
            Assert.That(_status.IsActive, Is.False);
            Assert.That(_status.Apply(snapshot), Is.False, "already cleared");
        }

        [Test]
        public void TheEndShowsTheServersWinner()
        {
            var snapshot = Snapshot(remaining: 0, pointOpen: true);
            snapshot.Match.State = GameModeState.Ended;
            snapshot.Match.HasWinner = true;
            snapshot.Match.Winner = Team.Bravo;

            _status.Apply(snapshot);

            Assert.That(_status.IsEnded, Is.True);
            Assert.That(_status.Winner, Is.EqualTo((Side?)Side.Bravo));
        }

        [TestCase(StashOutcome.None, RaidOutcome.Raiding)]
        [TestCase(StashOutcome.Extracted, RaidOutcome.Extracted)]
        [TestCase(StashOutcome.Killed, RaidOutcome.Died)]
        [TestCase(StashOutcome.TimedOut, RaidOutcome.TimedOut)]
        public void TheSentOutcomeIsTheOfflineOutcome(StashOutcome sent, RaidOutcome expected)
        {
            Assert.That(ClientExtractionStatus.ToRaidOutcome(sent), Is.EqualTo(expected));
        }

        [Test]
        public void BadArgumentsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => _status.Apply(null));
            Assert.Throws<ArgumentNullException>(() => _status.Receive(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClientExtractionStatus.ToRaidOutcome((StashOutcome)99));
        }

        private void AssertEndLike(int capacity, Action<ExtractionSession> end, StashOutcome sent)
        {
            var stash = StashWithAmmo(capacity);
            var offline = Begin(stash);
            offline.Pick(Bandage, 2);
            offline.Tick(100f, null);
            _status.Receive(Message(stash, offline.Carried, StashOutcome.None));

            end(offline);
            Assert.That(offline.IsOver, Is.True);
            var changed = _status.Receive(Message(stash, Array.Empty<ItemCount>(), sent));

            Assert.That(changed, Is.True);
            Assert.That(_status.Outcome, Is.EqualTo(offline.Outcome));
            Assert.That(_status.IsOver, Is.True);
            Assert.That(_status.Carried, Is.Empty);
            AssertSameItems(_status.Stored, offline.Stored);
            AssertSameItems(_status.Lost, offline.Lost);
        }

        private static void AssertSameItems(IReadOnlyList<ItemCount> actual, IReadOnlyList<ItemCount> expected)
        {
            Assert.That(actual, Has.Count.EqualTo(expected.Count));
            for (var i = 0; i < expected.Count; i++)
            {
                Assert.That(actual[i].ItemId, Is.EqualTo(expected[i].ItemId), "item " + i);
                Assert.That(actual[i].Count, Is.EqualTo(expected[i].Count), "count of item " + i);
            }
        }

        private static ExtractionSpec Spec()
        {
            return new ExtractionSpec(raidSeconds: 600f, extractHoldSeconds: 8f, openPoints: 1, openAfterSeconds: 60f);
        }

        private static ItemCatalog Catalog()
        {
            return new ItemCatalog(
                new[] { new ItemSpec(Ammo, maxStack: 30), new ItemSpec(Bandage, maxStack: 5) },
                InventoryRule.Default);
        }

        private static Stash StashWithAmmo(int capacity)
        {
            var stash = new Stash(capacity, Catalog());
            stash.Add(Ammo, 30);
            return stash;
        }

        private static ExtractionSession Begin(Stash stash)
        {
            return new ExtractionSession(Spec(), stash, _northOnly, new[] { new ItemCount(Ammo, 20) });
        }

        private static StashStateMessage Message(Stash stash, IReadOnlyList<ItemCount> carried, StashOutcome outcome)
        {
            var message = new StashStateMessage { Outcome = outcome };
            foreach (var stack in stash.Stacks)
            {
                message.Stash.Add(stack.ItemId, stack.Count);
            }

            foreach (var item in carried)
            {
                message.Carried.Add(item.ItemId, item.Count);
            }

            return message;
        }

        private static SnapshotMessage Snapshot(int remaining, bool pointOpen, int health = 100)
        {
            var snapshot = new SnapshotMessage { Tick = 10 };
            ref var match = ref snapshot.Match;
            match.State = GameModeState.Playing;
            match.Objective = MatchObjective.Extraction;
            match.RemainingSeconds = remaining;
            AddItem(snapshot, _point, ObjectiveItemKind.ExtractionPoint, new Vector3(0.5f, 3f, 0f), pointOpen, 4f);

            ref var self = ref snapshot.Players.Add();
            self.PlayerId = _self;
            self.Team = Team.Alpha;
            self.Health = health;
            self.WeaponId = "";
            return snapshot;
        }

        private static void AddItem(SnapshotMessage snapshot, int id, ObjectiveItemKind kind, Vector3 position, bool opened, float radius)
        {
            ref var item = ref snapshot.ObjectiveItems.Add();
            item.ItemId = id;
            item.Present = ObjectiveItemFields.All;
            item.Kind = kind;
            item.Position = position;
            item.State = ObjectiveItemState.Home;
            item.Opened = opened;
            item.Radius = radius;
        }
    }
}
