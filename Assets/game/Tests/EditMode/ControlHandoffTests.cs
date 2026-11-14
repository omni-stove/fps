using System;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>操作を手放す口 (<see cref="ControlHandoff"/>) の宣言の数え方と、<see cref="WeaponController"/> がそれを見ること。</summary>
    public sealed class ControlHandoffTests
    {
        [Test]
        public void ControlIsHandedOffUntilEveryHolderReleases()
        {
            var handoff = new ControlHandoff();
            var seat = new object();
            var turret = new object();

            Assert.That(handoff.IsHandedOff, Is.False);
            Assert.That(handoff.Holder, Is.Null);

            Assert.That(handoff.Claim(seat), Is.True);
            Assert.That(handoff.Claim(seat), Is.False, "claimed once");
            Assert.That(handoff.Claim(turret), Is.True);
            Assert.That(handoff.Holder, Is.SameAs(turret), "the latest holder");
            Assert.That(handoff.IsHeldBy(seat), Is.True);

            Assert.That(handoff.Release(turret), Is.True);
            Assert.That(handoff.IsHandedOff, Is.True, "the seat still holds it");
            Assert.That(handoff.Release(turret), Is.False);

            Assert.That(handoff.Release(seat), Is.True);
            Assert.That(handoff.IsHandedOff, Is.False);
        }

        [Test]
        public void ADestroyedHolderNoLongerHoldsControl()
        {
            var handoff = new ControlHandoff();
            var seat = new GameObject("Seat");
            handoff.Claim(seat);

            Object.DestroyImmediate(seat);

            Assert.That(handoff.IsHandedOff, Is.False);
            Assert.That(handoff.Holder, Is.Null);
            Assert.Throws<ArgumentException>(() => handoff.Claim(seat), "a destroyed object cannot claim");
        }

        [Test]
        public void RejectsNullHolders()
        {
            var handoff = new ControlHandoff();

            Assert.Throws<ArgumentNullException>(() => handoff.Claim(null));
            Assert.Throws<ArgumentNullException>(() => handoff.Release(null));
            Assert.Throws<ArgumentNullException>(() => handoff.IsHeldBy(null));
        }

        [Test]
        public void TheWeaponStopsReadingInputWhileItsHandoffIsHeld()
        {
            var camera = new GameObject("Camera");
            try
            {
                var weapon = camera.AddComponent<WeaponController>();
                Assert.That(weapon.IsInputHandedOff, Is.False, "no handoff wired");

                var handoff = new ControlHandoff();
                weapon.Handoff = handoff;
                Assert.That(weapon.IsInputHandedOff, Is.False);

                var seat = new object();
                handoff.Claim(seat);
                Assert.That(weapon.IsInputHandedOff, Is.True);

                handoff.Release(seat);
                Assert.That(weapon.IsInputHandedOff, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(camera);
            }
        }

        [Test]
        public void ThePlayerHandoffIsTheSameAcrossCalls()
        {
            var body = new GameObject("Body");
            try
            {
                var mover = body.AddComponent<PlayerController>();

                Assert.That(mover.Handoff, Is.Not.Null);
                Assert.That(mover.Handoff, Is.SameAs(mover.Handoff));
                Assert.That(mover.IsControlHandedOff, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }
    }
}
