using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ExtractionSessionTests
    {
        private const string _ammo = StashTests.Ammo;
        private const string _bandage = StashTests.Bandage;
        private const string _north = "north";
        private const string _south = "south";

        private static readonly string[] _northOnly = { _north };
        private static readonly string[] _threePoints = { "a", "b", "c" };
        private static readonly string[] _twoPoints = { "a", "b" };
        private static readonly string[] _repeatedPoints = { "a", "a" };
        private static readonly string[] _emptyPoint = { string.Empty };
        private static readonly string[] _cThenB = { "c", "b" };
        private static readonly string[] _aThenC = { "a", "c" };
        private static readonly ExtractionOutcome[] _extracted = { ExtractionOutcome.Extracted };
        private static readonly ExtractionOutcome[] _died = { ExtractionOutcome.Died };

        private static ExtractionSpec Spec(bool keepCarriedOnDeath = false)
        {
            return new ExtractionSpec(
                raidSeconds: 60f,
                extractHoldSeconds: 5f,
                openPoints: 1,
                openAfterSeconds: 10f,
                keepCarriedOnDeath: keepCarriedOnDeath);
        }

        private static Stash StashWithAmmo(int capacity = 10)
        {
            var stash = new Stash(capacity, StashTests.Catalog());
            stash.Add(_ammo, 30);
            return stash;
        }

        private static ExtractionSession Begin(Stash stash, ExtractionSpec spec = null)
        {
            return new ExtractionSession(spec ?? Spec(), stash, _northOnly, new[] { new ItemCount(_ammo, 20) });
        }

        [Test]
        public void BringingItemsInTakesThemOutOfTheStash()
        {
            var stash = StashWithAmmo();

            var session = Begin(stash);

            Assert.That(stash.CountOf(_ammo), Is.EqualTo(10));
            AssertItems(session.BroughtIn, (_ammo, 20));
            AssertItems(session.Carried, (_ammo, 20));
            Assert.That(session.Outcome, Is.EqualTo(ExtractionOutcome.Raiding));
            Assert.That(session.IsOver, Is.False);
            Assert.That(session.OpenPointIds, Is.EqualTo(_northOnly));
        }

        [Test]
        public void CannotBringInMoreThanTheStashHolds()
        {
            var stash = StashWithAmmo();

            Assert.Throws<ArgumentException>(
                () => new ExtractionSession(Spec(), stash, _northOnly, new[] { new ItemCount(_ammo, 31) }));
            Assert.That(stash.CountOf(_ammo), Is.EqualTo(30));
        }

        [Test]
        public void ExtractingMovesWhatWasBroughtInAndPickedUpIntoTheStash()
        {
            var stash = StashWithAmmo();
            var session = Begin(stash);
            var ended = new List<ExtractionOutcome>();
            session.Ended += ended.Add;
            session.Pick(_bandage, 2);

            Assert.That(session.Tick(10f, _north), Is.EqualTo(ExtractionOutcome.Raiding));
            Assert.That(session.PointsOpen, Is.True);
            Assert.That(session.HoldSeconds, Is.EqualTo(0f));
            Assert.That(session.Tick(4f, _north), Is.EqualTo(ExtractionOutcome.Raiding));
            Assert.That(session.HoldProgress, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(session.Tick(1f, _north), Is.EqualTo(ExtractionOutcome.Extracted));

            Assert.That(stash.CountOf(_ammo), Is.EqualTo(30));
            Assert.That(stash.CountOf(_bandage), Is.EqualTo(2));
            AssertItems(session.Stored, (_ammo, 20), (_bandage, 2));
            AssertItems(session.Lost);
            AssertItems(session.Carried);
            Assert.That(session.ElapsedSeconds, Is.EqualTo(15f));
            Assert.That(ended, Is.EqualTo(_extracted));
        }

        [Test]
        public void DyingLosesWhatWasBroughtInAndPickedUp()
        {
            var stash = StashWithAmmo();
            var session = Begin(stash);
            var ended = new List<ExtractionOutcome>();
            session.Ended += ended.Add;
            session.Pick(_bandage, 2);

            session.Die();

            Assert.That(session.Outcome, Is.EqualTo(ExtractionOutcome.Died));
            Assert.That(stash.CountOf(_ammo), Is.EqualTo(10));
            Assert.That(stash.CountOf(_bandage), Is.EqualTo(0));
            AssertItems(session.Stored);
            AssertItems(session.Lost, (_ammo, 20), (_bandage, 2));
            Assert.That(ended, Is.EqualTo(_died));
        }

        [Test]
        public void KeepCarriedOnDeathSendsTheCarriedItemsHomeOnDeath()
        {
            var stash = StashWithAmmo();
            var session = Begin(stash, Spec(keepCarriedOnDeath: true));
            session.Pick(_bandage, 2);

            session.Die();

            Assert.That(session.Outcome, Is.EqualTo(ExtractionOutcome.Died));
            Assert.That(stash.CountOf(_ammo), Is.EqualTo(30));
            Assert.That(stash.CountOf(_bandage), Is.EqualTo(2));
            AssertItems(session.Stored, (_ammo, 20), (_bandage, 2));
            AssertItems(session.Lost);
        }

        [Test]
        public void RunningOutOfTimeLosesTheCarriedItems()
        {
            var stash = StashWithAmmo();
            var session = Begin(stash, Spec(keepCarriedOnDeath: true));

            Assert.That(session.Tick(59f, null), Is.EqualTo(ExtractionOutcome.Raiding));
            Assert.That(session.RemainingSeconds, Is.EqualTo(1f));
            Assert.That(session.Tick(5f, null), Is.EqualTo(ExtractionOutcome.TimedOut));

            Assert.That(session.ElapsedSeconds, Is.EqualTo(60f));
            Assert.That(session.RemainingSeconds, Is.EqualTo(0f));
            Assert.That(stash.CountOf(_ammo), Is.EqualTo(10));
            AssertItems(session.Lost, (_ammo, 20));
        }

        [Test]
        public void StandingBeforeThePointsOpenDoesNotCount()
        {
            var session = Begin(StashWithAmmo());

            session.Tick(9f, _north);
            Assert.That(session.PointsOpen, Is.False);
            Assert.That(session.SecondsUntilOpen, Is.EqualTo(1f));
            Assert.That(session.HoldingPointId, Is.Null);
            Assert.That(session.IsOpen(_north), Is.False);

            session.Tick(3f, _north);
            Assert.That(session.HoldingPointId, Is.EqualTo(_north));
            Assert.That(session.HoldSeconds, Is.EqualTo(2f));
        }

        [Test]
        public void LeavingThePointStartsTheHoldOver()
        {
            var session = Begin(StashWithAmmo());
            session.Tick(10f, null);

            session.Tick(4f, _north);
            session.Tick(1f, null);
            Assert.That(session.HoldSeconds, Is.EqualTo(0f));
            Assert.That(session.HoldingPointId, Is.Null);

            session.Tick(4f, _north);
            Assert.That(session.Outcome, Is.EqualTo(ExtractionOutcome.Raiding));
            Assert.That(session.HoldSeconds, Is.EqualTo(4f));
        }

        [Test]
        public void APointThatDidNotOpenNeverExtracts()
        {
            var session = Begin(StashWithAmmo());

            session.Tick(30f, _south);

            Assert.That(session.IsOpen(_south), Is.False);
            Assert.That(session.HoldSeconds, Is.EqualTo(0f));
            Assert.That(session.Outcome, Is.EqualTo(ExtractionOutcome.Raiding));
        }

        [Test]
        public void WhatDoesNotFitInTheStashIsLost()
        {
            var stash = new Stash(1, StashTests.Catalog());
            stash.Add(_ammo, 25);
            var spec = new ExtractionSpec(raidSeconds: 60f, extractHoldSeconds: 0f, openPoints: 1, openAfterSeconds: 0f);
            var session = new ExtractionSession(spec, stash, _northOnly);
            session.Pick(_ammo, 10);
            session.Pick(_bandage, 3);

            Assert.That(session.Tick(0f, _north), Is.EqualTo(ExtractionOutcome.Extracted));

            Assert.That(stash.CountOf(_ammo), Is.EqualTo(30));
            AssertItems(session.Stored, (_ammo, 5));
            AssertItems(session.Lost, (_ammo, 5), (_bandage, 3));
        }

        [Test]
        public void DropAndReplaceCarriedChangeWhatComesHome()
        {
            var stash = StashWithAmmo();
            var session = Begin(stash);
            session.Pick(_bandage, 3);

            Assert.That(session.Drop(_bandage, 1), Is.EqualTo(1));
            Assert.That(session.Drop(_bandage, 5), Is.EqualTo(2));
            Assert.That(session.Drop(_bandage, 1), Is.EqualTo(0));
            AssertItems(session.Carried, (_ammo, 20));

            session.ReplaceCarried(new[] { new ItemCount(_bandage, 1), new ItemCount(_ammo, 4), new ItemCount(_bandage, 1) });
            AssertItems(session.Carried, (_bandage, 2), (_ammo, 4));

            session.Tick(15f, _north);
            Assert.That(session.Outcome, Is.EqualTo(ExtractionOutcome.Extracted));
            Assert.That(stash.CountOf(_ammo), Is.EqualTo(14));
            Assert.That(stash.CountOf(_bandage), Is.EqualTo(2));
        }

        [Test]
        public void NothingChangesAfterTheRaidEnds()
        {
            var stash = StashWithAmmo();
            var session = Begin(stash);
            var ended = 0;
            session.Ended += _ => ended++;
            session.Tick(15f, _north);

            session.Die();
            Assert.That(session.Tick(100f, null), Is.EqualTo(ExtractionOutcome.Extracted));
            Assert.Throws<InvalidOperationException>(() => session.Pick(_bandage, 1));
            Assert.Throws<InvalidOperationException>(() => session.Drop(_bandage, 1));
            Assert.Throws<InvalidOperationException>(() => session.ReplaceCarried(Array.Empty<ItemCount>()));

            Assert.That(session.Outcome, Is.EqualTo(ExtractionOutcome.Extracted));
            Assert.That(session.ElapsedSeconds, Is.EqualTo(15f));
            Assert.That(stash.CountOf(_ammo), Is.EqualTo(30));
            Assert.That(ended, Is.EqualTo(1));
        }

        [Test]
        public void PickOpenPointsShufflesAndKeepsTheSpecsCount()
        {
            var spec = new ExtractionSpec(openPoints: 2);

            Assert.That(ExtractionSession.PickOpenPoints(spec, _threePoints, new FixedRandom(0.9, 0.0)), Is.EqualTo(_cThenB));
            Assert.That(ExtractionSession.PickOpenPoints(spec, _threePoints, new FixedRandom(0.0, 0.99)), Is.EqualTo(_aThenC));
        }

        [Test]
        public void PickOpenPointsOpensEveryPointWhenThereAreFewerThanTheSpecAsks()
        {
            var spec = new ExtractionSpec(openPoints: 5);

            Assert.That(ExtractionSession.PickOpenPoints(spec, _twoPoints, new FixedRandom(0.0, 0.0)), Is.EqualTo(_twoPoints));
            Assert.That(ExtractionSession.PickOpenPoints(spec, Array.Empty<string>(), new FixedRandom()), Is.Empty);
        }

        [Test]
        public void RejectsBadArguments()
        {
            var stash = StashWithAmmo();

            Assert.Throws<ArgumentNullException>(() => new ExtractionSession(null, stash, _northOnly));
            Assert.Throws<ArgumentNullException>(() => new ExtractionSession(Spec(), null, _northOnly));
            Assert.Throws<ArgumentNullException>(() => new ExtractionSession(Spec(), stash, null));
            Assert.Throws<ArgumentException>(() => new ExtractionSession(Spec(), stash, _repeatedPoints));
            Assert.Throws<ArgumentException>(() => new ExtractionSession(Spec(), stash, _emptyPoint));
            Assert.Throws<ArgumentNullException>(() => ExtractionSession.PickOpenPoints(Spec(), _twoPoints, null));

            var session = new ExtractionSession(Spec(), stash, _northOnly);
            Assert.Throws<ArgumentOutOfRangeException>(() => session.Tick(-1f, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.Pick(_bandage, 0));
            Assert.Throws<ArgumentException>(() => session.Pick("Bandage", 1));
            Assert.That(stash.CountOf(_ammo), Is.EqualTo(30));
        }

        [Test]
        public void LogsTheStartAndTheEnd()
        {
            var session = Begin(StashWithAmmo());
            session.Pick(_bandage, 2);
            session.Tick(15f, _north);

            Assert.That(
                ExtractionLog.Started(Begin(StashWithAmmo())),
                Is.EqualTo("[Extraction] started points=north raid=60.0s open-after=10.0s hold=5.0s brought=" + _ammo + "x20"));
            Assert.That(
                ExtractionLog.Ended(session),
                Is.EqualTo("[Extraction] ended outcome=Extracted at=15.0s stored=" + _ammo + "x20," + _bandage + "x2 lost=-"));
            Assert.Throws<ArgumentException>(() => ExtractionLog.Ended(Begin(StashWithAmmo())));
        }

        private static void AssertItems(IReadOnlyList<ItemCount> items, params (string ItemId, int Count)[] expected)
        {
            Assert.That(items.Count, Is.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.That(items[i].ItemId, Is.EqualTo(expected[i].ItemId), "item " + i);
                Assert.That(items[i].Count, Is.EqualTo(expected[i].Count), "item " + i);
            }
        }
    }
}
