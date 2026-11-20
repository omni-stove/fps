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
    // 移動・ルール・戦闘・持ち物と箱を同じホストで動かし、置く・開ける・断る・スナップショットに出すを確かめる
    public sealed class ServerLootTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const int _seed = 1416;
        private const string _herbId = "0d3c6f1a-5b2e-4c7d-8a9f-1e2b3c4d5e6f";
        private const string _rifleItemId = "4d5e6f7a-8b9c-4d0e-9f2a-3b4c5d6e7f8a";
        private const string _handgunItemId = "5e6f7a8b-9c0d-4e1f-8a3b-4c5d6e7f8a9b";
        private const string _shotgunItemId = "8b9c0d1e-2f3a-4b4c-9d6e-7f8a9b0c1d2e";
        private const string _shotgunId = "9c0d1e2f-3a4b-4c5d-8e7f-8a9b0c1d2e3f";
        private const string _supplies = "supplies";
        private const string _armory = "armory";
        private const int _firstBox = ServerLoot.FirstItemId;
        private const int _armoryBox = ServerLoot.FirstItemId + 1;
        private static readonly DateTime _time = new DateTime(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Vector3 _alphaSpawn = new Vector3(-5f, 1.6f, 0f);
        private static readonly Vector3 _bravoSpawn = new Vector3(5f, 1.6f, 0f);
        private static readonly Vector3 _suppliesAt = new Vector3(-5f, 0f, 10f);
        private static readonly Vector3 _armoryAt = new Vector3(5f, 0f, 10f);
        private static readonly WeaponCategory[] _firstSlot = { WeaponCategory.AssaultRifle, WeaponCategory.Handgun, WeaponCategory.Shotgun };
        private static readonly WeaponCategory[] _secondSlot = { WeaponCategory.Handgun, WeaponCategory.AssaultRifle, WeaponCategory.Shotgun };

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private ServerMovement _movement;
        private MatchRulesModule _rules;
        private ServerCombat _combat;
        private ServerInventories _inventories;
        private ServerLoot _loot;
        private TestObjectiveRules _objective;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
            _objective = new TestObjectiveRules { OnInteract = (_, _) => InteractVerdict.Accepted, Loot = true };
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
        public void TheMapBoxesArePlacedWhenTheMatchStartsWithContentsDecidedByTheSeed()
        {
            Start();
            Join(1);
            Join(2);

            // 置いた順 (地点ファイルの並び) に同じ種の乱数で引く
            var random = new Random(_seed);
            var expectedSupplies = LootRoll.Roll(Loot().Find(_supplies), random);
            var expectedArmory = LootRoll.Roll(Loot().Find(_armory), random);

            Assert.That(_loot.TryGetContents(_firstBox, out var weapons, out var items), Is.True, _log.ToString());
            Assert.That(weapons, Is.Empty);
            Assert.That(items, Is.EqualTo(expectedSupplies));
            Assert.That(items.Sum(i => i.Count), Is.InRange(2, 6));
            Assert.That(_loot.TryGetContents(_armoryBox, out var armoryWeapons, out var armoryItems), Is.True);
            Assert.That(expectedArmory.Single().ItemId, Is.EqualTo(_shotgunItemId));
            Assert.That(armoryWeapons.Select(w => w.Id), Is.EqualTo(new[] { _shotgunId }));
            Assert.That(armoryItems, Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Loot] seed=1416 tables=2 dropReach=1.6"));
            Assert.That(_log.ToString(), Does.Contain("[Loot] placed boxes=2 map=NetTest"));
        }

        [Test]
        public void AnObjectiveThatOpensNoBoxesGetsNoBoxesFromTheMap()
        {
            _objective = new TestObjectiveRules { OnInteract = (_, _) => InteractVerdict.Accepted };
            Start();
            Join(1);
            Join(2);
            Step(0.2f);

            Assert.That(_loot.TryGetContents(_firstBox, out _, out _), Is.False, _log.ToString());
            Assert.That(_loot.TryGetContents(_armoryBox, out _, out _), Is.False);
            Assert.That(Contribute(1), Is.Empty, "no box reaches the snapshot");
            Assert.That(_log.ToString(), Does.Contain("[Loot] placed boxes=0 map=NetTest (objective None opens no boxes)"));
            Assert.That(_log.ToString(), Does.Not.Contain("[Loot] placed boxes=2"));

            MoveTo(1, _armoryAt + new Vector3(0.5f, 1.6f, 0f));
            Request(1, _armoryBox);

            Assert.That(
                _log.ToString(),
                Does.Contain("[Loot] open item=16777217 by=1 refused=UnknownItem"),
                "a request for a map box is refused like any unknown item");
        }

        [Test]
        public void TheSameSeedPutsTheSameContentsInTheBoxes()
        {
            Start();
            Join(1);
            Join(2);
            _loot.TryGetContents(_firstBox, out _, out var first);
            var firstItems = first.ToList();
            _host.Stop();

            _log.GetStringBuilder().Clear();
            Start();
            Join(1);
            Join(2);
            _loot.TryGetContents(_firstBox, out _, out var second);

            Assert.That(second, Is.EqualTo(firstItems));
        }

        [Test]
        public void OpeningPutsTheWeaponIntoTheOpenersWeaponsAndInventory()
        {
            Start();
            Join(1);
            Join(2);
            Step(0.2f);
            MoveTo(1, _armoryAt + new Vector3(0.5f, 1.6f, 0f));

            Request(1, _armoryBox);

            var weapons = _combat.WeaponsOf(1);
            Assert.That(weapons.Specs.Select(s => s.Id), Is.EqualTo(new[] { TestRules.RifleId, TestRules.HandgunId, _shotgunId }), _log.ToString());
            Assert.That(weapons.CurrentSpec.Id, Is.EqualTo(TestRules.RifleId), "the held weapon stays in hand");
            Assert.That(_inventories.InventoryOf(1).HoldsWeapon(_shotgunId), Is.True);
            Assert.That(_loot.IsOpened(_armoryBox), Is.True);
            Assert.That(_log.ToString(), Does.Contain("[Loot] open item=" + _armoryBox + " kind=LootBox by=1 weapons=1/1 items=0/0" + Environment.NewLine));
        }

        [Test]
        public void OpeningPutsTheItemsIntoTheOpenersInventory()
        {
            Start();
            Join(1);
            Join(2);
            Step(0.2f);
            _loot.TryGetContents(_firstBox, out _, out var contents);
            var count = contents.Sum(i => i.Count);
            var before = _inventories.InventoryOf(1).CountOf(_herbId);
            MoveTo(1, _suppliesAt + new Vector3(0f, 1.6f, 1f));

            Request(1, _firstBox);

            Assert.That(_inventories.InventoryOf(1).CountOf(_herbId), Is.EqualTo(before + count), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[Inventory] give owner=1 item=" + _herbId + " count=" + count + "/" + count));
        }

        [Test]
        public void ABoxOutOfReachIsRefused()
        {
            Start();
            Join(1);
            Join(2);
            Step(0.2f);
            // 届く距離は水平に 1.6m
            MoveTo(1, _armoryAt + new Vector3(1.7f, 0f, 0f));

            var verdict = _loot.Open(1, _armoryBox);

            Assert.That(verdict, Is.EqualTo(InteractVerdict.Refused(InteractRefusal.OutOfReach)));
            Assert.That(_loot.IsOpened(_armoryBox), Is.False);
            Assert.That(_combat.WeaponsOf(1).Specs, Has.Count.EqualTo(2));
            Assert.That(_log.ToString(), Does.Contain("[Loot] open item=" + _armoryBox + " by=1 refused=OutOfReach"));

            MoveTo(1, _armoryAt + new Vector3(1.5f, 1.6f, 0f));
            Assert.That(_loot.Open(1, _armoryBox).IsAccepted, Is.True, _log.ToString());
        }

        // 体の原点は足元の 1 m 上なので、原点を 5 m 上げると足元は箱より 4 m 上になり、上下の幅 (2 m) を超える
        [Test]
        public void ABoxOnAnotherFloorIsOutOfReachEvenWhenItIsCloseFromAbove()
        {
            Start();
            Join(1);
            Join(2);
            Step(0.2f);
            Assert.That(ServerLoot.MaxVerticalReach, Is.EqualTo(2f));

            MoveTo(1, _armoryAt + new Vector3(0.5f, 5f, 0f));
            Assert.That(_loot.Open(1, _armoryBox), Is.EqualTo(InteractVerdict.Refused(InteractRefusal.OutOfReach)));
            Assert.That(_loot.IsInReach(_armoryBox, 1), Is.False);
            Assert.That(_loot.IsOpened(_armoryBox), Is.False);

            MoveTo(1, _armoryAt + new Vector3(0.5f, -2f, 0f));
            Assert.That(_loot.Open(1, _armoryBox), Is.EqualTo(InteractVerdict.Refused(InteractRefusal.OutOfReach)), "from the floor below too");

            MoveTo(1, _armoryAt + new Vector3(0.5f, 2.5f, 0f));
            Assert.That(_loot.IsInReach(_armoryBox, 1), Is.True, "feet 1.5 m above the box are still in reach");
            Assert.That(_loot.Open(1, _armoryBox).IsAccepted, Is.True, _log.ToString());
        }

        [Test]
        public void ABoxBehindAWallIsRefusedAndOpensFromTheOpenSide()
        {
            Start(wall: true);
            Join(1);
            Join(2);
            Step(0.2f);

            // 壁 (z 10.5〜10.7) の向こうから、水平には届く 1.2 m の所に立つ
            MoveTo(1, _armoryAt + new Vector3(0f, 1.6f, 1.2f));
            var verdict = _loot.Open(1, _armoryBox);

            Assert.That(verdict, Is.EqualTo(InteractVerdict.Refused(InteractRefusal.OutOfSight)));
            Assert.That(_loot.IsInReach(_armoryBox, 1), Is.True, "the reach alone does not see the wall");
            Assert.That(_loot.IsOpened(_armoryBox), Is.False);
            Assert.That(_combat.WeaponsOf(1).Specs, Has.Count.EqualTo(2));
            Assert.That(_log.ToString(), Does.Contain("[Loot] open item=" + _armoryBox + " by=1 refused=OutOfSight"));

            // 同じ 1.2 m でも壁の手前からなら、箱の載る床に当たる線は箱に届いたと数える
            MoveTo(1, _armoryAt + new Vector3(0f, 1.6f, -1.2f));
            Assert.That(_loot.Open(1, _armoryBox).IsAccepted, Is.True, _log.ToString());
        }

        [Test]
        public void TheObjectiveIsNotAskedAboutABoxBehindAWall()
        {
            var asked = 0;
            _objective.OnInteract = (_, _) =>
            {
                asked++;
                return InteractVerdict.Accepted;
            };
            Start(wall: true);
            Join(1);
            Join(2);
            Step(0.2f);
            MoveTo(1, _armoryAt + new Vector3(0f, 1.6f, 1.2f));

            Request(1, _armoryBox);

            Assert.That(asked, Is.Zero);
            Assert.That(_loot.IsOpened(_armoryBox), Is.False);
        }

        [Test]
        public void ABoxIsNotOpenedTwice()
        {
            Start();
            Join(1);
            Join(2);
            Step(0.2f);
            MoveTo(1, _armoryAt);
            MoveTo(2, _armoryAt);
            Assert.That(_loot.Open(1, _armoryBox).IsAccepted, Is.True, _log.ToString());

            var again = _loot.Open(1, _armoryBox);
            var other = _loot.Open(2, _armoryBox);

            Assert.That(again, Is.EqualTo(InteractVerdict.Refused(InteractRefusal.AlreadyOpened)));
            Assert.That(other, Is.EqualTo(InteractVerdict.Refused(InteractRefusal.AlreadyOpened)));
            Assert.That(_combat.WeaponsOf(2).Specs.Select(s => s.Id), Does.Not.Contain(_shotgunId));
            Assert.That(_log.ToString(), Does.Contain("[Loot] open item=" + _armoryBox + " by=1 refused=AlreadyOpened"));
            Assert.That(_log.ToString(), Does.Contain("[Loot] open item=" + _armoryBox + " by=2 refused=AlreadyOpened"));
        }

        [Test]
        public void ADeadPlayerCannotOpen()
        {
            Start();
            Join(1);
            Join(2);
            Step(0.2f);
            MoveTo(1, _armoryAt);
            _rules.ApplyDamage(2, 1, 1000);

            var verdict = _loot.Open(1, _armoryBox);

            Assert.That(verdict, Is.EqualTo(InteractVerdict.Refused(InteractRefusal.CannotAct)));
            Assert.That(_loot.IsOpened(_armoryBox), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Loot] open item=" + _armoryBox + " by=1 refused=CannotAct"));
        }

        [Test]
        public void TheObjectiveDecidesLastWhetherABoxMayBeOpened()
        {
            var asked = new List<(int Player, int Item)>();
            _objective.OnInteract = (player, item) =>
            {
                asked.Add((player, item));
                return InteractVerdict.Refused(InteractRefusal.NotInteractable);
            };
            Start();
            Join(1);
            Join(2);
            Step(0.2f);
            MoveTo(1, _armoryAt + new Vector3(10f, 0f, 0f));
            Assert.That(_loot.Open(1, _armoryBox).Refusal, Is.EqualTo(InteractRefusal.OutOfReach));
            Assert.That(asked, Is.Empty, "the objective is not asked about a box the server already refuses");

            MoveTo(1, _armoryAt);
            var verdict = _loot.Open(1, _armoryBox);

            Assert.That(verdict, Is.EqualTo(InteractVerdict.Refused(InteractRefusal.NotInteractable)));
            Assert.That(asked, Is.EqualTo(new[] { (1, _armoryBox) }));
            Assert.That(_loot.IsOpened(_armoryBox), Is.False);
        }

        [Test]
        public void AnObjectiveWithoutBoxesRefusesEveryOpen()
        {
            _objective.OnInteract = null;
            Start();
            Join(1);
            Join(2);
            Step(0.2f);
            MoveTo(1, _armoryAt);

            Assert.That(_loot.Open(1, _armoryBox), Is.EqualTo(InteractVerdict.Refused(InteractRefusal.NotInteractable)));
        }

        [Test]
        public void AnUnknownBoxIsRefused()
        {
            Start();
            Join(1);
            Join(2);
            Step(0.2f);

            Assert.That(_loot.Open(1, 7), Is.EqualTo(InteractVerdict.Refused(InteractRefusal.UnknownItem)));
            Assert.That(_log.ToString(), Does.Contain("[Loot] open item=7 by=1 refused=UnknownItem"));
        }

        [Test]
        public void RefusalsArePrintedAtMostOncePerSecondForEachPlayer()
        {
            Start();
            Join(1);
            Join(2);
            Step(0.2f);
            var line = "[Loot] open item=7 by=1 refused=UnknownItem";

            _loot.Open(1, 7);
            _loot.Open(1, 7);
            Assert.That(Count(_log.ToString(), line), Is.EqualTo(1));

            Step(1.1f);
            _loot.Open(1, 7);
            Assert.That(Count(_log.ToString(), line), Is.EqualTo(2));
        }

        [Test]
        public void ADeathBoxHoldsTheWeaponsAndItemsOfThePlayerWhoFell()
        {
            Start();
            Join(1);
            Join(2);
            Step(0.2f);
            MoveTo(1, new Vector3(0f, 1.6f, -10f));
            var herbs = _inventories.InventoryOf(1).CountOf(_herbId);
            Assert.That(herbs, Is.EqualTo(2), _log.ToString());

            var id = _loot.SpawnDeathBox(1);

            Assert.That(id, Is.Not.Null);
            Assert.That(_loot.TryGetContents(id.Value, out var weapons, out var items), Is.True);
            Assert.That(weapons.Select(w => w.Id), Is.EqualTo(new[] { TestRules.RifleId, TestRules.HandgunId }));
            Assert.That(items, Is.EqualTo(new[] { new ItemCount(_herbId, 2) }));
            Assert.That(_combat.WeaponsOf(1).Items, Is.Empty, "the weapons left the player who fell");
            Assert.That(_inventories.InventoryOf(1).CountOf(_herbId), Is.Zero);
            Assert.That(_inventories.InventoryOf(1).HoldsWeapon(TestRules.RifleId), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Loot] box item=" + id.Value + " kind=DeathBox from=player:1 at=0,1.6,-10 weapons=2 items=1"));

            // 開ける人は武器を手放しておき、倒れた人の武器を受け取れるようにする
            _combat.WeaponsOf(2).Take();
            _inventories.HoldWeapons(2);
            MoveTo(2, new Vector3(0.5f, 1.6f, -10f));
            Assert.That(_loot.Open(2, id.Value).IsAccepted, Is.True, _log.ToString());

            Assert.That(_combat.WeaponsOf(2).Specs.Select(s => s.Id), Is.EqualTo(new[] { TestRules.RifleId, TestRules.HandgunId }));
            Assert.That(_inventories.InventoryOf(2).CountOf(_herbId), Is.EqualTo(4));
            Assert.That(_inventories.InventoryOf(2).HoldsWeapon(TestRules.HandgunId), Is.True);
        }

        [Test]
        public void APlayerWithoutABodyLeavesNoDeathBox()
        {
            Start();
            Join(1);
            Join(2);

            Assert.That(_loot.SpawnDeathBox(99), Is.Null);
            Assert.That(_log.ToString(), Does.Contain("[Loot] death-box owner=99 skipped: the player has no body"));
        }

        [Test]
        public void ASupplyCrateRollsItsTableAndOpensLikeAMapBox()
        {
            Start();
            Join(1);
            Join(2);
            Step(0.2f);

            var crate = _loot.SpawnCrate(new Vector3(0f, 0f, 20f), _armory);
            var empty = _loot.SpawnCrate(new Vector3(0f, 0f, 30f), "no-such-table");

            Assert.That(crate, Is.EqualTo(ServerLoot.FirstItemId + 2));
            Assert.That(_loot.TryGetContents(crate, out var weapons, out _), Is.True);
            Assert.That(weapons.Select(w => w.Id), Is.EqualTo(new[] { _shotgunId }));
            Assert.That(_loot.TryGetContents(empty, out var noWeapons, out var noItems), Is.True);
            Assert.That(noWeapons, Is.Empty);
            Assert.That(noItems, Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Loot] table=no-such-table of crate is not in loot.json; the box opens empty"));

            MoveTo(1, new Vector3(1f, 1.6f, 20f));
            Assert.That(_loot.Open(1, crate).IsAccepted, Is.True, _log.ToString());
            Assert.That(_combat.WeaponsOf(1).Specs.Select(s => s.Id), Does.Contain(_shotgunId));
            Assert.Throws<ArgumentNullException>(() => _loot.SpawnCrate(Vector3.Zero, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _loot.SpawnCrate(new Vector3(float.NaN, 0f, 0f), _armory));
        }

        [Test]
        public void TheBoxesAreSentAfterTheObjectiveItems()
        {
            Start();
            Join(1);
            Join(2);
            Step(0.2f);
            var crate = _loot.SpawnCrate(new Vector3(0f, 0f, 20f), _armory);
            MoveTo(2, new Vector3(0f, 1.6f, -10f));
            var deathBox = _loot.SpawnDeathBox(2).Value;
            MoveTo(1, _armoryAt);
            _loot.Open(1, _armoryBox);

            var rows = Contribute(1);

            Assert.That(rows.Select(r => r.ItemId), Is.EqualTo(new[] { _firstBox, _armoryBox, crate, deathBox }));
            var supplies = rows[0];
            Assert.That(supplies.Kind, Is.EqualTo(ObjectiveItemKind.LootBox));
            Assert.That(supplies.Present, Is.EqualTo(ObjectiveItemFields.All));
            Assert.That(supplies.Position, Is.EqualTo(_suppliesAt));
            Assert.That(supplies.Radius, Is.EqualTo(1.6f));
            Assert.That(supplies.Opened, Is.False);
            Assert.That(supplies.HasOwner, Is.False);
            Assert.That(supplies.State, Is.EqualTo(ObjectiveItemState.Home));
            Assert.That(rows[1].Opened, Is.True);
            Assert.That(rows[2].Kind, Is.EqualTo(ObjectiveItemKind.SupplyCrate));
            Assert.That(rows[2].Radius, Is.EqualTo(ServerLoot.DefaultDropReach));
            Assert.That(rows[3].Kind, Is.EqualTo(ObjectiveItemKind.DeathBox));
            Assert.That(rows[3].HasOwner, Is.True);
            Assert.That(rows[3].OwnerId, Is.EqualTo(2));
            Assert.That(rows[3].Position, Is.EqualTo(new Vector3(0f, 1.6f, -10f)));
        }

        [Test]
        public void WhenTheBoxesDoNotFitTheNearestAreSent()
        {
            Start();
            Join(1);
            Join(2);
            Step(0.2f);
            for (var i = 0; i < SnapshotMessage.MaxObjectiveItems; i++)
            {
                _loot.SpawnCrate(new Vector3(100f + i, 0f, 100f), _supplies);
            }

            MoveTo(1, _suppliesAt);

            var rows = Contribute(1);

            Assert.That(rows, Has.Count.EqualTo(SnapshotMessage.MaxObjectiveItems));
            Assert.That(rows.Take(2).Select(r => r.ItemId), Is.EqualTo(new[] { _firstBox, _armoryBox }));
            Assert.That(rows.Select(r => r.ItemId), Does.Not.Contain(ServerLoot.FirstItemId + 2 + SnapshotMessage.MaxObjectiveItems - 1), "the farthest crate is left out");
        }

        [Test]
        public void TheNextMatchPlacesFreshBoxes()
        {
            Start();
            Join(1);
            Join(2);
            Step(0.2f);
            _loot.SpawnCrate(Vector3.Zero, _armory);
            MoveTo(1, _armoryAt);
            _loot.Open(1, _armoryBox);
            _rules.Session.Finish(Team.Alpha);
            Step(NetMatchSettings.DefaultRestartDelaySeconds + 0.5f);

            Assert.That(_rules.Session.State, Is.EqualTo(GameModeState.Playing), _log.ToString());
            Assert.That(_loot.TryGetContents(_armoryBox, out _, out _), Is.False, "the boxes of the last match are gone");
            Assert.That(_loot.TryGetContents(ServerLoot.FirstItemId + 2, out _, out _), Is.False, "the crate of the last match is gone");
            Assert.That(Count(_log.ToString(), "[Loot] placed boxes=2 map=NetTest"), Is.EqualTo(2));

            // Id は前の試合の続きから振るので、前の箱への要求を新しい箱と取り違えない
            var nextArmory = ServerLoot.FirstItemId + 4;
            Assert.That(_loot.TryGetContents(nextArmory, out var weapons, out _), Is.True);
            Assert.That(weapons.Select(w => w.Id), Is.EqualTo(new[] { _shotgunId }));
            Assert.That(_loot.IsOpened(nextArmory), Is.False);
        }

        // BR の目的が触れる口 (ServerBattleRoyaleWorld) が、同じ試合の移動・戦闘・持ち物・箱のモジュールに届くこと
        [Test]
        public void TheBattleRoyaleWorldReachesTheModulesOfTheMatch()
        {
            Start();
            Join(1);
            Join(2);
            Step(0.2f);
            var world = new ServerBattleRoyaleWorld(_movement, _combat, _inventories, _loot);

            var boxes = new List<ServerLootBox>();
            world.CollectUnopenedBoxes(boxes);
            Assert.That(boxes.Select(b => b.ItemId), Is.EqualTo(new[] { _firstBox, _armoryBox }), _log.ToString());
            Assert.That(boxes[0].HasWeapon, Is.False);
            Assert.That(boxes[1].HasWeapon, Is.True);
            Assert.That(boxes[1].Kind, Is.EqualTo(ObjectiveItemKind.LootBox));
            Assert.That(boxes[1].Position, Is.EqualTo(_armoryAt));
            Assert.That(boxes[1].Reach, Is.EqualTo(1.6f));

            Assert.That(world.IsArmed(1), Is.True);
            world.Disarm(1);
            Assert.That(world.IsArmed(1), Is.False);
            Assert.That(_inventories.InventoryOf(1).HoldsWeapon(TestRules.RifleId), Is.False, "the copy in the inventory goes too");

            MoveTo(1, _armoryAt);
            Assert.That(world.Open(1, _armoryBox).IsAccepted, Is.True, _log.ToString());
            Assert.That(world.IsArmed(1), Is.True, "the shotgun from the box");
            boxes.Clear();
            world.CollectUnopenedBoxes(boxes);
            Assert.That(boxes.Select(b => b.ItemId), Is.EqualTo(new[] { _firstBox }), "an opened box is no longer offered");

            var crate = world.SpawnCrate(new Vector3(0f, 0.5f, 20f), _armory, ServerLoot.DefaultDropReach);
            Assert.That(_loot.TryGetContents(crate, out _, out _), Is.True);

            var health = _rules.Session.GetHealth(2);
            Assert.That(world.HurtByZone(2, 7), Is.EqualTo(DamageOutcome.Damaged));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(health - 7));

            Assert.That(world.Drop(2, new Vector3(5f, 40f, 0f)), Is.True);
            Assert.That(world.Drop(9, new Vector3(5f, 40f, 0f)), Is.False, "no mover for a player who is not here");

            var dropped = world.SpawnDeathBox(2);
            Assert.That(dropped.HasValue, Is.True);
            Assert.That(world.IsArmed(2), Is.False, "the weapons went into the box");

            Assert.Throws<ArgumentNullException>(() => new ServerBattleRoyaleWorld(null, _combat, _inventories, _loot));
            Assert.Throws<ArgumentNullException>(() => new ServerBattleRoyaleWorld(_movement, null, _inventories, _loot));
            Assert.Throws<ArgumentNullException>(() => new ServerBattleRoyaleWorld(_movement, _combat, null, _loot));
            Assert.Throws<ArgumentNullException>(() => new ServerBattleRoyaleWorld(_movement, _combat, _inventories, null));
        }

        [Test]
        public void TheConstructorChecksItsArguments()
        {
            var rules = new MatchRulesModule();
            var combat = new ServerCombat(rules, new ServerMovement(ServerMotorSettings.Default));
            var inventories = new ServerInventories(rules, combat);

            Assert.Throws<ArgumentNullException>(() => new ServerLoot(null, combat, inventories));
            Assert.Throws<ArgumentNullException>(() => new ServerLoot(rules, null, inventories));
            Assert.Throws<ArgumentNullException>(() => new ServerLoot(rules, combat, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerLoot(rules, combat, inventories, dropReach: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerLoot(rules, combat, inventories, dropReach: float.PositiveInfinity));
            Assert.Throws<InvalidOperationException>(() => new ServerLoot(rules, combat, inventories).SpawnCrate(Vector3.Zero, _armory));
            Assert.That(new ServerLoot(rules, combat, inventories, 5).Seed, Is.EqualTo(5));
        }

        private static WeaponSpec Shotgun() => new WeaponSpec(
            _shotgunId, "weapon.shotgun", WeaponCategory.Shotgun, FireMode.Semi, 6, 24, 0.8f, 2.5f, 12, 30f, DamageFalloff.None);

        // 補給の表は薬草を 2 回 (1-3 個ずつ)、武器庫の表は散弾銃を 1 つ
        private static LootSpec Loot()
        {
            return new LootSpec(new[]
            {
                new LootTable(_supplies, new[] { new LootEntry(_herbId, 1f, 1, 3) }, rolls: 2),
                new LootTable(_armory, new[] { new LootEntry(_shotgunItemId, 1f) }),
            });
        }

        // 8x4 の持ち物に、薬草 (5 個で 1 山) と小銃・拳銃・散弾銃の武器。薬草を 2 個持って始める
        private static ItemCatalog Items()
        {
            return new ItemCatalog(
                new[]
                {
                    new ItemSpec(_herbId, "item.herb", maxStack: 5, weight: 0.1f, useHealth: 30),
                    new ItemSpec(_rifleItemId, "weapon.rifle", width: 4, height: 2, weaponId: TestRules.RifleId),
                    new ItemSpec(_handgunItemId, "weapon.handgun", width: 2, height: 1, weaponId: TestRules.HandgunId),
                    new ItemSpec(_shotgunItemId, "weapon.shotgun", width: 4, height: 2, weaponId: _shotgunId),
                },
                new InventoryRule(gridWidth: 8, gridHeight: 4),
                new[] { new ItemCount(_herbId, 2) });
        }

        private static GameRules Rules()
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off);
            return GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
        }

        // 散弾銃はロードアウトに無く、武器の一覧にだけある (箱からしか持てない)
        private static NetMatchFile MatchFile()
        {
            return new NetMatchFile(
                "TeamDeathmatch",
                Rules(),
                NetMatchSettings.Default,
                "DefaultLoadout",
                new LoadoutRule(new[] { _firstSlot, _secondSlot }),
                new[] { TestRules.Rifle(), TestRules.Handgun() },
                0.4f,
                "WeaponCatalog",
                new WeaponCatalog(new[] { TestRules.Rifle(), TestRules.Handgun(), Shotgun() }),
                null);
        }

        // 床の上面は y=0.5 で、箱 (y=0) は床に埋まって置かれる。wall は武器庫の箱の 0.5 m 奥 (z 10.5〜10.7) に床から 2 m 立てた壁
        private void Start(bool wall = false)
        {
            var colliders = new List<WorldCollider>
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(60f, 0.5f, 60f), Quaternion.Identity),
            };
            if (wall)
            {
                colliders.Add(WorldCollider.Box(2, 0, WorldCollider.StaticGroup, _armoryAt + new Vector3(0f, 1.5f, 0.6f), new Vector3(2f, 1f, 0.1f), Quaternion.Identity));
            }

            var containers = new[]
            {
                new NetMapLootContainer("crate-supplies", _supplies, _suppliesAt, 1.6f),
                new NetMapLootContainer("crate-armory", _armory, _armoryAt, 1.6f),
            };
            var points = new NetMapPoints(
                new[] { new SpawnPoint(_alphaSpawn, Team.Alpha), new SpawnPoint(_bravoSpawn, Team.Bravo) },
                Array.Empty<ControlPointArea>(),
                lootContainers: containers);
            var map = new ServerMapData("NetTest", new WorldMap(colliders), TestMaps.NavMesh(), "memory", points, null);
            var data = new ServerRulesData("TeamDeathmatch", MatchFile(), PingSpec.Default, "memory", null, items: Items(), loot: Loot());
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
            _movement = movement;
            _rules = new MatchRulesModule((_, _, _) => _objective);
            _combat = new ServerCombat(_rules, movement);
            _inventories = new ServerInventories(_rules, _combat);
            _loot = new ServerLoot(_rules, _combat, _inventories, _seed);
            _host = new ServerHost();
            _host.Register(movement);
            _host.Register(_rules);
            _host.Register(_combat);
            _host.Register(_inventories);
            _host.Register(_loot);
            _host.Start(_context);
        }

        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        private void Request(int id, int itemId)
        {
            _host.OnMessage(new ConnectionId((ulong)id), new InteractRequestMessage { ItemId = itemId });
        }

        private void MoveTo(int id, Vector3 position)
        {
            _context.Bodies.Teleport(id, position, 0f, 0);
        }

        // 目的が先に書いた (このテストの目的は空にする) 後に箱を足す、スナップショットを組む順で書く
        private List<ObjectiveItemSnapshot> Contribute(int id)
        {
            Assert.That(_match.TryGetPlayer(new ConnectionId((ulong)id), out var receiver), Is.True);
            var snapshot = new SnapshotMessage();
            _rules.Contribute(snapshot, receiver);
            _loot.Contribute(snapshot, receiver);
            var rows = new List<ObjectiveItemSnapshot>();
            for (var i = 0; i < snapshot.ObjectiveItems.Count; i++)
            {
                rows.Add(snapshot.ObjectiveItems[i]);
            }

            return rows;
        }

        private void Step(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                _match.Tick(_tickSeconds);
                _host.Tick(_tickSeconds);
            }
        }

        private static int Count(string text, string line)
        {
            var count = 0;
            for (var at = text.IndexOf(line, StringComparison.Ordinal); at >= 0; at = text.IndexOf(line, at + line.Length, StringComparison.Ordinal))
            {
                count++;
            }

            return count;
        }
    }
}
