using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class RangeWeaponRackFixtureTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void TheRackIsFoundAmongTheRangeFixtures()
        {
            Assert.That(TpsRangeSceneBuilder.Fixtures().OfType<RangeWeaponRackFixture>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void PlacesEveryRangeWeaponAsALookPickupInOrder()
        {
            var range = WeaponAssetBuilder.LoadRangeWeapons();
            var scene = TestScenes.NewAdditive();
            try
            {
                var fixture = new RangeWeaponRackFixture();
                var placed = fixture.Place(scene, GameRules.Default);

                Assert.That(placed.name, Is.EqualTo(RangeWeaponRackFixture.RootName));
                Assert.That(placed.scene, Is.EqualTo(scene));
                var points = placed.GetComponentsInChildren<PickupSpawnPoint>().Where(p => p.Contents.Kind == PickupKind.Weapon).ToArray();
                Assert.That(points.Select(p => p.Contents.Weapon), Is.EqualTo(range.Weapons));
                for (var i = 0; i < points.Length; i++)
                {
                    Assert.That(points[i].transform.position, Is.EqualTo(RangeWeaponRackFixture.SlotPosition(i)));
                    Assert.That(points[i].CollectMode, Is.EqualTo(PickupCollectMode.Look));
                    Assert.That(points[i].Radius, Is.EqualTo(RangeWeaponRackFixture.PickupRadius));
                    Assert.That(points[i].Contents.Kind, Is.EqualTo(PickupKind.Weapon));
                    Assert.That(points[i].Contents.Amount, Is.EqualTo(range.Weapons[i].ToSpec().MagazineSize));
                    Assert.That(fixture.Footprint.Contains(points[i].transform.position + (Vector3.up * 0.5f)), Is.True, points[i].name);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TheOpticsOfTheTuningFollowTheWeaponsAsItemPickups()
        {
            var range = WeaponAssetBuilder.LoadRangeWeapons();
            var optics = RangeWeaponRackFixture.OpticItemIds(KitTuning.Items);
            Assert.That(
                optics,
                Is.EqualTo(new[] { "2451a5a1-24e5-45ed-bbc1-8b23276152f8", "a2c5e73b-69fa-4402-b0ef-3c4297973226", "79fb2f9e-fc61-425a-b651-8baa8d1a6b2b", "c8e4f2a1-7b3d-4e95-a6c0-1d2f3b4a5e67" }),
                "the red dot, the 4x scope, the holographic sight and the thermal scope");
            Assert.That(range.Weapons.Count + optics.Count, Is.LessThanOrEqualTo(RangeWeaponRackFixture.Columns * RangeWeaponRackFixture.Rows));

            var scene = TestScenes.NewAdditive();
            try
            {
                var fixture = new RangeWeaponRackFixture();
                var placed = fixture.Place(scene, GameRules.Default);

                var items = placed.GetComponentsInChildren<PickupSpawnPoint>().Where(p => p.Contents.Kind == PickupKind.Item).ToArray();
                Assert.That(items.Select(p => p.Contents.ItemId), Is.EqualTo(optics));
                for (var i = 0; i < items.Length; i++)
                {
                    Assert.That(items[i].transform.position, Is.EqualTo(RangeWeaponRackFixture.SlotPosition(range.Weapons.Count + i)));
                    Assert.That(items[i].Contents.Amount, Is.EqualTo(1));
                    Assert.That(items[i].CollectMode, Is.EqualTo(PickupCollectMode.Look));
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TheRackHoldsTheRiotShieldAndTheThermalScope()
        {
            var range = WeaponAssetBuilder.LoadRangeWeapons();
            var optics = RangeWeaponRackFixture.OpticItemIds(KitTuning.Items);

            Assert.That(range.TryFind(WeaponAssetBuilder.RiotShieldId, out var shield), Is.True, $"rerun {nameof(WeaponAssetBuilder)}.Build");
            Assert.That(shield.ToSpec().IsShield, Is.True);
            Assert.That(optics, Does.Contain(AttachmentModelCatalogAssetStep.ThermalScopeId));
            Assert.That(range.Weapons.Count + optics.Count, Is.LessThanOrEqualTo(RangeWeaponRackFixture.Columns * RangeWeaponRackFixture.Rows));

            var footprint = new RangeWeaponRackFixture().Footprint;
            Assert.That(footprint.max.x, Is.LessThan(TpsRangeSceneBuilder.FloorHalfExtent), "the wider rack still fits on the floor");
            Assert.That(footprint.max.z, Is.LessThan(TpsRangeSceneBuilder.FloorHalfExtent));
        }

        [Test]
        public void TooManyWeaponsAndOpticsPlaceNothing()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            _created.Add(weapon);
            weapon.AssignIdIfEmpty();
            var weapons = Enumerable.Repeat(weapon, RangeWeaponRackFixture.Columns * RangeWeaponRackFixture.Rows).ToArray();
            var scene = TestScenes.NewAdditive();
            try
            {
                Assert.Throws<InvalidOperationException>(() => RangeWeaponRackFixture.Place(scene, weapons, new[] { "2451a5a1-24e5-45ed-bbc1-8b23276152f8" }));

                Assert.That(scene.GetRootGameObjects(), Is.Empty);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void EverySlotIsInsideTheFootprintAndFarEnoughApartToPickOne()
        {
            var footprint = new RangeWeaponRackFixture().Footprint;
            var count = RangeWeaponRackFixture.Columns * RangeWeaponRackFixture.Rows;
            for (var i = 0; i < count; i++)
            {
                Assert.That(footprint.Contains(RangeWeaponRackFixture.SlotPosition(i) + (Vector3.up * 0.5f)), Is.True, i.ToString());
            }

            Assert.That(RangeWeaponRackFixture.ColumnSpacing, Is.GreaterThan(Pickup.DefaultRadius * 2f));
            Assert.That(RangeWeaponRackFixture.RowSpacing, Is.GreaterThan(Pickup.DefaultRadius * 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => RangeWeaponRackFixture.SlotPosition(count));
        }

        [Test]
        public void TooManyWeaponsPlaceNothing()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            _created.Add(weapon);
            weapon.AssignIdIfEmpty();
            var weapons = Enumerable.Repeat(weapon, (RangeWeaponRackFixture.Columns * RangeWeaponRackFixture.Rows) + 1).ToArray();
            var scene = TestScenes.NewAdditive();
            try
            {
                Assert.Throws<InvalidOperationException>(() => RangeWeaponRackFixture.Place(scene, weapons));

                Assert.That(scene.GetRootGameObjects(), Is.Empty);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
