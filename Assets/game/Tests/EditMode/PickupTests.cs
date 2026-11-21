using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
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
            foreach (var pickup in Object.FindObjectsByType<Pickup>(FindObjectsInactive.Include))
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
            LogAssert.Expect(LogType.Log, "[Combat] target died id=Bot");
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Combat\] Killed killer=-?\d+ victim=-?\d+ weapon= headshot=False friendly=False$"));
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
            var weapons = AttachWeapons(player, NewWeapon("Rifle", TestWeapons.RifleSpec()));
            var launcher = NewWeapon("Launcher", TestWeapons.LauncherSpec());
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
            var weapons = AttachWeapons(player, NewWeapon("Rifle", TestWeapons.RifleSpec()));
            weapons.Weapon.TryFire();
            var magazine = weapons.Weapon.AmmoInMagazine;

            Assert.That(weapons.AddWeapon(NewWeapon("Launcher", TestWeapons.LauncherSpec())), Is.True);

            Assert.That(weapons.Weapon.AmmoInMagazine, Is.EqualTo(magazine));
            Assert.That(weapons.AddWeapon(NewWeapon("Launcher2", TestWeapons.LauncherSpec())), Is.False, "same id is held already");
        }

        [Test]
        public void OwnedWeaponGivesAmmoInstead()
        {
            var player = NewCollector("Player");
            var rifle = NewWeapon("Rifle", TestWeapons.RifleSpec());
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
            var weapons = AttachWeapons(player, NewWeapon("Rifle", TestWeapons.RifleSpec()));
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
        public void AnInteractWeaponPromptNestsTheWeaponName()
        {
            var player = NewCollector("Player");
            player.IsLocalPlayer = true;
            AttachWeapons(player, NewWeapon("Rifle", TestWeapons.RifleSpec()));
            var launcher = NewWeapon("Launcher", TestWeapons.LauncherSpec());
            var pickup = Pickup.Create(PickupContents.ForWeapon(launcher, 2), Vector3.zero, PickupCollectMode.Interact);

            pickup.HandleEnter(player.GetComponent<Collider>());

            Assert.That(pickup.LocalPlayerInside(), Is.SameAs(player));
            Assert.That(pickup.TryDescribe(player, out var hint), Is.True);
            Assert.That(hint, Is.EqualTo(new PickupHint(PickupHintAction.TakeWeapon, launcher.Id, launcher.NameKey, 0, Pickup.InteractActionPath)));
        }

        [Test]
        public void ALookPickupLiesStillWithATargetOnlyTheAimHits()
        {
            var launcher = NewWeapon("Launcher", TestWeapons.LauncherSpec());
            var pickup = Pickup.Create(PickupContents.ForWeapon(launcher, 2), new Vector3(0f, 0f, 1f), PickupCollectMode.Look);

            var visual = pickup.transform.Find(Pickup.VisualName);
            Assert.That(visual.GetComponentsInChildren<Renderer>(), Is.Not.Empty, "a gun shape stands in for the missing model");
            var target = visual.Find(Pickup.LookTargetName);
            Assert.That(target, Is.Not.Null);
            var box = target.GetComponent<BoxCollider>();
            Assert.That(box.isTrigger, Is.True, "the aim target does not block movement or shots");
            Assert.That(target.GetComponent<Rigidbody>().isKinematic, Is.True);
            Assert.That(pickup.GetComponents<Collider>().Length, Is.EqualTo(1), "the range is still only the trigger sphere");

            Physics.SyncTransforms();
            Assert.That(box.bounds.min.y, Is.EqualTo(Pickup.LyingGap).Within(0.001f), "lies on the ground");
            var rotation = visual.localRotation;
            pickup.Tick(1f);
            Assert.That(visual.localRotation, Is.EqualTo(rotation), "does not spin");

            pickup.CollectMode = PickupCollectMode.Touch;
            Assert.That(visual.Find(Pickup.LookTargetName), Is.Null);
            Assert.That(visual.localPosition, Is.EqualTo(new Vector3(0f, Pickup.VisualHeight, 0f)));
        }

        [Test]
        public void ALookPickupIsTakenOnlyWhileTheViewAimsAtIt()
        {
            var player = NewCollector("Player");
            player.IsLocalPlayer = true;
            var weapons = AttachWeapons(player, NewWeapon("Rifle", TestWeapons.RifleSpec()));
            weapons.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var launcher = NewWeapon("Launcher", TestWeapons.LauncherSpec());
            var pickup = Pickup.Create(PickupContents.ForWeapon(launcher, 2), new Vector3(0f, 0f, 1.5f), PickupCollectMode.Look, radius: 2f);
            pickup.HandleEnter(player.GetComponent<Collider>());
            var target = pickup.transform.Find(Pickup.VisualName).Find(Pickup.LookTargetName).GetComponent<BoxCollider>();

            weapons.transform.rotation = Quaternion.LookRotation(Vector3.up);
            Physics.SyncTransforms();
            Assert.That(pickup.IsLookedAtBy(player), Is.False);
            Assert.That(pickup.Interact(), Is.False, "looking away");

            weapons.transform.rotation = Quaternion.LookRotation(target.bounds.center - weapons.transform.position);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(wall);
            wall.transform.position = Vector3.Lerp(weapons.transform.position, target.bounds.center, 0.5f);
            wall.transform.localScale = Vector3.one * 0.2f;
            Physics.SyncTransforms();
            Assert.That(Pickup.LookedAt(player), Is.Null, "a wall in the way stops the aim");

            Object.DestroyImmediate(wall);
            Physics.SyncTransforms();
            Assert.That(Pickup.LookedAt(player), Is.SameAs(pickup));
            Assert.That(pickup.TryDescribe(player, out var hint), Is.True);
            Assert.That(hint, Is.EqualTo(new PickupHint(PickupHintAction.TakeWeapon, launcher.Id, launcher.NameKey, 0, Pickup.InteractActionPath)));
            Assert.That(pickup.Interact(), Is.True);
            Assert.That(weapons.Loadout.Count, Is.EqualTo(2));
        }

        [Test]
        public void ALookPickupAtTheFeetIsTakenWhileLookingStraightAhead()
        {
            var player = NewCollector("Player");
            player.IsLocalPlayer = true;
            var weapons = AttachWeapons(player, NewWeapon("Rifle", TestWeapons.RifleSpec()));
            weapons.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            weapons.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            var launcher = NewWeapon("Launcher", TestWeapons.LauncherSpec());
            var pickup = Pickup.Create(PickupContents.ForWeapon(launcher, 2), new Vector3(0f, 0f, 1f), PickupCollectMode.Look, radius: 2f);
            pickup.HandleEnter(player.GetComponent<Collider>());
            Physics.SyncTransforms();

            // 目の高さ 1.6 m から 1 m 先の足元は、正面から約 58° 下に見え、既定の 90° 以内に入る。
            Assert.That(Pickup.LookedAt(player), Is.SameAs(pickup));
            Assert.That(pickup.Interact(), Is.True);
            Assert.That(weapons.Loadout.Count, Is.EqualTo(2));
        }

        [Test]
        public void ALookPickupOutsideItsLookAngleOrBehindIsNotAimedAt()
        {
            var player = NewCollector("Player");
            player.IsLocalPlayer = true;
            var weapons = AttachWeapons(player, NewWeapon("Rifle", TestWeapons.RifleSpec()));
            weapons.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            weapons.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            var launcher = NewWeapon("Launcher", TestWeapons.LauncherSpec());
            var ahead = Pickup.Create(
                PickupContents.ForWeapon(launcher, 2), new Vector3(0f, 0f, 1f), PickupCollectMode.Look, radius: 2f, lookAngleDegrees: 45f);
            var behind = Pickup.Create(PickupContents.ForWeapon(launcher, 2), new Vector3(0f, 0f, -1f), PickupCollectMode.Look, radius: 2f);
            ahead.HandleEnter(player.GetComponent<Collider>());
            behind.HandleEnter(player.GetComponent<Collider>());
            Physics.SyncTransforms();

            Assert.That(ahead.LookAngleDegrees, Is.EqualTo(45f));
            Assert.That(Pickup.LookedAt(player), Is.Null, "about 58° below the aim is past 45°, and the one behind is about 122° away");

            ahead.LookAngleDegrees = 60f;
            Assert.That(Pickup.LookedAt(player), Is.SameAs(ahead));

            ahead.LookAngleDegrees = 500f;
            Assert.That(ahead.LookAngleDegrees, Is.EqualTo(Pickup.MaxLookAngleDegrees));
            ahead.LookAngleDegrees = float.NaN;
            Assert.That(ahead.LookAngleDegrees, Is.EqualTo(0f));
        }

        [Test]
        public void OfTheLookPickupsInRangeTheOneNearestTheAimIsChosen()
        {
            var player = NewCollector("Player");
            player.IsLocalPlayer = true;
            var weapons = AttachWeapons(player, NewWeapon("Rifle", TestWeapons.RifleSpec()));
            weapons.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var launcher = NewWeapon("Launcher", TestWeapons.LauncherSpec());
            var left = Pickup.Create(PickupContents.ForWeapon(launcher, 2), new Vector3(-1f, 0f, 1f), PickupCollectMode.Look, radius: 2f);
            var right = Pickup.Create(PickupContents.ForWeapon(launcher, 2), new Vector3(1f, 0f, 1f), PickupCollectMode.Look, radius: 2f);
            left.HandleEnter(player.GetComponent<Collider>());
            right.HandleEnter(player.GetComponent<Collider>());

            weapons.transform.rotation = Quaternion.LookRotation(new Vector3(0.3f, 0f, 1f));
            Physics.SyncTransforms();
            Assert.That(Pickup.LookedAt(player), Is.SameAs(right));

            weapons.transform.rotation = Quaternion.LookRotation(new Vector3(-0.3f, 0f, 1f));
            Physics.SyncTransforms();
            Assert.That(Pickup.LookedAt(player), Is.SameAs(left));

            left.HandleExit(player.GetComponent<Collider>());
            Assert.That(Pickup.LookedAt(player), Is.SameAs(right), "one the player has left is not a candidate");
        }

        [Test]
        public void AFarLookPickupInTheAimDoesNotHideOneInRange()
        {
            var player = NewCollector("Player");
            player.IsLocalPlayer = true;
            var weapons = AttachWeapons(player, NewWeapon("Rifle", TestWeapons.RifleSpec()));
            weapons.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var launcher = NewWeapon("Launcher", TestWeapons.LauncherSpec());
            var far = Pickup.Create(PickupContents.ForWeapon(launcher, 2), new Vector3(0f, 0f, 10f), PickupCollectMode.Look);
            var near = Pickup.Create(PickupContents.ForWeapon(launcher, 2), new Vector3(0f, 0f, 1f), PickupCollectMode.Look, radius: 2f);
            near.HandleEnter(player.GetComponent<Collider>());
            Physics.SyncTransforms();
            var farTarget = far.transform.Find(Pickup.VisualName).Find(Pickup.LookTargetName).GetComponent<BoxCollider>();
            weapons.transform.rotation = Quaternion.LookRotation(farTarget.bounds.center - weapons.transform.position);
            Physics.SyncTransforms();

            Assert.That(Pickup.LookedAt(player), Is.SameAs(near));
        }

        [Test]
        public void AtTheWeaponLimitANewWeaponSwapsForTheOneInHandAndDropsIt()
        {
            var player = NewCollector("Player");
            var rifle = NewWeapon("Rifle", TestWeapons.RifleSpec());
            var weapons = AttachWeapons(player, rifle);
            weapons.Rules = GameRules.Default.WithMaxWeapons(1);
            weapons.Weapon.TryFire();
            var magazine = weapons.Weapon.AmmoInMagazine;
            var reserve = weapons.Weapon.ReserveAmmo;
            var launcher = NewWeapon("Launcher", TestWeapons.LauncherSpec());
            var pickup = Pickup.Create(PickupContents.ForWeapon(launcher, 2), new Vector3(0f, 0f, 2f), PickupCollectMode.Interact);
            Assert.That(pickup.TryDescribe(player, out var hint), Is.True);
            Assert.That(hint, Is.EqualTo(new PickupHint(PickupHintAction.SwapWeapon, launcher.Id, launcher.NameKey, 0, Pickup.InteractActionPath)));

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Pickup\] dropped by=Player pickup=Pickup-Weapon kind=Weapon weapon=" + rifle.Id));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Pickup\] collected .* added dropped=" + rifle.Id + "$"));
            Assert.That(pickup.TryCollect(player, out var outcome), Is.True);

            Assert.That(outcome.DroppedWeaponId, Is.EqualTo(rifle.Id));
            Assert.That(weapons.Loadout.Count, Is.EqualTo(1));
            Assert.That(weapons.Weapon.Spec.Id, Is.EqualTo(launcher.Id), "holds the new weapon");
            var dropped = Object.FindObjectsByType<Pickup>(FindObjectsInactive.Exclude).Single();
            Assert.That(dropped.CollectMode, Is.EqualTo(PickupCollectMode.Look), "touching it would take it right back");
            Assert.That(dropped.Contents.Weapon, Is.SameAs(rifle));
            Assert.That(dropped.Contents.CarriedAmmo.AmmoInMagazine, Is.EqualTo(magazine));
            Assert.That(Vector3.Distance(dropped.transform.position, new Vector3(0f, 0f, 2f - Pickup.DroppedWeaponOffset)), Is.LessThan(0.001f), "moved toward the player");

            Assert.That(dropped.TryCollect(player, out var back), Is.True);

            Assert.That(back.DroppedWeaponId, Is.EqualTo(launcher.Id));
            Assert.That(weapons.Weapon.Spec.Id, Is.EqualTo(rifle.Id));
            Assert.That(weapons.Weapon.AmmoInMagazine, Is.EqualTo(magazine), "the dropped rifle keeps its ammo");
            Assert.That(weapons.Weapon.ReserveAmmo, Is.EqualTo(reserve));
        }

        [Test]
        public void ADroppedWeaponBelowTheLimitIsAddedWithItsAmmo()
        {
            var player = NewCollector("Player");
            var weapons = AttachWeapons(player, NewWeapon("Rifle", TestWeapons.RifleSpec()));
            var launcher = NewWeapon("Launcher", TestWeapons.LauncherSpec());
            var contents = PickupContents.ForDroppedWeapon(launcher, new WeaponAmmo(launcher.Id, 0, 1));
            var pickup = Pickup.Create(contents, Vector3.zero, PickupCollectMode.Look);

            Assert.That(pickup.TryCollect(player, out var outcome), Is.True);

            Assert.That(outcome.DroppedWeaponId, Is.Null);
            Assert.That(weapons.Loadout.Count, Is.EqualTo(2));
            Assert.That(weapons.Loadout.Items[1].AmmoInMagazine, Is.Zero);
            Assert.That(weapons.Loadout.Items[1].ReserveAmmo, Is.EqualTo(1));
            Assert.Throws<System.ArgumentException>(() => PickupContents.ForDroppedWeapon(launcher, new WeaponAmmo("other", 0, 0)));
        }

        [Test]
        public void NoPromptForTouchPickupsOrWhatCannotBeTaken()
        {
            var player = NewCollector("Player");
            player.IsLocalPlayer = true;
            var touch = Pickup.Create(PickupContents.ForHealth(25), Vector3.zero);
            var interact = Pickup.Create(PickupContents.ForHealth(25), Vector3.zero, PickupCollectMode.Interact);

            Assert.That(touch.TryDescribe(player, out _), Is.False, "touch pickups are taken without a key");
            Assert.That(interact.TryDescribe(player, out _), Is.False, "health is full");

            player.TakeDamage(50);
            Assert.That(interact.TryDescribe(player, out var hint), Is.True);
            Assert.That(hint, Is.EqualTo(new PickupHint(PickupHintAction.Health, null, null, 0, Pickup.InteractActionPath)));
        }

        [Test]
        public void TheHintIsPublishedWhileThePlayerCanTakeItAndClearedWhenTheyLeave()
        {
            GameSignals.ResetAll();
            try
            {
                var player = NewCollector("Player");
                player.IsLocalPlayer = true;
                player.TakeDamage(50);
                var pickup = Pickup.Create(PickupContents.ForHealth(25), Vector3.zero, PickupCollectMode.Interact);

                pickup.UpdatePrompt();
                Assert.That(GameSignals.InteractHint.HasValue, Is.False, "no one is in range");

                pickup.HandleEnter(player.GetComponent<Collider>());
                pickup.UpdatePrompt();
                var expected = new InteractHint(pickup, new PickupHint(PickupHintAction.Health, null, null, 0, Pickup.InteractActionPath));
                Assert.That(GameSignals.InteractHint.Value, Is.EqualTo(expected));

                pickup.HandleExit(player.GetComponent<Collider>());
                pickup.UpdatePrompt();
                Assert.That(GameSignals.InteractHint.HasValue, Is.False, "leaving the range clears the hint");

                pickup.HandleEnter(player.GetComponent<Collider>());
                pickup.UpdatePrompt();
                player.ResetHealth();
                pickup.UpdatePrompt();
                Assert.That(GameSignals.InteractHint.HasValue, Is.False, "a full player has nothing to take");
            }
            finally
            {
                GameSignals.ResetAll();
            }
        }

        [Test]
        public void LeavingDoesNotClearAnotherPickupsHint()
        {
            GameSignals.ResetAll();
            try
            {
                var player = NewCollector("Player");
                player.IsLocalPlayer = true;
                player.TakeDamage(50);
                var first = Pickup.Create(PickupContents.ForHealth(25), Vector3.zero, PickupCollectMode.Interact);
                var second = Pickup.Create(PickupContents.ForHealth(10), Vector3.zero, PickupCollectMode.Interact);
                first.HandleEnter(player.GetComponent<Collider>());
                second.HandleEnter(player.GetComponent<Collider>());

                first.UpdatePrompt();
                second.UpdatePrompt();
                Assert.That(GameSignals.InteractHint.Value?.Source, Is.SameAs(second), "the newer hint replaces the older one");

                first.UpdatePrompt();
                Assert.That(GameSignals.InteractHint.Value?.Source, Is.SameAs(second), "an unchanged hint does not take it back every frame");

                second.HandleExit(player.GetComponent<Collider>());
                second.UpdatePrompt();
                first.UpdatePrompt();
                Assert.That(GameSignals.InteractHint.Value?.Source, Is.SameAs(first), "the hint comes back once the other is cleared");

                second.HandleEnter(player.GetComponent<Collider>());
                second.UpdatePrompt();
                first.HandleExit(player.GetComponent<Collider>());
                first.UpdatePrompt();
                Assert.That(GameSignals.InteractHint.Value?.Source, Is.SameAs(second), "leaving clears only its own hint");
            }
            finally
            {
                GameSignals.ResetAll();
            }
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
            definition.LookAngleDegrees = 30f;

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
            Assert.That(pickup.LookAngleDegrees, Is.EqualTo(30f));
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
            Assert.That(definition.LookAngleDegrees, Is.EqualTo(Pickup.DefaultLookAngleDegrees));
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
            definition.Radius = 1f;
            definition.LookAngleDegrees = 200f;
            Assert.Throws<System.ArgumentException>(definition.Validate);
            definition.LookAngleDegrees = float.NaN;
            Assert.Throws<System.ArgumentException>(definition.Validate);
            definition.Radius = 0f;
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
            Assert.Catch<System.ArgumentException>(() => point.Contents = PickupContents.ForReserveAmmo(0));
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

            Assert.That(dropped.Count(), Is.EqualTo(2));
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
            Assert.That(dropped.Count(), Is.EqualTo(1));

            var collector = NewCollector("Player");
            collector.gameObject.AddComponent<SideMember>().Side = Side.Alpha;
            Assert.That(dropped[0].TryCollect(collector, out var outcome), Is.True);
            Assert.That(outcome.TagOwnerId, Is.EqualTo(7));
            Assert.That(outcome.IsOwnSideTag, Is.False);
            Assert.That(drop.Drops.Count, Is.EqualTo(1), "the hook edits a copy, not the configured drops");
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

            Assert.That(Object.FindObjectsByType<Pickup>(FindObjectsInactive.Include), Is.Empty);
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
            WeaponAssets.Configure(definition, spec);
            _created.Add(definition);
            return definition;
        }
    }
}
