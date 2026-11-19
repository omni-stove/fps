using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 移動・ルール・戦闘・持ち物を同じホストで動かし、出現で持たせる・頼みを確かめる・持ち主に送るを確かめる
    public sealed class ServerInventoriesTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const string _herbId = "0d3c6f1a-5b2e-4c7d-8a9f-1e2b3c4d5e6f";
        private const string _caseId = "2f4e6d8c-0b1a-4c3e-8d5f-7a9b1c3d5e7f";
        private const string _rifleAmmoId = "1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d";
        private const string _pistolAmmoId = "3c4d5e6f-7a8b-4c9d-8e1f-2a3b4c5d6e7f";
        private const string _rifleItemId = "4d5e6f7a-8b9c-4d0e-9f2a-3b4c5d6e7f8a";
        private const string _handgunItemId = "5e6f7a8b-9c0d-4e1f-8a3b-4c5d6e7f8a9b";
        private const string _magazineId = "6f7a8b9c-0d1e-4f2a-9b4c-5d6e7f8a9b0c";
        private const string _suppressorId = "7a8b9c0d-1e2f-4a3b-8c5d-6e7f8a9b0c1d";
        private static readonly DateTime _time = new DateTime(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Vector3 _alphaSpawn = new Vector3(-5f, 1.6f, 0f);
        private static readonly Vector3 _bravoSpawn = new Vector3(5f, 1.6f, 0f);

        private static readonly ItemSpec _herb = new ItemSpec(_herbId, "item.herb", maxStack: 3, weight: 0.5f, useHealth: 30);
        private static readonly ItemSpec _case = new ItemSpec(_caseId, "item.case", weight: 4f, width: 2, height: 1);
        private static readonly string[] _rifleSlots = { "muzzle", "magazine" };

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private MatchRulesModule _rules;
        private ServerCombat _combat;
        private ServerInventories _inventories;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null && _host.State == ServerHostState.Running)
            {
                _host.Stop();
            }

            _transport.Dispose();
            _log.Dispose();
            _json.Dispose();
        }

        [Test]
        public void SpawningGivesTheStartingItemsAndTellsOnlyTheOwner()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);

            var states = _transport.Read<InventoryStateMessage>(1, Copy);
            Assert.That(states, Is.Not.Empty, _log.ToString());
            var last = states.Last();
            Assert.That(last.GridWidth, Is.EqualTo(4));
            Assert.That(last.GridHeight, Is.EqualTo(2));
            Assert.That(Enumerable.Range(0, last.EntryCount).Sum(i => last.EntryAt(i).Count), Is.EqualTo(5));
            Assert.That(_inventories.InventoryOf(1).CountOf(_herbId), Is.EqualTo(4));
            Assert.That(_inventories.InventoryOf(1).CountOf(_caseId), Is.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Inventory] items=2 starting=2 maxStacks=0 maxWeight=0 grid=4x2 rotation=on carryLoad=on"));
        }

        [Test]
        public void TheCountedWeightIsCopiedToTheBody()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);

            // ケース 4 kg + ハーブ 4 個 × 0.5 kg
            Assert.That(Body(1).CarriedItemWeight, Is.EqualTo(6f).Within(1e-4f));
        }

        [Test]
        public void MovingAStackIsCheckedAndTheResultIsSentBack()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);
            var inventory = _inventories.InventoryOf(1);
            var caseHandle = inventory.Stacks.Single(s => s.ItemId == _caseId).Handle;
            var sentBefore = _transport.Read<InventoryStateMessage>(1, Copy).Count;

            Request(1, new InventoryRequestMessage { Action = InventoryAction.Move, Handle = (ushort)caseHandle, X = 3, Y = 0 });
            Tick();

            Assert.That(_log.ToString(), Does.Contain("[Inventory] move owner=1 handle=" + caseHandle + " to=3,0 result=OutOfGrid"));
            Assert.That(_transport.Read<InventoryStateMessage>(1, Copy), Has.Count.EqualTo(sentBefore + 1), "a refused move still resends the state");

            Request(1, new InventoryRequestMessage { Action = InventoryAction.Move, Handle = (ushort)caseHandle, X = 2, Y = 1 });
            Tick();

            Assert.That(inventory.TryGetStack(caseHandle, out var moved), Is.True);
            Assert.That(moved.X, Is.EqualTo(2));
            Assert.That(moved.Y, Is.EqualTo(1));
        }

        [Test]
        public void UsingAnItemHealsAndTakesOne()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);
            _rules.ApplyDamage(2, 1, 50);
            var inventory = _inventories.InventoryOf(1);
            var herbHandle = inventory.Stacks.First(s => s.ItemId == _herbId).Handle;

            Request(1, new InventoryRequestMessage { Action = InventoryAction.Use, Handle = (ushort)herbHandle });
            Tick();

            Assert.That(_rules.Session.GetHealth(1), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 20), _log.ToString());
            Assert.That(inventory.CountOf(_herbId), Is.EqualTo(3));
            Assert.That(_log.ToString(), Does.Contain("[Inventory] use owner=1 item=" + _herbId + " health=+30"));
        }

        [Test]
        public void UsingAtFullHealthOrAnUnusableItemIsRefused()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);
            var inventory = _inventories.InventoryOf(1);
            var herbHandle = inventory.Stacks.First(s => s.ItemId == _herbId).Handle;
            var caseHandle = inventory.Stacks.Single(s => s.ItemId == _caseId).Handle;

            Request(1, new InventoryRequestMessage { Action = InventoryAction.Use, Handle = (ushort)herbHandle });
            Request(1, new InventoryRequestMessage { Action = InventoryAction.Use, Handle = (ushort)caseHandle });
            Tick();

            Assert.That(inventory.CountOf(_herbId), Is.EqualTo(4));
            Assert.That(inventory.CountOf(_caseId), Is.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Inventory] use owner=1 handle=" + herbHandle + " rejected=Full"));
            Assert.That(_log.ToString(), Does.Contain("[Inventory] use owner=1 handle=" + caseHandle + " rejected=NotUsable"));
        }

        [Test]
        public void DroppingDiscardsTheCountAndLightensTheBody()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);
            var inventory = _inventories.InventoryOf(1);
            var caseHandle = inventory.Stacks.Single(s => s.ItemId == _caseId).Handle;

            Request(1, new InventoryRequestMessage { Action = InventoryAction.Drop, Handle = (ushort)caseHandle, Count = 1 });
            Tick();

            Assert.That(inventory.CountOf(_caseId), Is.Zero);
            Assert.That(Body(1).CarriedItemWeight, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(_log.ToString(), Does.Contain("[Inventory] drop owner=1 item=" + _caseId + " count=1 discarded"));
        }

        [Test]
        public void DyingAndRespawningRefillsTheStartingItems()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);
            var inventory = _inventories.InventoryOf(1);
            inventory.Remove(_herbId, 4);

            _rules.ApplyDamage(2, 1, 1000);
            Step(NetMatchSettings.DefaultRespawnSeconds + 0.5f);

            Assert.That(_rules.Session.IsAlive(1), Is.True, _log.ToString());
            Assert.That(inventory.CountOf(_herbId), Is.EqualTo(4));
        }

        [Test]
        public void SpawningPutsTheLoadoutIntoTheInventoryWithItsReserveAsAmmo()
        {
            Start(WeaponCatalog());
            Join(1);
            Join(2);
            Step(0.2f);

            var inventory = _inventories.InventoryOf(1);
            Assert.That(inventory.HoldsWeapon(TestRules.RifleId), Is.True, _log.ToString());
            Assert.That(inventory.HoldsWeapon(TestRules.HandgunId), Is.True);
            Assert.That(inventory.CountOf(_rifleAmmoId), Is.EqualTo(90));
            Assert.That(inventory.CountOf(_pistolAmmoId), Is.EqualTo(48));
            Assert.That(_combat.WeaponsOf(1).Items[0].ReserveAmmo, Is.EqualTo(90));
            Assert.That(_combat.WeaponsOf(1).Items[1].ReserveAmmo, Is.EqualTo(48));
        }

        [Test]
        public void TheWeaponsWeightIsSentButLeftOutOfTheCarriedItemWeight()
        {
            Start(WeaponCatalog(), new WeaponSpec(
                TestRules.RifleId, "weapon.rifle", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0.1f, 2f, 25, 100f, DamageFalloff.None, weight: 3.5f));
            Join(1);
            Join(2);
            Step(0.2f);

            // 弾 90 + 48 発 × 0.01 kg。小銃の 3.5 kg は移動のルールが武器として数える
            Assert.That(Body(1).CarriedItemWeight, Is.EqualTo(1.38f).Within(1e-4f));
            Assert.That(_inventories.InventoryOf(1).TotalWeight, Is.EqualTo(4.88f).Within(1e-4f));
            var last = _transport.Read<InventoryStateMessage>(1, Copy).Last();
            var rifle = Enumerable.Range(0, last.ItemCount).Select(last.ItemAt).Single(i => i.ItemId == _rifleItemId);
            Assert.That(rifle.Weight, Is.EqualTo(3.5f));
        }

        [Test]
        public void UsingAWeaponItemSwitchesToThatWeapon()
        {
            Start(WeaponCatalog());
            Join(1);
            Join(2);
            Step(0.2f);
            var inventory = _inventories.InventoryOf(1);
            var handgun = inventory.Stacks.Single(s => s.ItemId == _handgunItemId).Handle;

            Request(1, new InventoryRequestMessage { Action = InventoryAction.Use, Handle = (ushort)handgun });
            Tick();

            Assert.That(_combat.WeaponsOf(1).CurrentSpec.Id, Is.EqualTo(TestRules.HandgunId));
            Assert.That(inventory.HoldsWeapon(TestRules.HandgunId), Is.True);
            Assert.That(_log.ToString(), Does.Contain("[Inventory] equip owner=1 weapon=" + TestRules.HandgunId + Environment.NewLine));
        }

        [Test]
        public void DroppingAWeaponItemIsRefused()
        {
            Start(WeaponCatalog());
            Join(1);
            Join(2);
            Step(0.2f);
            var inventory = _inventories.InventoryOf(1);
            var rifle = inventory.Stacks.Single(s => s.ItemId == _rifleItemId).Handle;

            Request(1, new InventoryRequestMessage { Action = InventoryAction.Drop, Handle = (ushort)rifle, Count = 1 });
            Tick();

            Assert.That(inventory.HoldsWeapon(TestRules.RifleId), Is.True);
            Assert.That(_combat.WeaponsOf(1).Items, Has.Count.EqualTo(2));
            Assert.That(_log.ToString(), Does.Contain("[Inventory] drop owner=1 weapon=" + TestRules.RifleId + " rejected=weapon"));
        }

        [Test]
        public void ReloadingTakesRoundsFromTheInventory()
        {
            Start(WeaponCatalog());
            Join(1);
            Join(2);
            Step(0.2f);
            var weapon = _combat.WeaponsOf(1).Items[0];
            for (var i = 0; i < 5; i++)
            {
                weapon.TryFire();
            }

            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(2f);

            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(30));
            Assert.That(_inventories.InventoryOf(1).CountOf(_rifleAmmoId), Is.EqualTo(85));
        }

        [Test]
        public void RespawningRestocksTheAmmoWithoutGoingOverTheStartingReserve()
        {
            Start(WeaponCatalog());
            Join(1);
            Join(2);
            Step(0.2f);
            var inventory = _inventories.InventoryOf(1);
            inventory.Remove(_rifleAmmoId, 70);

            _rules.ApplyDamage(2, 1, 1000);
            Step(NetMatchSettings.DefaultRespawnSeconds + 0.5f);

            Assert.That(_rules.Session.IsAlive(1), Is.True, _log.ToString());
            Assert.That(inventory.CountOf(_rifleAmmoId), Is.EqualTo(90));
            Assert.That(inventory.CountOf(_pistolAmmoId), Is.EqualTo(48));
            Assert.That(inventory.HoldsWeapon(TestRules.RifleId), Is.True);
        }

        [Test]
        public void AttachingFromTheInventoryChangesTheHeldWeapon()
        {
            Start(AttachmentCatalog());
            Join(1);
            Join(2);
            Step(0.2f);
            var inventory = _inventories.InventoryOf(1);
            var magazine = inventory.Stacks.Single(s => s.ItemId == _magazineId).Handle;

            Request(1, new InventoryRequestMessage { Action = InventoryAction.Attach, Handle = (ushort)magazine });
            Tick();

            var rifle = _combat.WeaponsOf(1).Items[0];
            Assert.That(rifle.Spec.MagazineSize, Is.EqualTo(40), _log.ToString());
            Assert.That(rifle.AmmoInMagazine, Is.EqualTo(30));
            Assert.That(_combat.WeaponsOf(1).Specs[0].MagazineSize, Is.EqualTo(40));
            Assert.That(_combat.WeaponsOf(1).AttachmentsOf(TestRules.RifleId), Is.EqualTo(new[] { _magazineId }));
            Assert.That(inventory.CountOf(_magazineId), Is.EqualTo(0));
            Assert.That(inventory.AttachmentsOf(TestRules.RifleId), Is.EqualTo(new[] { _magazineId }));
            Assert.That(
                _log.ToString(),
                Does.Contain("[Inventory] attach owner=1 handle=" + magazine + " item=" + _magazineId + " weapon=" + TestRules.RifleId + " replaced=-" + Environment.NewLine));

            var last = _transport.Read<InventoryStateMessage>(1, Copy).Last();
            var rifleEntry = Enumerable.Range(0, last.EntryCount).Single(i => last.ItemAt(last.EntryAt(i).ItemIndex).ItemId == _rifleItemId);
            Assert.That(last.EntryAt(rifleEntry).AttachedCount, Is.EqualTo(1));
            Assert.That(last.ItemAt(last.AttachedItemAt(rifleEntry, 0)).ItemId, Is.EqualTo(_magazineId));
        }

        [Test]
        public void DetachingPutsTheItemBackAndTheExtraRoundsIntoTheAmmo()
        {
            Start(AttachmentCatalog());
            Join(1);
            Join(2);
            Step(0.2f);
            var inventory = _inventories.InventoryOf(1);
            Request(1, new InventoryRequestMessage { Action = InventoryAction.Attach, Handle = (ushort)inventory.Stacks.Single(s => s.ItemId == _magazineId).Handle });
            Tick();
            var rifle = _combat.WeaponsOf(1).Items[0];
            Assert.That(rifle.TryStartReload(), Is.True);
            rifle.Tick(2f);
            Assert.That(rifle.AmmoInMagazine, Is.EqualTo(40));
            Assert.That(inventory.CountOf(_rifleAmmoId), Is.EqualTo(80));
            var rifleStack = inventory.Stacks.Single(s => s.ItemId == _rifleItemId).Handle;

            Request(1, new InventoryRequestMessage { Action = InventoryAction.Detach, Handle = (ushort)rifleStack, ItemId = _magazineId });
            Tick();

            Assert.That(rifle.Spec.MagazineSize, Is.EqualTo(30), _log.ToString());
            Assert.That(rifle.AmmoInMagazine, Is.EqualTo(30));
            Assert.That(inventory.CountOf(_rifleAmmoId), Is.EqualTo(90));
            Assert.That(inventory.CountOf(_magazineId), Is.EqualTo(1));
            Assert.That(inventory.AttachmentsOf(TestRules.RifleId), Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Inventory] detach owner=1 weapon=" + TestRules.RifleId + " item=" + _magazineId + Environment.NewLine));
        }

        [Test]
        public void AnAttachmentThatDoesNotFitTheHeldWeaponIsRefused()
        {
            Start(AttachmentCatalog());
            Join(1);
            Join(2);
            Step(0.2f);
            var inventory = _inventories.InventoryOf(1);
            Request(1, new InventoryRequestMessage { Action = InventoryAction.Use, Handle = (ushort)inventory.Stacks.Single(s => s.ItemId == _handgunItemId).Handle });
            Tick();

            Request(1, new InventoryRequestMessage { Action = InventoryAction.Attach, Handle = (ushort)inventory.Stacks.Single(s => s.ItemId == _suppressorId).Handle });
            Tick();

            Assert.That(inventory.CountOf(_suppressorId), Is.EqualTo(1));
            Assert.That(_combat.WeaponsOf(1).AttachmentsOf(TestRules.HandgunId), Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("rejected=DoesNotFit"));
        }

        [Test]
        public void RespawningTakesTheAttachmentsOffWithTheStartingItems()
        {
            Start(AttachmentCatalog());
            Join(1);
            Join(2);
            Step(0.2f);
            var inventory = _inventories.InventoryOf(1);
            Request(1, new InventoryRequestMessage { Action = InventoryAction.Attach, Handle = (ushort)inventory.Stacks.Single(s => s.ItemId == _magazineId).Handle });
            Tick();

            _rules.ApplyDamage(2, 1, 1000);
            Step(NetMatchSettings.DefaultRespawnSeconds + 0.5f);

            Assert.That(_rules.Session.IsAlive(1), Is.True, _log.ToString());
            Assert.That(_combat.WeaponsOf(1).Items[0].Spec.MagazineSize, Is.EqualTo(30));
            Assert.That(inventory.AttachmentsOf(TestRules.RifleId), Is.Empty);
            Assert.That(inventory.CountOf(_magazineId), Is.EqualTo(1));
        }

        [Test]
        public void GivingPutsWhatFitsAndTellsTheOwner()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);
            var inventory = _inventories.InventoryOf(1);
            inventory.Remove(_herbId, 4);
            var sentBefore = _transport.Read<InventoryStateMessage>(1, Copy).Count;

            // 4x2 にケース (2x1) が 1 つ。残り 6 マスにハーブ 3 個の山が 6 つまで入る
            var given = _inventories.Give(1, _herbId, 20);
            Tick();

            Assert.That(given, Is.EqualTo(18));
            Assert.That(inventory.CountOf(_herbId), Is.EqualTo(18));
            Assert.That(_transport.Read<InventoryStateMessage>(1, Copy), Has.Count.EqualTo(sentBefore + 1));
            Assert.That(_log.ToString(), Does.Contain("[Inventory] give owner=1 item=" + _herbId + " count=18/20 partial"));
        }

        [Test]
        public void GivingAnUnknownItemOrToSomeoneWithoutAnInventoryGivesNothing()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);

            Assert.That(_inventories.Give(1, "no-such-item", 1), Is.Zero);
            Assert.That(_inventories.Give(99, _herbId, 1), Is.Zero);
            Assert.That(_inventories.Give(1, _herbId, 0), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => _inventories.Give(1, _herbId, -1));
        }

        [Test]
        public void TakingReturnsEveryStackAndLeavesTheWeapons()
        {
            Start(WeaponCatalog());
            Join(1);
            Join(2);
            Step(0.2f);
            var inventory = _inventories.InventoryOf(1);

            var taken = _inventories.Take(1);

            // 小銃の弾 90 発 (60 + 30) と拳銃の弾 48 発
            Assert.That(taken.Where(i => i.ItemId == _rifleAmmoId).Sum(i => i.Count), Is.EqualTo(90));
            Assert.That(taken.Where(i => i.ItemId == _pistolAmmoId).Sum(i => i.Count), Is.EqualTo(48));
            Assert.That(taken.Any(i => i.ItemId == _rifleItemId || i.ItemId == _handgunItemId), Is.False, "weapons are taken through ServerPlayerWeapons");
            Assert.That(inventory.CountOf(_rifleAmmoId), Is.Zero);
            Assert.That(inventory.CountOf(_pistolAmmoId), Is.Zero);
            Assert.That(inventory.HoldsWeapon(TestRules.RifleId), Is.True);
            Assert.That(inventory.HoldsWeapon(TestRules.HandgunId), Is.True);
            Assert.That(_log.ToString(), Does.Contain("[Inventory] take owner=1 stacks=" + taken.Count + " items=138"));
            Assert.That(_inventories.Take(99), Is.Empty);
        }

        [Test]
        public void ClearingEmptiesTheItemsAndLightensTheBody()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);

            _inventories.Clear(1);
            Tick();

            Assert.That(_inventories.InventoryOf(1).Stacks, Is.Empty);
            Assert.That(Body(1).CarriedItemWeight, Is.Zero);
            Assert.That(_inventories.Take(1), Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Inventory] clear owner=1 items=5"));
        }

        [Test]
        public void TakenWeaponsLeaveTheInventoryOnceItIsHeldAgain()
        {
            Start(WeaponCatalog());
            Join(1);
            Join(2);
            Step(0.2f);
            var weapons = _combat.WeaponsOf(1);
            var inventory = _inventories.InventoryOf(1);

            weapons.Take();
            _inventories.HoldWeapons(1);

            Assert.That(inventory.HoldsWeapon(TestRules.RifleId), Is.False);
            Assert.That(inventory.HoldsWeapon(TestRules.HandgunId), Is.False);

            Assert.That(weapons.Give(TestRules.Handgun(), 0.4f, Rules()), Is.True);
            var missing = _inventories.HoldWeapons(1);

            Assert.That(missing, Is.Empty);
            Assert.That(inventory.HoldsWeapon(TestRules.HandgunId), Is.True);
        }

        // 8x4 の持ち物に、小銃 (4x2・5.56) と拳銃 (2x1・9mm) と、1 発 0.01 kg・60 発で 1 山の弾薬
        private static ItemCatalog WeaponCatalog()
        {
            return new ItemCatalog(
                new[]
                {
                    new ItemSpec(_rifleAmmoId, "item.ammo556", maxStack: 60, weight: 0.01f),
                    new ItemSpec(_pistolAmmoId, "item.ammo9mm", maxStack: 60, weight: 0.01f),
                    new ItemSpec(_rifleItemId, "weapon.rifle", width: 4, height: 2, weaponId: TestRules.RifleId, ammoItemId: _rifleAmmoId),
                    new ItemSpec(_handgunItemId, "weapon.handgun", width: 2, height: 1, weaponId: TestRules.HandgunId, ammoItemId: _pistolAmmoId),
                },
                new InventoryRule(gridWidth: 8, gridHeight: 4, countWeightInCarryLoad: true));
        }

        // WeaponCatalog に、小銃にだけ付く拡張弾倉 (+10 発) と、小銃の銃口に付く消音器を足し、1 つずつ持って始める
        private static ItemCatalog AttachmentCatalog()
        {
            return new ItemCatalog(
                new[]
                {
                    new ItemSpec(_rifleAmmoId, "item.ammo556", maxStack: 60, weight: 0.01f),
                    new ItemSpec(_pistolAmmoId, "item.ammo9mm", maxStack: 60, weight: 0.01f),
                    new ItemSpec(
                        _rifleItemId, "weapon.rifle", width: 4, height: 2, weaponId: TestRules.RifleId, ammoItemId: _rifleAmmoId,
                        attachmentSlots: _rifleSlots),
                    new ItemSpec(_handgunItemId, "weapon.handgun", width: 2, height: 1, weaponId: TestRules.HandgunId, ammoItemId: _pistolAmmoId),
                    new ItemSpec(_magazineId, "item.extendedMagazine", weight: 0.3f, attachment: new AttachmentSpec("magazine", magazineSizeBonus: 10)),
                    new ItemSpec(_suppressorId, "item.suppressor", weight: 0.4f, attachment: new AttachmentSpec("muzzle", shotNoiseRadiusMultiplier: 0.3f)),
                },
                new InventoryRule(gridWidth: 8, gridHeight: 4, countWeightInCarryLoad: true),
                new[] { new ItemCount(_magazineId, 1), new ItemCount(_suppressorId, 1) });
        }

        private static ItemCatalog Catalog()
        {
            return new ItemCatalog(
                new[] { _herb, _case },
                new InventoryRule(gridWidth: 4, gridHeight: 2, countWeightInCarryLoad: true),
                new[] { new ItemCount(_caseId, 1), new ItemCount(_herbId, 4) });
        }

        private static GameRules Rules()
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off);
            return GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
        }

        private void Start(ItemCatalog items, WeaponSpec rifle = null)
        {
            var colliders = new List<WorldCollider>
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(60f, 0.5f, 60f), Quaternion.Identity),
            };
            var points = new NetMapPoints(new[] { new SpawnPoint(_alphaSpawn, Team.Alpha), new SpawnPoint(_bravoSpawn, Team.Bravo) }, Array.Empty<ControlPointArea>());
            var map = new ServerMapData("NetTest", new WorldMap(colliders), TestMaps.NavMesh(), "memory", points, null);
            var data = new ServerRulesData("TeamDeathmatch", TestRules.File(Rules(), rifle: rifle), PingSpec.Default, "memory", null, items: items);
            _match = new ServerMatch(NetMatchSettings.DefaultRestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time);
            _context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                map,
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                data);
            var movement = new ServerMovement(ServerModules.MotorSettingsFrom(data));
            _rules = new MatchRulesModule();
            _combat = new ServerCombat(_rules, movement);
            _inventories = new ServerInventories(_rules, _combat);
            _host = new ServerHost();
            _host.Register(movement);
            _host.Register(_rules);
            _host.Register(_combat);
            _host.Register(_inventories);
            _host.Start(_context);
        }

        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        private void Request(int id, InventoryRequestMessage request)
        {
            _host.OnMessage(new ConnectionId((ulong)id), request);
        }

        private ServerBody Body(int id)
        {
            Assert.That(_context.Bodies.TryGet(id, out var body), Is.True);
            return body;
        }

        private void Step(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                Tick();
            }
        }

        private void Tick()
        {
            _match.Tick(_tickSeconds);
            _host.Tick(_tickSeconds);
        }

        private static InventoryStateMessage Copy(InventoryStateMessage m)
        {
            var copy = new InventoryStateMessage
            {
                MaxStacks = m.MaxStacks,
                MaxWeight = m.MaxWeight,
                GridWidth = m.GridWidth,
                GridHeight = m.GridHeight,
                AllowRotation = m.AllowRotation,
            };
            for (var i = 0; i < m.ItemCount; i++)
            {
                copy.AddItem(m.ItemAt(i));
            }

            for (var i = 0; i < m.EntryCount; i++)
            {
                var attached = new byte[m.EntryAt(i).AttachedCount];
                for (var a = 0; a < attached.Length; a++)
                {
                    attached[a] = m.AttachedItemAt(i, a);
                }

                copy.AddEntry(m.EntryAt(i), attached);
            }

            return copy;
        }
    }
}
