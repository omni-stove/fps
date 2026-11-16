using System;
using System.Collections.Generic;
using System.Linq;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class InventoryRuntimeTests
    {
        private const string _bandageId = "31d5979e-6067-49d9-b70c-fdcf44dc77d1";
        private const string _caseId = "542515db-5705-497c-8fe6-076a6709131b";
        private const string _keyId = "6879cab0-d2cd-4ace-b51a-cc882b7c8aa9";

        private static readonly ItemSpec _bandage = new ItemSpec(_bandageId, "item.bandage", maxStack: 5, weight: 0.1f, useHealth: 15);
        private static readonly ItemSpec _case = new ItemSpec(_caseId, "item.toolbox", weight: 6f, width: 3, height: 2);
        private static readonly ItemSpec _key = new ItemSpec(_keyId, "item.keycard", maxCarried: 1);

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
            foreach (var pickup in Object.FindObjectsByType<Pickup>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(pickup.gameObject);
            }
        }

        [Test]
        public void InitializingGivesTheStartingItems()
        {
            var inventory = NewPlayer(Catalog(InventoryRule.Default)).GetComponent<PlayerInventory>();

            Assert.That(inventory.Inventory.CountOf(_bandageId), Is.EqualTo(2));
        }

        [Test]
        public void UsingABandageHealsAndTakesOne()
        {
            var player = NewPlayer(Catalog(InventoryRule.Default));
            var inventory = player.GetComponent<PlayerInventory>();
            player.TakeDamage(40);

            inventory.Use(inventory.Inventory.Stacks[0].Handle);

            Assert.That(player.Health.Current, Is.EqualTo(75));
            Assert.That(inventory.Inventory.CountOf(_bandageId), Is.EqualTo(1));
        }

        [Test]
        public void UsingAtFullHealthKeepsTheItem()
        {
            var player = NewPlayer(Catalog(InventoryRule.Default));
            var inventory = player.GetComponent<PlayerInventory>();

            inventory.Use(inventory.Inventory.Stacks[0].Handle);

            Assert.That(inventory.Inventory.CountOf(_bandageId), Is.EqualTo(2));
        }

        [Test]
        public void DroppingPutsTheItemsOnTheGroundAsAPickup()
        {
            var player = NewPlayer(Catalog(InventoryRule.Default));
            var inventory = player.GetComponent<PlayerInventory>();

            inventory.Drop(inventory.Inventory.Stacks[0].Handle, 2);

            Assert.That(inventory.Inventory.CountOf(_bandageId), Is.Zero);
            var pickup = Object.FindObjectsByType<Pickup>(FindObjectsInactive.Include).Single();
            Assert.That(pickup.Spec.Kind, Is.EqualTo(PickupKind.Item));
            Assert.That(pickup.Spec.ItemId, Is.EqualTo(_bandageId));
            Assert.That(pickup.Spec.Amount, Is.EqualTo(2));
            Assert.That(pickup.name, Is.EqualTo(Pickup.ObjectName(PickupKind.Item)));
        }

        [Test]
        public void TheItemWeightSlowsOnlyWhenTheRuleCountsIt()
        {
            var counted = NewPlayer(Catalog(new InventoryRule(countWeightInCarryLoad: true))).GetComponent<PlayerInventory>();
            var ignored = NewPlayer(Catalog(InventoryRule.Default)).GetComponent<PlayerInventory>();
            counted.Inventory.Add(_caseId, 1);
            ignored.Inventory.Add(_caseId, 1);
            var carry = new CarryLoadSpec(CarrySource.None, CarrySource.None, 0f, 0.05f, 0.2f, CarryGaits.All);

            // 包帯 2 個 (0.2 kg) とケース (6 kg) → 1 − 6.2 × 0.05
            Assert.That(counted.CarriedWeight, Is.EqualTo(6.2f).Within(1e-4f));
            Assert.That(new PlayerCarryLoad(null, counted).CarrySpeedMultiplier(carry), Is.EqualTo(0.69f).Within(1e-4f));
            Assert.That(ignored.CarriedWeight, Is.Zero);
            Assert.That(new PlayerCarryLoad(null, ignored).CarrySpeedMultiplier(carry), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void TouchingAnItemPickupPutsItIntoTheInventory()
        {
            var player = NewPlayer(Catalog(InventoryRule.Default));
            var pickup = Pickup.Create(PickupContents.ForItem(_caseId, 1), Vector3.zero);

            pickup.HandleEnter(player.GetComponent<Collider>());

            Assert.That(player.GetComponent<PlayerInventory>().Inventory.CountOf(_caseId), Is.EqualTo(1));
            Assert.That(pickup == null, Is.True, "destroyed once collected");
        }

        [Test]
        public void WhatDoesNotFitStaysOnTheGround()
        {
            // 2 枠まで。包帯は 1 山 5 個までなので、始めの 2 個と合わせて 10 個まで
            var player = NewPlayer(Catalog(new InventoryRule(maxStacks: 2)));
            var inventory = player.GetComponent<PlayerInventory>().Inventory;
            var first = Pickup.Create(PickupContents.ForItem(_bandageId, 6), Vector3.zero);

            first.HandleEnter(player.GetComponent<Collider>());

            Assert.That(inventory.CountOf(_bandageId), Is.EqualTo(8));
            Assert.That(first == null, Is.True, "everything fit");

            var second = Pickup.Create(PickupContents.ForItem(_bandageId, 6), Vector3.zero);
            second.HandleEnter(player.GetComponent<Collider>());

            Assert.That(inventory.CountOf(_bandageId), Is.EqualTo(10));
            Assert.That(second != null && second.IsAvailable, Is.True, "the rest stays");
            Assert.That(second.Spec.Amount, Is.EqualTo(4));
        }

        [Test]
        public void ThePromptNamesTheItem()
        {
            var player = NewPlayer(Catalog(InventoryRule.Default));
            var pickup = Pickup.Create(PickupContents.ForItem(_caseId, 1), Vector3.zero, PickupCollectMode.Interact);

            Assert.That(pickup.TryDescribe(player, "E", out var text), Is.True);
            Assert.That(text.Key, Is.EqualTo(PickupPrompt.ItemKey));
            Assert.That(Localizer.Resolve(text), Does.Contain("工具箱"));
        }

        [Test]
        public void TheLayoutHasTheInventoryArea()
        {
            Assert.That(CloneLayout().Q<VisualElement>(InventoryView.UxmlName), Is.Not.Null);
        }

        [Test]
        public void HudAddViewsAttachesTheInventoryScreen()
        {
            var hud = Hud.Create("InventoryRuntimeTests.Hud", null, null, null);
            _created.Add(hud.gameObject);
            _created.Add(hud.OwnedPanelSettings);

            Assert.That(hud.InventoryScreen, Is.Not.Null, "Hud.AddViews attaches the inventory screen");
        }

        [Test]
        public void TheScreenDoesNotOpenWithoutASource()
        {
            var view = CreateBoundView(null);

            view.SetOpen(true);

            Assert.That(view.IsOpen, Is.False);
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void AGridIsDrawnWithEachStackAtItsPlaceAndSize()
        {
            var source = new FakeControl(Catalog(new InventoryRule(maxWeight: 30f, gridWidth: 8, gridHeight: 4)));
            source.Inventory.Add(_caseId, 1);
            var view = CreateBoundView(source);

            view.SetOpen(true);

            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(view.Element.Query(className: InventoryView.CellClass).ToList(), Has.Count.EqualTo(32));
            var boxes = view.Element.Query(className: InventoryView.ItemClass).ToList();
            Assert.That(boxes, Has.Count.EqualTo(2));
            var caseBox = boxes[1];
            Assert.That(caseBox.style.left.value.value, Is.EqualTo(InventoryView.CellSize));
            Assert.That(caseBox.style.width.value.value, Is.EqualTo(3 * InventoryView.CellSize));
            Assert.That(caseBox.style.height.value.value, Is.EqualTo(2 * InventoryView.CellSize));
            Assert.That(Labels(view, InventoryView.StatusClass), Is.EqualTo(new[] { "重さ 6.2 / 30 kg" }));
            Assert.That(Labels(view, InventoryView.ItemCountClass), Is.EqualTo(new[] { "×2" }));
        }

        [Test]
        public void WithoutAGridTheStacksAreListed()
        {
            var source = new FakeControl(Catalog(new InventoryRule(maxStacks: 4)));
            var view = CreateBoundView(source);

            view.SetOpen(true);

            Assert.That(view.Element.Query(className: InventoryView.CellClass).ToList(), Is.Empty);
            Assert.That(view.Element.Query(className: InventoryView.ListItemClass).ToList(), Has.Count.EqualTo(1));
            Assert.That(Labels(view, InventoryView.StatusClass), Is.EqualTo(new[] { "重さ 0.2 kg    1 / 4 枠" }));
        }

        [Test]
        public void SelectingAUsableItemOffersUseAndDrop()
        {
            var source = new FakeControl(Catalog(InventoryRule.Default));
            source.Inventory.Add(_keyId, 1);
            var view = CreateBoundView(source);
            view.SetOpen(true);

            view.Select(source.Inventory.Stacks[0].Handle);
            Assert.That(Buttons(view), Is.EqualTo(new[] { "使う", "1 個捨てる", "全部捨てる" }));

            view.Select(source.Inventory.Stacks[1].Handle);
            Assert.That(Buttons(view), Is.EqualTo(new[] { "1 個捨てる" }), "a key cannot be used and there is only one");
        }

        [Test]
        public void TheScreenRedrawsWhenTheSourceChanges()
        {
            var source = new FakeControl(Catalog(InventoryRule.Default));
            var view = CreateBoundView(source);
            view.SetOpen(true);

            source.Inventory.Add(_keyId, 1);

            Assert.That(view.Element.Query(className: InventoryView.ItemClass).ToList(), Has.Count.EqualTo(2));
        }

        private static ItemCatalog Catalog(InventoryRule rule)
        {
            return new ItemCatalog(new[] { _bandage, _case, _key }, rule, new[] { new ItemCount(_bandageId, 2) });
        }

        private Damageable NewPlayer(ItemCatalog catalog)
        {
            var go = new GameObject("Player", typeof(CapsuleCollider));
            _created.Add(go);
            var damageable = go.AddComponent<Damageable>();
            damageable.Initialize("Player", 100);
            go.AddComponent<PlayerInventory>().Initialize(catalog);
            return damageable;
        }

        private InventoryView CreateBoundView(IInventoryControl source)
        {
            var gameObject = new GameObject(nameof(InventoryView));
            _created.Add(gameObject);
            var view = gameObject.AddComponent<InventoryView>();
            view.Bind(CloneLayout());
            view.Source = source;
            return view;
        }

        private static string[] Labels(InventoryView view, string className)
        {
            return view.Element.Query<Label>(className: className).ToList().Select(l => l.text).ToArray();
        }

        private static string[] Buttons(InventoryView view)
        {
            return view.Element.Query<Button>(className: InventoryView.ButtonClass).ToList().Select(b => b.text).ToArray();
        }

        private static VisualElement CloneLayout()
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            return layout.CloneTree();
        }

        private sealed class FakeControl : IInventoryControl
        {
            public FakeControl(ItemCatalog catalog)
            {
                Inventory = new Inventory(catalog);
                Inventory.Fill();
                Inventory.Changed += () => Changed?.Invoke();
            }

            public event Action Changed;

            public Inventory Inventory { get; }

            public void Move(int handle, int x, int y, bool rotated)
            {
                Inventory.TryMove(handle, x, y, rotated);
            }

            public void Use(int handle)
            {
                Inventory.TryTake(handle, 1, out _);
            }

            public void Drop(int handle, int count)
            {
                Inventory.TryTake(handle, count, out _);
            }
        }
    }
}
