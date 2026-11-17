using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class AmmoCrateTests
    {
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
