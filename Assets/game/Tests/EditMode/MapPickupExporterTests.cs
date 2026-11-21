using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class MapPickupExporterTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Pickups");
            _created.Add(_root);
        }

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
        public void SpawnPointsAndAmmoCratesAreWrittenWithTheirSettings()
        {
            var medkit = AddPoint("Medkit", new Vector3(1f, 0.5f, 2f), PickupContents.ForHealth(40));
            medkit.CollectMode = PickupCollectMode.Interact;
            medkit.Radius = 1.5f;
            medkit.RespawnSeconds = 12f;
            AddPoint("Ammo", new Vector3(-2f, 0.5f, 0f), PickupContents.ForReserveAmmo(15));
            var crate = AddCrate("Crate", new Vector3(4f, 0.5f, -4f));
            crate.Radius = 2f;
            crate.CooldownSeconds = 3f;

            var pickups = MapPickupExporter.Collect(new[] { _root });

            Assert.That(pickups.Pickups.Count, Is.EqualTo(2));
            var first = pickups.Pickups[0];
            Assert.That(first.Id, Is.EqualTo("Pickups/Medkit"));
            Assert.That(first.Spec.Kind, Is.EqualTo(PickupKind.Health));
            Assert.That(first.Spec.Amount, Is.EqualTo(40));
            Assert.That(first.Position, Is.EqualTo(new System.Numerics.Vector3(1f, 0.5f, 2f)));
            Assert.That(first.Radius, Is.EqualTo(1.5f));
            Assert.That(first.RespawnSeconds, Is.EqualTo(12f));
            Assert.That(first.Touch, Is.False, "Interact waits for the pickup request");
            Assert.That(pickups.Pickups[1].Spec.Kind, Is.EqualTo(PickupKind.ReserveAmmo));
            Assert.That(pickups.Pickups[1].Touch, Is.True, "the default collect mode is Touch");
            Assert.That(pickups.Pickups[1].RespawnSeconds, Is.EqualTo(PickupSpawnPoint.DefaultRespawnSeconds));
            Assert.That(pickups.AmmoCrates.Count, Is.EqualTo(1));
            Assert.That(pickups.AmmoCrates[0].Id, Is.EqualTo("Pickups/Crate"));
            Assert.That(pickups.AmmoCrates[0].Radius, Is.EqualTo(2f));
            Assert.That(pickups.AmmoCrates[0].CooldownSeconds, Is.EqualTo(3f));
        }

        [Test]
        public void TwoObjectsWithTheSameNameGetDifferentIds()
        {
            AddPoint("Ammo", Vector3.zero, PickupContents.ForReserveAmmo(15));
            AddPoint("Ammo", Vector3.one, PickupContents.ForReserveAmmo(15));

            var pickups = MapPickupExporter.Collect(new[] { _root });

            Assert.That(pickups.Pickups[0].Id, Is.EqualTo("Pickups/Ammo"));
            Assert.That(pickups.Pickups[1].Id, Is.EqualTo("Pickups/Ammo#2"));
        }

        [Test]
        public void AnInactiveSpawnPointIsLeftOut()
        {
            AddPoint("Hidden", Vector3.zero, PickupContents.ForHealth(10)).gameObject.SetActive(false);

            Assert.That(MapPickupExporter.Collect(new[] { _root }).Pickups, Is.Empty);
        }

        [Test]
        public void ATagSpawnPointStopsTheExport()
        {
            AddPoint("Tag", Vector3.zero, PickupContents.ForTag(1, null));

            var error = Assert.Throws<InvalidOperationException>(() => MapPickupExporter.Collect(new[] { _root }));

            Assert.That(error.Message, Does.Contain("Pickups/Tag"));
        }

        [Test]
        public void TheWrittenFileReadsBack()
        {
            AddPoint("Medkit", new Vector3(1f, 0.5f, 2f), PickupContents.ForHealth(40));
            AddCrate("Crate", Vector3.zero);

            var text = NetMapPickups.Write(MapPickupExporter.Collect(new[] { _root }));
            var read = NetMapPickups.Parse(text, "Test" + NetMapPickups.Extension);

            Assert.That(read.Pickups[0].Id, Is.EqualTo("Pickups/Medkit"));
            Assert.That(read.AmmoCrates[0].Radius, Is.EqualTo(AmmoCrate.DefaultRadius));
            Assert.That(read.AmmoCrates[0].CooldownSeconds, Is.EqualTo(AmmoCrate.DefaultCooldownSeconds));
        }

        [Test]
        public void CollectRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => MapPickupExporter.Collect(null));
            Assert.Throws<ArgumentNullException>(() => MapPickupExporter.ToMapPickup(null, "id"));
        }

        private PickupSpawnPoint AddPoint(string name, Vector3 position, PickupContents contents)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            go.transform.position = position;
            var point = go.AddComponent<PickupSpawnPoint>();
            point.Contents = contents;
            return point;
        }

        private AmmoCrate AddCrate(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            go.transform.position = position;
            return go.AddComponent<AmmoCrate>();
        }
    }
}
