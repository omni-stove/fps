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
    public sealed class InventoryViewAttachmentTests
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
        public void AnAttachmentAboveTheLevelShowsTheLockedTextAndCannotBeAttached()
        {
            var source = new FakeControl(Catalog(InventoryRule.Default));
            source.Inventory.Add(_suppressorId, 1);
            source.Inventory.Add(_compensatorId, 1);
            var view = CreateBoundView(source);
            view.SetOpen(true);
            var ledger = new ProgressionLedger(new ProgressionSpec(
                100, 50, 50, 250, 500, new[] { 0, 500, 1500 }, new[] { new ProgressionUnlock(_suppressorId, 3) }));

            view.ShowUnlocks(ledger, new PlayerProfile(600, 0, 0, 0, 0));
            view.Select(source.Inventory.Stacks.Single(s => s.ItemId == _suppressorId).Handle);

            Assert.That(Buttons(view), Is.EqualTo(new[] { "今の武器に付ける", "1 個捨てる" }));
            Assert.That(AttachButton(view).enabledSelf, Is.False);
            Assert.That(LockedTexts(view), Is.EqualTo(new[] { "レベル 3 になると付けられます" }));

            view.Select(source.Inventory.Stacks.Single(s => s.ItemId == _compensatorId).Handle);

            Assert.That(AttachButton(view).enabledSelf, Is.True, "the compensator is not in the unlock table");
            Assert.That(LockedTexts(view), Is.Empty);
        }

        [Test]
        public void ReachingTheLevelOrDroppingTheLedgerLetsTheAttachmentBeAttached()
        {
            var source = new FakeControl(Catalog(InventoryRule.Default));
            source.Inventory.Add(_suppressorId, 1);
            source.Inventory.Add(_compensatorId, 1);
            var view = CreateBoundView(source);
            view.SetOpen(true);
            var ledger = new ProgressionLedger(new ProgressionSpec(
                100, 50, 50, 250, 500, new[] { 0, 500, 1500 }, new[] { new ProgressionUnlock(_suppressorId, 3) }));
            view.Select(source.Inventory.Stacks.Single(s => s.ItemId == _suppressorId).Handle);

            view.ShowUnlocks(ledger, new PlayerProfile(1500, 0, 0, 0, 0));
            Assert.That(AttachButton(view).enabledSelf, Is.True);
            Assert.That(LockedTexts(view), Is.Empty);

            view.ShowUnlocks(ledger, PlayerProfile.Empty);
            Assert.That(LockedTexts(view), Is.EqualTo(new[] { "レベル 3 になると付けられます" }));

            view.ShowUnlocks(null, null);
            Assert.That(AttachButton(view).enabledSelf, Is.True);
            Assert.That(LockedTexts(view), Is.Empty);
        }

        [Test]
        public void ShowUnlocksNeedsAProfileWithALedger()
        {
            var view = CreateBoundView(new FakeControl(Catalog(InventoryRule.Default)));
            var ledger = new ProgressionLedger(ProgressionSpec.Default);

            Assert.Throws<System.ArgumentNullException>(() => view.ShowUnlocks(ledger, null));
        }

        private static Button AttachButton(InventoryView view)
        {
            return view.Element.Query<Button>(className: InventoryView.ButtonClass).ToList().Single(b => b.text == "今の武器に付ける");
        }

        private static string[] LockedTexts(InventoryView view)
        {
            return view.Element.Query<Label>(className: InventoryView.LockedClass).ToList().Select(l => l.text).ToArray();
        }

        private static ItemCatalog Catalog(InventoryRule rule)
        {
            return new ItemCatalog(
                new[] { _rifleAmmo, _pistolAmmo, _rifleItem, _handgunItem, _magazine, _suppressor, _compensator },
                rule,
                new[] { new ItemCount(_magazineId, 1), new ItemCount(_suppressorId, 1), new ItemCount(_compensatorId, 1) });
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
