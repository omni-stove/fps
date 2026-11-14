using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class PlayerActionStateTests
    {
        private static PlayerActionState Create(
            PlayerActionConditions flags = PlayerActionConditions.None,
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

            Assert.That(state.Flags, Is.EqualTo(PlayerActionConditions.None));
            Assert.That(state.ReloadProgress, Is.Zero);
            Assert.That(state.ActiveReload, Is.Null);
            Assert.That(state.Shoulder, Is.EqualTo(ShoulderSide.Right));
            Assert.That(state.ViewMode, Is.EqualTo(ViewMode.FirstPerson));
            Assert.That(state.WeaponName, Is.Empty, "a default state never hands out a null name");
            Assert.That(state.SpreadDegrees, Is.Zero);
            Assert.That(state, Is.EqualTo(Create(weaponName: null)));
        }

        [TestCase(PlayerActionConditions.Sprinting)]
        [TestCase(PlayerActionConditions.Crouching)]
        [TestCase(PlayerActionConditions.Dodging)]
        [TestCase(PlayerActionConditions.Vaulting)]
        [TestCase(PlayerActionConditions.Grounded)]
        [TestCase(PlayerActionConditions.InCover)]
        [TestCase(PlayerActionConditions.BlindFiring)]
        [TestCase(PlayerActionConditions.Aiming)]
        [TestCase(PlayerActionConditions.Firing)]
        [TestCase(PlayerActionConditions.Reloading)]
        [TestCase(PlayerActionConditions.Downed)]
        [TestCase(PlayerActionConditions.InVehicle)]
        public void EachFlagTurnsOnOnlyItsOwnProperty(PlayerActionConditions flag)
        {
            var state = Create(flag);

            Assert.That(state.IsSprinting, Is.EqualTo(flag == PlayerActionConditions.Sprinting));
            Assert.That(state.IsCrouching, Is.EqualTo(flag == PlayerActionConditions.Crouching));
            Assert.That(state.IsDodging, Is.EqualTo(flag == PlayerActionConditions.Dodging));
            Assert.That(state.IsVaulting, Is.EqualTo(flag == PlayerActionConditions.Vaulting));
            Assert.That(state.IsGrounded, Is.EqualTo(flag == PlayerActionConditions.Grounded));
            Assert.That(state.IsInCover, Is.EqualTo(flag == PlayerActionConditions.InCover));
            Assert.That(state.IsBlindFiring, Is.EqualTo(flag == PlayerActionConditions.BlindFiring));
            Assert.That(state.IsAiming, Is.EqualTo(flag == PlayerActionConditions.Aiming));
            Assert.That(state.IsFiring, Is.EqualTo(flag == PlayerActionConditions.Firing));
            Assert.That(state.IsReloading, Is.EqualTo(flag == PlayerActionConditions.Reloading));
            Assert.That(state.IsDowned, Is.EqualTo(flag == PlayerActionConditions.Downed));
            Assert.That(state.IsInVehicle, Is.EqualTo(flag == PlayerActionConditions.InVehicle));
        }

        [Test]
        public void KeepsEveryValueItWasBuiltWith()
        {
            var flags = PlayerActionConditions.Grounded | PlayerActionConditions.InCover | PlayerActionConditions.Reloading;
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
            Assert.That(state.IsInVehicle, Is.True);
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
            var a = Create(PlayerActionConditions.Aiming, weaponName: "Rifle", spreadDegrees: 8f);
            var b = Create(PlayerActionConditions.Aiming, weaponName: "Rifle", spreadDegrees: 8f);

            Assert.That(a == b, Is.True);
            Assert.That(a.Equals((object)b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));

            Assert.That(a != Create(PlayerActionConditions.Firing, weaponName: "Rifle", spreadDegrees: 8f), Is.True);
            Assert.That(a != Create(PlayerActionConditions.Aiming, weaponName: "Handgun", spreadDegrees: 8f), Is.True);
            Assert.That(a != Create(PlayerActionConditions.Aiming, weaponName: "Rifle", spreadDegrees: 0f), Is.True);
            Assert.That(a != Create(PlayerActionConditions.Aiming, shoulder: ShoulderSide.Left, spreadDegrees: 8f), Is.True);
            Assert.That(a != Create(PlayerActionConditions.Aiming, viewMode: ViewMode.ThirdPerson, spreadDegrees: 8f), Is.True);
            Assert.That(a != Create(PlayerActionConditions.Aiming, activeReload: ActiveReloadResult.Fail, spreadDegrees: 8f), Is.True);
        }

        [Test]
        public void ToStringNamesTheFlagsAndValues()
        {
            var state = new PlayerActionState(
                PlayerActionConditions.Grounded | PlayerActionConditions.Reloading,
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
            Assert.Throws<ArgumentOutOfRangeException>(() => Create((PlayerActionConditions)(1 << 20)));
        }

        [TestCase(-0.01f)]
        [TestCase(1.01f)]
        [TestCase(float.NaN)]
        public void RejectsReloadProgressOutsideZeroToOne(float progress)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(PlayerActionConditions.Reloading, reloadProgress: progress));
        }

        [Test]
        public void RejectsReloadProgressWhileNotReloading()
        {
            Assert.Throws<ArgumentException>(() => Create(reloadProgress: 0.5f));
            Assert.DoesNotThrow(() => Create(PlayerActionConditions.Reloading, reloadProgress: 0.5f));
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
