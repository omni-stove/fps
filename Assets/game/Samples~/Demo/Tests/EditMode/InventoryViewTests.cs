using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    /// <summary>
    /// 持ち物の画面 (<see cref="InventoryView"/>) が持ち物を格子か一覧で描き、選んだ物にできることのボタンを出し、
    /// <see cref="GameSignals.Inventory"/> の持ち物を出すことを確かめる。
    /// </summary>
    public sealed class InventoryViewTests
    {
        private const string _bandageId = "31d5979e-6067-49d9-b70c-fdcf44dc77d1";
        private const string _caseId = "542515db-5705-497c-8fe6-076a6709131b";
        private const string _keyId = "6879cab0-d2cd-4ace-b51a-cc882b7c8aa9";

        private static readonly ItemSpec _bandage = new ItemSpec(_bandageId, "item.bandage", maxStack: 5, weight: 0.1f, useHealth: 15);
        private static readonly ItemSpec _case = new ItemSpec(_caseId, "item.toolbox", weight: 6f, width: 3, height: 2);
        private static readonly ItemSpec _key = new ItemSpec(_keyId, "item.keycard", maxCarried: 1);

        private const string _rifleAmmoId = "212170da-0998-4fa9-9457-761c75a2a797";
        private const string _pistolAmmoId = "02e07a84-f063-4b41-8f63-17a410a3d069";
        private const string _rifleItemId = "39cb64db-b31c-478c-9784-9ecf8006a0f1";
        private const string _handgunItemId = "b5b5df13-2868-4b0a-b2c0-fbe6a055a4fa";

        // 小銃 (4x2・5.56) と拳銃 (2x1・9mm)。弾は 60 発で 1 山・1 発 0.01 kg
        private static readonly ItemSpec _rifleAmmo = new ItemSpec(_rifleAmmoId, "item.ammo556", maxStack: 60, weight: 0.01f);
        private static readonly ItemSpec _pistolAmmo = new ItemSpec(_pistolAmmoId, "item.ammo9mm", maxStack: 60, weight: 0.01f);
        private static readonly ItemSpec _rifleItem = new ItemSpec(
            _rifleItemId, TestWeapons.RifleNameKey, width: 4, height: 2, weaponId: TestWeapons.RifleId, ammoItemId: _rifleAmmoId);
        private static readonly ItemSpec _handgunItem = new ItemSpec(
            _handgunItemId, TestWeapons.HandgunNameKey, width: 2, height: 1, weaponId: TestWeapons.HandgunId, ammoItemId: _pistolAmmoId);

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            GameSignals.ResetAll();
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
        public void TheLayoutHasTheInventoryArea()
        {
            Assert.That(CloneLayout().Q<VisualElement>(InventoryView.UxmlName), Is.Not.Null);
        }

        [Test]
        public void HudAddViewsAttachesTheInventoryScreen()
        {
            var hud = Hud.Create("InventoryRuntimeTests.Hud");
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
        public void ThePublishedInventoryIsShownUntilCleared()
        {
            var view = CreateBoundView();
            var source = new FakeControl(Catalog(InventoryRule.Default));

            GameSignals.Inventory.Publish(source);
            Assert.That(view.Source, Is.SameAs(source));
            view.SetOpen(true);
            Assert.That(view.IsOpen, Is.True);

            GameSignals.Inventory.Clear();
            Assert.That(view.Source, Is.Null);
            Assert.That(view.IsOpen, Is.False, "losing the inventory closes the screen");
        }

        [Test]
        public void ABindingViewCatchesUpWithThePublishedInventoryAndKeepsOneSetDirectly()
        {
            var published = new FakeControl(Catalog(InventoryRule.Default));
            GameSignals.Inventory.Publish(published);
            var view = CreateBoundView();
            Assert.That(view.Source, Is.SameAs(published));

            GameSignals.Inventory.Clear();
            var direct = new FakeControl(Catalog(InventoryRule.Default));
            view.Source = direct;
            GameSignals.Inventory.Publish(published);
            view.Source = direct;
            GameSignals.Inventory.Clear();

            Assert.That(view.Source, Is.SameAs(direct), "a clear only takes back what the signal gave");
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

        [Test]
        public void SelectingAWeaponOffersEquipAndDrop()
        {
            var source = new FakeControl(WeaponCatalog(InventoryRule.Default));
            source.Inventory.HoldWeapons(new[] { TestWeapons.RifleSpec() });
            var view = CreateBoundView(source);
            view.SetOpen(true);

            view.Select(source.Inventory.Stacks.Single(s => s.ItemId == _rifleItemId).Handle);

            Assert.That(Buttons(view), Is.EqualTo(new[] { "装備する", "捨てる" }));
        }

        private static ItemCatalog WeaponCatalog(InventoryRule rule)
        {
            return new ItemCatalog(
                new[] { _bandage, _case, _key, _rifleAmmo, _pistolAmmo, _rifleItem, _handgunItem }, rule, new[] { new ItemCount(_bandageId, 2) });
        }

        private static ItemCatalog Catalog(InventoryRule rule)
        {
            return new ItemCatalog(new[] { _bandage, _case, _key }, rule, new[] { new ItemCount(_bandageId, 2) });
        }

        private InventoryView CreateBoundView(IInventoryControl source)
        {
            var view = CreateBoundView();
            view.Source = source;
            return view;
        }

        private InventoryView CreateBoundView()
        {
            var gameObject = new GameObject(nameof(InventoryView));
            _created.Add(gameObject);
            var view = gameObject.AddComponent<InventoryView>();
            view.Bind(CloneLayout());
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
            Assert.That(layout, Is.Not.Null, "Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
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

            public List<int> Attached { get; } = new List<int>();

            public List<(int, string)> Detached { get; } = new List<(int, string)>();

            public void Attach(int handle)
            {
                Attached.Add(handle);
            }

            public void Detach(int weaponHandle, string itemId)
            {
                Detached.Add((weaponHandle, itemId));
            }
        }
    }
}
