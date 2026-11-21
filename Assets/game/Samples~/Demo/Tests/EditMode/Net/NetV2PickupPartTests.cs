using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Net.Tests
{
    public sealed class NetV2PickupPartTests
    {
        private static readonly int _baseColorId = Shader.PropertyToID("_BaseColor");

        private readonly List<int> _sent = new List<int>();
        private readonly List<string> _logs = new List<string>();
        private readonly List<Object> _created = new List<Object>();
        private NetV2PickupVisuals _visuals;
        private bool _pressed;
        private bool _canAct;
        private Vector3? _position;
        private NetV2PickupPart _part;

        [SetUp]
        public void SetUp()
        {
            _sent.Clear();
            _logs.Clear();
            _pressed = false;
            _canAct = true;
            _position = Vector3.zero;
            _visuals = new NetV2PickupVisuals();
            _part = new NetV2PickupPart(Send, () => _canAct, _visuals, () => _position, () => _pressed, _logs.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _part.Dispose();
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
        public void PressingInteractNextToAPickupSendsAPickupRequestForIt()
        {
            var snapshot = new SnapshotMessage();
            AddItem(snapshot, 3, ObjectiveItemKind.WeaponPickup, new System.Numerics.Vector3(0.5f, 0f, 0f), false, 1f);
            AddItem(snapshot, 4, ObjectiveItemKind.AmmoCrate, new System.Numerics.Vector3(0f, 0f, 5f), false, 1.6f);
            _part.Apply(snapshot);

            _part.Tick(0.016f);
            Assert.That(_sent, Is.Empty, "nothing is sent without pressing Interact");

            _pressed = true;
            _part.Tick(0.016f);

            Assert.That(_sent, Is.EqualTo(new[] { 3 }));
            Assert.That(_logs, Does.Contain("[Pickup] net collect request item=3 kind=WeaponPickup distance=0.50 sent=True"));
        }

        [Test]
        public void PressingInteractWhileUnableToActSendsNothing()
        {
            ApplyOne(ObjectiveItemKind.HealthPickup, opened: false);
            _pressed = true;
            _canAct = false;

            _part.Tick(0.016f);

            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public void APlaceholderPickupIsAColouredBoxWithoutColliders()
        {
            ApplyOne(ObjectiveItemKind.HealthPickup, opened: false);

            var root = _part.ViewOf(5);
            Assert.That(root, Is.Not.Null);
            Assert.That(root.transform.position, Is.EqualTo(new Vector3(0.5f, 0f, 0f)));
            var visual = root.transform.Find(Pickup.VisualName);
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.localPosition, Is.EqualTo(new Vector3(0f, Pickup.VisualHeight, 0f)));
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty, "the server's movement does not collide with it");
            AssertColor(ColorOf(visual), Pickup.ColorOf(PickupKind.Health));
            Assert.That(_logs, Does.Contain("[Pickup] net look item=5 kind=HealthPickup prefab=False"));
        }

        [Test]
        public void APlaceholderWeaponIsShapedLikeAGun()
        {
            ApplyOne(ObjectiveItemKind.WeaponPickup, opened: false);

            var visual = _part.ViewOf(5).transform.Find(Pickup.VisualName);
            Assert.That(visual.Find("Body"), Is.Not.Null);
            Assert.That(visual.Find("Barrel"), Is.Not.Null);
            Assert.That(visual.Find("Grip"), Is.Not.Null);
        }

        [Test]
        public void APlaceholderAmmoCrateIsTheOfflineCrateBox()
        {
            ApplyOne(ObjectiveItemKind.AmmoCrate, opened: false);

            var visual = _part.ViewOf(5).transform.Find(AmmoCrate.VisualName);
            Assert.That(visual.localScale, Is.EqualTo(AmmoCrate.DefaultVisualSize));
            AssertColor(ColorOf(visual), AmmoCrate.DefaultVisualColor);
        }

        [Test]
        public void ATakenPickupIsHiddenAndShownAgainWhenItComesBack()
        {
            ApplyOne(ObjectiveItemKind.AmmoPickup, opened: false);
            var root = _part.ViewOf(5);

            ApplyOne(ObjectiveItemKind.AmmoPickup, opened: true);
            Assert.That(_part.ViewOf(5), Is.SameAs(root));
            Assert.That(root.activeSelf, Is.False);

            ApplyOne(ObjectiveItemKind.AmmoPickup, opened: false);
            Assert.That(root.activeSelf, Is.True);
        }

        [Test]
        public void ACoolingAmmoCrateStaysVisible()
        {
            ApplyOne(ObjectiveItemKind.AmmoCrate, opened: true);

            Assert.That(_part.ViewOf(5).activeSelf, Is.True);
        }

        [Test]
        public void AReplacedLookIsUsedWithoutItsColliders()
        {
            _visuals.AmmoCrateVisual = NewPrefab("Crate");
            ApplyOne(ObjectiveItemKind.AmmoCrate, opened: false);

            var visual = _part.ViewOf(5).transform.Find(Pickup.VisualName);
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.localPosition, Is.EqualTo(Vector3.zero), "a crate stands on its base");
            Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(_logs, Does.Contain("[Pickup] net look item=5 kind=AmmoCrate prefab=True"));
        }

        [Test]
        public void AnItemThatLeavesTheSnapshotIsRemovedAndAnotherKindUnderTheSameIdIsRebuilt()
        {
            ApplyOne(ObjectiveItemKind.AmmoPickup, opened: false);
            var before = _part.ViewOf(5);

            ApplyOne(ObjectiveItemKind.HealthPickup, opened: false);
            Assert.That(before == null, Is.True);
            Assert.That(_part.ViewOf(5).name, Does.Contain("HealthPickup"));

            _part.Apply(new SnapshotMessage());
            Assert.That(_part.ViewOf(5), Is.Null);
            Assert.That(_part.ViewCount, Is.EqualTo(0));
        }

        [Test]
        public void BoxesAndFlagsAreLeftToTheOtherParts()
        {
            var snapshot = new SnapshotMessage();
            AddItem(snapshot, 1, ObjectiveItemKind.LootBox, System.Numerics.Vector3.Zero, false, 1.6f);
            AddItem(snapshot, 2, ObjectiveItemKind.Flag, System.Numerics.Vector3.Zero, false, 0f);

            _part.Apply(snapshot);

            Assert.That(_part.ViewCount, Is.EqualTo(0));
        }

        [Test]
        public void DisposingRemovesTheViews()
        {
            ApplyOne(ObjectiveItemKind.HealthPickup, opened: false);
            var root = _part.ViewOf(5);

            _part.Dispose();

            Assert.That(root == null, Is.True);
            Assert.That(_part.ViewCount, Is.EqualTo(0));
        }

        private void ApplyOne(ObjectiveItemKind kind, bool opened)
        {
            var snapshot = new SnapshotMessage();
            AddItem(snapshot, 5, kind, new System.Numerics.Vector3(0.5f, 0f, 0f), opened, 1f);
            _part.Apply(snapshot);
        }

        private static void AddItem(
            SnapshotMessage snapshot, int id, ObjectiveItemKind kind, System.Numerics.Vector3 position, bool opened, float radius)
        {
            ref var row = ref snapshot.ObjectiveItems.Add();
            row.ItemId = id;
            row.Present = ObjectiveItemFields.All;
            row.Kind = kind;
            row.Position = position;
            row.Opened = opened;
            row.Radius = radius;
        }

        private GameObject NewPrefab(string name)
        {
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefab.name = name;
            _created.Add(prefab);
            return prefab;
        }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(1e-3f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(1e-3f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(1e-3f));
        }

        private static Color ColorOf(Transform visual)
        {
            var block = new MaterialPropertyBlock();
            visual.GetComponent<Renderer>().GetPropertyBlock(block);
            return block.GetColor(_baseColorId);
        }

        private bool Send(IMessage message)
        {
            _sent.Add(((PickupRequestMessage)message).ItemId);
            return true;
        }
    }
}
