using System;
using System.Linq;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class WeaponAttachmentTests
    {
        private const string _rifleId = "5e6f7a8b-9c0d-4e1f-8a3b-4c5d6e7f8a9b";
        private const string _pistolId = "6f7a8b9c-0d1e-4f2a-9b4c-5d6e7f8a9b0c";
        private const string _rifleItemId = "2b3c4d5e-6f7a-4b8c-9d0e-1f2a3b4c5d6e";
        private const string _pistolItemId = "3c4d5e6f-7a8b-4c9d-8e1f-2a3b4c5d6e7f";
        private const string _suppressorId = "8b9c0d1e-2f3a-4b4c-9d6e-7f8a9b0c1d2e";
        private const string _compensatorId = "9c0d1e2f-3a4b-4c5d-8e7f-8a9b0c1d2e3f";
        private const string _scopeId = "0d1e2f3a-4b5c-4d6e-9f8a-9b0c1d2e3f4a";
        private const string _magazineId = "1e2f3a4b-5c6d-4e7f-8a9b-0c1d2e3f4a5b";
        private const string _pistolSightId = "2f3a4b5c-6d7e-4f8a-9b0c-1d2e3f4a5b6c";

        // 小銃は照準器 (optic) と銃口 (muzzle) を持つ。拳銃は照準器だけ
        private static readonly ItemSpec _rifleItem = new ItemSpec(
            _rifleItemId, "weapon.rifle", width: 2, height: 1, weaponId: _rifleId, attachmentSlots: Strings("optic", "muzzle"));

        private static readonly ItemSpec _pistolItem = new ItemSpec(
            _pistolItemId, "weapon.handgun", weaponId: _pistolId, attachmentSlots: Strings("optic"));

        // 消音器: 銃声 1/4・射程 0.9 倍・反動 0.8 倍・0.5 kg
        private static readonly ItemSpec _suppressor = new ItemSpec(
            _suppressorId, "item.suppressor", maxStack: 2, weight: 0.5f,
            attachment: new AttachmentSpec("muzzle", rangeMultiplier: 0.9f, recoilMultiplier: 0.8f, shotNoiseRadiusMultiplier: 0.25f));

        // 補正器: 反動 0.5 倍・0.25 kg
        private static readonly ItemSpec _compensator = new ItemSpec(
            _compensatorId, "item.compensator", weight: 0.25f, attachment: new AttachmentSpec("muzzle", recoilMultiplier: 0.5f));

        // スコープ: ADS の散らばり 0.5 倍・倍率を 30 度と 15 度に置き換え・0.75 kg
        private static readonly ItemSpec _scope = new ItemSpec(
            _scopeId, "item.scope", weight: 0.75f, attachment: new AttachmentSpec("optic", adsSpreadMultiplier: 0.5f, zoomLevels: Floats(30f, 15f)));

        // 小銃に無い取り付け口 (magazine) の拡張弾倉
        private static readonly ItemSpec _magazine = new ItemSpec(
            _magazineId, "item.extendedMagazine", attachment: new AttachmentSpec("magazine", magazineSizeBonus: 10));

        // 拳銃にだけ付く照準器
        private static readonly ItemSpec _pistolSight = new ItemSpec(
            _pistolSightId, "item.pistolSight", attachment: new AttachmentSpec("optic", weaponIds: new[] { _pistolId }));

        // 散らばり 腰 4・ADS 1・移動 2、反動 縦 2・横 1、弾倉 30・予備 90・リロード 2 秒、射程 100 m、銃声 50 m、3 kg
        private static readonly WeaponSpec _rifle = new WeaponSpec(
            _rifleId,
            "weapon.rifle",
            WeaponCategory.AssaultRifle,
            ammo: new AmmoSpec(30, 90, 2f),
            round: new RoundSpec(range: 100f),
            aim: new AimSpec(hipSpreadDegrees: 4f, adsSpreadDegrees: 1f, moveSpreadDegrees: 2f, recoilPitchDegrees: 2f, recoilYawDegrees: 1f),
            shotNoiseRadius: 50f,
            weight: 3f,
            description: "test rifle");

        private static string[] Strings(params string[] values) => values;

        private static float[] Floats(params float[] values) => values;

        private static ItemCatalog Catalog(InventoryRule rule = null)
        {
            return new ItemCatalog(
                new[] { _rifleItem, _pistolItem, _suppressor, _compensator, _scope, _magazine, _pistolSight },
                rule ?? new InventoryRule(gridWidth: 6, gridHeight: 2));
        }

        [Test]
        public void AttachmentsChangeTheWeaponValuesAndAddTheirWeight()
        {
            var spec = WeaponAttachments.Apply(_rifle, new[] { _scopeId, _suppressorId }, Catalog());

            Assert.That(spec.Range, Is.EqualTo(90f).Within(1e-4f));
            Assert.That(spec.ShotNoiseRadius, Is.EqualTo(12.5f).Within(1e-4f));
            Assert.That(spec.RecoilPitchDegrees, Is.EqualTo(1.6f).Within(1e-4f));
            Assert.That(spec.RecoilYawDegrees, Is.EqualTo(0.8f).Within(1e-4f));
            Assert.That(spec.AdsSpreadDegrees, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(spec.HipSpreadDegrees, Is.EqualTo(4f));
            Assert.That(spec.MoveSpreadDegrees, Is.EqualTo(2f));
            Assert.That(spec.Aim.ZoomLevels, Is.EqualTo(Floats(30f, 15f)));
            Assert.That(spec.Weight, Is.EqualTo(4.25f).Within(1e-4f));
            Assert.That(spec.MagazineSize, Is.EqualTo(30));
            Assert.That(spec.Id, Is.EqualTo(_rifleId));
            Assert.That(spec.Description, Is.EqualTo("test rifle"));
        }

        [Test]
        public void NoAttachmentsKeepTheSameSpec()
        {
            Assert.That(WeaponAttachments.Apply(_rifle, WeaponAttachments.None, Catalog()), Is.SameAs(_rifle));
            Assert.That(WeaponAttachments.Apply(_rifle, null, Catalog()), Is.SameAs(_rifle));
        }

        [Test]
        public void AttachmentsThatDoNotFitDoNothing()
        {
            var spec = WeaponAttachments.Apply(_rifle, new[] { _magazineId, _pistolSightId, "unknown" }, Catalog());

            Assert.That(spec, Is.SameAs(_rifle));
        }

        [Test]
        public void MagazineBonusesAndReloadMultipliersReachTheAmmo()
        {
            var rifleItem = new ItemSpec(_rifleItemId, weaponId: _rifleId, attachmentSlots: Strings("magazine"));
            var fastMagazine = new ItemSpec(_magazineId, attachment: new AttachmentSpec("magazine", reloadSecondsMultiplier: 0.75f, magazineSizeBonus: 10));
            var catalog = new ItemCatalog(new[] { rifleItem, fastMagazine }, InventoryRule.Default);

            var spec = WeaponAttachments.Apply(_rifle, new[] { _magazineId }, catalog);

            Assert.That(spec.MagazineSize, Is.EqualTo(40));
            Assert.That(spec.ReloadSeconds, Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(spec.ReserveAmmo, Is.EqualTo(90));
        }

        [Test]
        public void SpreadAndRecoilStopAt90Degrees()
        {
            var rifleItem = new ItemSpec(_rifleItemId, weaponId: _rifleId, attachmentSlots: Strings("stock"));
            var wild = new ItemSpec(_magazineId, attachment: new AttachmentSpec("stock", hipSpreadMultiplier: 100f, recoilMultiplier: 100f));
            var catalog = new ItemCatalog(new[] { rifleItem, wild }, InventoryRule.Default);

            var spec = WeaponAttachments.Apply(_rifle, new[] { _magazineId }, catalog);

            Assert.That(spec.HipSpreadDegrees, Is.EqualTo(90f));
            Assert.That(spec.RecoilPitchDegrees, Is.EqualTo(90f));
            Assert.That(spec.RecoilYawDegrees, Is.EqualTo(90f));
        }

        [Test]
        public void KeepPutsOnePerSlotInTheWeaponSlotOrder()
        {
            var kept = WeaponAttachments.Keep(Catalog(), _rifleId, new[] { _suppressorId, _compensatorId, _magazineId, _scopeId });

            Assert.That(kept, Is.EqualTo(new[] { _scopeId, _compensatorId }));
        }

        [Test]
        public void AttachingToAnOccupiedSlotReplacesWhatWasThere()
        {
            var next = WeaponAttachments.Attach(Catalog(), _rifleId, new[] { _scopeId, _suppressorId }, _compensatorId, out var replaced);

            Assert.That(next, Is.EqualTo(new[] { _scopeId, _compensatorId }));
            Assert.That(replaced, Is.EqualTo(_suppressorId));
        }

        [Test]
        public void AttachingToAFreeSlotReplacesNothing()
        {
            var next = WeaponAttachments.Attach(Catalog(), _rifleId, new[] { _suppressorId }, _scopeId, out var replaced);

            Assert.That(next, Is.EqualTo(new[] { _scopeId, _suppressorId }));
            Assert.That(replaced, Is.Null);
        }

        [Test]
        public void AttachmentsWithoutASlotOrForOtherWeaponsAreRefused()
        {
            Assert.That(WeaponAttachments.Attach(Catalog(), _rifleId, WeaponAttachments.None, _magazineId, out _), Is.Null);
            Assert.That(WeaponAttachments.Attach(Catalog(), _rifleId, WeaponAttachments.None, _pistolSightId, out _), Is.Null);
            Assert.That(WeaponAttachments.Attach(Catalog(), _pistolId, WeaponAttachments.None, _pistolSightId, out _), Is.EqualTo(new[] { _pistolSightId }));
            Assert.That(WeaponAttachments.Attach(Catalog(), _rifleId, WeaponAttachments.None, _rifleItemId, out _), Is.Null);
        }

        [Test]
        public void DetachRemovesOnlyWhatIsAttached()
        {
            Assert.That(WeaponAttachments.Detach(new[] { _scopeId, _suppressorId }, _suppressorId), Is.EqualTo(new[] { _scopeId }));
            Assert.That(WeaponAttachments.Detach(new[] { _scopeId }, _scopeId), Is.Empty);
            Assert.That(WeaponAttachments.Detach(new[] { _scopeId }, _suppressorId), Is.Null);
        }

        [Test]
        public void TheCatalogTellsWhichAttachmentsFitWhichWeapons()
        {
            var catalog = Catalog();

            Assert.That(catalog.SlotsOf(_rifleId), Is.EqualTo(Strings("optic", "muzzle")));
            Assert.That(catalog.SlotsOf("not-a-weapon"), Is.Empty);
            Assert.That(catalog.CanAttach(_rifleId, _scopeId), Is.True);
            Assert.That(catalog.CanAttach(_pistolId, _suppressorId), Is.False);
            Assert.That(catalog.TryFindAttachment(_scopeId, out _), Is.True);
            Assert.That(catalog.TryFindAttachment(_rifleItemId, out _), Is.False);
        }

        [Test]
        public void TheInventoryKeepsWhatEachHeldWeaponHasAttached()
        {
            var inventory = new Inventory(Catalog());
            var rifle = WeaponAttachments.Apply(_rifle, new[] { _suppressorId }, inventory.Catalog);
            var changes = 0;
            inventory.Changed += () => changes++;

            inventory.HoldWeapons(new[] { rifle }, id => id == _rifleId ? new[] { _suppressorId } : null);

            Assert.That(inventory.AttachmentsOf(_rifleId), Is.EqualTo(new[] { _suppressorId }));
            Assert.That(inventory.WeaponWeight, Is.EqualTo(3.5f).Within(1e-4f));
            Assert.That(changes, Is.EqualTo(1));

            inventory.HoldWeapons(new[] { rifle }, id => id == _rifleId ? new[] { _suppressorId } : null);
            Assert.That(changes, Is.EqualTo(1));

            inventory.HoldWeapons(new[] { _rifle }, null);
            Assert.That(inventory.AttachmentsOf(_rifleId), Is.Empty);
            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void DroppedWeaponsTakeTheirAttachmentsOffTheInventory()
        {
            var inventory = new Inventory(Catalog());
            inventory.HoldWeapons(new[] { _rifle }, id => new[] { _scopeId });

            inventory.HoldWeapons(Array.Empty<WeaponSpec>(), id => new[] { _scopeId });

            Assert.That(inventory.AttachmentsOf(_rifleId), Is.Empty);
        }

        [Test]
        public void ReplaceAttachmentsTakesWhatTheServerSent()
        {
            var inventory = new Inventory(Catalog());
            var changes = 0;
            inventory.Changed += () => changes++;

            inventory.ReplaceAttachments(new[] { new System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IReadOnlyList<string>>(_rifleId, new[] { _scopeId }) });

            Assert.That(inventory.AttachmentsOf(_rifleId), Is.EqualTo(new[] { _scopeId }));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void ClearAlsoForgetsTheAttachments()
        {
            var inventory = new Inventory(Catalog());
            inventory.HoldWeapons(new[] { _rifle }, id => new[] { _scopeId });

            inventory.Clear();

            Assert.That(inventory.AttachmentsOf(_rifleId), Is.Empty);
        }

        [Test]
        public void ASwappedOutAttachmentCanTakeThePlaceOfTheOneAttached()
        {
            // 1 マスに消音器が 1 つ。補正器を付けて消音器を外すと、消音器は補正器の空けたマスに戻せる
            var inventory = new Inventory(Catalog(new InventoryRule(gridWidth: 1, gridHeight: 1)));
            inventory.Add(_compensatorId, 1);
            var handle = inventory.Stacks[0].Handle;

            Assert.That(inventory.RoomFor(_suppressorId), Is.EqualTo(0));
            Assert.That(inventory.HasRoomAfterTaking(handle, _suppressorId), Is.True);
            Assert.That(inventory.HasRoomAfterTaking(handle + 1, _suppressorId), Is.False);
            Assert.That(inventory.HasRoomAfterTaking(handle, _rifleItemId), Is.False);
        }

        [Test]
        public void FreedWeightCountsAsSpareWeight()
        {
            // 重さの上限 3 kg に 3 kg の小銃。消音器 (0.5 kg) は入らないが、小銃から 0.5 kg 外すなら入る
            var inventory = new Inventory(Catalog(new InventoryRule(maxWeight: 3f, gridWidth: 4, gridHeight: 1)));
            inventory.HoldWeapons(new[] { _rifle });

            Assert.That(inventory.RoomFor(_suppressorId), Is.EqualTo(0));
            Assert.That(inventory.RoomFor(_suppressorId, 0.5f), Is.EqualTo(1));
        }

        [Test]
        public void ItemsCheckTheirAttachmentFields()
        {
            Assert.Throws<ArgumentException>(() => new ItemSpec(_scopeId, weaponId: _rifleId, attachment: new AttachmentSpec("optic")));
            Assert.Throws<ArgumentException>(() => new ItemSpec(_scopeId, useHealth: 10, attachment: new AttachmentSpec("optic")));
            Assert.Throws<ArgumentException>(() => new ItemSpec(_scopeId, attachmentSlots: Strings("optic")));
            Assert.Throws<ArgumentException>(() => new ItemSpec(_rifleItemId, weaponId: _rifleId, attachmentSlots: Strings("optic", "optic")));
            Assert.Throws<ArgumentException>(() => new AttachmentSpec("under-barrel"));
            Assert.Throws<ArgumentException>(() => new AttachmentSpec(" "));
            Assert.Throws<ArgumentException>(() => new AttachmentSpec("optic", zoomLevels: Array.Empty<float>()));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AttachmentSpec("optic", rangeMultiplier: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AttachmentSpec("optic", recoilMultiplier: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AttachmentSpec("optic", zoomLevels: Floats(180f)));
        }

        [Test]
        public void AnAttachmentForEveryWeaponFitsAnyNamedWeapon()
        {
            var any = new AttachmentSpec("optic");
            var only = new AttachmentSpec("optic", weaponIds: new[] { _pistolId, _pistolId });

            Assert.That(any.Fits(_rifleId), Is.True);
            Assert.That(any.Fits(null), Is.False);
            Assert.That(only.WeaponIds, Is.EqualTo(new[] { _pistolId }));
            Assert.That(only.Fits(_rifleId), Is.False);
        }

        [Test]
        public void TheJsonReadsAttachmentsAndWeaponSlots()
        {
            var catalog = InventoryJson.Parse(
                "{ \"items\": ["
                + " { \"id\": \"" + _rifleItemId + "\", \"weaponId\": \"" + _rifleId + "\", \"attachmentSlots\": [\"optic\", \"muzzle\"] },"
                + " { \"id\": \"" + _scopeId + "\", \"weight\": 0.75, \"attachment\": { \"slot\": \"optic\", \"weaponIds\": [\"" + _rifleId + "\"],"
                + " \"rangeMultiplier\": 1.1, \"hipSpreadMultiplier\": 1.2, \"adsSpreadMultiplier\": 0.5, \"moveSpreadMultiplier\": 1.3,"
                + " \"recoilMultiplier\": 0.9, \"reloadSecondsMultiplier\": 1.4, \"magazineSizeBonus\": 5, \"shotNoiseRadiusMultiplier\": 0.7,"
                + " \"moveSpeedMultiplier\": 0.95, \"zoomLevels\": [30, 15] } } ] }",
                "inventory.json");

            Assert.That(catalog.Items[0].AttachmentSlots, Is.EqualTo(Strings("optic", "muzzle")));
            var scope = catalog.Items[1];
            Assert.That(scope.IsAttachment, Is.True);
            Assert.That(scope.Weight, Is.EqualTo(0.75f));
            var a = scope.Attachment;
            Assert.That(a.Slot, Is.EqualTo("optic"));
            Assert.That(a.WeaponIds, Is.EqualTo(new[] { _rifleId }));
            Assert.That(a.RangeMultiplier, Is.EqualTo(1.1f));
            Assert.That(a.HipSpreadMultiplier, Is.EqualTo(1.2f));
            Assert.That(a.AdsSpreadMultiplier, Is.EqualTo(0.5f));
            Assert.That(a.MoveSpreadMultiplier, Is.EqualTo(1.3f));
            Assert.That(a.RecoilMultiplier, Is.EqualTo(0.9f));
            Assert.That(a.ReloadSecondsMultiplier, Is.EqualTo(1.4f));
            Assert.That(a.MagazineSizeBonus, Is.EqualTo(5));
            Assert.That(a.ShotNoiseRadiusMultiplier, Is.EqualTo(0.7f));
            Assert.That(a.MoveSpeedMultiplier, Is.EqualTo(0.95f));
            Assert.That(a.ZoomLevels, Is.EqualTo(Floats(30f, 15f)));
        }

        [Test]
        public void LeftOutAttachmentValuesChangeNothing()
        {
            var catalog = InventoryJson.Parse("{ \"items\": [ { \"id\": \"" + _scopeId + "\", \"attachment\": { \"slot\": \"optic\" } } ] }", "inventory.json");

            var a = catalog.Items[0].Attachment;
            Assert.That(a.WeaponIds, Is.Empty);
            Assert.That(a.RangeMultiplier, Is.EqualTo(1f));
            Assert.That(a.MagazineSizeBonus, Is.EqualTo(0));
            Assert.That(a.ZoomLevels, Is.Null);
            Assert.That(catalog.Items[0].AttachmentSlots, Is.Empty);
        }

        [Test]
        public void UnknownAttachmentValuesAreRefusedWithTheirPath()
        {
            var e = Assert.Throws<TuningJsonException>(() => InventoryJson.Parse(
                "{ \"items\": [ { \"id\": \"" + _scopeId + "\", \"attachment\": { \"slot\": \"optic\", \"zoom\": 2 } } ] }", "inventory.json"));

            Assert.That(e.Item, Is.EqualTo("items[0].attachment.zoom"));
        }

        [Test]
        public void BadAttachmentValuesNameTheItem()
        {
            var e = Assert.Throws<TuningJsonException>(() => InventoryJson.Parse(
                "{ \"items\": [ { \"id\": \"" + _scopeId + "\", \"attachment\": { \"slot\": \"optic\", \"rangeMultiplier\": 0 } } ] }", "inventory.json"));

            Assert.That(e.Item, Is.EqualTo("items[0].attachment.rangeMultiplier"));
        }

        [Test]
        public void AnAttachmentForAWeaponWithoutItsSlotIsRefused()
        {
            var e = Assert.Throws<TuningJsonException>(() => InventoryJson.Parse(
                "{ \"items\": ["
                + " { \"id\": \"" + _rifleItemId + "\", \"weaponId\": \"" + _rifleId + "\", \"attachmentSlots\": [\"muzzle\"] },"
                + " { \"id\": \"" + _scopeId + "\", \"attachment\": { \"slot\": \"optic\", \"weaponIds\": [\"" + _rifleId + "\"] } } ] }",
                "inventory.json"));

            Assert.That(e.Item, Is.EqualTo("items"));
        }

        [Test]
        public void AnAttachmentForAWeaponNotInTheItemsIsRefused()
        {
            var e = Assert.Throws<TuningJsonException>(() => InventoryJson.Parse(
                "{ \"items\": [ { \"id\": \"" + _scopeId + "\", \"attachment\": { \"slot\": \"optic\", \"weaponIds\": [\"" + _rifleId + "\"] } } ] }",
                "inventory.json"));

            Assert.That(e.Item, Is.EqualTo("items"));
        }

        [Test]
        public void WeaponSlotsOnANonWeaponAreRefusedWithTheirPath()
        {
            var e = Assert.Throws<TuningJsonException>(() => InventoryJson.Parse(
                "{ \"items\": [ { \"id\": \"" + _scopeId + "\", \"attachmentSlots\": [\"optic\"] } ] }", "inventory.json"));

            Assert.That(e.Item, Is.EqualTo("items[0].attachmentSlots"));
        }

        [Test]
        public void ASwapCountsTheHeavierAttachmentComingOffAsFreedWeight()
        {
            // 上限 3.5 kg に 3 kg の小銃と補正器 (0.25 kg) が 2 つ。消音器 (0.5 kg) は入らないが、補正器 1 つと付け替えて武器から 0.25 kg 外れるなら入る
            var inventory = new Inventory(Catalog(new InventoryRule(maxWeight: 3.5f, gridWidth: 4, gridHeight: 1)));
            inventory.HoldWeapons(new[] { _rifle });
            inventory.Add(_compensatorId, 2);
            var handle = inventory.Stacks.First(s => s.ItemId == _compensatorId).Handle;

            Assert.That(inventory.HasRoomAfterTaking(handle, _suppressorId), Is.False);
            Assert.That(inventory.HasRoomAfterTaking(handle, _suppressorId, 0.25f), Is.True);
        }

        [Test]
        public void AttachingMovesTheItemFromTheInventoryOntoTheWeapon()
        {
            var holder = new Holder(new Inventory(Catalog()));
            holder.Inventory.Add(_scopeId, 1);
            var handle = holder.Handle(_scopeId);
            var before = holder.Inventory.TotalWeight;

            var result = InventoryAttachments.Attach(holder.Inventory, handle, _rifleId, holder.Attached, holder.Set, out var itemId, out var replaced);

            Assert.That(result, Is.EqualTo(AttachmentRejection.None));
            Assert.That(itemId, Is.EqualTo(_scopeId));
            Assert.That(replaced, Is.Null);
            Assert.That(holder.Inventory.CountOf(_scopeId), Is.EqualTo(0));
            Assert.That(holder.Attached, Is.EqualTo(Strings(_scopeId)));
            Assert.That(holder.Inventory.AttachmentsOf(_rifleId), Is.EqualTo(Strings(_scopeId)));
            Assert.That(holder.Inventory.WeaponWeight, Is.EqualTo(3.75f).Within(1e-4f));
            Assert.That(holder.Inventory.TotalWeight, Is.EqualTo(before).Within(1e-4f));
            Assert.That(holder.CountWhenSet, Is.EqualTo(0), "the item leaves the stack before the weapon is rebuilt");
        }

        [Test]
        public void AttachingToAnOccupiedSlotPutsTheOldOneBack()
        {
            var holder = new Holder(new Inventory(Catalog()), _suppressorId);
            holder.Inventory.Add(_compensatorId, 1);

            var result = InventoryAttachments.Attach(
                holder.Inventory, holder.Handle(_compensatorId), _rifleId, holder.Attached, holder.Set, out _, out var replaced);

            Assert.That(result, Is.EqualTo(AttachmentRejection.None));
            Assert.That(replaced, Is.EqualTo(_suppressorId));
            Assert.That(holder.Attached, Is.EqualTo(Strings(_compensatorId)));
            Assert.That(holder.Inventory.CountOf(_suppressorId), Is.EqualTo(1));
            Assert.That(holder.Inventory.CountOf(_compensatorId), Is.EqualTo(0));
        }

        [Test]
        public void ASwapIntoAFullInventoryUsesTheFreedSpot()
        {
            // 3x1 に小銃 (2x1) と補正器。付け替えで外す消音器は補正器の空けたマスに入る
            var holder = new Holder(new Inventory(Catalog(new InventoryRule(gridWidth: 3, gridHeight: 1))), _suppressorId);
            holder.Inventory.Add(_compensatorId, 1);

            var result = InventoryAttachments.Attach(
                holder.Inventory, holder.Handle(_compensatorId), _rifleId, holder.Attached, holder.Set, out _, out _);

            Assert.That(result, Is.EqualTo(AttachmentRejection.None));
            Assert.That(holder.Inventory.CountOf(_suppressorId), Is.EqualTo(1));
        }

        [Test]
        public void AttachmentsThatDoNotFitStayInTheInventory()
        {
            var holder = new Holder(new Inventory(Catalog()));
            holder.Inventory.Add(_magazineId, 1);

            var result = InventoryAttachments.Attach(holder.Inventory, holder.Handle(_magazineId), _rifleId, holder.Attached, holder.Set, out _, out _);

            Assert.That(result, Is.EqualTo(AttachmentRejection.DoesNotFit));
            Assert.That(holder.Inventory.CountOf(_magazineId), Is.EqualTo(1));
            Assert.That(holder.Attached, Is.Empty);
            Assert.That(holder.Sets, Is.EqualTo(0));
        }

        [Test]
        public void AttachRefusesStacksThatAreNotAttachmentsAndHoldersWithoutAWeapon()
        {
            var holder = new Holder(new Inventory(Catalog()));
            holder.Inventory.Add(_scopeId, 1);

            Assert.That(
                InventoryAttachments.Attach(holder.Inventory, holder.Handle(_rifleItemId), _rifleId, holder.Attached, holder.Set, out _, out _),
                Is.EqualTo(AttachmentRejection.NotAnAttachment));
            Assert.That(
                InventoryAttachments.Attach(holder.Inventory, 999, _rifleId, holder.Attached, holder.Set, out _, out _),
                Is.EqualTo(AttachmentRejection.NoStack));
            Assert.That(
                InventoryAttachments.Attach(holder.Inventory, holder.Handle(_scopeId), null, holder.Attached, holder.Set, out _, out _),
                Is.EqualTo(AttachmentRejection.NoWeapon));
            Assert.That(holder.Sets, Is.EqualTo(0));
        }

        [Test]
        public void AttachRefusesAnAttachmentThatIsNotAvailableYet()
        {
            var holder = new Holder(new Inventory(Catalog()), _suppressorId);
            holder.Inventory.Add(_scopeId, 1);
            holder.Inventory.Add(_compensatorId, 1);
            var asked = new System.Collections.Generic.List<string>();
            Func<string, bool> isAvailable = id =>
            {
                asked.Add(id);
                return id != _scopeId && id != _compensatorId;
            };
            var before = holder.Inventory.TotalWeight;

            var scope = InventoryAttachments.Attach(
                holder.Inventory, holder.Handle(_scopeId), _rifleId, holder.Attached, holder.Set, out var itemId, out var replaced, isAvailable);
            var swap = InventoryAttachments.Attach(
                holder.Inventory, holder.Handle(_compensatorId), _rifleId, holder.Attached, holder.Set, out _, out var swapReplaced, isAvailable);

            Assert.That(scope, Is.EqualTo(AttachmentRejection.Locked));
            Assert.That(itemId, Is.EqualTo(_scopeId));
            Assert.That(replaced, Is.Null);
            Assert.That(swap, Is.EqualTo(AttachmentRejection.Locked));
            Assert.That(swapReplaced, Is.Null, "a locked swap does not take off what is on the weapon");
            Assert.That(asked, Is.EqualTo(Strings(_scopeId, _compensatorId)));
            Assert.That(holder.Sets, Is.EqualTo(0));
            Assert.That(holder.Attached, Is.EqualTo(Strings(_suppressorId)));
            Assert.That(holder.Inventory.CountOf(_scopeId), Is.EqualTo(1));
            Assert.That(holder.Inventory.CountOf(_compensatorId), Is.EqualTo(1));
            Assert.That(holder.Inventory.CountOf(_suppressorId), Is.EqualTo(0));
            Assert.That(holder.Inventory.TotalWeight, Is.EqualTo(before).Within(1e-4f));
        }

        [Test]
        public void AttachTakesAnAttachmentTheAvailabilityCheckAllows()
        {
            var holder = new Holder(new Inventory(Catalog()));
            holder.Inventory.Add(_scopeId, 1);

            var result = InventoryAttachments.Attach(
                holder.Inventory, holder.Handle(_scopeId), _rifleId, holder.Attached, holder.Set, out _, out _, id => id == _scopeId);

            Assert.That(result, Is.EqualTo(AttachmentRejection.None));
            Assert.That(holder.Attached, Is.EqualTo(Strings(_scopeId)));
            Assert.That(holder.Inventory.CountOf(_scopeId), Is.EqualTo(0));
        }

        [Test]
        public void AttachChecksAvailabilityOnlyForAttachments()
        {
            var holder = new Holder(new Inventory(Catalog()));
            var asked = 0;

            Assert.That(
                InventoryAttachments.Attach(
                    holder.Inventory, holder.Handle(_rifleItemId), _rifleId, holder.Attached, holder.Set, out _, out _, _ => { asked++; return false; }),
                Is.EqualTo(AttachmentRejection.NotAnAttachment));
            Assert.That(
                InventoryAttachments.Attach(
                    holder.Inventory, 999, _rifleId, holder.Attached, holder.Set, out _, out _, _ => { asked++; return false; }),
                Is.EqualTo(AttachmentRejection.NoStack));
            Assert.That(asked, Is.EqualTo(0));
        }

        [Test]
        public void DetachingPutsTheItemBackAndLightensTheWeapon()
        {
            var holder = new Holder(new Inventory(Catalog()), _scopeId, _suppressorId);

            var result = InventoryAttachments.Detach(holder.Inventory, _rifleId, holder.Attached, _scopeId, holder.Set);

            Assert.That(result, Is.EqualTo(AttachmentRejection.None));
            Assert.That(holder.Attached, Is.EqualTo(Strings(_suppressorId)));
            Assert.That(holder.Inventory.CountOf(_scopeId), Is.EqualTo(1));
            Assert.That(holder.Inventory.WeaponWeight, Is.EqualTo(3.5f).Within(1e-4f));
        }

        [Test]
        public void DetachingIntoAFullInventoryIsRefused()
        {
            // 3x1 に小銃 (2x1) と補正器で満杯
            var holder = new Holder(new Inventory(Catalog(new InventoryRule(gridWidth: 3, gridHeight: 1))), _scopeId);
            holder.Inventory.Add(_compensatorId, 1);

            var result = InventoryAttachments.Detach(holder.Inventory, _rifleId, holder.Attached, _scopeId, holder.Set);

            Assert.That(result, Is.EqualTo(AttachmentRejection.NoRoom));
            Assert.That(holder.Attached, Is.EqualTo(Strings(_scopeId)));
            Assert.That(holder.Sets, Is.EqualTo(0));
        }

        [Test]
        public void AnAttachThatTheWeaponRefusesPutsBothItemsBack()
        {
            var holder = new Holder(new Inventory(Catalog()), _suppressorId);
            holder.Inventory.Add(_compensatorId, 1);
            holder.Accepts = false;

            var result = InventoryAttachments.Attach(
                holder.Inventory, holder.Handle(_compensatorId), _rifleId, holder.Attached, holder.Set, out _, out _);

            Assert.That(result, Is.EqualTo(AttachmentRejection.NoRoom));
            Assert.That(holder.Attached, Is.EqualTo(Strings(_suppressorId)));
            Assert.That(holder.Inventory.CountOf(_compensatorId), Is.EqualTo(1));
            Assert.That(holder.Inventory.CountOf(_suppressorId), Is.EqualTo(0));
        }

        [Test]
        public void ADetachThatTheWeaponRefusesTakesTheItemBackOut()
        {
            var holder = new Holder(new Inventory(Catalog()), _scopeId);
            holder.Accepts = false;

            var result = InventoryAttachments.Detach(holder.Inventory, _rifleId, holder.Attached, _scopeId, holder.Set);

            Assert.That(result, Is.EqualTo(AttachmentRejection.NoRoom));
            Assert.That(holder.Attached, Is.EqualTo(Strings(_scopeId)));
            Assert.That(holder.Inventory.CountOf(_scopeId), Is.EqualTo(0));
        }

        [Test]
        public void ASwapIsWeighedAsItWillBeAndHoldsTheReplacedSpotBeforeTheWeaponIsRebuilt()
        {
            // 消音器 (0.5 kg) を付けた小銃 3.5 kg と補正器 0.25 kg で 3.75 kg。付け替えても合計は 3.75 kg のまま
            var holder = new Holder(new Inventory(Catalog()), _suppressorId);
            holder.Inventory.Add(_compensatorId, 1);
            var before = holder.Inventory.TotalWeight;

            var result = InventoryAttachments.Attach(
                holder.Inventory, holder.Handle(_compensatorId), _rifleId, holder.Attached, holder.Set, out _, out _);

            Assert.That(result, Is.EqualTo(AttachmentRejection.None));
            Assert.That(before, Is.EqualTo(3.75f).Within(1e-4f));
            Assert.That(holder.TotalWeightWhenSet, Is.EqualTo(3.75f).Within(1e-4f), "the overflowing rounds are judged against the weight after the swap");
            Assert.That(holder.SuppressorsWhenSet, Is.EqualTo(1), "the suppressor takes its spot before the rounds can");
            Assert.That(holder.Inventory.TotalWeight, Is.EqualTo(3.75f).Within(1e-4f));
            Assert.That(holder.Inventory.WeaponWeight, Is.EqualTo(3.25f).Within(1e-4f));
        }

        [Test]
        public void ADetachIsWeighedAsItWillBeAndHoldsTheItemsSpotBeforeTheWeaponIsRebuilt()
        {
            // スコープ (0.75 kg) を付けた小銃で 3.75 kg。外しても合計は 3.75 kg のまま
            var holder = new Holder(new Inventory(Catalog()), _scopeId);

            var result = InventoryAttachments.Detach(holder.Inventory, _rifleId, holder.Attached, _scopeId, holder.Set);

            Assert.That(result, Is.EqualTo(AttachmentRejection.None));
            Assert.That(holder.TotalWeightWhenSet, Is.EqualTo(3.75f).Within(1e-4f), "the scope is not counted on both the weapon and the inventory");
            Assert.That(holder.CountWhenSet, Is.EqualTo(1), "the scope takes its spot before the rounds can");
            Assert.That(holder.Inventory.TotalWeight, Is.EqualTo(3.75f).Within(1e-4f));
            Assert.That(holder.Inventory.WeaponWeight, Is.EqualTo(3f).Within(1e-4f));
        }

        [Test]
        public void RefusedSwapsAndDetachesPutTheWeaponWeightBack()
        {
            var swap = new Holder(new Inventory(Catalog()), _suppressorId);
            swap.Inventory.Add(_compensatorId, 1);
            swap.Accepts = false;
            InventoryAttachments.Attach(swap.Inventory, swap.Handle(_compensatorId), _rifleId, swap.Attached, swap.Set, out _, out _);

            Assert.That(swap.Inventory.WeaponWeight, Is.EqualTo(3.5f).Within(1e-4f));
            Assert.That(swap.Inventory.TotalWeight, Is.EqualTo(3.75f).Within(1e-4f));

            var detach = new Holder(new Inventory(Catalog()), _scopeId);
            detach.Accepts = false;
            InventoryAttachments.Detach(detach.Inventory, _rifleId, detach.Attached, _scopeId, detach.Set);

            Assert.That(detach.Inventory.WeaponWeight, Is.EqualTo(3.75f).Within(1e-4f));
            Assert.That(detach.Inventory.TotalWeight, Is.EqualTo(3.75f).Within(1e-4f));
        }

        [Test]
        public void WeaponWeightsCanBeSetOnlyForHeldWeapons()
        {
            var inventory = new Inventory(Catalog());
            inventory.HoldWeapons(new[] { _rifle });

            inventory.SetWeaponWeight(_rifleId, 4f);
            inventory.SetWeaponWeight(_pistolId, 1f);

            Assert.That(inventory.TryGetWeaponWeight(_rifleId, out var rifle), Is.True);
            Assert.That(rifle, Is.EqualTo(4f));
            Assert.That(inventory.TryGetWeaponWeight(_pistolId, out _), Is.False);
            Assert.That(inventory.WeaponWeight, Is.EqualTo(4f));
        }

        [Test]
        public void DetachingWhatIsNotAttachedIsRefused()
        {
            var holder = new Holder(new Inventory(Catalog()), _scopeId);

            Assert.That(InventoryAttachments.Detach(holder.Inventory, _rifleId, holder.Attached, _suppressorId, holder.Set), Is.EqualTo(AttachmentRejection.NotAttached));
            Assert.That(InventoryAttachments.Detach(holder.Inventory, null, holder.Attached, _scopeId, holder.Set), Is.EqualTo(AttachmentRejection.NoWeapon));
        }

        // 小銃 1 本を持ち、付けた物を持ち物に知らせる持ち主
        private sealed class Holder
        {
            public Holder(Inventory inventory, params string[] attached)
            {
                Inventory = inventory;
                Attached = attached;
                Inventory.HoldWeapons(new[] { WeaponAttachments.Apply(_rifle, Attached, Inventory.Catalog) }, _ => Attached);
            }

            public Inventory Inventory { get; }

            public System.Collections.Generic.IReadOnlyList<string> Attached { get; private set; }

            public int Sets { get; private set; }

            public int CountWhenSet { get; private set; } = -1;

            public int SuppressorsWhenSet { get; private set; } = -1;

            public float TotalWeightWhenSet { get; private set; } = -1f;

            public int Handle(string itemId) => Inventory.Stacks.First(s => s.ItemId == itemId).Handle;

            public bool Accepts { get; set; } = true;

            public bool Set(System.Collections.Generic.IReadOnlyList<string> next)
            {
                Sets++;
                CountWhenSet = Inventory.CountOf(_scopeId);
                SuppressorsWhenSet = Inventory.CountOf(_suppressorId);
                TotalWeightWhenSet = Inventory.TotalWeight;
                if (!Accepts)
                {
                    return false;
                }

                Attached = next;
                Inventory.HoldWeapons(new[] { WeaponAttachments.Apply(_rifle, next, Inventory.Catalog) }, _ => Attached);
                return true;
            }
        }

        [Test]
        public void AnOpticWithASightReplacesTheWeaponsSight()
        {
            var holo = new ItemSpec(_pistolSightId, "item.holo", attachment: new AttachmentSpec("optic", sight: new SightSpec("holo")));
            var scoped = new WeaponSpec(_rifleId, "weapon.rifle", WeaponCategory.AssaultRifle, aim: new AimSpec(sight: new SightSpec("mildot", scopeOverlay: true)));
            var catalog = new ItemCatalog(new[] { _rifleItem, holo, _suppressor }, InventoryRule.Default);

            Assert.That(WeaponAttachments.Apply(scoped, new[] { _pistolSightId }, catalog).Aim.Sight.ReticleId, Is.EqualTo("holo"));
            Assert.That(WeaponAttachments.Apply(scoped, new[] { _pistolSightId }, catalog).Aim.Sight.ScopeOverlay, Is.False);
            Assert.That(WeaponAttachments.Apply(scoped, new[] { _suppressorId }, catalog).Aim.Sight, Is.SameAs(scoped.Aim.Sight), "a muzzle keeps the sight");
        }

        [Test]
        public void AnOpticThatOnlySetsTheZoomDropsTheWeaponsScope()
        {
            var scoped = new WeaponSpec(_rifleId, "weapon.rifle", WeaponCategory.AssaultRifle, aim: new AimSpec(sight: new SightSpec("mildot", scopeOverlay: true)));

            var spec = WeaponAttachments.Apply(scoped, new[] { _scopeId }, Catalog());

            Assert.That(spec.Aim.Sight, Is.SameAs(SightSpec.Default));
            Assert.That(spec.Aim.ZoomLevels, Is.EqualTo(Floats(30f, 15f)));
        }
    }
}
