using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ServerWeaponTests
    {
        private const string _rifleId = "00000000-0000-4000-8000-000000000001";

        private static WeaponSpec Spec(int magazine = 3, int reserve = 5, float fireInterval = 0.1f, float reloadSeconds = 2f)
        {
            return new WeaponSpec(_rifleId, null, WeaponCategory.AssaultRifle, FireMode.Auto, magazine, reserve, fireInterval, reloadSeconds, 25, 100f, DamageFalloff.None);
        }

        [Test]
        public void StartsWithFullMagazineAndReserve()
        {
            var weapon = new ServerWeapon(Spec());

            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(5));
            Assert.That(weapon.IsReloading, Is.False);
        }

        [Test]
        public void RejectsNullSpec()
        {
            Assert.Throws<ArgumentNullException>(() => new ServerWeapon(null));
        }

        [Test]
        public void DoesNotLimitTheFireIntervalItself()
        {
            // 連射間隔は ShotBudget が見るので、同じフレームに届いた射撃も弾がある限り撃てる
            var weapon = new ServerWeapon(Spec(fireInterval: 1f));

            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.None));
            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.None));
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(1));
            Assert.That(weapon.Spec.FireInterval, Is.EqualTo(1f));
        }

        [Test]
        public void EmptyMagazineRejectsAndStartsReloadAutomatically()
        {
            var weapon = new ServerWeapon(Spec());
            bool? automatic = null;
            weapon.ReloadStarted += a => automatic = a;
            for (var i = 0; i < 3; i++)
            {
                Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.None));
            }

            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.OutOfAmmo));
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(0));
            Assert.That(weapon.IsReloading, Is.True);
            Assert.That(automatic, Is.True);

            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.Reloading));
        }

        [Test]
        public void EmptyMagazineWithoutReserveStaysEmpty()
        {
            var weapon = new ServerWeapon(Spec(magazine: 1, reserve: 0));
            weapon.TryFire();

            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.OutOfAmmo));
            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.TryStartReload(), Is.False);
        }

        [Test]
        public void CanFireAgainAfterTheReloadFinishes()
        {
            var weapon = new ServerWeapon(Spec());
            var reloaded = 0;
            weapon.Reloaded += () => reloaded++;
            weapon.TryFire();
            weapon.TryFire();

            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(1.5f);
            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.Reloading));
            Assert.That(reloaded, Is.EqualTo(0));

            weapon.Tick(0.5f);
            Assert.That(reloaded, Is.EqualTo(1));
            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(3));
            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.None));
        }

        [Test]
        public void ManualReloadIsNotAutomaticAndRefusedWhenFull()
        {
            var weapon = new ServerWeapon(Spec());
            bool? automatic = null;
            weapon.ReloadStarted += a => automatic = a;

            Assert.That(weapon.TryStartReload(), Is.False);
            Assert.That(automatic, Is.Null);

            weapon.TryFire();
            Assert.That(weapon.TryStartReload(), Is.True);
            Assert.That(automatic, Is.False);
            Assert.That(weapon.TryStartReload(), Is.False);
        }

        [Test]
        public void RefillRestoresFullAmmoAndCancelsTheReload()
        {
            var weapon = new ServerWeapon(Spec());
            var reloaded = 0;
            weapon.Reloaded += () => reloaded++;
            weapon.TryFire();
            weapon.TryFire();
            weapon.TryFire();
            weapon.TryFire();
            Assert.That(weapon.IsReloading, Is.True);

            weapon.Refill();

            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(5));
            Assert.That(weapon.IsReloading, Is.False);

            // 捨てたリロードが後から完了しない
            weapon.Tick(5f);
            Assert.That(reloaded, Is.EqualTo(0));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(5));
        }

        [Test]
        public void ReloadingAfterRefillStillNotifies()
        {
            var weapon = new ServerWeapon(Spec());
            var reloaded = 0;
            weapon.Reloaded += () => reloaded++;
            weapon.Refill();
            weapon.TryFire();
            weapon.TryStartReload();

            weapon.Tick(2f);

            Assert.That(reloaded, Is.EqualTo(1));
        }

        [Test]
        public void ChangedFiresOnEveryStateChange()
        {
            var weapon = new ServerWeapon(Spec());
            var changes = 0;
            weapon.Changed += () => changes++;

            weapon.TryFire();
            Assert.That(changes, Is.EqualTo(1), "fire");

            weapon.TryStartReload();
            Assert.That(changes, Is.EqualTo(2), "reload start");

            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.Reloading));
            Assert.That(changes, Is.EqualTo(2), "a rejected shot changes nothing");

            weapon.Tick(2f);
            Assert.That(changes, Is.EqualTo(3), "reload end");

            weapon.Refill();
            Assert.That(changes, Is.EqualTo(4), "refill");
        }

        [Test]
        public void CancelReloadStopsWithoutChangingAmmoAndNotifies()
        {
            var weapon = new ServerWeapon(Spec());
            var reloaded = 0;
            weapon.Reloaded += () => reloaded++;
            weapon.TryFire();
            weapon.TryStartReload();
            weapon.Tick(1f);
            var changes = 0;
            weapon.Changed += () => changes++;

            weapon.CancelReload();

            Assert.That(changes, Is.EqualTo(1));
            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(2));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(5));
            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.None));

            weapon.Tick(5f);
            Assert.That(reloaded, Is.EqualTo(0));
        }

        [Test]
        public void CancelReloadWhenNotReloadingDoesNotNotify()
        {
            var weapon = new ServerWeapon(Spec());
            var changes = 0;
            weapon.Changed += () => changes++;

            weapon.CancelReload();

            Assert.That(changes, Is.EqualTo(0));
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
        }

        [Test]
        public void TickRejectsInvalidDelta()
        {
            var weapon = new ServerWeapon(Spec());

            Assert.Throws<ArgumentOutOfRangeException>(() => weapon.Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => weapon.Tick(float.NaN));
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

        [Test]
        public void RejectsNullRules()
        {
            Assert.Throws<ArgumentNullException>(() => new ServerWeapon(Spec(), null));
        }

        [Test]
        public void DefaultRulesKeepTheSpecReserve()
        {
            var weapon = new ServerWeapon(Spec(reserve: 5), GameRules.Default);

            Assert.That(weapon.ReserveAmmo, Is.EqualTo(5));
            Assert.That(weapon.HasInfiniteReserve, Is.False);
            Assert.That(new ServerWeapon(Spec()).HasInfiniteReserve, Is.False);
        }

        [TestCase(10, 0f, 0)]
        [TestCase(10, 0.5f, 5)]
        [TestCase(5, 0.5f, 3)]
        [TestCase(10, 2f, 20)]
        public void StartsWithReserveScaledByTheMultiplier(int specReserve, float multiplier, int expected)
        {
            var weapon = new ServerWeapon(Spec(reserve: specReserve), Rules(false, multiplier));

            Assert.That(weapon.ReserveAmmo, Is.EqualTo(expected));
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
        }

        [Test]
        public void RefillRestoresTheScaledReserve()
        {
            var weapon = new ServerWeapon(Spec(magazine: 3, reserve: 5), Rules(false, 2f));
            weapon.TryFire();
            weapon.TryStartReload();
            weapon.Tick(2f);
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(9));

            weapon.Refill();

            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(10));
            Assert.That(weapon.HasInfiniteReserve, Is.False);
        }

        [Test]
        public void InfiniteReserveRefillsWithoutConsumingReserve()
        {
            var weapon = new ServerWeapon(Spec(magazine: 3, reserve: 5), Rules(true, 1f));
            var reloaded = 0;
            weapon.Reloaded += () => reloaded++;
            for (var i = 0; i < 3; i++)
            {
                weapon.TryFire();
            }

            Assert.That(weapon.HasInfiniteReserve, Is.True);
            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(2f);

            Assert.That(reloaded, Is.EqualTo(1));
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(5));
        }

        [Test]
        public void InfiniteReserveWithZeroReserveStillReloadsAutomatically()
        {
            var weapon = new ServerWeapon(Spec(magazine: 1, reserve: 0), Rules(true, 1f));
            bool? automatic = null;
            weapon.ReloadStarted += a => automatic = a;
            weapon.TryFire();

            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.OutOfAmmo));
            Assert.That(weapon.IsReloading, Is.True);
            Assert.That(automatic, Is.True);

            weapon.Tick(2f);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(1));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(0));
            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.None));
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

        // WeaponTests の ActiveReloadWeapon と同じく、リロード 2 秒、成功の幅は 1.0〜1.5 秒 (進み 0.5〜0.75)、失敗で 0.5 秒遅れる。
        // 連射間隔を 0 でない値にして、内側の Weapon を作り直す経路でも判定の値が引き継がれることを確かめる
        private static ServerWeapon ActiveReloadWeapon(ActiveReload activeReload = ActiveReload.On, bool infiniteReserve = false)
        {
            var spec = new WeaponSpec(
                _rifleId, null, WeaponCategory.AssaultRifle, FireMode.Auto, 3, 10, 0.1f, 2f, 25, 100f, DamageFalloff.None,
                activeReloadWindowStart: 0.5f,
                activeReloadWindowLength: 0.25f,
                activeReloadFailSeconds: 0.5f);
            var weapon = new ServerWeapon(spec, ActiveReloadRules(activeReload, infiniteReserve));
            for (var i = 0; i < 3; i++)
            {
                Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.None));
            }

            return weapon;
        }

        [Test]
        public void ActiveReloadRuleIsTakenFromTheRules()
        {
            Assert.That(ActiveReloadWeapon(ActiveReload.On).HasActiveReload, Is.True);
            Assert.That(ActiveReloadWeapon(ActiveReload.Off).HasActiveReload, Is.False);
            Assert.That(new ServerWeapon(Spec()).HasActiveReload, Is.False, "the default rules keep it off");
        }

        [Test]
        public void ReloadProgressAndDurationFollowTheReload()
        {
            var weapon = ActiveReloadWeapon();
            Assert.That(weapon.ReloadProgress, Is.Zero, "not reloading");
            Assert.That(weapon.ReloadDurationSeconds, Is.Zero, "not reloading");

            Assert.That(weapon.TryStartReload(), Is.True);
            Assert.That(weapon.ReloadProgress, Is.Zero);
            Assert.That(weapon.ReloadDurationSeconds, Is.EqualTo(2f));

            weapon.Tick(0.5f);
            Assert.That(weapon.ReloadProgress, Is.EqualTo(0.25f));

            weapon.Tick(1.5f);
            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.ReloadProgress, Is.Zero, "back to 0 once the reload is over");
            Assert.That(weapon.ReloadDurationSeconds, Is.Zero);
        }

        [TestCase(1f)]
        [TestCase(1.25f)]
        [TestCase(1.5f)]
        public void ActiveReloadSuccessRefillsAtOnce(float elapsed)
        {
            var weapon = ActiveReloadWeapon();
            var reloaded = 0;
            weapon.Reloaded += () => reloaded++;
            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(elapsed);
            var changes = 0;
            weapon.Changed += () => changes++;

            Assert.That(weapon.TryActiveReload(out var result), Is.True);

            Assert.That(result, Is.EqualTo(ActiveReloadResult.Success));
            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(7));
            Assert.That(weapon.HasJudgedActiveReload, Is.False, "the judgement belongs to the finished reload");
            Assert.That(reloaded, Is.EqualTo(1));
            Assert.That(changes, Is.EqualTo(1));

            weapon.Tick(5f);
            Assert.That(reloaded, Is.EqualTo(1), "the finished reload does not refill again");
            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.None), "the weapon can fire right after the success");
        }

        [TestCase(0.5f)]
        [TestCase(1.75f)]
        public void ActiveReloadFailDelaysTheReload(float elapsed)
        {
            var weapon = ActiveReloadWeapon();
            var reloaded = 0;
            weapon.Reloaded += () => reloaded++;
            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(elapsed);
            var changes = 0;
            weapon.Changed += () => changes++;

            Assert.That(weapon.TryActiveReload(out var result), Is.True);

            Assert.That(result, Is.EqualTo(ActiveReloadResult.Fail));
            Assert.That(changes, Is.EqualTo(1), "the longer reload has to be synced");
            Assert.That(weapon.IsReloading, Is.True);
            Assert.That(weapon.AmmoInMagazine, Is.Zero);
            Assert.That(weapon.HasJudgedActiveReload, Is.True);
            Assert.That(weapon.ReloadDurationSeconds, Is.EqualTo(2.5f));
            Assert.That(weapon.ReloadProgress, Is.EqualTo(elapsed / 2.5f), "the reload now takes 2 + 0.5 seconds");
            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.Reloading));

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
        public void ActiveReloadIsJudgedOncePerReload()
        {
            var weapon = ActiveReloadWeapon();
            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(0.25f);
            Assert.That(weapon.TryActiveReload(out var first), Is.True);
            Assert.That(first, Is.EqualTo(ActiveReloadResult.Fail));

            weapon.Tick(1f);
            var changes = 0;
            weapon.Changed += () => changes++;
            Assert.That(weapon.TryActiveReload(out _), Is.False, "a second press is ignored even inside the window");
            Assert.That(changes, Is.Zero);
            Assert.That(weapon.IsReloading, Is.True);
            Assert.That(weapon.AmmoInMagazine, Is.Zero);
        }

        [Test]
        public void ActiveReloadCanBeJudgedAgainAfterACancelOrRefill()
        {
            var weapon = ActiveReloadWeapon();
            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(0.25f);
            Assert.That(weapon.TryActiveReload(out _), Is.True);
            weapon.CancelReload();
            Assert.That(weapon.HasJudgedActiveReload, Is.False);
            Assert.That(weapon.ReloadDurationSeconds, Is.Zero);

            Assert.That(weapon.TryStartReload(), Is.True);
            Assert.That(weapon.ReloadDurationSeconds, Is.EqualTo(2f), "the cancelled reload's delay is gone");
            weapon.Tick(1.25f);
            Assert.That(weapon.TryActiveReload(out var result), Is.True);
            Assert.That(result, Is.EqualTo(ActiveReloadResult.Success));

            weapon.TryFire();
            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(0.25f);
            Assert.That(weapon.TryActiveReload(out _), Is.True);
            weapon.Refill();
            Assert.That(weapon.HasActiveReload, Is.True, "the refilled weapon keeps the rule");
            Assert.That(weapon.HasJudgedActiveReload, Is.False);
        }

        [Test]
        public void ActiveReloadOffRuleNeverJudges()
        {
            var weapon = ActiveReloadWeapon(ActiveReload.Off);
            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(1.25f);
            var changes = 0;
            weapon.Changed += () => changes++;

            Assert.That(weapon.TryActiveReload(out _), Is.False);
            Assert.That(changes, Is.Zero);
            Assert.That(weapon.IsReloading, Is.True);
            Assert.That(weapon.HasJudgedActiveReload, Is.False);
            Assert.That(weapon.ReloadDurationSeconds, Is.EqualTo(2f));

            weapon.Tick(0.75f);
            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
        }

        [Test]
        public void ActiveReloadDoesNothingWhileNotReloading()
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
        public void ActiveReloadSuccessWithInfiniteReserveKeepsTheReserve()
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

        [Test]
        public void RefillKeepsInfiniteReserve()
        {
            var weapon = new ServerWeapon(Spec(magazine: 1, reserve: 0), Rules(true, 1f));

            weapon.Refill();
            weapon.TryFire();

            Assert.That(weapon.HasInfiniteReserve, Is.True);
            Assert.That(weapon.TryStartReload(), Is.True);
        }
    }
}
