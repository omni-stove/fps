using System.Collections.Generic;
using NUnit.Framework;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Net.Tests
{
    public sealed class NetV2LootPartTests
    {
        private static readonly int _baseColorId = Shader.PropertyToID("_BaseColor");

        private readonly List<int> _sent = new List<int>();
        private readonly List<string> _logs = new List<string>();
        private readonly List<Object> _created = new List<Object>();
        private NetV2LootVisuals _visuals;
        private bool _pressed;
        private bool _canAct;
        private Vector3? _position;
        private NetV2LootPart _part;

        [SetUp]
        public void SetUp()
        {
            _sent.Clear();
            _logs.Clear();
            _pressed = false;
            _canAct = true;
            _position = Vector3.zero;
            _visuals = new NetV2LootVisuals();
            _part = new NetV2LootPart(Send, () => _canAct, _visuals, () => _position, () => _pressed, _logs.Add);
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
        public void PressingInteractNextToABoxSendsAnInteractRequestForIt()
        {
            var snapshot = new SnapshotMessage();
            AddItem(snapshot, 3, ObjectiveItemKind.LootBox, new System.Numerics.Vector3(1f, 0f, 0f), false, 1.6f);
            AddItem(snapshot, 4, ObjectiveItemKind.LootBox, new System.Numerics.Vector3(0f, 0f, 5f), false, 1.6f);
            _part.Apply(snapshot);

            _part.Tick(0.016f);
            Assert.That(_sent, Is.Empty, "nothing is sent without pressing Interact");

            _pressed = true;
            _part.Tick(0.016f);

            Assert.That(_sent, Is.EqualTo(new[] { 3 }));
            Assert.That(_logs, Does.Contain("[Loot] net open request item=3 kind=LootBox distance=1.00 sent=True"));
        }

        [Test]
        public void PressingInteractWhileUnableToActSendsNothing()
        {
            ApplyOneBox(opened: false);
            _pressed = true;
            _canAct = false;

            _part.Tick(0.016f);

            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public void APlaceholderBoxIsBuiltLikeLootContainerWithoutColliders()
        {
            ApplyOneBox(opened: false);

            var root = _part.ViewOf(5);
            Assert.That(root, Is.Not.Null);
            Assert.That(root.transform.position, Is.EqualTo(new Vector3(0.5f, 0f, 0f)));
            var visual = root.transform.Find(LootContainer.VisualName);
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.localScale, Is.EqualTo(LootContainer.DefaultVisualSize));
            Assert.That(visual.GetComponent<Collider>().enabled, Is.False, "the server's movement does not collide with it");
            AssertColor(ColorOf(visual), LootContainer.DefaultClosedColor);
            Assert.That(_logs, Does.Contain("[Loot] net look item=5 kind=LootBox prefab=False"));
        }

        [Test]
        public void APlaceholderBoxTurnsToTheOpenedColourWhenTheServerOpensIt()
        {
            ApplyOneBox(opened: false);
            var root = _part.ViewOf(5);

            ApplyOneBox(opened: true);

            Assert.That(_part.ViewOf(5), Is.SameAs(root));
            Assert.That(root.GetComponent<LootContainer>().IsOpened, Is.True);
            AssertColor(ColorOf(root.transform.Find(LootContainer.VisualName)), LootContainer.DefaultOpenedColor);
        }

        [Test]
        public void AReplacedBoxSwapsToItsOpenedLookAfterTheRequestedBoxOpens()
        {
            var closed = NewPrefab("ClosedBox");
            var opened = NewPrefab("OpenedBox");
            _visuals.BoxVisual = closed;
            _visuals.OpenedBoxVisual = opened;
            ApplyOneBox(opened: false);
            _pressed = true;
            _part.Tick(0.016f);

            ApplyOneBox(opened: true);

            var root = _part.ViewOf(5).transform;
            Assert.That(root.Find(LootContainer.VisualName).gameObject.activeSelf, Is.False);
            Assert.That(root.Find(LootContainer.VisualName + "-Opened").gameObject.activeSelf, Is.True);
            Assert.That(root.Find(LootContainer.VisualName + "-Opened").GetComponent<Collider>().enabled, Is.False);
            Assert.That(_logs, Does.Contain("[Loot] net look item=5 kind=LootBox prefab=True"));
            Assert.That(_logs, Does.Contain("[Loot] net opened item=5 kind=LootBox"));
        }

        [Test]
        public void ASupplyCrateUsesTheCrateLook()
        {
            _visuals.BoxVisual = NewPrefab("MapBox");
            _visuals.CrateVisual = NewPrefab("Crate");
            var snapshot = new SnapshotMessage();
            AddItem(snapshot, 8, ObjectiveItemKind.SupplyCrate, new System.Numerics.Vector3(0f, 30f, 0f), false, 2f);

            _part.Apply(snapshot);

            var root = _part.ViewOf(8);
            Assert.That(root.GetComponent<LootContainer>().Reach, Is.EqualTo(2f));
            Assert.That(root.transform.position.y, Is.EqualTo(30f), "a crate that is still falling is shown where the server has it");
            Assert.That(_logs, Does.Contain("[Loot] net look item=8 kind=SupplyCrate prefab=True"));
        }

        [Test]
        public void AnExtractionPointIsShownLikeExtractionZoneAndFollowsItsOpenState()
        {
            var snapshot = new SnapshotMessage();
            AddItem(snapshot, 6, ObjectiveItemKind.ExtractionPoint, new System.Numerics.Vector3(10f, 0f, 2f), false, 4f);
            _part.Apply(snapshot);

            var zone = _part.ViewOf(6).GetComponent<ExtractionZone>();
            Assert.That(zone, Is.Not.Null);
            Assert.That(zone.Radius, Is.EqualTo(4f));
            Assert.That(zone.IsOpen, Is.False);
            var closed = zone.transform.Find(ExtractionZone.VisualName);
            Assert.That(closed.localScale, Is.EqualTo(new Vector3(8f, ExtractionZone.PlaceholderThickness, 8f)));
            Assert.That(closed.gameObject.activeSelf, Is.True);

            snapshot.ObjectiveItems[0].Opened = true;
            _part.Apply(snapshot);

            Assert.That(zone.IsOpen, Is.True);
            Assert.That(zone.transform.Find(ExtractionZone.VisualName + "-Open").gameObject.activeSelf, Is.True);
            Assert.That(closed.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void ItemsTheServerNoLongerListsAreRemoved()
        {
            ApplyOneBox(opened: false);
            var root = _part.ViewOf(5);

            _part.Apply(new SnapshotMessage());

            Assert.That(_part.ViewOf(5), Is.Null);
            Assert.That(root == null, Is.True);
            Assert.That(_part.ViewCount, Is.EqualTo(0));
        }

        [Test]
        public void AClosedBoxUnderAnOpenedBoxIdIsBuiltAgain()
        {
            ApplyOneBox(opened: true);
            var before = _part.ViewOf(5);

            ApplyOneBox(opened: false);

            Assert.That(before == null, Is.True, "the next match reuses the id for another box");
            Assert.That(_part.ViewOf(5).GetComponent<LootContainer>().IsOpened, Is.False);
        }

        [Test]
        public void DisposingRemovesTheViews()
        {
            ApplyOneBox(opened: false);
            var root = _part.ViewOf(5);

            _part.Dispose();

            Assert.That(root == null, Is.True);
            Assert.That(_part.ViewCount, Is.EqualTo(0));
        }

        private void ApplyOneBox(bool opened)
        {
            var snapshot = new SnapshotMessage();
            AddItem(snapshot, 5, ObjectiveItemKind.LootBox, new System.Numerics.Vector3(0.5f, 0f, 0f), opened, 1.6f);
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
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(1e-3f));
        }

        private static Color ColorOf(Transform visual)
        {
            var block = new MaterialPropertyBlock();
            visual.GetComponent<Renderer>().GetPropertyBlock(block);
            return block.GetColor(_baseColorId);
        }

        private bool Send(IMessage message)
        {
            _sent.Add(((InteractRequestMessage)message).ItemId);
            return true;
        }
    }
}
