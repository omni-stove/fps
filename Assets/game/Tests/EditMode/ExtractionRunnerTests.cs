using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class ExtractionRunnerTests
    {
        private const string _ammo = "5b0c55a4-8f0e-4a43-9c55-0b1f3ad1e001";
        private const string _bandage = "5b0c55a4-8f0e-4a43-9c55-0b1f3ad1e002";
        private const string _crate = "crate";
        private const float _raidSeconds = 60f;
        private const float _holdSeconds = 5f;
        private const int _ammoPerCrate = 3;

        private static readonly Vector3 _crateAt = new Vector3(0f, 0f, 2f);
        private static readonly Vector3 _besideCrate = new Vector3(0f, 0f, 1.5f);
        private static readonly Vector3 _northAt = new Vector3(0f, 0f, 20f);
        private static readonly Vector3 _southAt = new Vector3(0f, 0f, -20f);

        private readonly List<Object> _objects = new List<Object>();
        private ExtractionRunner _runner;
        private StashStore _store;
        private Stash _stash;

        [SetUp]
        public void SetUp()
        {
            // 本物の持ち物庫 (DefaultKey) を壊さないよう、テストごとに別のキーを使う。
            _store = new StashStore("Shooter.Tests.Extraction." + Guid.NewGuid().ToString("N"));
            _stash = new Stash(10, Catalog());

            _runner = Track(new GameObject("Runner")).AddComponent<ExtractionRunner>();
            _runner.Spec = new ExtractionSpec(_raidSeconds, _holdSeconds, openPoints: 1, openAfterSeconds: 0f);
            _runner.Loot = new LootSpec(new[] { new LootTable(_crate, new[] { new LootEntry(_ammo, 1f, _ammoPerCrate, _ammoPerCrate) }) });
            _runner.Stash = _stash;
            _runner.Store = _store;
            _runner.Random = new System.Random(7);
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Stop();
            _store.Delete();
            for (var i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void OpeningACrateAndHoldingTheOpenExitEndsTheRaidWithTheLootStored()
        {
            var player = Member("Player", Vector3.zero);
            var crate = Container("Crate", _crateAt, _crate);
            var north = Zone("Exit-North", "north", _northAt);
            _runner.SetContainers(new[] { crate });
            _runner.SetZones(new[] { north });
            player.transform.position = _besideCrate;
            Assert.That(_runner.IsOfferingInteract, Is.False, "a runner that has not started offers nothing");
            player.transform.position = Vector3.zero;
            _runner.BeginRaid(new[] { player }, Array.Empty<SideSpawnPoint>(), player);

            Assert.That(north.IsOpen, Is.True, "the only exit is opened from the start");
            Assert.That(Detail().LootInReach, Is.False, "the crate is out of reach");
            Assert.That(_runner.IsOfferingInteract, Is.False);
            Assert.That(_runner.TryLoot(), Is.False);

            player.transform.position = _besideCrate;
            _runner.Tick(0f);
            Assert.That(Detail().LootInReach, Is.True);
            Assert.That(_runner.IsOfferingInteract, Is.True, "a shared key goes to the crate, not to melee");
            Assert.That(_runner.TryLoot(), Is.True);
            Assert.That(crate.IsOpened, Is.True);
            AssertItems(crate.Contents, (_ammo, _ammoPerCrate));
            AssertItems(_runner.Extraction.Carried, (_ammo, _ammoPerCrate));
            Assert.That(Detail().CarriedCount, Is.EqualTo(_ammoPerCrate));
            Assert.That(Detail().LootInReach, Is.False, "an opened crate is not offered again");
            Assert.That(_runner.IsOfferingInteract, Is.False);
            Assert.That(_runner.TryLoot(), Is.False, "an opened crate cannot be opened twice");

            player.transform.position = _northAt;
            _runner.Tick(2f);
            Assert.That(Detail().IsHolding, Is.True);
            Assert.That(Detail().HoldRemainingSeconds, Is.EqualTo(3f));

            _runner.Tick(1f);
            Assert.That(_runner.Extraction.Outcome, Is.EqualTo(ExtractionOutcome.Raiding), "the hold time has not passed yet");
            Assert.That(_runner.IsMatchOver, Is.False);

            _runner.Tick(2f);
            Assert.That(_runner.Extraction.Outcome, Is.EqualTo(ExtractionOutcome.Extracted));
            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_runner.Session.Winner, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(_runner.IsMatchOver, Is.True);
            Assert.That(_runner.Status.IsEnded, Is.True);
            Assert.That(_runner.Status.Winner, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(Detail().Outcome, Is.EqualTo(ExtractionOutcome.Extracted));
            Assert.That(Detail().StoredCount, Is.EqualTo(_ammoPerCrate));
            Assert.That(Detail().LostCount, Is.EqualTo(0));
            Assert.That(_stash.CountOf(_ammo), Is.EqualTo(_ammoPerCrate));
            Assert.That(north.IsOpen, Is.False, "the exits close once the raid is over");

            Assert.That(_store.HasSave, Is.True, "the stash is saved when the raid ends");
            Assert.That(_store.Load(new StashSpec(10), Catalog()).CountOf(_ammo), Is.EqualTo(_ammoPerCrate));
            Assert.That(GameSignals.Stash.Value, Is.EqualTo((StashStatus?)_stash.Status));
        }

        [Test]
        public void StandingAtAClosedExitDoesNotExtract()
        {
            var player = Member("Player", Vector3.zero);
            var north = Zone("Exit-North", "north", _northAt);
            var south = Zone("Exit-South", "south", _southAt);
            _runner.SetZones(new[] { north, south });
            _runner.BeginRaid(new[] { player }, Array.Empty<SideSpawnPoint>(), player);

            Assert.That(north.IsOpen ^ south.IsOpen, Is.True, "only one of the two exits opens");
            var closed = north.IsOpen ? south : north;
            player.transform.position = closed.transform.position;
            _runner.Tick(_holdSeconds * 2f);

            Assert.That(_runner.Extraction.Outcome, Is.EqualTo(ExtractionOutcome.Raiding));
            Assert.That(Detail().IsHolding, Is.False);
            Assert.That(Detail().StandingOnClosedPoint, Is.True);
            Assert.That(_runner.ZoneAt(closed.transform.position), Is.SameAs(closed));
        }

        [Test]
        public void TheExitsOpenOnlyAfterTheWait()
        {
            _runner.Spec = new ExtractionSpec(_raidSeconds, _holdSeconds, openPoints: 1, openAfterSeconds: 10f);
            var player = Member("Player", _northAt);
            var north = Zone("Exit-North", "north", _northAt);
            _runner.SetZones(new[] { north });
            _runner.BeginRaid(new[] { player }, Array.Empty<SideSpawnPoint>(), player);

            Assert.That(north.IsOpen, Is.False);
            Assert.That(Detail().SecondsUntilOpen, Is.EqualTo(10f));

            _runner.Tick(10f);
            Assert.That(north.IsOpen, Is.True);
            Assert.That(Detail().HoldRemainingSeconds, Is.EqualTo(_holdSeconds), "the time spent before the exit opened does not count");

            _runner.Tick(_holdSeconds);
            Assert.That(_runner.Extraction.Outcome, Is.EqualTo(ExtractionOutcome.Extracted));
        }

        [Test]
        public void DyingLosesWhatWasPickedUpAndTheRaidIsLost()
        {
            var player = Member("Player", _besideCrate);
            _runner.SetContainers(new[] { Container("Crate", _crateAt, _crate) });
            _runner.SetZones(new[] { Zone("Exit-North", "north", _northAt) });
            _runner.BeginRaid(new[] { player }, Array.Empty<SideSpawnPoint>(), player);
            Assert.That(_runner.TryLoot(), Is.True);

            player.Damageable.Kill();

            Assert.That(_runner.Extraction.Outcome, Is.EqualTo(ExtractionOutcome.Died));
            Assert.That(_runner.Session.Winner, Is.EqualTo((Side?)Side.Bravo), "a failed raid counts as a loss, not a draw");
            Assert.That(_runner.IsMatchOver, Is.True);
            AssertItems(_runner.Extraction.Lost, (_ammo, _ammoPerCrate));
            Assert.That(Detail().LostCount, Is.EqualTo(_ammoPerCrate));
            Assert.That(_stash.CountOf(_ammo), Is.EqualTo(0));
            Assert.That(_runner.TryLoot(), Is.False);
        }

        [Test]
        public void RunningOutOfTimeEndsTheRaid()
        {
            var player = Member("Player", Vector3.zero);
            _runner.SetZones(new[] { Zone("Exit-North", "north", _northAt) });
            _runner.BeginRaid(new[] { player }, Array.Empty<SideSpawnPoint>(), player);

            _runner.Tick(_raidSeconds - 1f);
            Assert.That(Detail().RemainingSeconds, Is.EqualTo(1f));
            Assert.That(_runner.Status.RemainingSeconds, Is.Null, "the raid clock is in the detail, not in the mode's time limit");

            _runner.Tick(1f);
            Assert.That(_runner.Extraction.Outcome, Is.EqualTo(ExtractionOutcome.TimedOut));
            Assert.That(_runner.Session.Winner, Is.EqualTo((Side?)Side.Bravo));
        }

        [Test]
        public void AnEnemyOfTheHostileSideJoinsAndCountsAsAKill()
        {
            var player = Member("Player", Vector3.zero);
            var enemy = Member("Bot-1-1", new Vector3(5f, 0f, 0f), Side.Bravo);
            _runner.SetZones(new[] { Zone("Exit-North", "north", _northAt) });
            _runner.BeginRaid(new[] { player, enemy }, Array.Empty<SideSpawnPoint>(), player);

            enemy.Damageable.Kill();

            Assert.That(_runner.Session.GetDeaths(_runner.IdOf(enemy)), Is.EqualTo(1));
            Assert.That(_runner.Extraction.Outcome, Is.EqualTo(ExtractionOutcome.Raiding), "only the player's death ends the raid");
        }

        [Test]
        public void ACrateWithAnUnknownTableOpensEmpty()
        {
            var player = Member("Player", _besideCrate);
            var crate = Container("Crate", _crateAt, "missing");
            _runner.SetContainers(new[] { crate });
            _runner.SetZones(new[] { Zone("Exit-North", "north", _northAt) });
            _runner.BeginRaid(new[] { player }, Array.Empty<SideSpawnPoint>(), player);

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Loot\] container=Crate names the table missing"));
            Assert.That(_runner.TryLoot(), Is.True);

            Assert.That(crate.IsOpened, Is.True);
            Assert.That(crate.Contents, Is.Empty);
            Assert.That(_runner.Extraction.Carried, Is.Empty);
        }

        [Test]
        public void TheDefinitionReplacesTheTuning()
        {
            var definition = ScriptableObject.CreateInstance<ExtractionDefinition>();
            _objects.Add(definition);
            definition.RaidSeconds = 120f;
            definition.ExtractHoldSeconds = 3f;
            definition.OpenPoints = 2;
            definition.OpenAfterSeconds = 30f;
            definition.KeepCarriedOnDeath = true;
            _runner.Spec = null;
            _runner.Definition = definition;

            var spec = _runner.Spec;

            Assert.That(spec.RaidSeconds, Is.EqualTo(120f));
            Assert.That(spec.ExtractHoldSeconds, Is.EqualTo(3f));
            Assert.That(spec.OpenPoints, Is.EqualTo(2));
            Assert.That(spec.OpenAfterSeconds, Is.EqualTo(30f));
            Assert.That(spec.KeepCarriedOnDeath, Is.True);
        }

        [Test]
        public void ADefinitionRoundTripsTheSpecAndRefusesAnUnwinnableRaid()
        {
            var definition = ScriptableObject.CreateInstance<ExtractionDefinition>();
            _objects.Add(definition);

            Assert.That(definition.RaidSeconds, Is.EqualTo(ExtractionSpec.DefaultRaidSeconds));
            Assert.That(definition.ExtractHoldSeconds, Is.EqualTo(ExtractionSpec.DefaultExtractHoldSeconds));
            Assert.That(definition.OpenPoints, Is.EqualTo(ExtractionSpec.DefaultOpenPoints));
            Assert.That(definition.OpenAfterSeconds, Is.EqualTo(ExtractionSpec.DefaultOpenAfterSeconds));
            Assert.That(definition.KeepCarriedOnDeath, Is.EqualTo(ExtractionSpec.DefaultKeepCarriedOnDeath));

            definition.CopyFrom(new ExtractionSpec(200f, 4f, 3, 20f, true));
            var spec = definition.ToSpec();
            Assert.That(spec.RaidSeconds, Is.EqualTo(200f));
            Assert.That(spec.ExtractHoldSeconds, Is.EqualTo(4f));
            Assert.That(spec.OpenPoints, Is.EqualTo(3));
            Assert.That(spec.OpenAfterSeconds, Is.EqualTo(20f));
            Assert.That(spec.KeepCarriedOnDeath, Is.True);

            definition.OpenAfterSeconds = 199f;
            Assert.Throws<InvalidOperationException>(() => definition.ToSpec(), "the exit would open too late to hold it before the raid ends");
            Assert.Throws<ArgumentNullException>(() => definition.CopyFrom(null));
        }

        [Test]
        public void ARaidNeedsAnExitWithAnIdOfItsOwn()
        {
            var player = Member("Player", Vector3.zero);

            _runner.SetZones(new[] { Zone("Exit-A", "same", _northAt), Zone("Exit-B", "same", _southAt) });
            Assert.Throws<InvalidOperationException>(() => _runner.BeginRaid(new[] { player }, Array.Empty<SideSpawnPoint>(), player));

            _runner.SetZones(new[] { Zone("Exit-Blank", " ", _northAt) });
            Assert.Throws<InvalidOperationException>(() => _runner.BeginRaid(new[] { player }, Array.Empty<SideSpawnPoint>(), player));

            Assert.That(_runner.IsStarted, Is.False);
        }

        [Test]
        public void TheHostileSideMustDifferFromThePlayers()
        {
            var player = Member("Player", Vector3.zero);
            _runner.SetZones(new[] { Zone("Exit-North", "north", _northAt) });
            _runner.HostileSide = _runner.PlayerSide;

            Assert.Throws<InvalidOperationException>(() => _runner.BeginRaid(new[] { player }, Array.Empty<SideSpawnPoint>(), player));
            Assert.That(_runner.IsStarted, Is.False);
        }

        [Test]
        public void ASessionMadeElsewhereIsRefused()
        {
            var player = Member("Player", Vector3.zero);
            var session = FreeForAllMode.Create(60f, 10, 1f, 1);

            Assert.Throws<ArgumentException>(() => _runner.Begin(session, new[] { player }, Array.Empty<SideSpawnPoint>(), player));
            Assert.That(_runner.IsStarted, Is.False);
        }

        [Test]
        public void StoppingClearsThePublishedStash()
        {
            var player = Member("Player", Vector3.zero);
            _runner.SetZones(new[] { Zone("Exit-North", "north", _northAt) });
            _runner.BeginRaid(new[] { player }, Array.Empty<SideSpawnPoint>(), player);
            Assert.That(GameSignals.Stash.HasValue, Is.True, "the stash is shown while raiding");

            _runner.Stop();

            Assert.That(GameSignals.Stash.HasValue, Is.False);
            Assert.That(_runner.Extraction, Is.Null);
        }

        private static ItemCatalog Catalog()
        {
            return new ItemCatalog(new[] { new ItemSpec(_ammo, maxStack: 30), new ItemSpec(_bandage, maxStack: 5) }, InventoryRule.Default);
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

        private ExtractionStatus Detail()
        {
            Assert.That(_runner.Status, Is.Not.Null);
            Assert.That(_runner.Status.Detail, Is.InstanceOf<ExtractionStatus>());
            return (ExtractionStatus)_runner.Status.Detail;
        }

        private SideMember Member(string name, Vector3 position, Side? side = null)
        {
            var go = Track(new GameObject(name));
            go.transform.position = position;
            go.AddComponent<Damageable>().Initialize(name, Damageable.DefaultMaxHealth);
            var member = go.AddComponent<SideMember>();
            member.Side = side ?? Side.Alpha;
            return member;
        }

        private LootContainer Container(string name, Vector3 position, string tableId)
        {
            var go = Track(new GameObject(name));
            go.transform.position = position;
            var container = go.AddComponent<LootContainer>();
            container.TableId = tableId;
            return container;
        }

        private ExtractionZone Zone(string name, string pointId, Vector3 position)
        {
            var go = Track(new GameObject(name));
            go.transform.position = position;
            var zone = go.AddComponent<ExtractionZone>();
            zone.PointId = pointId;
            return zone;
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }
    }
}
