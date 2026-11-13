using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class WeaponTests
    {
        private const string _rifleId = "00000000-0000-4000-8000-000000000001";

        private static Weapon Create(int magazineSize = 3, int reserveAmmo = 10, float fireInterval = 0.5f, float reloadSeconds = 2f, int damage = 10)
        {
            return new Weapon(new WeaponSpec(_rifleId, null, WeaponCategory.AssaultRifle, FireMode.Auto, magazineSize, reserveAmmo, fireInterval, reloadSeconds, damage, 100f, DamageFalloff.None));
        }

        private static void Empty(Weapon weapon, float fireInterval)
        {
            while (weapon.AmmoInMagazine > 0)
            {
                Assert.That(weapon.TryFire(), Is.True);
                weapon.Tick(fireInterval);
            }
        }

        [Test]
        public void StartsWithFullMagazineAndInitialReserve()
        {
            var weapon = Create(magazineSize: 30, reserveAmmo: 90);

            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(30));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(90));
            Assert.That(weapon.IsReloading, Is.False);
        }

        [Test]
        public void FireConsumesOneRoundAndRaisesFired()
        {
            var weapon = Create();
            var fired = 0;
            weapon.Fired += () => fired++;

            Assert.That(weapon.TryFire(), Is.True);

            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(2));
            Assert.That(fired, Is.EqualTo(1));
        }

        [Test]
        public void CannotFireAgainWithinInterval()
        {
            var weapon = Create(fireInterval: 0.5f);
            var fired = 0;
            weapon.Fired += () => fired++;
            weapon.TryFire();

            weapon.Tick(0.25f);

            Assert.That(weapon.TryFire(), Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(2));
            Assert.That(fired, Is.EqualTo(1));
        }

        [Test]
        public void CanFireExactlyWhenIntervalElapses()
        {
            var weapon = Create(fireInterval: 0.5f);
            weapon.TryFire();

            weapon.Tick(0.25f);
            weapon.Tick(0.25f);

            Assert.That(weapon.TryFire(), Is.True);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(1));
        }

        [Test]
        public void ZeroIntervalAllowsConsecutiveShots()
        {
            var weapon = Create(fireInterval: 0f);

            Assert.That(weapon.TryFire(), Is.True);
            Assert.That(weapon.TryFire(), Is.True);
        }

        [Test]
        public void CannotFireWhenMagazineIsEmpty()
        {
            var weapon = Create(magazineSize: 2, fireInterval: 0.5f);
            Empty(weapon, 0.5f);
            var fired = 0;
            weapon.Fired += () => fired++;

            Assert.That(weapon.TryFire(), Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.Zero);
            Assert.That(fired, Is.Zero);
        }

        [Test]
        public void EmptyMagazineDoesNotReloadAutomatically()
        {
            var weapon = Create(magazineSize: 2, reserveAmmo: 10, fireInterval: 0.5f);
            Empty(weapon, 0.5f);

            weapon.Tick(10f);

            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.Zero);
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(10));
        }

        [Test]
        public void CannotFireWhileReloading()
        {
            var weapon = Create(magazineSize: 3, fireInterval: 0.5f, reloadSeconds: 2f);
            weapon.TryFire();
            weapon.Tick(0.5f);

            Assert.That(weapon.TryStartReload(), Is.True);

            Assert.That(weapon.IsReloading, Is.True);
            Assert.That(weapon.TryFire(), Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(2));
        }

        [Test]
        public void ReloadRefillsAfterReloadSecondsAndRaisesReloaded()
        {
            var weapon = Create(magazineSize: 3, reserveAmmo: 10, fireInterval: 0.5f, reloadSeconds: 2f);
            var reloaded = 0;
            weapon.Reloaded += () => reloaded++;
            Empty(weapon, 0.5f);
            weapon.TryStartReload();

            weapon.Tick(1.5f);
            Assert.That(weapon.IsReloading, Is.True);
            Assert.That(weapon.AmmoInMagazine, Is.Zero);
            Assert.That(reloaded, Is.Zero);

            weapon.Tick(0.5f);
            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(7));
            Assert.That(reloaded, Is.EqualTo(1));
            Assert.That(weapon.TryFire(), Is.True);
        }

        [Test]
        public void ReloadOnlyFillsWhatReserveHas()
        {
            var weapon = Create(magazineSize: 5, reserveAmmo: 2, fireInterval: 0.5f, reloadSeconds: 1f);
            Empty(weapon, 0.5f);

            weapon.TryStartReload();
            weapon.Tick(1f);

            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(2));
            Assert.That(weapon.ReserveAmmo, Is.Zero);
        }

        [Test]
        public void PartialMagazineTakesOnlyMissingRounds()
        {
            var weapon = Create(magazineSize: 5, reserveAmmo: 10, fireInterval: 0.5f, reloadSeconds: 1f);
            weapon.TryFire();
            weapon.Tick(0.5f);
            weapon.TryFire();

            weapon.TryStartReload();
            weapon.Tick(1f);

            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(5));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(8));
        }

        [Test]
        public void DoesNotStartReloadWhenMagazineIsFull()
        {
            var weapon = Create();

            Assert.That(weapon.TryStartReload(), Is.False);
            Assert.That(weapon.IsReloading, Is.False);
        }

        [Test]
        public void DoesNotStartReloadWithoutReserve()
        {
            var weapon = Create(reserveAmmo: 0);
            weapon.TryFire();

            Assert.That(weapon.TryStartReload(), Is.False);
            Assert.That(weapon.IsReloading, Is.False);
        }

        [Test]
        public void DoesNotRestartReloadWhileReloading()
        {
            var weapon = Create(reloadSeconds: 2f);
            weapon.TryFire();
            weapon.TryStartReload();
            weapon.Tick(1f);

            Assert.That(weapon.TryStartReload(), Is.False);

            weapon.Tick(1f);
            Assert.That(weapon.IsReloading, Is.False);
        }

        [Test]
        public void ZeroReloadSecondsCompletesOnNextTick()
        {
            var weapon = Create(reloadSeconds: 0f);
            weapon.TryFire();
            weapon.TryStartReload();

            weapon.Tick(0f);

            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
        }

        [Test]
        public void CancelReloadStopsWithoutChangingAmmo()
        {
            var weapon = Create(magazineSize: 3, reserveAmmo: 10, reloadSeconds: 2f);
            var reloaded = 0;
            weapon.Reloaded += () => reloaded++;
            weapon.TryFire();
            weapon.TryStartReload();
            weapon.Tick(1.5f);

            weapon.CancelReload();

            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(2));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(10));

            // 途中まで進んだ時間は残らない
            weapon.Tick(5f);
            Assert.That(reloaded, Is.Zero);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(2));
            Assert.That(weapon.TryFire(), Is.True);
        }

        [Test]
        public void ReloadAfterCancelTakesTheFullTime()
        {
            var weapon = Create(magazineSize: 3, reserveAmmo: 10, reloadSeconds: 2f);
            weapon.TryFire();
            weapon.TryStartReload();
            weapon.Tick(1.5f);
            weapon.CancelReload();

            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(1.5f);
            Assert.That(weapon.IsReloading, Is.True);

            weapon.Tick(0.5f);
            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(9));
        }

        [Test]
        public void CancelReloadWhenNotReloadingDoesNothing()
        {
            var weapon = Create(fireInterval: 0.5f);
            weapon.TryFire();

            weapon.CancelReload();

            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(2));
            Assert.That(weapon.TryFire(), Is.False, "the fire interval is not reset");
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsInvalidDelta(float deltaSeconds)
        {
            var weapon = Create();

            Assert.Throws<ArgumentOutOfRangeException>(() => weapon.Tick(deltaSeconds));
        }

        [Test]
        public void RejectsNullSpec()
        {
            Assert.Throws<ArgumentNullException>(() => new Weapon(null));
        }

        [Test]
        public void StartsWithTheGivenAmmo()
        {
            var spec = new WeaponSpec(_rifleId, null, WeaponCategory.AssaultRifle, FireMode.Auto, 5, 10, 0.5f, 1f, 10, 100f, DamageFalloff.None);

            var weapon = new Weapon(spec, 2, 7);

            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(2));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(7));
            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.TryFire(), Is.True);

            weapon.Tick(0.5f);
            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(1f);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(5));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(3));
        }

        [Test]
        public void AcceptsEmptyAndFullAmmo()
        {
            var spec = new WeaponSpec(_rifleId, null, WeaponCategory.AssaultRifle, FireMode.Auto, 5, 10, 0.5f, 1f, 10, 100f, DamageFalloff.None);

            Assert.That(new Weapon(spec, 0, 0).AmmoInMagazine, Is.Zero);
            Assert.That(new Weapon(spec, 5, 0).AmmoInMagazine, Is.EqualTo(5));
        }

        [TestCase(-1, 0)]
        [TestCase(6, 0)]
        [TestCase(0, -1)]
        public void RejectsAmmoOutOfRange(int ammoInMagazine, int reserveAmmo)
        {
            var spec = new WeaponSpec(_rifleId, null, WeaponCategory.AssaultRifle, FireMode.Auto, 5, 10, 0.5f, 1f, 10, 100f, DamageFalloff.None);

            Assert.Throws<ArgumentOutOfRangeException>(() => new Weapon(spec, ammoInMagazine, reserveAmmo));
        }

        [Test]
        public void RejectsNullSpecWithAmmo()
        {
            Assert.Throws<ArgumentNullException>(() => new Weapon(null, 0, 0));
        }

        private static GameRules Rules(bool infiniteReserve, float reserveAmmoMultiplier)
        {
            var d = GameRules.Default;
            return new GameRules(
                d.FriendlyFire,
                d.RespawnMode,
                d.WaveIntervalSeconds,
                d.RegenDelaySeconds,
                d.RegenPerSecond,
                d.DeathBody,
                d.DeathCamera,
                infiniteReserve,
                reserveAmmoMultiplier,
                d.Destruction);
        }

        private static WeaponSpec Spec(int magazineSize = 3, int reserveAmmo = 10, float reloadSeconds = 1f)
        {
            return new WeaponSpec(_rifleId, null, WeaponCategory.AssaultRifle, FireMode.Auto, magazineSize, reserveAmmo, 0f, reloadSeconds, 10, 100f, DamageFalloff.None);
        }

        [TestCase(10, 0f, 0)]
        [TestCase(10, 0.5f, 5)]
        [TestCase(10, 1f, 10)]
        [TestCase(10, 2f, 20)]
        [TestCase(5, 0.5f, 3)]
        [TestCase(5, 0.7f, 4)]
        [TestCase(10, 0.33f, 3)]
        [TestCase(10, 0.37f, 4)]
        [TestCase(0, 2f, 0)]
        public void StartsWithReserveScaledByTheMultiplier(int specReserve, float multiplier, int expected)
        {
            var weapon = new Weapon(Spec(reserveAmmo: specReserve), Rules(false, multiplier));

            Assert.That(weapon.ReserveAmmo, Is.EqualTo(expected));
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
            Assert.That(weapon.HasInfiniteReserve, Is.False);
        }

        [Test]
        public void DefaultRulesKeepTheSpecReserveAndFiniteReserve()
        {
            var weapon = new Weapon(Spec(reserveAmmo: 10), GameRules.Default);

            Assert.That(weapon.ReserveAmmo, Is.EqualTo(10));
            Assert.That(weapon.HasInfiniteReserve, Is.False);
            Assert.That(Create().HasInfiniteReserve, Is.False);
        }

        [Test]
        public void ScaledReserveIsClampedToIntMaxValue()
        {
            Assert.That(Weapon.ScaleReserveAmmo(int.MaxValue, 2f), Is.EqualTo(int.MaxValue));
            Assert.That(Weapon.ScaleReserveAmmo(1, float.MaxValue), Is.EqualTo(int.MaxValue));
        }

        [TestCase(-1, 1f)]
        [TestCase(10, -0.1f)]
        [TestCase(10, float.NaN)]
        [TestCase(10, float.PositiveInfinity)]
        public void ScaleReserveAmmoRejectsInvalidArguments(int reserveAmmo, float multiplier)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Weapon.ScaleReserveAmmo(reserveAmmo, multiplier));
        }

        [Test]
        public void RejectsNullRules()
        {
            Assert.Throws<ArgumentNullException>(() => new Weapon(Spec(), null));
            Assert.Throws<ArgumentNullException>(() => new Weapon(Spec(), 0, 0, null));
        }

        [Test]
        public void InfiniteReserveRefillsWithoutConsumingReserve()
        {
            var weapon = new Weapon(Spec(magazineSize: 3, reserveAmmo: 10), Rules(true, 1f));
            var reloaded = 0;
            weapon.Reloaded += () => reloaded++;
            Empty(weapon, 0f);

            Assert.That(weapon.HasInfiniteReserve, Is.True);
            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(1f);

            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(10));
            Assert.That(reloaded, Is.EqualTo(1));
        }

        [Test]
        public void InfiniteReserveReloadsEvenWithZeroReserve()
        {
            var weapon = new Weapon(Spec(magazineSize: 3, reserveAmmo: 10), Rules(true, 0f));
            Assert.That(weapon.ReserveAmmo, Is.Zero);

            for (var i = 0; i < 3; i++)
            {
                Empty(weapon, 0f);
                Assert.That(weapon.TryStartReload(), Is.True);
                weapon.Tick(1f);
                Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
                Assert.That(weapon.ReserveAmmo, Is.Zero);
            }
        }

        [Test]
        public void InfiniteReserveStillRefusesReloadWhenFull()
        {
            var weapon = new Weapon(Spec(), Rules(true, 1f));

            Assert.That(weapon.TryStartReload(), Is.False);
        }

        [Test]
        public void RestoringFromSaveDoesNotReapplyTheMultiplier()
        {
            var weapon = new Weapon(Spec(magazineSize: 5, reserveAmmo: 10), 2, 7, Rules(false, 2f));

            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(2));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(7));
            Assert.That(weapon.HasInfiniteReserve, Is.False);

            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(1f);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(5));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(4));
        }

        [Test]
        public void RestoringFromSaveKeepsInfiniteReserve()
        {
            var weapon = new Weapon(Spec(magazineSize: 5, reserveAmmo: 10), 0, 0, Rules(true, 2f));

            Assert.That(weapon.ReserveAmmo, Is.Zero);
            Assert.That(weapon.HasInfiniteReserve, Is.True);
            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(1f);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(5));
            Assert.That(weapon.ReserveAmmo, Is.Zero);
        }

        [Test]
        public void RestoringFromSaveStillValidatesAmmoWithRules()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Weapon(Spec(magazineSize: 5), 6, 0, Rules(true, 1f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Weapon(Spec(magazineSize: 5), 0, -1, Rules(true, 1f)));
        }

        private static GameRules ActiveReloadRules(ActiveReload activeReload, bool infiniteReserve = false)
        {
            var d = GameRules.Default;
            var a = ActionRules.Default;
            return new GameRules(
                d.FriendlyFire,
                d.RespawnMode,
                d.WaveIntervalSeconds,
                d.RegenDelaySeconds,
                d.RegenPerSecond,
                d.DeathBody,
                d.DeathCamera,
                infiniteReserve,
                1f,
                d.Destruction,
                new ActionRules(a.Downed, a.DownedBleedoutSeconds, a.DownedHealthFraction, a.ReviveSeconds, a.Cover, a.BlindFire, activeReload));
        }

        // リロード 2 秒、成功の幅は 1.0〜1.5 秒 (進み 0.5〜0.75)、失敗で 0.5 秒遅れる。どれも 2 進で割り切れる値にしてある。
        private static Weapon ActiveReloadWeapon(ActiveReload activeReload = ActiveReload.On, bool infiniteReserve = false)
        {
            var spec = new WeaponSpec(
                _rifleId, null, WeaponCategory.AssaultRifle, FireMode.Auto, 3, 10, 0f, 2f, 10, 100f, DamageFalloff.None,
                activeReloadWindowStart: 0.5f,
                activeReloadWindowLength: 0.25f,
                activeReloadFailSeconds: 0.5f);
            var weapon = new Weapon(spec, ActiveReloadRules(activeReload, infiniteReserve));
            Empty(weapon, 0f);
            return weapon;
        }

        [Test]
        public void ReloadProgressIsTheElapsedShareOfTheReload()
        {
            var weapon = ActiveReloadWeapon();
            Assert.That(weapon.ReloadProgress, Is.Zero, "not reloading");

            Assert.That(weapon.TryStartReload(), Is.True);
            Assert.That(weapon.ReloadProgress, Is.Zero);

            weapon.Tick(0.5f);
            Assert.That(weapon.ReloadProgress, Is.EqualTo(0.25f));

            weapon.Tick(1f);
            Assert.That(weapon.ReloadProgress, Is.EqualTo(0.75f));

            weapon.Tick(0.5f);
            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.ReloadProgress, Is.Zero, "back to 0 once the reload is over");
        }

        [Test]
        public void TheActiveReloadRuleIsTakenFromTheRules()
        {
            Assert.That(ActiveReloadWeapon(ActiveReload.On).HasActiveReload, Is.True);
            Assert.That(ActiveReloadWeapon(ActiveReload.Off).HasActiveReload, Is.False);
            Assert.That(Create().HasActiveReload, Is.False, "the default rules keep it off");
            Assert.That(new Weapon(Spec(), 1, 0, ActiveReloadRules(ActiveReload.On)).HasActiveReload, Is.True, "restoring from a save keeps the rule");
        }

        [TestCase(1f)]
        [TestCase(1.25f)]
        [TestCase(1.5f)]
        public void ASuccessRefillsAtOnceAndEndsTheReload(float elapsed)
        {
            var weapon = ActiveReloadWeapon();
            var reloaded = 0;
            weapon.Reloaded += () => reloaded++;
            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(elapsed);

            Assert.That(weapon.TryActiveReload(out var result), Is.True);

            Assert.That(result, Is.EqualTo(ActiveReloadResult.Success));
            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(7));
            Assert.That(weapon.ReloadProgress, Is.Zero);
            Assert.That(weapon.HasJudgedActiveReload, Is.False, "the judgement belongs to the finished reload");
            Assert.That(reloaded, Is.EqualTo(1));

            weapon.Tick(5f);
            Assert.That(reloaded, Is.EqualTo(1), "the finished reload does not refill again");
            Assert.That(weapon.TryFire(), Is.True, "the weapon can fire right after the success");
        }

        [TestCase(0.5f)]
        [TestCase(1.75f)]
        public void AFailDelaysTheReloadByTheFailSeconds(float elapsed)
        {
            var weapon = ActiveReloadWeapon();
            var reloaded = 0;
            weapon.Reloaded += () => reloaded++;
            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(elapsed);

            Assert.That(weapon.TryActiveReload(out var result), Is.True);

            Assert.That(result, Is.EqualTo(ActiveReloadResult.Fail));
            Assert.That(weapon.IsReloading, Is.True);
            Assert.That(weapon.AmmoInMagazine, Is.Zero);
            Assert.That(weapon.HasJudgedActiveReload, Is.True);
            Assert.That(weapon.ReloadProgress, Is.EqualTo(elapsed / 2.5f), "the reload now takes 2 + 0.5 seconds");

            weapon.Tick(2f - elapsed);
            Assert.That(weapon.IsReloading, Is.True, "the usual reload time is no longer enough");
            Assert.That(reloaded, Is.Zero);

            weapon.Tick(0.5f);
            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(7));
            Assert.That(reloaded, Is.EqualTo(1));
        }

        [Test]
        public void OnlyTheFirstPressOfAReloadIsJudged()
        {
            var weapon = ActiveReloadWeapon();
            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(0.25f);
            Assert.That(weapon.TryActiveReload(out var first), Is.True);
            Assert.That(first, Is.EqualTo(ActiveReloadResult.Fail));

            weapon.Tick(1f);
            Assert.That(weapon.TryActiveReload(out var second), Is.False, "a second press is ignored even inside the window");
            Assert.That(second, Is.EqualTo(ActiveReloadResult.Fail));
            Assert.That(weapon.IsReloading, Is.True);
            Assert.That(weapon.AmmoInMagazine, Is.Zero);
        }

        [Test]
        public void ANewReloadCanBeJudgedAgain()
        {
            var weapon = ActiveReloadWeapon();
            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(0.25f);
            Assert.That(weapon.TryActiveReload(out _), Is.True);
            weapon.CancelReload();
            Assert.That(weapon.HasJudgedActiveReload, Is.False);

            Assert.That(weapon.TryStartReload(), Is.True);
            Assert.That(weapon.HasJudgedActiveReload, Is.False);
            weapon.Tick(1.25f);
            Assert.That(weapon.ReloadProgress, Is.EqualTo(0.625f), "the cancelled reload's time and delay are gone");
            Assert.That(weapon.TryActiveReload(out var result), Is.True);
            Assert.That(result, Is.EqualTo(ActiveReloadResult.Success));
        }

        [Test]
        public void TheOffRuleNeverJudgesAndKeepsTheUsualReload()
        {
            var weapon = ActiveReloadWeapon(ActiveReload.Off);
            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(1.25f);

            Assert.That(weapon.TryActiveReload(out _), Is.False);
            Assert.That(weapon.IsReloading, Is.True);
            Assert.That(weapon.HasJudgedActiveReload, Is.False);

            weapon.Tick(0.75f);
            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
        }

        [Test]
        public void NothingIsJudgedWhileNotReloading()
        {
            var weapon = ActiveReloadWeapon();
            var reloaded = 0;
            weapon.Reloaded += () => reloaded++;

            Assert.That(weapon.TryActiveReload(out _), Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.Zero);
            Assert.That(weapon.HasJudgedActiveReload, Is.False);
            Assert.That(reloaded, Is.Zero);
        }

        [Test]
        public void ASuccessWithAnInfiniteReserveRefillsWithoutConsumingIt()
        {
            var weapon = ActiveReloadWeapon(infiniteReserve: true);
            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(1.25f);

            Assert.That(weapon.TryActiveReload(out var result), Is.True);

            Assert.That(result, Is.EqualTo(ActiveReloadResult.Success));
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(10));
            Assert.That(weapon.IsReloading, Is.False);
        }

        [TestCase(0, 10, 0.5f, 2f, 10)]
        [TestCase(-1, 10, 0.5f, 2f, 10)]
        [TestCase(3, -1, 0.5f, 2f, 10)]
        [TestCase(3, 10, -0.1f, 2f, 10)]
        [TestCase(3, 10, float.NaN, 2f, 10)]
        [TestCase(3, 10, float.PositiveInfinity, 2f, 10)]
        [TestCase(3, 10, 0.5f, -0.1f, 10)]
        [TestCase(3, 10, 0.5f, float.NaN, 10)]
        [TestCase(3, 10, 0.5f, float.PositiveInfinity, 10)]
        [TestCase(3, 10, 0.5f, 2f, -1)]
        public void RejectsInvalidSpec(int magazineSize, int reserveAmmo, float fireInterval, float reloadSeconds, int damage)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new WeaponSpec(_rifleId, null, WeaponCategory.AssaultRifle, FireMode.Auto, magazineSize, reserveAmmo, fireInterval, reloadSeconds, damage, 100f, DamageFalloff.None));
        }
    }
}
