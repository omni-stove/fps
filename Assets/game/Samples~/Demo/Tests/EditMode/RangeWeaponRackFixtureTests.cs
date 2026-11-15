using System;
using System.Collections.Generic;
using System.Linq;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>射撃場で拾える武器の棚 (<see cref="RangeWeaponRackFixture"/>) を確かめる。</summary>
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
                var points = placed.GetComponentsInChildren<PickupSpawnPoint>();
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
