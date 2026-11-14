using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class PickupTests
    {
        private readonly List<Object> _created = new List<Object>();
        private InputAction _attack;
        private InputAction _reload;

        [SetUp]
        public void SetUp()
        {
            _attack = new InputAction("Attack", InputActionType.Button);
            _reload = new InputAction("Reload", InputActionType.Button);
        }

        [TearDown]
        public void TearDown()
        {
            _attack.Dispose();
            _reload.Dispose();
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            foreach (var pickup in Object.FindObjectsByType<Pickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(pickup.gameObject);
            }
        }

        [Test]
        public void CreateBuildsATriggerWithAKinematicBodyAndAVisual()
        {
            var pickup = Pickup.Create(PickupContents.ForHealth(25), new Vector3(1f, 0f, 2f), radius: 1.5f);

            Assert.That(pickup.name, Is.EqualTo(Pickup.ObjectName(PickupKind.Health)));
            Assert.That(pickup.transform.position, Is.EqualTo(new Vector3(1f, 0f, 2f)));
            var trigger = pickup.GetComponent<SphereCollider>();
            Assert.That(trigger.isTrigger, Is.True);
            Assert.That(trigger.radius, Is.EqualTo(1.5f));
            Assert.That(pickup.GetComponent<Rigidbody>().isKinematic, Is.True);
            var visual = pickup.transform.Find(Pickup.VisualName);
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.GetComponent<Collider>(), Is.Null, "the visual must not block movement or shots");
        }

        [Test]
        public void TouchingHealthHealsAndRemovesThePickup()
        {
            var player = NewCollector("Player");
            player.TakeDamage(30);
            var pickup = Pickup.Create(PickupContents.ForHealth(25), Vector3.zero);
            PickupCollection? collected = null;
            pickup.Collected += collection => collected = collection;

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Pickup\] collected pickup=Pickup-Health by=Player kind=Health health=\+25$"));
            pickup.HandleEnter(player.GetComponent<Collider>());

            Assert.That(player.Health.Current, Is.EqualTo(95));
            Assert.That(collected.HasValue, Is.True);
            Assert.That(collected.Value.Collector, Is.SameAs(player));
            Assert.That(collected.Value.Outcome.Health, Is.EqualTo(25));
            Assert.That(pickup == null, Is.True, "destroyed once collected");
        }

        [Test]
        public void FullHealthLeavesThePickupAndTakesItOnceHurt()
        {
            var player = NewCollector("Player");
            var pickup = Pickup.Create(PickupContents.ForHealth(25), Vector3.zero);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Pickup\] rejected .* reason=HealthFull$"));
            pickup.HandleEnter(player.GetComponent<Collider>());
            Assert.That(pickup.IsAvailable, Is.True);
            Assert.That(pickup.InsideCount, Is.EqualTo(1));

            player.TakeDamage(10);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Pickup\] collected .* health=\+10$"));
            pickup.Tick(0.1f);

            Assert.That(player.Health.Current, Is.EqualTo(100));
            Assert.That(pickup == null, Is.True);
        }

        [Test]
        public void DeadCollectorsAreIgnoredWithoutALog()
        {
            var bot = NewCollector("Bot");
            bot.Kill();
            var pickup = Pickup.Create(PickupContents.ForHealth(25), Vector3.zero);

            pickup.HandleEnter(bot.GetComponent<Collider>());

            LogAssert.NoUnexpectedReceived();
            Assert.That(pickup.IsAvailable, Is.True);
        }

        [Test]
        public void SideConditionKeepsOthersAway()
        {
            var player = NewCollector("Player");
            player.gameObject.AddComponent<SideMember>().Side = Side.Alpha;
            player.TakeDamage(50);
            var pickup = Pickup.Create(PickupContents.ForHealth(25, PickupEligibility.Only(Side.Bravo)), Vector3.zero);

            Assert.That(pickup.TryCollect(player, out var outcome, logRejection: false), Is.False);
            Assert.That(outcome.Rejection, Is.EqualTo(PickupRejection.SideNotAllowed));
        }

        [Test]
        public void WeaponIsAddedToTheLoadoutWithoutSwitching()
        {
            var player = NewCollector("Player");
            var weapons = AttachWeapons(player, NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec()));
            var launcher = NewWeapon("Launcher", WeaponAssetBuilder.LauncherSpec());
            var changed = 0;
            weapons.WeaponChanged += _ => changed++;
            var pickup = Pickup.Create(PickupContents.ForWeapon(launcher, 2), Vector3.zero);

            Assert.That(pickup.TryCollect(player, out var outcome), Is.True);

            Assert.That(outcome.AddsWeapon, Is.True);
            Assert.That(weapons.Loadout.Count, Is.EqualTo(2));
            Assert.That(weapons.Loadout.Items[1].Spec.Id, Is.EqualTo(launcher.Id));
            Assert.That(weapons.CurrentIndex, Is.Zero, "keeps the weapon in hand");
            Assert.That(changed, Is.EqualTo(1), "the HUD rebinds to the rebuilt weapon");
            Assert.That(weapons.Definitions.Count, Is.EqualTo(1), "the default loadout stays as configured");
        }

        [Test]
        public void AddingAWeaponKeepsTheAmmoOfTheOthers()
        {
            var player = NewCollector("Player");
            var weapons = AttachWeapons(player, NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec()));
            weapons.Weapon.TryFire();
            var magazine = weapons.Weapon.AmmoInMagazine;

            Assert.That(weapons.AddWeapon(NewWeapon("Launcher", WeaponAssetBuilder.LauncherSpec())), Is.True);

            Assert.That(weapons.Weapon.AmmoInMagazine, Is.EqualTo(magazine));
            Assert.That(weapons.AddWeapon(NewWeapon("Launcher2", WeaponAssetBuilder.LauncherSpec())), Is.False, "same id is held already");
        }

        [Test]
        public void OwnedWeaponGivesAmmoInstead()
        {
            var player = NewCollector("Player");
            var rifle = NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec());
            var weapons = AttachWeapons(player, rifle);
            weapons.Weapon.TryFire();
            weapons.StartReload();
            weapons.Tick(weapons.Weapon.Spec.ReloadSeconds + 0.1f);
            var reserve = weapons.Weapon.ReserveAmmo;
            var pickup = Pickup.Create(PickupContents.ForWeapon(rifle, 5), Vector3.zero);

            Assert.That(pickup.TryCollect(player, out var outcome), Is.True);

            Assert.That(outcome.AddsWeapon, Is.False);
            Assert.That(outcome.ReserveAmmo, Is.EqualTo(1), "only the fired round fits under the limit");
            Assert.That(weapons.Weapon.ReserveAmmo, Is.EqualTo(reserve + 1));
            Assert.That(weapons.Loadout.Count, Is.EqualTo(1));
        }

        [Test]
        public void AmmoGoesToTheCurrentWeapon()
        {
            var player = NewCollector("Player");
            var weapons = AttachWeapons(player, NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec()));
            weapons.Weapon.TryFire();
            weapons.StartReload();
            weapons.Tick(weapons.Weapon.Spec.ReloadSeconds + 0.1f);
            var reserve = weapons.Weapon.ReserveAmmo;
            var pickup = Pickup.Create(PickupContents.ForReserveAmmo(30), Vector3.zero);

            Assert.That(pickup.TryCollect(player, out var outcome), Is.True);

            Assert.That(outcome.WeaponId, Is.EqualTo(weapons.Weapon.Spec.Id));
            Assert.That(weapons.Weapon.ReserveAmmo, Is.EqualTo(reserve + outcome.ReserveAmmo));
        }

        [Test]
        public void CollectorsWithoutWeaponsLeaveAmmo()
        {
            var bot = NewCollector("Bot");
            var pickup = Pickup.Create(PickupContents.ForReserveAmmo(30), Vector3.zero);

            Assert.That(pickup.TryCollect(bot, out var outcome, logRejection: false), Is.False);
            Assert.That(outcome.Rejection, Is.EqualTo(PickupRejection.CannotHoldWeapons));
        }

        [Test]
        public void InteractOnlyTakesTheLocalPlayer()
        {
            var bot = NewCollector("Bot");
            bot.TakeDamage(50);
            var player = NewCollector("Player");
            player.IsLocalPlayer = true;
            player.TakeDamage(50);
            var pickup = Pickup.Create(PickupContents.ForHealth(25), Vector3.zero, PickupCollectMode.Interact);

            pickup.HandleEnter(bot.GetComponent<Collider>());
            Assert.That(pickup.IsAvailable, Is.True, "entering does not collect");
            Assert.That(pickup.Interact(), Is.False, "the bot cannot press Interact");

            pickup.HandleEnter(player.GetComponent<Collider>());
            Assert.That(pickup.Interact(), Is.True);
            Assert.That(player.Health.Current, Is.EqualTo(75));
            Assert.That(bot.Health.Current, Is.EqualTo(50));
        }

        [Test]
        public void ExpiresAfterItsLifetime()
        {
            var pickup = Pickup.Create(PickupContents.ForHealth(25), Vector3.zero);
            pickup.LifetimeSeconds = 2f;

            pickup.Tick(1.5f);
            Assert.That(pickup != null, Is.True);

            LogAssert.Expect(LogType.Log, "[Pickup] expired pickup=Pickup-Health");
            pickup.Tick(0.6f);
            Assert.That(pickup == null, Is.True);
        }

        [Test]
        public void SpawnPointHidesThePickupAndBringsItBack()
        {
            var point = new GameObject("Spawn").AddComponent<PickupSpawnPoint>();
            _created.Add(point.gameObject);
            point.transform.position = new Vector3(3f, 0f, 1f);
            point.Contents = PickupContents.ForHealth(25);
            point.RespawnSeconds = 5f;

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Pickup\] spawned pickup=Pickup-Health kind=Health amount=25 who=anyone at=\(3\.00, 0\.00, 1\.00\)$"));
            Assert.That(point.TrySpawn(), Is.True);
            var pickup = point.Current;
            Assert.That(pickup.transform.parent, Is.SameAs(point.transform));

            var player = NewCollector("Player");
            player.TakeDamage(10);
            var collected = 0;
            point.Collected += (source, collection) => collected++;
            Assert.That(pickup.TryCollect(player, out _), Is.True);

            Assert.That(collected, Is.EqualTo(1));
            Assert.That(pickup != null, Is.True, "kept for the next spawn");
            Assert.That(pickup.gameObject.activeSelf, Is.False);
            Assert.That(point.IsAvailable, Is.False);
            Assert.That(point.RemainingSeconds, Is.EqualTo(5f));

            point.Tick(4f);
            Assert.That(pickup.gameObject.activeSelf, Is.False);

            LogAssert.Expect(LogType.Log, "[Pickup] respawned pickup=Pickup-Health");
            point.Tick(1f);
            Assert.That(pickup.gameObject.activeSelf, Is.True);
            Assert.That(pickup.IsAvailable, Is.True);
            Assert.That(point.IsAvailable, Is.True);
        }

        [Test]
        public void SpawnPointReadsTheDefinitionInsteadOfItsOwnValues()
        {
            var definition = NewDefinition("BigMedkit");
            definition.Contents = PickupContents.ForHealth(40);
            definition.CollectMode = PickupCollectMode.Interact;
            definition.RespawnSeconds = 3f;
            definition.Radius = 2f;

            var point = new GameObject("Spawn").AddComponent<PickupSpawnPoint>();
            _created.Add(point.gameObject);
            point.Contents = PickupContents.ForHealth(25);
            point.RespawnSeconds = 30f;
            point.Definition = definition;

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Pickup\] spawned pickup=Pickup-Health kind=Health amount=40 "));
            Assert.That(point.TrySpawn(), Is.True);
            var pickup = point.Current;
            Assert.That(pickup.Spec.Amount, Is.EqualTo(40));
            Assert.That(pickup.CollectMode, Is.EqualTo(PickupCollectMode.Interact));
            Assert.That(pickup.Radius, Is.EqualTo(2f));
            Assert.That(pickup.Contents, Is.Not.SameAs(definition.Contents), "the pickup keeps its own copy of the asset's contents");

            var player = NewCollector("Player");
            player.TakeDamage(50);
            Assert.That(pickup.TryCollect(player, out _), Is.True);
            Assert.That(player.Health.Current, Is.EqualTo(90));
            Assert.That(point.RemainingSeconds, Is.EqualTo(3f), "the asset's respawn time, not the point's own");

            point.Tick(2f);
            Assert.That(pickup.gameObject.activeSelf, Is.False);

            LogAssert.Expect(LogType.Log, "[Pickup] respawned pickup=Pickup-Health");
            point.Tick(1f);
            Assert.That(pickup.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void ANewPickupDefinitionHoldsTheSpawnPointDefaults()
        {
            var definition = NewDefinition("Fresh");

            Assert.That(definition.RespawnSeconds, Is.EqualTo(PickupSpawnPoint.DefaultRespawnSeconds));
            Assert.That(definition.Radius, Is.EqualTo(Pickup.DefaultRadius));
            Assert.That(definition.CollectMode, Is.EqualTo(PickupCollectMode.Touch));
            Assert.That(definition.ToSpec().Kind, Is.EqualTo(PickupKind.Health));
            Assert.That(definition.ToSpec().Amount, Is.EqualTo(PickupContents.DefaultHealthAmount));
            Assert.DoesNotThrow(definition.Validate);
        }

        [Test]
        public void ABadPickupDefinitionNamesTheAssetAndTheSpawnPointStops()
        {
            var definition = NewDefinition("BrokenPack");
            definition.RespawnSeconds = -1f;
            var error = Assert.Throws<System.ArgumentException>(definition.Validate);
            Assert.That(error.Message, Does.Contain("BrokenPack"));

            definition.RespawnSeconds = 1f;
            definition.Radius = 0f;
            Assert.Throws<System.ArgumentException>(definition.Validate);
            Assert.Catch<System.ArgumentException>(() => definition.Contents = PickupContents.ForReserveAmmo(0));
            Assert.Throws<System.ArgumentNullException>(() => definition.Contents = null);

            var point = new GameObject("Spawn").AddComponent<PickupSpawnPoint>();
            _created.Add(point.gameObject);
            point.Definition = definition;

            LogAssert.Expect(LogType.Error, new Regex(@"^\[Pickup\] PickupSpawnPoint 'Spawn' の設定が不正です: pickup definition 'BrokenPack' "));
            Assert.That(point.TrySpawn(), Is.False);
            Assert.That(point.Current, Is.Null);
        }

        [Test]
        public void SpawnPointWithBadContentsReportsAndStops()
        {
            var point = new GameObject("Spawn").AddComponent<PickupSpawnPoint>();
            _created.Add(point.gameObject);
            Assert.Throws<System.ArgumentException>(() => point.Contents = PickupContents.ForReserveAmmo(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => point.RespawnSeconds = -1f);
        }

        [Test]
        public void DropOnDeathDropsItsContentsWhereTheVictimFell()
        {
            var bot = NewCollector("Bot");
            bot.transform.position = new Vector3(4f, 0f, 6f);
            var drop = bot.gameObject.AddComponent<DropOnDeath>();
            drop.Drops = new[] { PickupContents.ForReserveAmmo(30), PickupContents.ForHealth(25) };
            drop.Bind(bot);
            IReadOnlyList<Pickup> dropped = null;
            drop.Dropped += (source, pickups) => dropped = pickups;

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Pickup\] dropped by=Bot pickup=Pickup-ReserveAmmo "));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Pickup\] dropped by=Bot pickup=Pickup-Health "));
            bot.Kill();

            Assert.That(dropped, Has.Count.EqualTo(2));
            var ground = bot.GroundPoint;
            foreach (var pickup in dropped)
            {
                var offset = pickup.transform.position - ground;
                Assert.That(offset.y, Is.EqualTo(0f).Within(1e-4f));
                Assert.That(offset.magnitude, Is.EqualTo(drop.SpreadRadius).Within(1e-4f), "spread around the feet");
                Assert.That(pickup.LifetimeSeconds, Is.EqualTo(DropOnDeath.DefaultLifetimeSeconds));
            }
        }

        [Test]
        public void ModeHookDecidesWhatIsDropped()
        {
            var bot = NewCollector("Bot");
            bot.gameObject.AddComponent<SideMember>().Side = Side.Bravo;
            var drop = bot.gameObject.AddComponent<DropOnDeath>();
            drop.Bind(bot);
            DropContext? seen = null;
            drop.DecideDrops = (context, drops) =>
            {
                seen = context;
                drops.Clear();
                drops.Add(PickupContents.ForTag(7, context.VictimSide));
            };
            IReadOnlyList<Pickup> dropped = null;
            drop.Dropped += (source, pickups) => dropped = pickups;

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Pickup\] dropped by=Bot pickup=Pickup-Tag kind=Tag owner=7 owner-side=1 "));
            bot.Kill();

            Assert.That(seen.HasValue, Is.True);
            Assert.That(seen.Value.Victim, Is.SameAs(bot));
            Assert.That(seen.Value.Killer, Is.Null, "no attribution outside a shot");
            Assert.That(dropped, Has.Count.EqualTo(1));

            var collector = NewCollector("Player");
            collector.gameObject.AddComponent<SideMember>().Side = Side.Alpha;
            Assert.That(dropped[0].TryCollect(collector, out var outcome), Is.True);
            Assert.That(outcome.TagOwnerId, Is.EqualTo(7));
            Assert.That(outcome.IsOwnSideTag, Is.False);
            Assert.That(drop.Drops, Has.Count.EqualTo(1), "the hook edits a copy, not the configured drops");
        }

        [Test]
        public void AFailingHookDropsNothing()
        {
            var bot = NewCollector("Bot");
            var drop = bot.gameObject.AddComponent<DropOnDeath>();
            drop.Bind(bot);
            drop.DecideDrops = (context, drops) => throw new System.InvalidOperationException("boom");

            LogAssert.Expect(LogType.Error, new Regex("DecideDrops"));
            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            bot.Kill();

            Assert.That(Object.FindObjectsByType<Pickup>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
        }

        private Damageable NewCollector(string name)
        {
            var go = new GameObject(name, typeof(CapsuleCollider));
            _created.Add(go);
            var damageable = go.AddComponent<Damageable>();
            damageable.Initialize(name, 100);
            return damageable;
        }

        private WeaponController AttachWeapons(Damageable owner, WeaponDefinition definition)
        {
            var camera = new GameObject("Camera", typeof(Camera));
            camera.transform.SetParent(owner.transform, false);
            var weapons = camera.AddComponent<WeaponController>();
            weapons.Initialize(camera.transform, _attack, _reload, definition);
            return weapons;
        }

        private PickupDefinition NewDefinition(string name)
        {
            var definition = ScriptableObject.CreateInstance<PickupDefinition>();
            definition.name = name;
            _created.Add(definition);
            return definition;
        }

        private WeaponDefinition NewWeapon(string name, WeaponSpec spec)
        {
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            definition.name = name;
            WeaponAssetBuilder.Configure(definition, spec);
            _created.Add(definition);
            return definition;
        }
    }
}
