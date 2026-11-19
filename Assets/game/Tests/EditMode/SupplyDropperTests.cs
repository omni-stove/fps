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
    public sealed class SupplyDropperTests
    {
        private const float _tolerance = 1e-3f;
        private const string _item = "5b0c55a4-8f0e-4a43-9c55-0b1f3ad1e001";

        private readonly List<Object> _objects = new List<Object>();
        private SupplyDropper _dropper;

        [SetUp]
        public void SetUp()
        {
            _dropper = Track(new GameObject("SupplyDrops")).AddComponent<SupplyDropper>();
            _dropper.Seed = 5;
        }

        [TearDown]
        public void TearDown()
        {
            _dropper.Stop();
            for (var i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
            foreach (var pickup in Object.FindObjectsByType<Pickup>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(pickup.gameObject);
            }
        }

        [Test]
        public void CratesFallIntoTheNextCircleAtTheScheduledTimes()
        {
            Floor(Vector3.zero, 2f);
            var zone = Zone(Vector3.zero, 30f, new ShrinkingZone.PhaseSettings(10f, 5f, 20f, 1f));
            _dropper.Spec = new SupplyDropSpec(new[] { new SupplyDropStage(2, 1f, 3f) }, dropHeight: 10f, fallSpeed: 5f, placeFraction: 0.5f);
            var dropped = new List<SupplyCrate>();
            var landed = new List<SupplyCrate>();
            _dropper.Dropped += dropped.Add;
            _dropper.Landed += landed.Add;

            LogAssert.Expect(LogType.Log, "[SupplyDrop] stage phase=1 crates=2");
            _dropper.Begin(zone);

            Assert.That(_dropper.IsRunning, Is.True);
            Assert.That(_dropper.Schedule.StageIndex, Is.EqualTo(0));
            _dropper.Tick(0.9f);
            Assert.That(_dropper.Crates.Count, Is.Zero, "the first crate waits for the delay");

            LogAssert.Expect(LogType.Log, new Regex(@"^\[SupplyDrop\] dropped crate=1 at=\(.+,12\.00,.+\) ground=2\.00$"));
            _dropper.Tick(0.1f);

            Assert.That(dropped.Count, Is.EqualTo(1));
            var crate = dropped[0];
            Assert.That(crate.Number, Is.EqualTo(1));
            Assert.That(crate.HasLanded, Is.False);
            Assert.That(crate.GroundHeight, Is.EqualTo(2f).Within(_tolerance), "the crate aims for the top of the floor");
            Assert.That(crate.Position.y, Is.EqualTo(12f).Within(_tolerance), "it starts the drop height above the ground");
            var target = zone.State.TargetCenter;
            var offset = new Vector2(crate.Position.x - target.X, crate.Position.z - target.Y);
            Assert.That(offset.magnitude, Is.LessThanOrEqualTo((zone.State.TargetRadius * 0.5f) + _tolerance), "it falls inside half the next circle");

            _dropper.Tick(1f);
            Assert.That(crate.Position.y, Is.EqualTo(7f).Within(_tolerance));
            Assert.That(crate.HasLanded, Is.False);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[SupplyDrop\] landed crate=1 at=\(.+,2\.00,.+\)$"));
            _dropper.Tick(1f);
            Assert.That(crate.HasLanded, Is.True);
            Assert.That(crate.Position.y, Is.EqualTo(2f).Within(_tolerance), "it stops on the ground, not below it");
            Assert.That(landed, Is.EqualTo(new[] { crate }));

            _dropper.Tick(1f);
            Assert.That(dropped.Count, Is.EqualTo(2), "the second follows the interval after the first");
            Assert.That(dropped[1].Number, Is.EqualTo(2));
            _dropper.Tick(30f);
            Assert.That(dropped.Count, Is.EqualTo(2), "no more than the stage holds");
        }

        [Test]
        public void AStageWithNoWaitStillStartsItsDrops()
        {
            Floor(Vector3.zero, 0f);
            var zone = Zone(
                Vector3.zero,
                30f,
                new ShrinkingZone.PhaseSettings(1f, 1f, 20f, 1f),
                new ShrinkingZone.PhaseSettings(0f, 1f, 10f, 1f));
            _dropper.Spec = new SupplyDropSpec(
                new[] { new SupplyDropStage(1, 100f, 1f), new SupplyDropStage(3, 100f, 1f) },
                dropHeight: 5f,
                fallSpeed: 10f);

            LogAssert.Expect(LogType.Log, "[SupplyDrop] stage phase=1 crates=1");
            _dropper.Begin(zone);
            Assert.That(_dropper.Schedule.StageIndex, Is.EqualTo(0));

            LogAssert.Expect(LogType.Log, "[SupplyDrop] stage phase=2 crates=3");
            for (var i = 0; i < 5; i++)
            {
                zone.Tick(0.5f);
            }

            Assert.That(_dropper.Schedule.StageIndex, Is.EqualTo(1), "a stage that skips Waiting still begins");
        }

        [Test]
        public void ALandedCrateOpensOnceAndSetsItsContentsOnTheGround()
        {
            Floor(Vector3.zero, 0f);
            var zone = Zone(Vector3.zero, 30f, new ShrinkingZone.PhaseSettings(10f, 5f, 20f, 1f));
            _dropper.Spec = new SupplyDropSpec(new[] { new SupplyDropStage(1, 0f, 1f) }, dropHeight: 5f, fallSpeed: 10f, reach: 2f);
            _dropper.SetFixedContents(new[] { PickupContents.ForHealth(25), PickupContents.ForReserveAmmo(30) });
            _dropper.Begin(zone);
            _dropper.Tick(0f);
            var crate = _dropper.Crates[0];
            var beside = crate.Position + new Vector3(1.5f, 0f, 0f);

            Assert.That(_dropper.NearestOpenable(beside), Is.Null, "a falling crate cannot be opened");
            Assert.Throws<InvalidOperationException>(() => crate.Open(0, null, new System.Random(1), null));

            _dropper.Tick(1f);
            Assert.That(crate.HasLanded, Is.True);
            Assert.That(_dropper.NearestOpenable(beside), Is.SameAs(crate));
            Assert.That(_dropper.NearestOpenable(crate.Position + new Vector3(3f, 0f, 0f)), Is.Null, "out of reach");

            LogAssert.Expect(LogType.Log, "[SupplyDrop] opened crate=1 by=4 items=- extras=2");
            var pickups = _dropper.Open(crate, 4);

            Assert.That(crate.IsOpened, Is.True);
            Assert.That(pickups.Count, Is.EqualTo(2));
            Assert.That(pickups[0].Spec.Kind, Is.EqualTo(PickupKind.Health));
            Assert.That(pickups[1].Spec.Kind, Is.EqualTo(PickupKind.ReserveAmmo));
            foreach (var pickup in pickups)
            {
                var flat = new Vector2(pickup.transform.position.x - crate.Position.x, pickup.transform.position.z - crate.Position.z);
                Assert.That(flat.magnitude, Is.EqualTo(SupplyCrate.SpreadRadius).Within(_tolerance), "set around the crate, not inside it");
                Assert.That(pickup.transform.position.y, Is.EqualTo(0f).Within(_tolerance));
            }

            Assert.That(_dropper.NearestOpenable(beside), Is.Null, "an opened crate does not open again");
            Assert.Throws<InvalidOperationException>(() => _dropper.Open(crate, 4));
            var marks = new List<SupplyDropMark>();
            _dropper.CollectMarks(marks);
            Assert.That(marks, Is.Empty, "an opened crate is no longer shown");
        }

        [Test]
        public void TheLootTableAddsItsItemsBeforeTheFixedContents()
        {
            Floor(Vector3.zero, 0f);
            var zone = Zone(Vector3.zero, 30f, new ShrinkingZone.PhaseSettings(10f, 5f, 20f, 1f));
            _dropper.Spec = new SupplyDropSpec(new[] { new SupplyDropStage(1, 0f, 1f) }, tableId: "supply", dropHeight: 1f, fallSpeed: 10f);
            _dropper.Loot = new LootSpec(new[] { new LootTable("supply", new[] { new LootEntry(_item, 1f, 2, 2) }) });
            _dropper.SetFixedContents(new[] { PickupContents.ForHealth(25) });
            _dropper.Begin(zone);
            _dropper.Tick(0f);
            _dropper.Tick(1f);

            LogAssert.Expect(LogType.Log, "[SupplyDrop] opened crate=1 by=0 items=" + _item + "x2 extras=1");
            var pickups = _dropper.Open(_dropper.Crates[0], 0);

            Assert.That(pickups.Count, Is.EqualTo(2));
            Assert.That(pickups[0].Spec.Kind, Is.EqualTo(PickupKind.Item));
            Assert.That(pickups[0].Spec.ItemId, Is.EqualTo(_item));
            Assert.That(pickups[0].Spec.Amount, Is.EqualTo(2));
            Assert.That(pickups[1].Spec.Kind, Is.EqualTo(PickupKind.Health));
        }

        [Test]
        public void AnUnknownTableIsReportedOnceAndTheCratesStillOpen()
        {
            Floor(Vector3.zero, 0f);
            var zone = Zone(Vector3.zero, 30f, new ShrinkingZone.PhaseSettings(10f, 5f, 20f, 1f));
            _dropper.Spec = new SupplyDropSpec(new[] { new SupplyDropStage(1, 0f, 1f) }, tableId: "missing", dropHeight: 1f, fallSpeed: 10f);
            _dropper.Loot = LootSpec.Default;

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[SupplyDrop\] tableId=missing is not in the loot tables"));
            _dropper.Begin(zone);
            _dropper.Tick(0f);
            _dropper.Tick(1f);

            Assert.That(_dropper.Open(_dropper.Crates[0], 0), Is.Empty);
            Assert.That(_dropper.Crates[0].IsOpened, Is.True);
        }

        [Test]
        public void EachNewCircleStartsItsOwnStageAndTheLastCircleStopsTheDrops()
        {
            Floor(Vector3.zero, 0f);
            var zone = Zone(
                Vector3.zero,
                30f,
                new ShrinkingZone.PhaseSettings(10f, 5f, 20f, 1f),
                new ShrinkingZone.PhaseSettings(10f, 5f, 10f, 2f));
            _dropper.Spec = new SupplyDropSpec(new[] { new SupplyDropStage(0, 0f, 1f), new SupplyDropStage(3, 100f, 1f) });
            _dropper.Begin(zone);

            Assert.That(_dropper.Schedule.IsDropping, Is.False, "the first stage drops nothing");

            LogAssert.Expect(LogType.Log, "[SupplyDrop] stage phase=2 crates=3");
            zone.Tick(15.5f);

            Assert.That(_dropper.Schedule.StageIndex, Is.EqualTo(1));
            Assert.That(_dropper.Schedule.Remaining, Is.EqualTo(3));

            zone.Tick(20f);
            Assert.That(zone.State.Stage, Is.EqualTo(ZoneStage.Closed));
            Assert.That(_dropper.Schedule.IsDropping, Is.False, "after the last circle there is no next one to drop into");
            _dropper.Tick(200f);
            Assert.That(_dropper.Crates, Is.Empty);
        }

        [Test]
        public void WithoutGroundTheCrateLandsAtTheZoneHeight()
        {
            // 他のテストの床に当たらないよう、遠く離れた所で落とす。
            var zone = Zone(new Vector3(5000f, 3f, 5000f), 30f, new ShrinkingZone.PhaseSettings(10f, 5f, 20f, 1f));
            _dropper.Spec = new SupplyDropSpec(new[] { new SupplyDropStage(1, 0f, 1f) }, dropHeight: 4f, fallSpeed: 10f);
            _dropper.Begin(zone);

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[SupplyDrop\] crate=1 found no ground below"));
            _dropper.Tick(0f);

            var crate = _dropper.Crates[0];
            Assert.That(crate.GroundHeight, Is.EqualTo(3f).Within(_tolerance));
            Assert.That(crate.Position.y, Is.EqualTo(7f).Within(_tolerance));
        }

        [Test]
        public void StoppingLetsFallingCratesLandButDropsNoMore()
        {
            Floor(Vector3.zero, 0f);
            var zone = Zone(Vector3.zero, 30f, new ShrinkingZone.PhaseSettings(10f, 5f, 20f, 1f));
            _dropper.Spec = new SupplyDropSpec(new[] { new SupplyDropStage(3, 0f, 1f) }, dropHeight: 10f, fallSpeed: 5f);
            _dropper.Begin(zone);
            _dropper.Tick(0f);
            _dropper.Stop();

            Assert.That(_dropper.IsRunning, Is.False);
            _dropper.Tick(5f);

            Assert.That(_dropper.Crates.Count, Is.EqualTo(1));
            Assert.That(_dropper.Crates[0].HasLanded, Is.True, "a crate in the air is not left hanging");

            _dropper.ClearCrates();
            Assert.That(_dropper.Crates, Is.Empty);
        }

        [Test]
        public void TheCrateUsesTheReplacementLookWhenOneIsGiven()
        {
            Floor(Vector3.zero, 0f);
            var look = Track(new GameObject("CrateLook"));
            _dropper.CrateVisualPrefab = look;

            var crate = _dropper.DropAt(new Vector3(1f, 0f, 1f), new SupplyDropSpec(dropHeight: 1f));

            var visual = crate.transform.Find(LootContainer.VisualName);
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.GetComponent<MeshFilter>(), Is.Null, "the replacement is used instead of a built box");
            Assert.That(crate.Container.VisualPrefab, Is.SameAs(look));
            Assert.That(crate.Container.Reach, Is.EqualTo(SupplyDropSpec.DefaultReach));

            _dropper.CrateVisualPrefab = null;
            var built = _dropper.DropAt(new Vector3(3f, 0f, 1f), new SupplyDropSpec(dropHeight: 1f));
            Assert.That(built.transform.Find(LootContainer.VisualName).GetComponent<MeshFilter>(), Is.Not.Null, "without a look a box is built");
        }

        [Test]
        public void TheDropperRejectsInvalidUse()
        {
            var zone = Zone(Vector3.zero, 30f, new ShrinkingZone.PhaseSettings(10f, 5f, 20f, 1f));
            _dropper.Spec = SupplyDropSpec.Default;

            Assert.Throws<ArgumentNullException>(() => _dropper.Begin(null));
            _dropper.Begin(zone);
            Assert.Throws<InvalidOperationException>(() => _dropper.Begin(zone));
            Assert.Throws<ArgumentOutOfRangeException>(() => _dropper.Tick(-1f));
            Assert.Throws<ArgumentNullException>(() => _dropper.Open(null, 0));
            Assert.Throws<ArgumentNullException>(() => _dropper.SetFixedContents(null));
            Assert.Throws<ArgumentException>(() => _dropper.SetFixedContents(new PickupContents[] { null }));
            Assert.Throws<ArgumentNullException>(() => _dropper.CollectMarks(null));
            Assert.Throws<ArgumentNullException>(() => _dropper.CollectUnopenedLandingPoints(null));

            var stranger = Track(new GameObject("Stranger")).AddComponent<SupplyDropper>();
            var foreign = stranger.DropAt(new Vector3(5000f, 0f, -5000f), SupplyDropSpec.Default);
            Assert.Throws<ArgumentException>(() => _dropper.Open(foreign, 0));
        }

        [Test]
        public void TheTuningFileHoldsTheDefaults()
        {
            var asset = Resources.Load<TextAsset>(SupplyDropJson.ResourcePath);
            Assert.That(asset, Is.Not.Null, "the kit ships Resources/" + SupplyDropJson.ResourcePath + ".json");
            var spec = SupplyDropJson.Parse(asset.text, SupplyDropJson.ResourcePath);
            var defaults = SupplyDropSpec.Default;

            Assert.That(spec.Stages.Count, Is.EqualTo(defaults.Stages.Count));
            for (var i = 0; i < defaults.Stages.Count; i++)
            {
                Assert.That(spec.Stages[i].Count, Is.EqualTo(defaults.Stages[i].Count));
                Assert.That(spec.Stages[i].DelaySeconds, Is.EqualTo(defaults.Stages[i].DelaySeconds));
                Assert.That(spec.Stages[i].IntervalSeconds, Is.EqualTo(defaults.Stages[i].IntervalSeconds));
            }

            Assert.That(spec.TableId, Is.EqualTo(defaults.TableId));
            Assert.That(spec.DropHeight, Is.EqualTo(defaults.DropHeight));
            Assert.That(spec.FallSpeed, Is.EqualTo(defaults.FallSpeed));
            Assert.That(spec.PlaceFraction, Is.EqualTo(defaults.PlaceFraction));
            Assert.That(spec.Reach, Is.EqualTo(defaults.Reach));
            Assert.That(spec.BotSearchRadius, Is.EqualTo(defaults.BotSearchRadius));
        }

        private ShrinkingZone Zone(Vector3 center, float initialRadius, params ShrinkingZone.PhaseSettings[] phases)
        {
            var zone = Track(new GameObject("Zone")).AddComponent<ShrinkingZone>();
            zone.transform.position = center;
            zone.ShowsBoundary = false;
            zone.BeginsOnStart = false;
            zone.UseRandomSeed = false;
            zone.InitialRadius = initialRadius;
            zone.SetPhases(phases);
            Assert.That(zone.Begin(7), Is.True);
            return zone;
        }

        // 上面が top の高さに来る広い床。
        private void Floor(Vector3 center, float top)
        {
            var floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.name = "Floor";
            floor.transform.position = new Vector3(center.x, top - 0.5f, center.z);
            floor.transform.localScale = new Vector3(200f, 1f, 200f);
            Physics.SyncTransforms();
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }
    }
}
