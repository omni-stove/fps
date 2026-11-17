using System;
using System.Collections.Generic;
using System.Linq;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
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
        private readonly List<InputAction> _actions = new List<InputAction>();

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
            foreach (var action in _actions)
            {
                action.Dispose();
            }

            _actions.Clear();
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

            Assert.That(pickup.TryDescribe(player, out var hint), Is.True);
            Assert.That(hint.Action, Is.EqualTo(PickupHintAction.Item));
            Assert.That(hint.ContentId, Is.EqualTo(_caseId));
            Assert.That(hint.Count, Is.EqualTo(1));
            Assert.That(PickupPrompt.TryDescribe(hint, "E", out var text), Is.True);
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

        [Test]
        public void HeldWeaponsSitInTheInventoryAndTheirReserveIsAmmo()
        {
            var (inventory, weapons) = NewArmedPlayer();

            // 包帯 (0,0) の次に、武器を弾薬より先に置く
            Assert.That(inventory.Inventory.Stacks.Select(s => (s.ItemId, s.X, s.Y)).Take(3), Is.EqualTo(new[]
            {
                (_bandageId, 0, 0), (_rifleItemId, 1, 0), (_handgunItemId, 5, 0),
            }));

            Assert.That(inventory.Inventory.HoldsWeapon(TestWeapons.RifleId), Is.True);
            Assert.That(inventory.Inventory.HoldsWeapon(TestWeapons.HandgunId), Is.True);
            Assert.That(inventory.Inventory.CountOf(_rifleAmmoId), Is.EqualTo(90));
            Assert.That(inventory.Inventory.CountOf(_pistolAmmoId), Is.EqualTo(48));
            Assert.That(weapons.Weapon.ReserveAmmo, Is.EqualTo(90));
            Assert.That(weapons.Weapon.AmmoPool, Is.Not.Null);
        }

        [Test]
        public void PickingUpAmmoItemsRaisesTheWeaponsReserve()
        {
            var (inventory, weapons) = NewArmedPlayer();
            var changes = 0;
            weapons.Weapon.ReserveAmmoChanged += () => changes++;

            inventory.Inventory.Add(_rifleAmmoId, 10);

            Assert.That(weapons.Weapon.ReserveAmmo, Is.EqualTo(100));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void UsingAWeaponItemEquipsIt()
        {
            var (inventory, weapons) = NewArmedPlayer();
            var handgun = inventory.Inventory.Stacks.Single(s => s.ItemId == _handgunItemId).Handle;

            inventory.Use(handgun);

            Assert.That(weapons.Weapon.Spec.Id, Is.EqualTo(TestWeapons.HandgunId));
            Assert.That(inventory.Inventory.HoldsWeapon(TestWeapons.HandgunId), Is.True, "equipping does not take the weapon out");
        }

        [Test]
        public void DroppingAWeaponItemPutsTheWeaponOnTheGroundWithOnlyItsMagazine()
        {
            var (inventory, weapons) = NewArmedPlayer();
            var rifle = inventory.Inventory.Stacks.Single(s => s.ItemId == _rifleItemId).Handle;

            inventory.Drop(rifle, 1);

            Assert.That(weapons.Loadout.Count, Is.EqualTo(1));
            Assert.That(weapons.Weapon.Spec.Id, Is.EqualTo(TestWeapons.HandgunId));
            Assert.That(inventory.Inventory.HoldsWeapon(TestWeapons.RifleId), Is.False);
            Assert.That(inventory.Inventory.CountOf(_rifleAmmoId), Is.EqualTo(90), "the reserve stays in the inventory");
            var pickup = Object.FindObjectsByType<Pickup>(FindObjectsInactive.Include).Single();
            Assert.That(pickup.Spec.Kind, Is.EqualTo(PickupKind.Weapon));
            Assert.That(pickup.Spec.WeaponId, Is.EqualTo(TestWeapons.RifleId));
            Assert.That(pickup.Contents.CarriedAmmo.AmmoInMagazine, Is.EqualTo(30));
            Assert.That(pickup.Contents.CarriedAmmo.ReserveAmmo, Is.Zero);
        }

        [Test]
        public void DroppingTheLastWeaponLeavesThePlayerUnarmed()
        {
            var (inventory, weapons) = NewArmedPlayer();
            inventory.Drop(inventory.Inventory.Stacks.Single(s => s.ItemId == _rifleItemId).Handle, 1);

            inventory.Drop(inventory.Inventory.Stacks.Single(s => s.ItemId == _handgunItemId).Handle, 1);

            Assert.That(weapons.IsUnarmed, Is.True);
            Assert.That(inventory.Inventory.Stacks.Any(s => s.ItemId == _handgunItemId), Is.False);
            Assert.That(Object.FindObjectsByType<Pickup>(FindObjectsInactive.Include), Has.Length.EqualTo(2));
        }

        [Test]
        public void ADroppedWeaponPickedUpAgainBringsNoExtraAmmo()
        {
            var (inventory, weapons) = NewArmedPlayer();
            inventory.Drop(inventory.Inventory.Stacks.Single(s => s.ItemId == _rifleItemId).Handle, 1);
            inventory.Inventory.Remove(_rifleAmmoId, 90);
            var pickup = Object.FindObjectsByType<Pickup>(FindObjectsInactive.Include).Single();

            Assert.That(pickup.TryCollect(inventory.GetComponent<Damageable>(), out var outcome), Is.True, outcome.Rejection.ToString());

            Assert.That(weapons.Loadout.Count, Is.EqualTo(2));
            Assert.That(inventory.Inventory.HoldsWeapon(TestWeapons.RifleId), Is.True);
            Assert.That(inventory.Inventory.CountOf(_rifleAmmoId), Is.Zero);
        }

        [Test]
        public void AWeaponThatDoesNotFitStaysOnTheGround()
        {
            var (inventory, _) = NewArmedPlayer();
            inventory.Drop(inventory.Inventory.Stacks.Single(s => s.ItemId == _handgunItemId).Handle, 1);
            var pickup = Object.FindObjectsByType<Pickup>(FindObjectsInactive.Include).Single();

            // 空いたマスを包帯で埋める
            inventory.Inventory.Add(_bandageId, 1000);

            Assert.That(pickup.TryCollect(inventory.GetComponent<Damageable>(), out var outcome), Is.False);
            Assert.That(outcome.Rejection, Is.EqualTo(PickupRejection.InventoryFull));
        }

        [Test]
        public void RespawningRestocksTheAmmoAndKeepsTheWeapons()
        {
            var (inventory, _) = NewArmedPlayer();
            inventory.Inventory.Remove(_rifleAmmoId, 80);

            inventory.Refill();

            Assert.That(inventory.Inventory.CountOf(_rifleAmmoId), Is.EqualTo(90));
            Assert.That(inventory.Inventory.HoldsWeapon(TestWeapons.RifleId), Is.True);
            Assert.That(inventory.Inventory.CountOf(_bandageId), Is.EqualTo(2));
        }

        [Test]
        public void ApplyingRulesWithLessReserveLowersTheAmmo()
        {
            var (inventory, weapons) = NewArmedPlayer();

            weapons.ApplyRules(GameRules.Default.WithReserveAmmoMultiplier(0.5f));

            Assert.That(inventory.Inventory.CountOf(_rifleAmmoId), Is.EqualTo(45));
            Assert.That(inventory.Inventory.CountOf(_pistolAmmoId), Is.EqualTo(24));
            Assert.That(weapons.Weapon.ReserveAmmo, Is.EqualTo(45));
        }

        [Test]
        public void RestoringASaveSetsTheAmmoToTheSavedReserve()
        {
            var (inventory, weapons) = NewArmedPlayer();

            weapons.RestoreLoadout(new[] { new WeaponAmmo(TestWeapons.RifleId, 10, 20) }, TestWeapons.RifleId);

            Assert.That(inventory.Inventory.CountOf(_rifleAmmoId), Is.EqualTo(20));
            Assert.That(inventory.Inventory.CountOf(_pistolAmmoId), Is.EqualTo(48), "a weapon missing from the save starts full");
            Assert.That(weapons.SnapshotAmmo().First().ReserveAmmo, Is.EqualTo(20));
        }

        [Test]
        public void TheCarriedWeightLeavesOutTheWeapons()
        {
            var (inventory, _) = NewArmedPlayer(new InventoryRule(gridWidth: 8, gridHeight: 4, countWeightInCarryLoad: true), rifleWeight: 3.5f);

            // 包帯 2 個 (0.2 kg) と弾 138 発 (1.38 kg)。小銃の 3.5 kg は移動のルールが武器として数える
            Assert.That(inventory.CarriedWeight, Is.EqualTo(1.58f).Within(1e-4f));
            Assert.That(inventory.Inventory.TotalWeight, Is.EqualTo(5.08f).Within(1e-4f));
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

        private (PlayerInventory Inventory, WeaponController Weapons) NewArmedPlayer(InventoryRule rule = null, float rifleWeight = 0f)
        {
            var player = NewPlayer(WeaponCatalog(rule ?? new InventoryRule(gridWidth: 8, gridHeight: 4)));
            var camera = new GameObject("Camera");
            camera.transform.SetParent(player.transform, false);
            var rifle = Definition("Rifle", TestWeapons.RifleSpec());
            rifle.Weight = rifleWeight;
            var handgun = Definition("Handgun", TestWeapons.HandgunSpec());
            var weapons = camera.AddComponent<WeaponController>();
            weapons.Initialize(camera.transform, NewAction("Attack"), NewAction("Reload"), null, null, new[] { rifle, handgun });
            var inventory = player.GetComponent<PlayerInventory>();
            inventory.Bind(weapons);
            return (inventory, weapons);
        }

        private WeaponDefinition Definition(string name, WeaponSpec spec)
        {
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            _created.Add(definition);
            definition.name = name;
            WeaponAssets.Configure(definition, spec);
            return definition;
        }

        private InputAction NewAction(string name)
        {
            var action = new InputAction(name, InputActionType.Button);
            _actions.Add(action);
            return action;
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
