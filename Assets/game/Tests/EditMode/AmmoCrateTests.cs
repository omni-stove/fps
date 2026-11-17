using System.Collections.Generic;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class AmmoCrateTests
    {
        private readonly List<Object> _created = new List<Object>();
        private InputAction _attack;
        private InputAction _reload;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
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
            GameSignals.ResetAll();
        }

        [Test]
        public void TheHintSaysWhetherAnyWeaponHasRoom()
        {
            var player = NewPlayer(out var weapons);
            Assert.That(AmmoCrate.TryDescribe(player, out var full), Is.True);
            Assert.That(full, Is.EqualTo(new AmmoCrateHint(false, AmmoCrate.InteractActionPath)), "a new weapon starts with a full reserve");

            weapons.RestoreLoadout(new[] { new WeaponAmmo(weapons.Weapon.Spec.Id, 0, 0) }, null);
            Assert.That(AmmoCrate.TryDescribe(player, out var room), Is.True);
            Assert.That(room, Is.EqualTo(new AmmoCrateHint(true, AmmoCrate.InteractActionPath)));
        }

        [Test]
        public void NoHintWithoutWeaponsOrWhileDown()
        {
            var unarmed = NewCollector("Unarmed");
            Assert.That(AmmoCrate.TryDescribe(unarmed, out _), Is.False);

            var player = NewPlayer(out _);
            player.SetDowned(true);
            Assert.That(AmmoCrate.TryDescribe(player, out _), Is.False);
            Assert.That(AmmoCrate.TryDescribe(null, out _), Is.False);
        }

        [Test]
        public void TheHintIsPublishedWhileThePlayerIsInRangeAndClearedWhenTheyLeave()
        {
            var player = NewPlayer(out _);
            player.IsLocalPlayer = true;
            var crate = new GameObject("Crate").AddComponent<AmmoCrate>();
            _created.Add(crate.gameObject);

            crate.UpdatePrompt();
            Assert.That(GameSignals.InteractHint.HasValue, Is.False);

            crate.HandleEnter(player.GetComponent<Collider>());
            crate.UpdatePrompt();
            Assert.That(GameSignals.InteractHint.Value, Is.EqualTo(new InteractHint(crate, new AmmoCrateHint(false, AmmoCrate.InteractActionPath))));

            crate.HandleExit(player.GetComponent<Collider>());
            crate.UpdatePrompt();
            Assert.That(GameSignals.InteractHint.HasValue, Is.False);
        }

        [Test]
        public void ABotInRangeGetsNoHint()
        {
            var bot = NewPlayer(out _);
            var crate = new GameObject("Crate").AddComponent<AmmoCrate>();
            _created.Add(crate.gameObject);

            crate.HandleEnter(bot.GetComponent<Collider>());
            crate.UpdatePrompt();

            Assert.That(crate.InsideCount, Is.EqualTo(1));
            Assert.That(GameSignals.InteractHint.HasValue, Is.False, "only the player on this screen is told");
        }
        [Test]
        public void RefillTopsUpEveryWeaponButTheInfiniteOnes()
        {
            var holder = new FakeHolder(
                new PickupWeaponState("rifle", 10, 90, false),
                new PickupWeaponState("handgun", 36, 36, false),
                new PickupWeaponState("launcher", 0, 4, false),
                new PickupWeaponState("knife", 0, 0, true));

            var added = AmmoCrate.Refill(holder);

            Assert.That(added, Is.EqualTo(84));
            Assert.That(holder.Added, Is.EqualTo(new[] { ("rifle", 80), ("launcher", 4) }));
        }

        [Test]
        public void RefillOfFullWeaponsAddsNothing()
        {
            var holder = new FakeHolder(new PickupWeaponState("rifle", 90, 90, false));

            Assert.That(AmmoCrate.Refill(holder), Is.EqualTo(0));
            Assert.That(holder.Added, Is.Empty);
        }

        [Test]
        public void BuildsATriggerAndASolidPlaceholderBox()
        {
            var crate = new GameObject("Crate").AddComponent<AmmoCrate>();
            try
            {
                crate.Build();
                crate.Build();
                var trigger = crate.GetComponent<SphereCollider>();
                Assert.That(trigger.isTrigger, Is.True);
                Assert.That(trigger.radius, Is.EqualTo(AmmoCrate.DefaultRadius));
                var visual = crate.transform.Find(AmmoCrate.VisualName);
                Assert.That(visual, Is.Not.Null);
                Assert.That(visual.GetComponent<Collider>().isTrigger, Is.False, "the box stays solid so it can be used as cover");
            }
            finally
            {
                Object.DestroyImmediate(crate.gameObject);
            }
        }

        private Damageable NewCollector(string name)
        {
            var go = new GameObject(name, typeof(CapsuleCollider));
            _created.Add(go);
            var damageable = go.AddComponent<Damageable>();
            damageable.Initialize(name, 100);
            return damageable;
        }

        private Damageable NewPlayer(out WeaponController weapons)
        {
            var player = NewCollector("Player");
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            definition.name = "Rifle";
            WeaponAssets.Configure(definition, TestWeapons.RifleSpec());
            _created.Add(definition);
            var camera = new GameObject("Camera", typeof(Camera));
            camera.transform.SetParent(player.transform, false);
            weapons = camera.AddComponent<WeaponController>();
            weapons.Initialize(camera.transform, _attack, _reload, definition);
            return player;
        }

        private sealed class FakeHolder : IWeaponHolder
        {
            private readonly List<PickupWeaponState> _weapons;

            public FakeHolder(params PickupWeaponState[] weapons)
            {
                _weapons = new List<PickupWeaponState>(weapons);
            }

            public List<(string, int)> Added { get; } = new List<(string, int)>();

            public IReadOnlyList<PickupWeaponState> DescribeWeapons(out string currentWeaponId)
            {
                currentWeaponId = _weapons.Count > 0 ? _weapons[0].WeaponId : null;
                return _weapons;
            }

            public int MaxWeapons => 0;

            public bool AddWeapon(WeaponDefinition definition, WeaponAmmo ammo = null, IReadOnlyList<string> attachments = null) => false;

            public bool SwapWeapon(
                string droppedWeaponId, WeaponDefinition definition, WeaponAmmo ammo, IReadOnlyList<string> attachments, out HeldWeapon dropped)
            {
                dropped = default;
                return false;
            }

            public int AddReserveAmmo(string weaponId, int amount)
            {
                Added.Add((weaponId, amount));
                return amount;
            }
        }
    }
}
