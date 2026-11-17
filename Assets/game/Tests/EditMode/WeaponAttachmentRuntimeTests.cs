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
    public sealed class WeaponAttachmentRuntimeTests
    {
        private const string _rifleAmmoId = "212170da-0998-4fa9-9457-761c75a2a797";
        private const string _pistolAmmoId = "02e07a84-f063-4b41-8f63-17a410a3d069";
        private const string _rifleItemId = "39cb64db-b31c-478c-9784-9ecf8006a0f1";
        private const string _handgunItemId = "b5b5df13-2868-4b0a-b2c0-fbe6a055a4fa";
        private const string _magazineId = "eb45e06b-489d-410d-ae0d-f1095df96a0b";
        private const string _suppressorId = "8d8c8546-59d4-4b4c-ba71-890664e5c260";
        private const string _compensatorId = "3fc98d19-5fb3-4680-bd5f-30d30d14f84e";

        // 小銃 (4x2・5.56) は銃口と弾倉、拳銃 (2x1・9mm) は何も付かない。弾は 60 発で 1 山・1 発 0.01 kg
        private static readonly ItemSpec _rifleAmmo = new ItemSpec(_rifleAmmoId, "item.ammo556", maxStack: 60, weight: 0.01f);
        private static readonly ItemSpec _pistolAmmo = new ItemSpec(_pistolAmmoId, "item.ammo9mm", maxStack: 60, weight: 0.01f);
        private static readonly ItemSpec _rifleItem = new ItemSpec(
            _rifleItemId, TestWeapons.RifleNameKey, width: 4, height: 2, weaponId: TestWeapons.RifleId, ammoItemId: _rifleAmmoId,
            attachmentSlots: new[] { "muzzle", "magazine" });
        private static readonly ItemSpec _handgunItem = new ItemSpec(
            _handgunItemId, TestWeapons.HandgunNameKey, width: 2, height: 1, weaponId: TestWeapons.HandgunId, ammoItemId: _pistolAmmoId);

        // 拡張弾倉 +10 発・0.3 kg、消音器は銃声 0.3 倍・0.4 kg、補正器は反動 0.5 倍・0.2 kg
        private static readonly ItemSpec _magazine = new ItemSpec(
            _magazineId, "item.extendedMagazine", weight: 0.3f, attachment: new AttachmentSpec("magazine", magazineSizeBonus: 10));
        private static readonly ItemSpec _suppressor = new ItemSpec(
            _suppressorId, "item.suppressor", weight: 0.4f, attachment: new AttachmentSpec("muzzle", shotNoiseRadiusMultiplier: 0.3f));
        private static readonly ItemSpec _compensator = new ItemSpec(
            _compensatorId, "item.compensator", weight: 0.2f, attachment: new AttachmentSpec("muzzle", recoilMultiplier: 0.5f));

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
        public void AttachingFromTheInventoryChangesTheHeldWeapon()
        {
            var (inventory, weapons) = NewArmedPlayer();

            inventory.Attach(Handle(inventory, _magazineId));

            Assert.That(weapons.Weapon.Spec.MagazineSize, Is.EqualTo(40));
            Assert.That(weapons.Weapon.AmmoInMagazine, Is.EqualTo(30));
            Assert.That(weapons.AttachmentsOf(TestWeapons.RifleId), Is.EqualTo(new[] { _magazineId }));
            Assert.That(inventory.Inventory.CountOf(_magazineId), Is.Zero);
            Assert.That(inventory.Inventory.AttachmentsOf(TestWeapons.RifleId), Is.EqualTo(new[] { _magazineId }));
            Assert.That(inventory.Inventory.WeaponWeight, Is.EqualTo(0.3f).Within(1e-4f), "the test rifle weighs nothing; the magazine's weight moves onto it");
            Assert.That(weapons.Weapon.AmmoPool, Is.Not.Null, "the rebuilt weapon still takes its reserve from the inventory");
        }

        [Test]
        public void DetachingPutsTheItemBackAndTheExtraRoundsIntoTheAmmo()
        {
            var (inventory, weapons) = NewArmedPlayer();
            inventory.Attach(Handle(inventory, _magazineId));
            Assert.That(weapons.StartReload(), Is.True);
            weapons.Tick(2.5f);
            Assert.That(weapons.Weapon.AmmoInMagazine, Is.EqualTo(40));
            Assert.That(inventory.Inventory.CountOf(_rifleAmmoId), Is.EqualTo(80));

            inventory.Detach(Handle(inventory, _rifleItemId), _magazineId);

            Assert.That(weapons.Weapon.Spec.MagazineSize, Is.EqualTo(30));
            Assert.That(weapons.Weapon.AmmoInMagazine, Is.EqualTo(30));
            Assert.That(inventory.Inventory.CountOf(_rifleAmmoId), Is.EqualTo(90));
            Assert.That(inventory.Inventory.CountOf(_magazineId), Is.EqualTo(1));
            Assert.That(weapons.AttachmentsOf(TestWeapons.RifleId), Is.Empty);
        }

        [Test]
        public void AttachingToAnOccupiedSlotPutsTheOldOneBack()
        {
            var (inventory, weapons) = NewArmedPlayer();
            inventory.Attach(Handle(inventory, _suppressorId));

            inventory.Attach(Handle(inventory, _compensatorId));

            Assert.That(weapons.AttachmentsOf(TestWeapons.RifleId), Is.EqualTo(new[] { _compensatorId }));
            Assert.That(inventory.Inventory.CountOf(_suppressorId), Is.EqualTo(1));
            Assert.That(inventory.Inventory.CountOf(_compensatorId), Is.Zero);
            Assert.That(weapons.Weapon.Spec.RecoilPitchDegrees, Is.EqualTo(0.25f).Within(1e-4f));
        }

        [Test]
        public void AnAttachmentThatDoesNotFitTheHeldWeaponStaysInTheInventory()
        {
            var (inventory, weapons) = NewArmedPlayer();
            weapons.Select(TestWeapons.HandgunId);

            inventory.Attach(Handle(inventory, _suppressorId));

            Assert.That(inventory.Inventory.CountOf(_suppressorId), Is.EqualTo(1));
            Assert.That(weapons.AttachmentsOf(TestWeapons.HandgunId), Is.Empty);
        }

        [Test]
        public void NothingIsAttachedWhileUnarmed()
        {
            var (inventory, weapons) = NewArmedPlayer();
            weapons.Disarm();

            inventory.Attach(Handle(inventory, _suppressorId));

            Assert.That(inventory.Inventory.CountOf(_suppressorId), Is.EqualTo(1));
        }

        [Test]
        public void ADroppedWeaponKeepsItsAttachmentsAndBringsThemBackWhenPickedUp()
        {
            var (inventory, weapons) = NewArmedPlayer();
            inventory.Attach(Handle(inventory, _suppressorId));

            inventory.Drop(Handle(inventory, _rifleItemId), 1);

            var pickup = Object.FindObjectsByType<Pickup>(FindObjectsInactive.Include).Single();
            Assert.That(pickup.Contents.Attachments, Is.EqualTo(new[] { _suppressorId }));
            Assert.That(inventory.Inventory.CountOf(_suppressorId), Is.Zero, "the suppressor leaves with the rifle");
            Assert.That(weapons.AttachmentsOf(TestWeapons.RifleId), Is.Empty);

            Assert.That(pickup.TryCollect(inventory.GetComponent<Damageable>(), out var outcome), Is.True, outcome.Rejection.ToString());

            Assert.That(weapons.AttachmentsOf(TestWeapons.RifleId), Is.EqualTo(new[] { _suppressorId }));
            Assert.That(inventory.Inventory.AttachmentsOf(TestWeapons.RifleId), Is.EqualTo(new[] { _suppressorId }));
            Assert.That(weapons.Loadout.Items.Single(w => w.Spec.Id == TestWeapons.RifleId).Spec.ShotNoiseRadius, Is.EqualTo(TestWeapons.RifleSpec().ShotNoiseRadius * 0.3f).Within(1e-3f));
        }

        [Test]
        public void SwappingAtTheWeaponLimitDropsTheOldWeaponWithItsAttachments()
        {
            var (inventory, weapons) = NewArmedPlayer();
            inventory.Attach(Handle(inventory, _suppressorId));
            var shotgun = Definition("Shotgun", TestWeapons.LauncherSpec());

            Assert.That(weapons.SwapWeapon(TestWeapons.RifleId, shotgun, null, new[] { _compensatorId }, out var dropped), Is.True);

            Assert.That(dropped.Attachments, Is.EqualTo(new[] { _suppressorId }));
            Assert.That(weapons.AttachmentsOf(TestWeapons.LauncherId), Is.Empty, "the launcher has no muzzle slot in this inventory");
            Assert.That(weapons.AttachmentsOf(TestWeapons.RifleId), Is.Empty);
        }

        [Test]
        public void RestoringASaveAttachesWhatWasSaved()
        {
            var (_, weapons) = NewArmedPlayer();

            weapons.RestoreLoadout(
                new[] { new WeaponAmmo(TestWeapons.RifleId, 35, 20) },
                TestWeapons.RifleId,
                new Dictionary<string, IReadOnlyList<string>> { [TestWeapons.RifleId] = new[] { _magazineId, "not-an-item" } });

            Assert.That(weapons.AttachmentsOf(TestWeapons.RifleId), Is.EqualTo(new[] { _magazineId }));
            Assert.That(weapons.Weapon.Spec.MagazineSize, Is.EqualTo(40));
            Assert.That(weapons.Weapon.AmmoInMagazine, Is.EqualTo(35));
            Assert.That(weapons.SnapshotAttachments()[TestWeapons.RifleId], Is.EqualTo(new[] { _magazineId }));
        }

        [Test]
        public void ReinitializingTakesTheAttachmentsOff()
        {
            var (inventory, weapons) = NewArmedPlayer();
            inventory.Attach(Handle(inventory, _magazineId));

            weapons.Initialize(weapons.Aim, NewAction("Attack2"), NewAction("Reload2"), null, null, weapons.Definitions);

            Assert.That(weapons.AttachmentsOf(TestWeapons.RifleId), Is.Empty);
            Assert.That(weapons.Weapon.Spec.MagazineSize, Is.EqualTo(30));
        }

        [Test]
        public void ADroppedWeaponPickupWeighsItsAttachments()
        {
            var rifle = Definition("Rifle", TestWeapons.RifleSpec());
            rifle.Weight = 3f;

            // 実際の持ち物の表 (Tuning/inventory.json) の消音器は 0.4 kg
            var contents = PickupContents.ForDroppedWeapon(rifle, new WeaponAmmo(TestWeapons.RifleId, 10, 0), attachments: new[] { _suppressorId });

            Assert.That(contents.ToSpec().WeaponWeight, Is.EqualTo(3.4f).Within(1e-4f));
            Assert.That(contents.Clone().Attachments, Is.EqualTo(new[] { _suppressorId }));
            Assert.That(PickupContents.ForWeapon(rifle, 5).WithAttachments(new[] { _suppressorId }).Attachments, Is.EqualTo(new[] { _suppressorId }));
            Assert.Throws<System.ArgumentException>(() => PickupContents.ForHealth(10).WithAttachments(new[] { _suppressorId }));
        }

        [Test]
        public void TheScreenOffersToAttachAndToDetach()
        {
            var source = new FakeControl(Catalog(InventoryRule.Default));
            source.Inventory.Add(_suppressorId, 1);
            source.Inventory.HoldWeapons(new[] { TestWeapons.RifleSpec() }, _ => new[] { _magazineId });
            var view = CreateBoundView(source);
            view.SetOpen(true);

            view.Select(source.Inventory.Stacks.Single(s => s.ItemId == _rifleItemId).Handle);

            Assert.That(Buttons(view), Is.EqualTo(new[] { "装備する", "捨てる", "外す" }));
            Assert.That(
                view.Element.Query<Label>(className: InventoryView.ItemNameClass).ToList().Select(l => l.text),
                Has.Some.EqualTo("付けている物: 拡張マガジン"));

            view.Select(source.Inventory.Stacks.Single(s => s.ItemId == _suppressorId).Handle);

            Assert.That(Buttons(view), Is.EqualTo(new[] { "今の武器に付ける", "1 個捨てる" }));
        }

        [Test]
        public void AttachmentModelsGoOnTheSlotSocketOrTheModelsOutline()
        {
            var model = new GameObject("Gun").transform;
            _created.Add(model.gameObject);
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.transform.SetParent(model, false);
            body.transform.localScale = new Vector3(0.1f, 0.2f, 0.6f);
            var magazineSocket = new GameObject("Socket_magazine").transform;
            magazineSocket.SetParent(model, false);
            magazineSocket.localPosition = new Vector3(0f, -0.3f, 0.1f);
            var muzzle = new GameObject(WeaponSockets.MuzzleName).transform;
            muzzle.SetParent(model, false);
            var models = ScriptableObject.CreateInstance<AttachmentModelCatalog>();
            _created.Add(models);
            models.SetAttachments(new[]
            {
                new AttachmentModelEntry(_suppressorId, null, AttachmentAnchor.Front, new Vector3(0.04f, 0.04f, 0.2f)),
            });

            WeaponAttachmentModels.Apply(model, new[] { _suppressorId, _magazineId }, Catalog(InventoryRule.Default), models);

            var attached = model.GetComponentsInChildren<WeaponAttachmentModel>(true);
            Assert.That(attached.Select(a => a.ItemId), Is.EquivalentTo(new[] { _suppressorId, _magazineId }));
            var suppressor = attached.Single(a => a.ItemId == _suppressorId).transform;
            Assert.That(suppressor.parent, Is.SameAs(model));
            Assert.That(suppressor.localPosition.z, Is.EqualTo(0.3f).Within(1e-3f), "the front of the 0.6 m body");
            Assert.That(attached.Single(a => a.ItemId == _magazineId).transform.parent, Is.SameAs(magazineSocket));

            var sockets = new WeaponSockets(model);
            Assert.That(sockets.Muzzle, Is.Not.SameAs(muzzle), "the suppressor's tip is the muzzle now");
            Assert.That(sockets.Muzzle.position.z, Is.EqualTo(0.5f).Within(1e-3f));

            WeaponAttachmentModels.Apply(model, WeaponAttachments.None, Catalog(InventoryRule.Default), models);

            Assert.That(model.GetComponentsInChildren<WeaponAttachmentModel>(true), Is.Empty);
            Assert.That(new WeaponSockets(model).Muzzle, Is.SameAs(muzzle));
        }

        private static int Handle(PlayerInventory inventory, string itemId)
        {
            return inventory.Inventory.Stacks.First(s => s.ItemId == itemId).Handle;
        }

        private (PlayerInventory Inventory, WeaponController Weapons) NewArmedPlayer()
        {
            var player = NewPlayer(Catalog(new InventoryRule(gridWidth: 8, gridHeight: 4)));
            var camera = new GameObject("Camera");
            camera.transform.SetParent(player.transform, false);
            var rifle = Definition("Rifle", TestWeapons.RifleSpec());
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

        // 付ける物を 1 つずつ持って始める
        private static ItemCatalog Catalog(InventoryRule rule)
        {
            return new ItemCatalog(
                new[] { _rifleAmmo, _pistolAmmo, _rifleItem, _handgunItem, _magazine, _suppressor, _compensator },
                rule,
                new[] { new ItemCount(_magazineId, 1), new ItemCount(_suppressorId, 1), new ItemCount(_compensatorId, 1) });
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
            view.Bind(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree());
            view.Source = source;
            return view;
        }

        private static string[] Buttons(InventoryView view)
        {
            return view.Element.Query<Button>(className: InventoryView.ButtonClass).ToList().Select(b => b.text).ToArray();
        }

        private sealed class FakeControl : IInventoryControl
        {
            public FakeControl(ItemCatalog catalog)
            {
                Inventory = new Inventory(catalog);
                Inventory.Changed += () => Changed?.Invoke();
            }

            public event System.Action Changed;

            public Inventory Inventory { get; }

            public void Move(int handle, int x, int y, bool rotated)
            {
            }

            public void Use(int handle)
            {
            }

            public void Drop(int handle, int count)
            {
            }

            public void Attach(int handle)
            {
            }

            public void Detach(int weaponHandle, string itemId)
            {
            }
        }
    }
}
