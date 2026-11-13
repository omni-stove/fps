using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class PlayerActionStateTests
    {
        private static PlayerActionState Create(
            PlayerActionFlags flags = PlayerActionFlags.None,
            float reloadProgress = 0f,
            ActiveReloadResult? activeReload = null,
            ShoulderSide shoulder = ShoulderSide.Right,
            ViewMode viewMode = ViewMode.FirstPerson,
            string weaponName = "Rifle",
            float spreadDegrees = 0f)
        {
            return new PlayerActionState(flags, reloadProgress, activeReload, shoulder, viewMode, weaponName, spreadDegrees);
        }

        [Test]
        public void DefaultIsIdleFirstPersonOnTheRightShoulderWithoutAWeapon()
        {
            var state = default(PlayerActionState);

            Assert.That(state.Flags, Is.EqualTo(PlayerActionFlags.None));
            Assert.That(state.ReloadProgress, Is.Zero);
            Assert.That(state.ActiveReload, Is.Null);
            Assert.That(state.Shoulder, Is.EqualTo(ShoulderSide.Right));
            Assert.That(state.ViewMode, Is.EqualTo(ViewMode.FirstPerson));
            Assert.That(state.WeaponName, Is.Empty, "a default state never hands out a null name");
            Assert.That(state.SpreadDegrees, Is.Zero);
            Assert.That(state, Is.EqualTo(Create(weaponName: null)));
        }

        [TestCase(PlayerActionFlags.Sprinting)]
        [TestCase(PlayerActionFlags.Crouching)]
        [TestCase(PlayerActionFlags.Dodging)]
        [TestCase(PlayerActionFlags.Vaulting)]
        [TestCase(PlayerActionFlags.Grounded)]
        [TestCase(PlayerActionFlags.InCover)]
        [TestCase(PlayerActionFlags.BlindFiring)]
        [TestCase(PlayerActionFlags.Aiming)]
        [TestCase(PlayerActionFlags.Firing)]
        [TestCase(PlayerActionFlags.Reloading)]
        [TestCase(PlayerActionFlags.Downed)]
        public void EachFlagTurnsOnOnlyItsOwnProperty(PlayerActionFlags flag)
        {
            var state = Create(flag);

            Assert.That(state.IsSprinting, Is.EqualTo(flag == PlayerActionFlags.Sprinting));
            Assert.That(state.IsCrouching, Is.EqualTo(flag == PlayerActionFlags.Crouching));
            Assert.That(state.IsDodging, Is.EqualTo(flag == PlayerActionFlags.Dodging));
            Assert.That(state.IsVaulting, Is.EqualTo(flag == PlayerActionFlags.Vaulting));
            Assert.That(state.IsGrounded, Is.EqualTo(flag == PlayerActionFlags.Grounded));
            Assert.That(state.IsInCover, Is.EqualTo(flag == PlayerActionFlags.InCover));
            Assert.That(state.IsBlindFiring, Is.EqualTo(flag == PlayerActionFlags.BlindFiring));
            Assert.That(state.IsAiming, Is.EqualTo(flag == PlayerActionFlags.Aiming));
            Assert.That(state.IsFiring, Is.EqualTo(flag == PlayerActionFlags.Firing));
            Assert.That(state.IsReloading, Is.EqualTo(flag == PlayerActionFlags.Reloading));
            Assert.That(state.IsDowned, Is.EqualTo(flag == PlayerActionFlags.Downed));
        }

        [Test]
        public void KeepsEveryValueItWasBuiltWith()
        {
            var flags = PlayerActionFlags.Grounded | PlayerActionFlags.InCover | PlayerActionFlags.Reloading;
            var state = new PlayerActionState(flags, 0.4f, ActiveReloadResult.Fail, ShoulderSide.Left, ViewMode.ThirdPerson, "Handgun", 12f);

            Assert.That(state.Flags, Is.EqualTo(flags));
            Assert.That(state.IsGrounded && state.IsInCover && state.IsReloading, Is.True);
            Assert.That(state.IsSprinting || state.IsAiming || state.IsFiring, Is.False);
            Assert.That(state.ReloadProgress, Is.EqualTo(0.4f));
            Assert.That(state.ActiveReload, Is.EqualTo(ActiveReloadResult.Fail));
            Assert.That(state.Shoulder, Is.EqualTo(ShoulderSide.Left));
            Assert.That(state.ViewMode, Is.EqualTo(ViewMode.ThirdPerson));
            Assert.That(state.WeaponName, Is.EqualTo("Handgun"));
            Assert.That(state.SpreadDegrees, Is.EqualTo(12f));
        }

        [Test]
        public void AllFlagsCanBeSetAtOnce()
        {
            var state = Create(PlayerActionState.AllFlags, reloadProgress: 1f);

            Assert.That(state.IsSprinting && state.IsCrouching && state.IsDodging && state.IsVaulting && state.IsGrounded, Is.True);
            Assert.That(state.IsInCover && state.IsBlindFiring && state.IsAiming && state.IsFiring && state.IsReloading && state.IsDowned, Is.True);
        }

        [Test]
        public void ActiveReloadSuccessStaysAfterTheReloadEnds()
        {
            var state = Create(activeReload: ActiveReloadResult.Success);

            Assert.That(state.IsReloading, Is.False);
            Assert.That(state.ActiveReload, Is.EqualTo(ActiveReloadResult.Success));
        }

        [Test]
        public void EqualStatesCompareEqualAndDifferentOnesDoNot()
        {
            var a = Create(PlayerActionFlags.Aiming, weaponName: "Rifle", spreadDegrees: 8f);
            var b = Create(PlayerActionFlags.Aiming, weaponName: "Rifle", spreadDegrees: 8f);

            Assert.That(a == b, Is.True);
            Assert.That(a.Equals((object)b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));

            Assert.That(a != Create(PlayerActionFlags.Firing, weaponName: "Rifle", spreadDegrees: 8f), Is.True);
            Assert.That(a != Create(PlayerActionFlags.Aiming, weaponName: "Handgun", spreadDegrees: 8f), Is.True);
            Assert.That(a != Create(PlayerActionFlags.Aiming, weaponName: "Rifle", spreadDegrees: 0f), Is.True);
            Assert.That(a != Create(PlayerActionFlags.Aiming, shoulder: ShoulderSide.Left, spreadDegrees: 8f), Is.True);
            Assert.That(a != Create(PlayerActionFlags.Aiming, viewMode: ViewMode.ThirdPerson, spreadDegrees: 8f), Is.True);
            Assert.That(a != Create(PlayerActionFlags.Aiming, activeReload: ActiveReloadResult.Fail, spreadDegrees: 8f), Is.True);
        }

        [Test]
        public void ToStringNamesTheFlagsAndValues()
        {
            var state = new PlayerActionState(
                PlayerActionFlags.Grounded | PlayerActionFlags.Reloading,
                0.5f,
                ActiveReloadResult.Success,
                ShoulderSide.Left,
                ViewMode.ThirdPerson,
                "Rifle",
                8f);

            Assert.That(
                state.ToString(),
                Is.EqualTo("flags=Grounded, Reloading reload=0.50 activeReload=Success shoulder=Left view=ThirdPerson weapon=Rifle spread=8.0"));
            Assert.That(default(PlayerActionState).ToString(), Does.Contain("activeReload=-"));
        }

        [Test]
        public void RejectsUndefinedFlagBits()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create((PlayerActionFlags)(1 << 20)));
        }

        [TestCase(-0.01f)]
        [TestCase(1.01f)]
        [TestCase(float.NaN)]
        public void RejectsReloadProgressOutsideZeroToOne(float progress)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(PlayerActionFlags.Reloading, reloadProgress: progress));
        }

        [Test]
        public void RejectsReloadProgressWhileNotReloading()
        {
            Assert.Throws<ArgumentException>(() => Create(reloadProgress: 0.5f));
            Assert.DoesNotThrow(() => Create(PlayerActionFlags.Reloading, reloadProgress: 0.5f));
        }

        [Test]
        public void RejectsUndefinedEnums()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(activeReload: (ActiveReloadResult)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(shoulder: (ShoulderSide)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(viewMode: (ViewMode)99));
        }

        [TestCase(-0.1f)]
        [TestCase(90.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsSpreadOutsideZeroToNinety(float spread)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(spreadDegrees: spread));
        }

        [Test]
        public void SpreadAtTheLimitsIsAccepted()
        {
            Assert.That(Create(spreadDegrees: 0f).SpreadDegrees, Is.Zero);
            Assert.That(Create(spreadDegrees: PlayerActionState.MaxSpreadDegrees).SpreadDegrees, Is.EqualTo(90f));
        }

        [Test]
        public void FiringLastsForTheFireInterval()
        {
            Assert.That(PlayerActionState.IsFiringAfter(0f, 0.5f), Is.True);
            Assert.That(PlayerActionState.IsFiringAfter(0.5f, 0.5f), Is.True);
            Assert.That(PlayerActionState.IsFiringAfter(0.51f, 0.5f), Is.False);
        }

        [Test]
        public void FiringLastsAtLeastTheMinimumEvenForAFastWeapon()
        {
            Assert.That(PlayerActionState.IsFiringAfter(PlayerActionState.MinFiringSeconds, 0f), Is.True);
            Assert.That(PlayerActionState.IsFiringAfter(PlayerActionState.MinFiringSeconds + 0.01f, 0.05f), Is.False);
        }

        [Test]
        public void NeverFiredIsNotFiring()
        {
            Assert.That(PlayerActionState.IsFiringAfter(float.PositiveInfinity, 0.1f), Is.False);
        }

        [TestCase(-0.01f, 0.1f)]
        [TestCase(float.NaN, 0.1f)]
        [TestCase(0f, -0.1f)]
        [TestCase(0f, float.NaN)]
        [TestCase(0f, float.PositiveInfinity)]
        public void IsFiringAfterRejectsInvalidSeconds(float secondsSinceShot, float fireInterval)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PlayerActionState.IsFiringAfter(secondsSinceShot, fireInterval));
        }
    }
}
