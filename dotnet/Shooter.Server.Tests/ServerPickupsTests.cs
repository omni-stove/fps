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
    // 移動・ルール・戦闘・持ち物と拾い物を同じホストで動かし、置く・触れて拾う・拾う要求・弾薬箱・倒れた人の落とし物を確かめる
    public sealed class ServerPickupsTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const string _herbId = "0d3c6f1a-5b2e-4c7d-8a9f-1e2b3c4d5e6f";
        private const string _rifleItemId = "4d5e6f7a-8b9c-4d0e-9f2a-3b4c5d6e7f8a";
        private const string _handgunItemId = "5e6f7a8b-9c0d-4e1f-8a3b-4c5d6e7f8a9b";
        private const string _shotgunItemId = "8b9c0d1e-2f3a-4b4c-9d6e-7f8a9b0c1d2e";
        private const string _shotgunId = "9c0d1e2f-3a4b-4c5d-8e7f-8a9b0c1d2e3f";
        private const int _ammo = ServerPickups.FirstItemId;
        private const int _medkit = ServerPickups.FirstItemId + 1;
        private const int _shotgun = ServerPickups.FirstItemId + 2;
        private const int _herbs = ServerPickups.FirstItemId + 3;
        private const int _crate = ServerPickups.FirstItemId + 4;
        private const int _firstDrop = ServerPickups.FirstItemId + 5;
        private const float _ammoRespawnSeconds = 5f;
        private const float _shotgunRespawnSeconds = 10f;
        private const float _crateCooldownSeconds = 1f;
        private static readonly DateTime _time = new DateTime(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Vector3 _alphaSpawn = new Vector3(-5f, 1.6f, 0f);
        private static readonly Vector3 _bravoSpawn = new Vector3(5f, 1.6f, 0f);
        private static readonly Vector3 _ammoAt = new Vector3(0f, 0.5f, 10f);
        private static readonly Vector3 _medkitAt = new Vector3(-5f, 0.5f, 10f);
        private static readonly Vector3 _shotgunAt = new Vector3(5f, 0.5f, 10f);
        private static readonly Vector3 _herbsAt = new Vector3(10f, 0.5f, 10f);
        private static readonly Vector3 _crateAt = new Vector3(-10f, 0.5f, 10f);

        // 床の上面 (y=0.5) に置いた物の横に立つ体の高さ。ServerLootTests の箱と体の高さの差に揃える
        private static readonly Vector3 _standing = new Vector3(0f, 1.1f, 0f);
        private static readonly WeaponCategory[] _firstSlot = { WeaponCategory.AssaultRifle, WeaponCategory.Handgun, WeaponCategory.Shotgun };
        private static readonly WeaponCategory[] _secondSlot = { WeaponCategory.Handgun, WeaponCategory.AssaultRifle, WeaponCategory.Shotgun };

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private MatchRulesModule _rules;
        private ServerCombat _combat;
        private ServerInventories _inventories;
        private ServerPickups _pickups;
        private TestObjectiveRules _objective;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
            _objective = new TestObjectiveRules();
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
        public void NothingIsPlacedUntilTheMatchStarts()
        {
            Start();
            Join(1);

            Assert.That(_pickups.ItemIds(), Is.Empty, _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[Pickup] net dropsWeapon=True dropAmmo=30 dropLifetime=30 dropReach=1"));
        }

        [Test]
        public void TheMapPickupsAndAmmoCratesArePlacedWhenTheMatchStartsAndReachTheSnapshot()
        {
            StartMatch();

            Assert.That(_pickups.ItemIds(), Is.EqualTo(new[] { _ammo, _medkit, _shotgun, _herbs, _crate }), _log.ToString());
            var rows = Contribute(1);
            Assert.That(rows.Select(r => r.Kind), Is.EqualTo(new[]
            {
                ObjectiveItemKind.AmmoPickup, ObjectiveItemKind.HealthPickup, ObjectiveItemKind.WeaponPickup, ObjectiveItemKind.ItemPickup,
                ObjectiveItemKind.AmmoCrate,
            }));
            Assert.That(rows.Select(r => r.Opened), Is.All.False);
            Assert.That(rows.Select(r => r.HasOwner), Is.All.False);
            Assert.That(rows[0].Position, Is.EqualTo(_ammoAt));
            Assert.That(rows[0].Radius, Is.EqualTo(1f));
            Assert.That(rows[4].Radius, Is.EqualTo(1.6f));
            Assert.That(_log.ToString(), Does.Contain("[Pickup] net placed pickups=4 ammoCrates=1 map=NetTest"));
        }

        [Test]
        public void AWeaponMissingFromTheLoadoutIsNotPlaced()
        {
            StartMatch(extra: new NetMapPickup("ghost", PickupSpec.Weapon("missing-weapon", 5), new Vector3(20f, 0.5f, 10f), 1f, 0f, false));

            Assert.That(_pickups.PickupCount, Is.EqualTo(4), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[Pickup] net skipped map:ghost weapon=missing-weapon is not in the loadout or the weapon catalog"));
        }

        [Test]
        public void TouchingTheAmmoFillsTheWeaponInHandAndItComesBackAfterItsRespawn()
        {
            StartMatch();
            var rifle = _combat.WeaponsOf(1).Find(TestRules.RifleId);
            rifle.SetAmmo(30, 10);

            MoveTo(1, _ammoAt + _standing);
            Step(_tickSeconds * 2f);

            Assert.That(rifle.ReserveAmmo, Is.EqualTo(30), _log.ToString());
            Assert.That(_pickups.TryGetAvailable(_ammo, out _), Is.False);
            Assert.That(Contribute(1).Single(r => r.ItemId == _ammo).Opened, Is.True, "a taken pickup stays listed while it waits");
            Assert.That(_log.ToString(), Does.Contain("[Pickup] net collected item=" + _ammo + " by=1 kind=ReserveAmmo weapon=" + TestRules.RifleId + " ammo=20"));

            MoveTo(1, _alphaSpawn);
            Step(_ammoRespawnSeconds + 0.1f);

            Assert.That(_pickups.TryGetAvailable(_ammo, out var spec), Is.True);
            Assert.That(spec.Amount, Is.EqualTo(20));
            Assert.That(_log.ToString(), Does.Contain("[Pickup] net respawned item=" + _ammo));
        }

        [Test]
        public void ARefusedTouchIsLoggedOnceWhenThePlayerStepsIn()
        {
            StartMatch();

            MoveTo(1, _ammoAt + _standing);
            Step(1.5f);

            var line = "[Pickup] net collect item=" + _ammo + " by=1 refused=Judged rejection=AmmoFull";
            Assert.That(Count(_log.ToString(), line), Is.EqualTo(1), _log.ToString());
            Assert.That(_pickups.TryGetAvailable(_ammo, out _), Is.True, "a full player leaves the ammo for others");
        }

        [Test]
        public void TheMedkitHealsTheDamagedAndIsRefusedAtFullHealth()
        {
            StartMatch();
            MoveTo(1, _medkitAt + _standing);

            Assert.That(_pickups.Collect(1, _medkit), Is.EqualTo(ServerPickupRefusal.Judged));
            Assert.That(_log.ToString(), Does.Contain("[Pickup] net collect item=" + _medkit + " by=1 refused=Judged rejection=HealthFull"));

            _combat.DealDamage(2, 1, 40, TestRules.RifleId, HitZone.Body, null);
            var hurt = _rules.Session.GetHealth(1);

            Assert.That(_pickups.Collect(1, _medkit), Is.EqualTo(ServerPickupRefusal.None), _log.ToString());
            Assert.That(_rules.Session.GetHealth(1), Is.EqualTo(hurt + 30));
            Assert.That(_pickups.Collect(1, _medkit), Is.EqualTo(ServerPickupRefusal.Gone), "a respawn time of 0 gives the medkit once");
            Assert.That(_log.ToString(), Does.Contain("[Pickup] net collected item=" + _medkit + " by=1 kind=Health health=30"));
        }

        [Test]
        public void TheRequestMessagePicksUpTheWeaponBesideTheLoadoutAndItComesBack()
        {
            StartMatch();
            MoveTo(1, _shotgunAt + _standing);

            Request(1, _shotgun);

            var weapons = _combat.WeaponsOf(1);
            Assert.That(weapons.Specs.Select(s => s.Id), Is.EqualTo(new[] { TestRules.RifleId, TestRules.HandgunId, _shotgunId }), _log.ToString());
            Assert.That(weapons.CurrentSpec.Id, Is.EqualTo(TestRules.RifleId), "the weapon in hand stays");
            Assert.That(_inventories.InventoryOf(1).HoldsWeapon(_shotgunId), Is.True);
            Assert.That(_pickups.TryGetAvailable(_shotgun, out _), Is.False);

            MoveTo(1, _alphaSpawn);
            Step(_shotgunRespawnSeconds + 0.1f);

            Assert.That(_pickups.TryGetAvailable(_shotgun, out _), Is.True);
        }

        [Test]
        public void AFullHandSwapsTheWeaponInHandAndDropsItTowardThePlayer()
        {
            StartMatch(rules: Rules().WithMaxWeapons(2));
            var standAt = _shotgunAt + _standing + new Vector3(0f, 0f, 0.8f);
            MoveTo(1, standAt);

            Assert.That(_pickups.Collect(1, _shotgun), Is.EqualTo(ServerPickupRefusal.None), _log.ToString());

            var weapons = _combat.WeaponsOf(1);
            Assert.That(weapons.Specs.Select(s => s.Id), Is.EqualTo(new[] { _shotgunId, TestRules.HandgunId }));
            Assert.That(weapons.CurrentSpec.Id, Is.EqualTo(_shotgunId));
            Assert.That(_pickups.TryGetAvailable(_firstDrop, out var dropped), Is.True);
            Assert.That(dropped.Kind, Is.EqualTo(PickupKind.Weapon));
            Assert.That(dropped.WeaponId, Is.EqualTo(TestRules.RifleId));
            Assert.That(dropped.Amount, Is.EqualTo(30 + 90), "someone who holds a rifle takes all of its ammo");
            var row = Contribute(1).Single(r => r.ItemId == _firstDrop);
            Assert.That(row.Kind, Is.EqualTo(ObjectiveItemKind.WeaponPickup));
            Assert.That(row.OwnerId, Is.EqualTo(1));
            Assert.That(row.Position.Z, Is.EqualTo(_shotgunAt.Z + ServerPickups.DroppedWeaponOffset).Within(1e-4f));
            Assert.That(_log.ToString(), Does.Contain("[Net] give weapon player=1 weapon=" + _shotgunId + " replaced=" + TestRules.RifleId));
            Assert.That(_log.ToString(), Does.Contain("swapped=" + TestRules.RifleId));
        }

        [Test]
        public void AKilledPlayerDropsTheWeaponInHandWithItsAmmoAndAnotherPlayerPicksItUp()
        {
            StartMatch();
            var victim = _combat.WeaponsOf(2);
            Assert.That(victim.Replace(TestRules.RifleId, Shotgun(), 0.4f, Rules()), Is.Not.Null);
            victim.Find(_shotgunId).SetAmmo(2, 7);

            Kill(2);

            var drops = Contribute(1).Where(r => r.HasOwner && r.OwnerId == 2).ToList();
            Assert.That(drops.Select(r => r.Kind), Is.EquivalentTo(new[] { ObjectiveItemKind.WeaponPickup, ObjectiveItemKind.AmmoPickup }), _log.ToString());
            Assert.That(_pickups.TryGetAvailable(_firstDrop, out var spec), Is.True);
            Assert.That(spec.WeaponId, Is.EqualTo(_shotgunId));
            Assert.That(spec.Amount, Is.EqualTo(9));
            Assert.That(_pickups.TryGetAvailable(_firstDrop + 1, out var ammo), Is.True);
            Assert.That(ammo.Kind, Is.EqualTo(PickupKind.ReserveAmmo));
            Assert.That(ammo.Amount, Is.EqualTo(PickupDropSpec.DefaultReserveAmmo));
            var weaponDrop = drops.Single(r => r.Kind == ObjectiveItemKind.WeaponPickup);
            var ammoDrop = drops.Single(r => r.Kind == ObjectiveItemKind.AmmoPickup);
            Assert.That(Vector3.Distance(weaponDrop.Position, ammoDrop.Position), Is.EqualTo(PickupDropSpec.DefaultSpreadRadius * 2f).Within(1e-3f));
            Assert.That(_log.ToString(), Does.Contain("[Pickup] net dropped item=" + _firstDrop + " owner=2 "));

            MoveTo(1, weaponDrop.Position + _standing);
            Request(1, _firstDrop);

            var shotgun = _combat.WeaponsOf(1).Find(_shotgunId);
            Assert.That(shotgun, Is.Not.Null, _log.ToString());
            Assert.That(shotgun.AmmoInMagazine, Is.EqualTo(2), "the dropped magazine comes with the weapon");
            Assert.That(shotgun.ReserveAmmo, Is.EqualTo(7));
            Assert.That(_pickups.TryGetAvailable(_firstDrop, out _), Is.False);
            Assert.That(Contribute(1).Any(r => r.ItemId == _firstDrop), Is.False, "a taken drop leaves the snapshot");
        }

        [Test]
        public void DropsVanishAfterTheirLifetime()
        {
            StartMatch(drops: new PickupDropSpec(lifetimeSeconds: 1f));
            Kill(2);
            Assert.That(_pickups.TryGetAvailable(_firstDrop, out _), Is.True, _log.ToString());

            Step(1.1f);

            Assert.That(_pickups.TryGetAvailable(_firstDrop, out _), Is.False);
            Assert.That(_pickups.TryGetAvailable(_firstDrop + 1, out _), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Pickup] net expired item=" + _firstDrop));
        }

        [Test]
        public void DropsCanBeTurnedOffInTheTuning()
        {
            StartMatch(drops: new PickupDropSpec(dropsWeapon: false, reserveAmmo: 0));

            Kill(2);

            Assert.That(_pickups.PickupCount, Is.EqualTo(4), _log.ToString());
        }

        [Test]
        public void AnObjectiveThatDropsDeathBoxesGetsNoDrops()
        {
            _objective = new TestObjectiveRules { Loot = true };
            StartMatch();

            Kill(2);

            Assert.That(_pickups.PickupCount, Is.EqualTo(4), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[Pickup] net no drop player=2 reason=objective drops a death box"));
        }

        [Test]
        public void TheAmmoCrateRefillsEveryWeaponThenCoolsDown()
        {
            StartMatch();
            var weapons = _combat.WeaponsOf(1);
            weapons.Find(TestRules.RifleId).SetAmmo(30, 10);
            weapons.Find(TestRules.HandgunId).SetAmmo(12, 0);
            MoveTo(1, _crateAt + _standing);

            Assert.That(_pickups.Collect(1, _crate), Is.EqualTo(ServerPickupRefusal.None), _log.ToString());

            Assert.That(weapons.Find(TestRules.RifleId).ReserveAmmo, Is.EqualTo(90));
            Assert.That(weapons.Find(TestRules.HandgunId).ReserveAmmo, Is.EqualTo(48));
            Assert.That(_pickups.IsCoolingDown(_crate), Is.True);
            Assert.That(Contribute(1).Single(r => r.ItemId == _crate).Opened, Is.True);
            Assert.That(_log.ToString(), Does.Contain("[Pickup] net ammo crate item=" + _crate + " by=1 added=128 readyIn=1"));

            weapons.Find(TestRules.RifleId).SetAmmo(30, 0);
            Assert.That(_pickups.Collect(1, _crate), Is.EqualTo(ServerPickupRefusal.CoolingDown));

            Step(_crateCooldownSeconds + 0.1f);
            MoveTo(1, _crateAt + _standing);

            Assert.That(_pickups.IsCoolingDown(_crate), Is.False);
            Assert.That(_pickups.Collect(1, _crate), Is.EqualTo(ServerPickupRefusal.None));
            Assert.That(weapons.Find(TestRules.RifleId).ReserveAmmo, Is.EqualTo(90));
            Step(_crateCooldownSeconds + 0.1f);
            MoveTo(1, _crateAt + _standing);
            Assert.That(_pickups.Collect(1, _crate), Is.EqualTo(ServerPickupRefusal.NothingToRefill), "a full player does not start the wait");
            Assert.That(_pickups.IsCoolingDown(_crate), Is.False);
        }

        [Test]
        public void TheHerbsGoIntoTheInventory()
        {
            StartMatch();
            var before = _inventories.InventoryOf(1).CountOf(_herbId);
            MoveTo(1, _herbsAt + _standing);

            Assert.That(_pickups.Collect(1, _herbs), Is.EqualTo(ServerPickupRefusal.None), _log.ToString());

            Assert.That(_inventories.InventoryOf(1).CountOf(_herbId), Is.EqualTo(before + 3));
            Assert.That(_pickups.TryGetAvailable(_herbs, out _), Is.False);
        }

        [Test]
        public void UnknownFarAwayAndDeadCollectorsAreRefused()
        {
            StartMatch();

            Assert.That(_pickups.Collect(1, 12345), Is.EqualTo(ServerPickupRefusal.UnknownItem));
            Assert.That(_pickups.Collect(1, _medkit), Is.EqualTo(ServerPickupRefusal.OutOfReach), "the player is still at the spawn");
            MoveTo(1, _medkitAt + new Vector3(0f, 4.5f, 0f));
            Assert.That(_pickups.Collect(1, _medkit), Is.EqualTo(ServerPickupRefusal.OutOfReach), "a pickup on the floor below is not in reach");

            MoveTo(1, _medkitAt + _standing);
            Kill(1);

            Assert.That(_pickups.Collect(1, _medkit), Is.EqualTo(ServerPickupRefusal.CannotAct));
            Assert.That(_pickups.Collect(1, _crate), Is.EqualTo(ServerPickupRefusal.CannotAct));
        }

        [Test]
        public void AWallBetweenThePlayerAndThePickupBlocksIt()
        {
            StartMatch(wall: true);
            MoveTo(1, _medkitAt + _standing + new Vector3(0f, 0f, 0.9f));
            _combat.DealDamage(2, 1, 40, TestRules.RifleId, HitZone.Body, null);

            Assert.That(_pickups.Collect(1, _medkit), Is.EqualTo(ServerPickupRefusal.OutOfSight), _log.ToString());
        }

        [Test]
        public void ARequestFromSomeoneNotInTheMatchIsIgnored()
        {
            StartMatch();

            _host.OnMessage(new ConnectionId(99), new PickupRequestMessage { ItemId = _medkit });

            Assert.That(_log.ToString(), Does.Not.Contain("net collect item=" + _medkit));
        }

        [Test]
        public void TheSpreadMatchesTheOfflineDropOnDeath()
        {
            Assert.That(ServerPickups.SpreadOffset(0, 1, 0.75f), Is.EqualTo(Vector3.Zero));
            Assert.That(ServerPickups.SpreadOffset(0, 2, 0f), Is.EqualTo(Vector3.Zero));
            Assert.That(ServerPickups.SpreadOffset(0, 2, 0.75f).X, Is.EqualTo(0.75f).Within(1e-5f));
            Assert.That(ServerPickups.SpreadOffset(1, 2, 0.75f).X, Is.EqualTo(-0.75f).Within(1e-5f));
        }

        [Test]
        public void TheConstructorChecksItsArgumentsAndTheModuleMustBeRunning()
        {
            var rules = new MatchRulesModule();
            var combat = new ServerCombat(rules, new ServerMovement(ServerMotorSettings.Default));

            Assert.Throws<ArgumentNullException>(() => new ServerPickups(null, combat));
            Assert.Throws<ArgumentNullException>(() => new ServerPickups(rules, null));
            Assert.Throws<InvalidOperationException>(() => new ServerPickups(rules, combat).Collect(1, _medkit));
            Assert.Throws<InvalidOperationException>(() => new ServerPickups(rules, combat).DropFor(1));
            Assert.That(new ServerPickups(rules, combat).Name, Is.EqualTo("pickups"));
        }

        private static WeaponSpec Shotgun() => new WeaponSpec(
            _shotgunId, "weapon.shotgun", WeaponCategory.Shotgun, FireMode.Semi, 6, 24, 0.8f, 2.5f, 12, 30f, DamageFalloff.None);

        // 8x4 の持ち物に、薬草 (5 個で 1 山) と小銃・拳銃・散弾銃の武器。薬草を 2 個持って始める (ServerLootTests と同じ)
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

        // 散弾銃はロードアウトに無く、武器の一覧にだけある (拾ってしか持てない)
        private static NetMatchFile MatchFile(GameRules rules)
        {
            return new NetMatchFile(
                "TeamDeathmatch",
                rules,
                NetMatchSettings.Default,
                "DefaultLoadout",
                new LoadoutRule(new[] { _firstSlot, _secondSlot }),
                new[] { TestRules.Rifle(), TestRules.Handgun() },
                0.4f,
                "WeaponCatalog",
                new WeaponCatalog(new[] { TestRules.Rifle(), TestRules.Handgun(), Shotgun() }),
                null);
        }

        // 置く順は 予備弾 (触れて拾う)・回復 (押して拾う・一度きり)・散弾銃 (押して拾う)・薬草 (押して拾う・一度きり)、その後に弾薬箱
        private static NetMapPickups Pickups(NetMapPickup extra)
        {
            var pickups = new List<NetMapPickup>
            {
                new NetMapPickup("ammo", PickupSpec.ReserveAmmo(20), _ammoAt, 1f, _ammoRespawnSeconds, true),
                new NetMapPickup("medkit", PickupSpec.Health(30), _medkitAt, 1f, 0f, false),
                new NetMapPickup("shotgun", PickupSpec.Weapon(_shotgunId, 6), _shotgunAt, 1f, _shotgunRespawnSeconds, false),
                new NetMapPickup("herbs", PickupSpec.Item(_herbId, 3), _herbsAt, 1f, 0f, false),
            };
            if (extra != null)
            {
                pickups.Add(extra);
            }

            return new NetMapPickups(pickups, new[] { new NetMapAmmoCrate("crate", _crateAt, 1.6f, _crateCooldownSeconds) });
        }

        // 床の上面は y=0.5。wall は回復の 0.5 m 奥 (z 10.5〜10.7) に床から 2 m 立てた壁
        private void Start(GameRules rules = null, PickupDropSpec drops = null, bool wall = false, NetMapPickup extra = null)
        {
            var colliders = new List<WorldCollider>
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(60f, 0.5f, 60f), Quaternion.Identity),
            };
            if (wall)
            {
                colliders.Add(WorldCollider.Box(2, 0, WorldCollider.StaticGroup, _medkitAt + new Vector3(0f, 1f, 0.6f), new Vector3(2f, 1f, 0.1f), Quaternion.Identity));
            }

            var points = new NetMapPoints(
                new[] { new SpawnPoint(_alphaSpawn, Team.Alpha), new SpawnPoint(_bravoSpawn, Team.Bravo) },
                Array.Empty<ControlPointArea>());
            var map = new ServerMapData("NetTest", new WorldMap(colliders), TestMaps.NavMesh(), "memory", points, null, pickups: Pickups(extra));
            var data = new ServerRulesData(
                "TeamDeathmatch", MatchFile(rules ?? Rules()), PingSpec.Default, "memory", null, items: Items(), pickupDrops: drops);
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
            _rules = new MatchRulesModule((_, _, _) => _objective);
            _combat = new ServerCombat(_rules, movement);
            _inventories = new ServerInventories(_rules, _combat);
            _pickups = new ServerPickups(_rules, _combat, _inventories);
            _host = new ServerHost();
            _host.Register(movement);
            _host.Register(_rules);
            _host.Register(_combat);
            _host.Register(_inventories);
            _host.Register(_pickups);
            _host.Start(_context);
        }

        private void StartMatch(GameRules rules = null, PickupDropSpec drops = null, bool wall = false, NetMapPickup extra = null)
        {
            Start(rules, drops, wall, extra);
            Join(1);
            Join(2);
            Step(0.2f);
        }

        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        private void Request(int id, int itemId)
        {
            _host.OnMessage(new ConnectionId((ulong)id), new PickupRequestMessage { ItemId = itemId });
        }

        private void MoveTo(int id, Vector3 position)
        {
            _context.Bodies.Teleport(id, position, 0f, 0);
        }

        private void Kill(int victimId)
        {
            var killer = victimId == 1 ? 2 : 1;
            _combat.DealDamage(killer, victimId, NetMatchSettings.DefaultMaxHealth, TestRules.RifleId, HitZone.Body, null);
            Assert.That(_rules.Session.IsAlive(victimId), Is.False, _log.ToString());
        }

        private List<ObjectiveItemSnapshot> Contribute(int id)
        {
            Assert.That(_match.TryGetPlayer(new ConnectionId((ulong)id), out var receiver), Is.True);
            var snapshot = new SnapshotMessage();
            _pickups.Contribute(snapshot, receiver);
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
