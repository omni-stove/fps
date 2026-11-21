using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class HudModelTests
    {
        private const int _playerId = 0;
        private const string _rifleId = "00000000-0000-4000-8000-000000000001";

        private const string _handgunId = "00000000-0000-4000-8000-000000000002";

        private static Weapon NewWeapon(int magazine = 30, int reserve = 90, float fireInterval = 0f, float reloadSeconds = 1f)
        {
            return new Weapon(new WeaponSpec(_rifleId, "weapon.rifle", WeaponCategory.AssaultRifle, FireMode.Auto, magazine, reserve, fireInterval, reloadSeconds, 25, 100f, DamageFalloff.None));
        }

        private static Weapon NewHandgun()
        {
            return new Weapon(new WeaponSpec(_handgunId, "weapon.handgun", WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f, DamageFalloff.None));
        }

        private static List<HudField> Record(HudModel model)
        {
            var changes = new List<HudField>();
            model.Changed += changes.Add;
            return changes;
        }

        [Test]
        public void StartsWithPlaceholders()
        {
            var model = new HudModel();

            Assert.That(model.CurrentHealth, Is.Null);
            Assert.That(model.MagazineAmmo, Is.Null);
            Assert.That(model.ReserveAmmo, Is.Null);
            Assert.That(model.WeaponSpec, Is.Null);
            Assert.That(model.RemainingSeconds, Is.Null);
            Assert.That(model.MaxHealth, Is.Null);
            Assert.That(model.IsReloading, Is.False);
            Assert.That(model.IsReserveInfinite, Is.False);
            Assert.That(model.ShowsReserveAmmo, Is.True);
        }

        [Test]
        public void SetHealthKeepsTheCurrentAndTheMaximum()
        {
            var model = new HudModel();

            model.SetHealth(30, 120);
            Assert.That(model.CurrentHealth, Is.EqualTo(30));
            Assert.That(model.MaxHealth, Is.EqualTo(120));

            model.ClearHealth();
            Assert.That(model.CurrentHealth, Is.Null);
            Assert.That(model.MaxHealth, Is.Null);
        }

        [Test]
        public void TheReserveNotifiesWhileItIsHiddenSoTheHudCanShowItAgainAtOnce()
        {
            var model = new HudModel();
            model.SetReserveAmmoShown(false);
            model.SetAmmo(5, 90, false);
            var changes = Record(model);

            model.SetAmmo(5, 60, false);

            Assert.That(model.ReserveAmmo, Is.EqualTo(60));
            Assert.That(changes, Is.EqualTo(new[] { HudField.Ammo }));
        }

        [Test]
        public void SetAmmoDoesNotNotifyWhenNothingChanges()
        {
            var model = new HudModel();
            model.SetAmmo(5, 90, true, true);
            var changes = Record(model);

            model.SetAmmo(5, 90, true, true);

            Assert.That(changes, Is.Empty);
        }

        [Test]
        public void BindingShowsTheCurrentValuesAtOnce()
        {
            var model = new HudModel();
            var changes = Record(model);

            model.BindHealth(new Health(100));
            model.BindWeapon(NewWeapon());
            model.BindMatch(new Match(180f, 1, _playerId));

            Assert.That(model.CurrentHealth, Is.EqualTo(100));
            Assert.That(model.MagazineAmmo, Is.EqualTo(30));
            Assert.That(model.ReserveAmmo, Is.EqualTo(90));
            Assert.That(model.WeaponSpec.Id, Is.EqualTo(_rifleId));
            Assert.That(model.RemainingSeconds, Is.EqualTo(180));
            Assert.That(changes, Is.EqualTo(new[] { HudField.Health, HudField.Ammo, HudField.WeaponName, HudField.Time }));
        }

        [Test]
        public void WeaponNameStartsEmptyAndFollowsTheBoundWeapon()
        {
            var model = new HudModel();
            Assert.That(model.WeaponSpec, Is.Null);
            var changes = Record(model);

            model.BindWeapon(NewWeapon());
            Assert.That(model.WeaponSpec.Id, Is.EqualTo(_rifleId));

            model.BindWeapon(NewHandgun());
            Assert.That(model.WeaponSpec.Id, Is.EqualTo(_handgunId), "switching weapons rebinds and shows the new name");
            Assert.That(model.MagazineAmmo, Is.EqualTo(12));
            Assert.That(model.ReserveAmmo, Is.EqualTo(48));

            model.BindWeapon(null);
            Assert.That(model.WeaponSpec, Is.Null);

            Assert.That(changes, Is.EqualTo(new[]
            {
                HudField.Ammo, HudField.WeaponName,
                HudField.Ammo, HudField.WeaponName,
                HudField.Ammo, HudField.WeaponName,
            }));
        }

        [Test]
        public void AnInfiniteReserveIsShownAsInfinityThroughReloads()
        {
            var rules = new GameRules(
                FriendlyFire.Off,
                RespawnMode.Individual,
                GameRules.DefaultWaveIntervalSeconds,
                0f,
                0f,
                DeathBody.Vanish,
                DeathCamera.None,
                true,
                1f,
                Destruction.Off);
            var weapon = new Weapon(
                new WeaponSpec(_rifleId, "Rifle", WeaponCategory.AssaultRifle, FireMode.Auto, 2, 0, 0f, 1f, 25, 100f, DamageFalloff.None),
                rules);
            var model = new HudModel();

            model.BindWeapon(weapon);
            Assert.That(model.MagazineAmmo, Is.EqualTo(2));
            Assert.That(model.IsReserveInfinite, Is.True);

            weapon.TryFire();
            Assert.That(weapon.TryStartReload(), Is.True);
            model.SetAmmo(weapon.AmmoInMagazine, weapon.ReserveAmmo, weapon.IsReloading, weapon.HasInfiniteReserve);
            Assert.That(model.IsReloading, Is.True);
            Assert.That(model.IsReserveInfinite, Is.True);

            weapon.Tick(1f);
            Assert.That(model.MagazineAmmo, Is.EqualTo(2), "the reload refills from the infinite reserve");
            Assert.That(model.IsReloading, Is.False);
            Assert.That(model.IsReserveInfinite, Is.True);
        }

        [Test]
        public void WeaponNameDoesNotNotifyWhenTheNameStaysTheSame()
        {
            var model = new HudModel();
            model.BindWeapon(NewWeapon());
            var changes = Record(model);

            model.BindWeapon(NewWeapon(magazine: 20));

            Assert.That(model.WeaponSpec.Id, Is.EqualTo(_rifleId));
            Assert.That(changes, Is.EqualTo(new[] { HudField.Ammo }));
        }

        [Test]
        public void SetWeaponSpecTreatsNullAsNoneAndCanBeCleared()
        {
            var model = new HudModel();
            var changes = Record(model);

            model.SetWeaponSpec(NewHandgun().Spec);
            Assert.That(model.WeaponSpec.Id, Is.EqualTo(_handgunId));

            model.SetWeaponSpec(null);
            Assert.That(model.WeaponSpec, Is.Null);

            model.SetWeaponSpec(NewWeapon().Spec);
            model.ClearWeaponSpec();
            model.ClearWeaponSpec();
            Assert.That(model.WeaponSpec, Is.Null);

            Assert.That(changes, Is.EqualTo(new[] { HudField.WeaponName, HudField.WeaponName, HudField.WeaponName, HudField.WeaponName }));
        }

        [Test]
        public void UnbindAllKeepsTheWeaponName()
        {
            var model = new HudModel();
            model.BindWeapon(NewHandgun());

            model.UnbindAll();

            Assert.That(model.WeaponSpec.Id, Is.EqualTo(_handgunId));
        }

        [Test]
        public void HealthUpdatesOnDamageAndHeal()
        {
            var model = new HudModel();
            var health = new Health(100);
            model.BindHealth(health);
            var changes = Record(model);

            health.TakeDamage(25);
            Assert.That(model.CurrentHealth, Is.EqualTo(75));

            health.Heal(10);
            Assert.That(model.CurrentHealth, Is.EqualTo(85));

            health.TakeDamage(500);
            Assert.That(model.CurrentHealth, Is.EqualTo(0));

            Assert.That(changes, Is.EqualTo(new[] { HudField.Health, HudField.Health, HudField.Health }));
        }

        [Test]
        public void HealthDoesNotNotifyWhenNothingChanges()
        {
            var model = new HudModel();
            var health = new Health(100);
            model.BindHealth(health);
            var changes = Record(model);

            health.Heal(10);
            health.TakeDamage(0);

            Assert.That(changes, Is.Empty);
        }

        [Test]
        public void AmmoUpdatesOnFireAndReload()
        {
            var model = new HudModel();
            var weapon = NewWeapon(magazine: 30, reserve: 90, reloadSeconds: 1f);
            model.BindWeapon(weapon);
            var changes = Record(model);

            weapon.TryFire();
            weapon.TryFire();
            Assert.That(model.MagazineAmmo, Is.EqualTo(28));
            Assert.That(model.ReserveAmmo, Is.EqualTo(90));

            weapon.TryStartReload();
            weapon.Tick(0.5f);
            Assert.That(model.MagazineAmmo, Is.EqualTo(28), "the magazine is only refilled when the reload finishes");
            Assert.That(model.ReserveAmmo, Is.EqualTo(90));

            weapon.Tick(0.5f);
            Assert.That(model.MagazineAmmo, Is.EqualTo(30));
            Assert.That(model.ReserveAmmo, Is.EqualTo(88));

            Assert.That(changes, Is.EqualTo(new[] { HudField.Ammo, HudField.Ammo, HudField.Ammo }));
        }

        [Test]
        public void TimeUpdatesOnlyWhenTheShownSecondChanges()
        {
            var model = new HudModel();
            var match = new Match(65f, 1, _playerId);
            model.BindMatch(match);
            match.Start();
            var changes = Record(model);

            // 65.0 → 64.9: 表示は 01:05 のまま。
            match.Tick(0.1f);
            Assert.That(model.RemainingSeconds, Is.EqualTo(65));
            Assert.That(changes, Is.Empty);

            // 64.9 → 63.9: 01:04。
            match.Tick(1f);
            Assert.That(model.RemainingSeconds, Is.EqualTo(64));
            Assert.That(changes, Is.EqualTo(new[] { HudField.Time }));
        }

        [Test]
        public void TimeShowsZeroWhenTheMatchRunsOut()
        {
            var model = new HudModel();
            var match = new Match(3f, 1, _playerId);
            model.BindMatch(match);
            match.Start();

            match.Tick(10f);

            Assert.That(match.IsOver, Is.True);
            Assert.That(model.RemainingSeconds, Is.EqualTo(0));
        }

        [Test]
        public void RebindingStopsListeningToTheOldSource()
        {
            var model = new HudModel();
            var oldHealth = new Health(100);
            var newHealth = new Health(50);
            model.BindHealth(oldHealth);

            model.BindHealth(newHealth);
            var changes = Record(model);
            oldHealth.TakeDamage(30);

            Assert.That(model.CurrentHealth, Is.EqualTo(50));
            Assert.That(changes, Is.Empty);
        }

        [Test]
        public void BindingNullRestoresThePlaceholder()
        {
            var model = new HudModel();
            model.BindHealth(new Health(100));
            model.BindWeapon(NewWeapon());
            model.BindMatch(new Match(10f, 1, _playerId));

            model.BindHealth(null);
            model.BindWeapon(null);
            model.BindMatch(null);

            Assert.That(model.CurrentHealth, Is.Null);
            Assert.That(model.MagazineAmmo, Is.Null);
            Assert.That(model.ReserveAmmo, Is.Null);
            Assert.That(model.WeaponSpec, Is.Null);
            Assert.That(model.RemainingSeconds, Is.Null);
        }

        [Test]
        public void UnbindAllKeepsTheValuesButStopsUpdates()
        {
            var model = new HudModel();
            var health = new Health(100);
            var weapon = NewWeapon();
            var match = new Match(10f, 1, _playerId);
            model.BindHealth(health);
            model.BindWeapon(weapon);
            model.BindMatch(match);
            match.Start();

            model.UnbindAll();
            var changes = Record(model);
            health.TakeDamage(10);
            weapon.TryFire();
            match.Tick(5f);

            Assert.That(changes, Is.Empty);
            Assert.That(model.CurrentHealth, Is.EqualTo(100));
            Assert.That(model.MagazineAmmo, Is.EqualTo(30));
            Assert.That(model.ReserveAmmo, Is.EqualTo(90));
            Assert.That(model.RemainingSeconds, Is.EqualTo(10));
        }

        [Test]
        public void AMatchWithoutATimeLimitHidesTheTime()
        {
            var model = new HudModel();
            model.BindMatch(new Match(30f, 1, _playerId));
            Assert.That(model.HasTime, Is.True);

            var changes = Record(model);
            model.BindMatch(new Match(float.PositiveInfinity, 1, _playerId));

            Assert.That(model.HasTime, Is.False);
            Assert.That(model.RemainingSeconds, Is.Null);
            Assert.That(changes, Is.EqualTo(new[] { HudField.Time }));
        }

        [Test]
        public void TimeIsShownOnlyAfterSetTime()
        {
            var model = new HudModel();
            Assert.That(model.HasTime, Is.False);

            model.SetTime(5f);
            Assert.That(model.HasTime, Is.True);

            model.ClearTime();
            Assert.That(model.HasTime, Is.False);
        }

        [Test]
        public void SoloResultFollowsTheBoundMatch()
        {
            var model = new HudModel();
            var match = new Match(float.PositiveInfinity, 1, _playerId);
            model.BindMatch(match);
            match.Start();
            Assert.That(model.SoloResult, Is.Null);
            var changes = Record(model);

            match.ReportKill(_playerId, _playerId + 1);

            Assert.That(model.SoloResult, Is.EqualTo(MatchOutcome.Won));
            Assert.That(model.HasResult, Is.False, "a solo result is not a team result");
            Assert.That(changes, Is.EqualTo(new[] { HudField.Result }));

            model.BindMatch(null);
            Assert.That(model.SoloResult, Is.Null);
        }

        [Test]
        public void BindingAnEndedMatchShowsItsResultAtOnce()
        {
            var match = new Match(10f, 1, _playerId);
            match.Start();
            match.ReportDeath(_playerId);
            var model = new HudModel();

            model.BindMatch(match);

            Assert.That(model.SoloResult, Is.EqualTo(MatchOutcome.Lost));
        }

        [Test]
        public void RestartKeyChangesTheResult()
        {
            var model = new HudModel();
            var changes = Record(model);

            model.SetRestartKey("R");
            model.SetRestartKey("R");
            Assert.That(model.RestartKey, Is.EqualTo("R"));

            model.SetRestartKey(null);
            Assert.That(model.RestartKey, Is.EqualTo(string.Empty));
            Assert.That(changes, Is.EqualTo(new[] { HudField.Result, HudField.Result }));
        }

        [Test]
        public void UnbindAllStopsFollowingTheMatchResult()
        {
            var model = new HudModel();
            var match = new Match(float.PositiveInfinity, 1, _playerId);
            model.BindMatch(match);
            match.Start();

            model.UnbindAll();
            match.ReportDeath(_playerId);

            Assert.That(model.SoloResult, Is.Null);
        }

        [Test]
        public void NoticeStartsEmpty()
        {
            var model = new HudModel();

            Assert.That(model.Notice, Is.EqualTo(HudNotice.None));
        }

        [Test]
        public void SetHealthShowsTheValueAndWarnsWhenLow()
        {
            var model = new HudModel();
            var changes = Record(model);

            model.SetHealth(100, 100);
            Assert.That(model.CurrentHealth, Is.EqualTo(100));
            Assert.That(model.IsHealthLow, Is.False);

            model.SetHealth(20, 100);
            Assert.That(model.CurrentHealth, Is.EqualTo(20));
            Assert.That(model.IsHealthLow, Is.True);

            model.SetHealth(20, 100);
            model.ClearHealth();
            Assert.That(model.CurrentHealth, Is.Null);
            Assert.That(model.IsHealthLow, Is.False);

            Assert.That(changes, Is.EqualTo(new[] { HudField.Health, HudField.Health, HudField.Health }));
        }

        [Test]
        public void SetHealthKeepsTheFractionOfTheMaximum()
        {
            var model = new HudModel();
            Assert.That(model.HealthFraction, Is.Null, "no health before it is set");

            model.SetHealth(30, 120);
            Assert.That(model.HealthFraction, Is.EqualTo(0.25f));

            model.SetHealth(150, 100);
            Assert.That(model.HealthFraction, Is.EqualTo(1f), "over the maximum is capped");

            model.ClearHealth();
            Assert.That(model.HealthFraction, Is.Null);
        }

        [Test]
        public void SetHealthNotifiesWhenOnlyTheFractionChanges()
        {
            var model = new HudModel();
            model.SetHealth(80, 100);
            var changes = Record(model);

            model.SetHealth(80, 200);

            Assert.That(model.CurrentHealth, Is.EqualTo(80));
            Assert.That(model.HealthFraction, Is.EqualTo(0.4f));
            Assert.That(changes, Is.EqualTo(new[] { HudField.Health }));
        }

        [Test]
        public void SetHealthNotifiesWhenOnlyTheWarningChanges()
        {
            var model = new HudModel();
            model.SetHealth(30, 100);
            var changes = Record(model);

            model.SetHealth(30, 200);

            Assert.That(model.IsHealthLow, Is.True);
            Assert.That(changes, Is.EqualTo(new[] { HudField.Health }));
        }

        [Test]
        public void BoundHealthWarnsBelowAQuarterOfItsMax()
        {
            var model = new HudModel();
            var health = new Health(100);
            model.BindHealth(health);

            health.TakeDamage(80);

            Assert.That(model.IsHealthLow, Is.True);
        }

        [Test]
        public void SetAmmoShowsReloading()
        {
            var model = new HudModel();
            var changes = Record(model);

            model.SetAmmo(5, 90, false);
            Assert.That(model.MagazineAmmo, Is.EqualTo(5));
            Assert.That(model.ReserveAmmo, Is.EqualTo(90));
            Assert.That(model.IsReloading, Is.False);

            model.SetAmmo(5, 90, true);
            Assert.That(model.MagazineAmmo, Is.EqualTo(5));
            Assert.That(model.ReserveAmmo, Is.EqualTo(90));
            Assert.That(model.IsReloading, Is.True);

            model.SetAmmo(30, 65, false);
            Assert.That(model.MagazineAmmo, Is.EqualTo(30));
            Assert.That(model.ReserveAmmo, Is.EqualTo(65));
            Assert.That(model.IsReloading, Is.False);

            model.ClearAmmo();
            Assert.That(model.MagazineAmmo, Is.Null);
            Assert.That(model.ReserveAmmo, Is.Null);

            Assert.That(changes, Is.EqualTo(new[] { HudField.Ammo, HudField.Ammo, HudField.Ammo, HudField.Ammo }));
        }

        [Test]
        public void HidingTheReserveKeepsTheAmmoNumbersAndNotifies()
        {
            var model = new HudModel();
            model.SetAmmo(12, 48, false, true);
            var changes = Record(model);

            model.SetReserveAmmoShown(false);
            Assert.That(model.ShowsReserveAmmo, Is.False);
            Assert.That(model.MagazineAmmo, Is.EqualTo(12), "the numbers stay so the HUD can show the reserve again at once");
            Assert.That(model.ReserveAmmo, Is.EqualTo(48));
            Assert.That(model.IsReserveInfinite, Is.True);

            model.SetAmmo(0, 48, true, false);
            Assert.That(model.MagazineAmmo, Is.EqualTo(0));
            Assert.That(model.IsReloading, Is.True);
            Assert.That(model.IsReserveInfinite, Is.False);

            model.SetReserveAmmoShown(true);
            Assert.That(model.ShowsReserveAmmo, Is.True);
            Assert.That(model.ReserveAmmo, Is.EqualTo(48));

            Assert.That(changes, Is.EqualTo(new[] { HudField.Ammo, HudField.Ammo, HudField.Ammo }));
        }

        [Test]
        public void HidingTheReserveFollowsTheBoundWeapon()
        {
            var model = new HudModel();
            model.SetReserveAmmoShown(false);
            var weapon = NewWeapon();

            model.BindWeapon(weapon);
            Assert.That(model.ShowsReserveAmmo, Is.False);
            Assert.That(model.MagazineAmmo, Is.EqualTo(30));

            weapon.TryFire();
            Assert.That(model.MagazineAmmo, Is.EqualTo(29));
            Assert.That(model.ReserveAmmo, Is.EqualTo(90));
        }

        [Test]
        public void HidingTheReserveWithoutAWeaponNotifiesOnlyWhenItChanges()
        {
            var model = new HudModel();
            var changes = Record(model);

            model.SetReserveAmmoShown(false);
            Assert.That(model.ShowsReserveAmmo, Is.False);
            Assert.That(model.MagazineAmmo, Is.Null);

            model.SetReserveAmmoShown(false);
            model.SetReserveAmmoShown(true);
            Assert.That(model.MagazineAmmo, Is.Null);
            Assert.That(model.ReserveAmmo, Is.Null);

            Assert.That(changes, Is.EqualTo(new[] { HudField.Ammo, HudField.Ammo }));
        }

        [Test]
        public void HidingTheReserveStillRejectsANegativeReserve()
        {
            var model = new HudModel();
            model.SetReserveAmmoShown(false);
            model.SetAmmo(5, 10, false);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => model.SetAmmo(5, -1, false));
            Assert.That(model.MagazineAmmo, Is.EqualTo(5));
            Assert.That(model.ReserveAmmo, Is.EqualTo(10));
        }

        [Test]
        public void SetTimeNotifiesOnlyWhenTheShownSecondChanges()
        {
            var model = new HudModel();
            var changes = Record(model);

            model.SetTime(65f);
            model.SetTime(64.5f);
            model.SetTime(64f);
            model.ClearTime();

            Assert.That(model.RemainingSeconds, Is.Null);
            Assert.That(changes, Is.EqualTo(new[] { HudField.Time, HudField.Time, HudField.Time }));
        }

        [Test]
        public void ScoresStartHiddenAndPutTheOwnTeamFirst()
        {
            var model = new HudModel();
            Assert.That(model.FirstScoreTeam, Is.Null);
            Assert.That(model.SecondScoreTeam, Is.Null);
            Assert.That(model.FirstScore, Is.Null);
            Assert.That(model.SecondScore, Is.Null);
            var changes = Record(model);

            model.SetScores(Team.Bravo, 2, 5);
            Assert.That(model.FirstScoreTeam, Is.EqualTo(Team.Bravo));
            Assert.That(model.FirstScore, Is.EqualTo(5));
            Assert.That(model.SecondScoreTeam, Is.EqualTo(Team.Alpha));
            Assert.That(model.SecondScore, Is.EqualTo(2));

            model.SetScores(Team.Bravo, 2, 5);
            model.SetScores(Team.Alpha, 2, 5);
            Assert.That(model.FirstScore, Is.EqualTo(2));
            Assert.That(model.SecondScore, Is.EqualTo(5));

            model.ClearScores();
            Assert.That(model.FirstScoreTeam, Is.Null);
            Assert.That(model.FirstScore, Is.Null);

            Assert.That(changes, Is.EqualTo(new[] { HudField.Score, HudField.Score, HudField.Score }));
        }

        [Test]
        public void ResultShowsTheWinnerOrDrawUntilCleared()
        {
            var model = new HudModel();
            Assert.That(model.HasResult, Is.False);
            var changes = Record(model);

            model.SetResult(Team.Alpha);
            Assert.That(model.HasResult, Is.True);
            Assert.That(model.Winner, Is.EqualTo(Team.Alpha));

            model.SetResult(null);
            Assert.That(model.HasResult, Is.True);
            Assert.That(model.Winner, Is.Null, "a draw has no winner");

            model.ClearResult();
            model.ClearResult();
            Assert.That(model.HasResult, Is.False);
            Assert.That(model.Winner, Is.Null);

            Assert.That(changes, Is.EqualTo(new[] { HudField.Result, HudField.Result, HudField.Result }));
        }

        [Test]
        public void ClearNoticeTakesTheNoticeOffAndNotifiesOnce()
        {
            var model = new HudModel();
            model.SetNotice(HudNotice.Respawn(3));
            var changes = Record(model);

            model.ClearNotice();
            model.ClearNotice();

            Assert.That(model.Notice, Is.EqualTo(HudNotice.None));
            Assert.That(changes, Is.EqualTo(new[] { HudField.Notice }));
        }

        [Test]
        public void SetRespawnNoticeFollowsTheRespawnMode()
        {
            var model = new HudModel();
            var changes = Record(model);

            model.SetRespawnNotice(RespawnMode.Wave, 5);
            Assert.That(model.Notice, Is.EqualTo(HudNotice.Respawn(5)));

            model.SetRespawnNotice(RespawnMode.None, 0);
            Assert.That(model.Notice, Is.EqualTo(HudNotice.NoRespawnUntilNextMatch));

            model.SetRespawnNotice(RespawnMode.None, 3);
            Assert.That(changes, Is.EqualTo(new[] { HudField.Notice, HudField.Notice }));
        }

        [Test]
        public void TheCrosshairStartsShownWithNoSpread()
        {
            var model = new HudModel();

            Assert.That(model.CrosshairSpread, Is.EqualTo(0f));
            Assert.That(model.IsCrosshairVisible, Is.True);
        }

        [Test]
        public void SetCrosshairChangesTheSpreadAndNotifiesOnlyWhenItChanges()
        {
            var model = new HudModel();
            var changes = Record(model);

            model.SetCrosshair(0.5f, true);
            model.SetCrosshair(0.5f, true);
            model.SetCrosshair(1f, true);

            Assert.That(model.CrosshairSpread, Is.EqualTo(1f));
            Assert.That(model.IsCrosshairVisible, Is.True);
            Assert.That(changes, Is.EqualTo(new[] { HudField.Crosshair, HudField.Crosshair }));
        }

        [Test]
        public void SetCrosshairHidesAndShowsTheCrosshair()
        {
            var model = new HudModel();
            var changes = Record(model);

            model.SetCrosshair(0f, false);
            Assert.That(model.IsCrosshairVisible, Is.False);
            model.SetCrosshair(0f, false);

            model.SetCrosshair(0f, true);
            Assert.That(model.IsCrosshairVisible, Is.True);
            Assert.That(changes, Is.EqualTo(new[] { HudField.Crosshair, HudField.Crosshair }));
        }

        [Test]
        public void ResetCrosshairShowsItWithNoSpread()
        {
            var model = new HudModel();
            model.SetCrosshair(0.75f, false);
            var changes = Record(model);

            model.ResetCrosshair();
            model.ResetCrosshair();

            Assert.That(model.CrosshairSpread, Is.EqualTo(0f));
            Assert.That(model.IsCrosshairVisible, Is.True);
            Assert.That(changes, Is.EqualTo(new[] { HudField.Crosshair }));
        }

        [Test]
        public void SetCrosshairKeepsTheConeGapAndNotifiesWhenOnlyItChanges()
        {
            var model = new HudModel();
            var changes = Record(model);

            model.SetCrosshair(0f, 40f, true);
            model.SetCrosshair(0f, 40f, true);
            Assert.That(model.CrosshairConeGap, Is.EqualTo(40f));

            model.SetCrosshair(0f, true);
            Assert.That(model.CrosshairConeGap, Is.EqualTo(0f), "the overload without a cone gap clears it");
            Assert.That(changes, Is.EqualTo(new[] { HudField.Crosshair, HudField.Crosshair }));
        }

        [Test]
        public void SetCrosshairRejectsANegativeOrNonFiniteConeGapAndKeepsTheShownOne()
        {
            var model = new HudModel();
            model.SetCrosshair(0.25f, 12f, true);
            var changes = Record(model);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => model.SetCrosshair(0f, -1f, true));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => model.SetCrosshair(0f, float.NaN, true));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => model.SetCrosshair(0f, float.PositiveInfinity, true));

            Assert.That(model.CrosshairConeGap, Is.EqualTo(12f));
            Assert.That(model.CrosshairSpread, Is.EqualTo(0.25f));
            Assert.That(changes, Is.Empty);
        }

        [Test]
        public void SetCrosshairRejectsASpreadOutsideZeroToOneAndKeepsTheShownOne()
        {
            var model = new HudModel();
            model.SetCrosshair(0.25f, true);
            var changes = Record(model);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => model.SetCrosshair(-0.1f, false));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => model.SetCrosshair(1.1f, false));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => model.SetCrosshair(float.NaN, false));

            Assert.That(model.CrosshairSpread, Is.EqualTo(0.25f));
            Assert.That(model.IsCrosshairVisible, Is.True);
            Assert.That(changes, Is.Empty);
        }

        [Test]
        public void ReloadProgressStartsHidden()
        {
            var model = new HudModel();

            Assert.That(model.ReloadProgress, Is.Null);
            Assert.That(model.ReloadWindowStart, Is.EqualTo(0f));
            Assert.That(model.ReloadWindowEnd, Is.EqualTo(0f));
        }

        [Test]
        public void SetReloadProgressShowsTheProgressAndTheWindow()
        {
            var model = new HudModel();
            var changes = Record(model);

            model.SetReloadProgress(0.25f, 0.5f, 0.65f);

            Assert.That(model.ReloadProgress, Is.EqualTo(0.25f));
            Assert.That(model.ReloadWindowStart, Is.EqualTo(0.5f));
            Assert.That(model.ReloadWindowEnd, Is.EqualTo(0.65f));
            Assert.That(changes, Is.EqualTo(new[] { HudField.ReloadProgress }));
        }

        [Test]
        public void ReloadProgressNotifiesOnlyWhenAValueChanges()
        {
            var model = new HudModel();
            model.SetReloadProgress(0.25f, 0.5f, 0.65f);
            var changes = Record(model);

            model.SetReloadProgress(0.25f, 0.5f, 0.65f);
            Assert.That(changes, Is.Empty, "same values");

            model.SetReloadProgress(0.3f, 0.5f, 0.65f);
            model.SetReloadProgress(0.3f, 0.4f, 0.65f);
            model.SetReloadProgress(0.3f, 0.4f, 0.6f);
            Assert.That(changes, Is.EqualTo(new[] { HudField.ReloadProgress, HudField.ReloadProgress, HudField.ReloadProgress }));
        }

        [Test]
        public void ClearReloadProgressHidesItAndNotifiesOnce()
        {
            var model = new HudModel();
            model.SetReloadProgress(0.75f, 0.5f, 0.65f);
            var changes = Record(model);

            model.ClearReloadProgress();
            model.ClearReloadProgress();

            Assert.That(model.ReloadProgress, Is.Null);
            Assert.That(model.ReloadWindowStart, Is.EqualTo(0f));
            Assert.That(model.ReloadWindowEnd, Is.EqualTo(0f));
            Assert.That(changes, Is.EqualTo(new[] { HudField.ReloadProgress }));
        }

        [Test]
        public void ClearingAHiddenReloadProgressDoesNotNotify()
        {
            var model = new HudModel();
            var changes = Record(model);

            model.ClearReloadProgress();

            Assert.That(changes, Is.Empty);
        }

        [Test]
        public void ReloadProgressAtZeroIsShownEvenWithAnEmptyWindow()
        {
            var model = new HudModel();
            var changes = Record(model);

            model.SetReloadProgress(0f, 0f, 0f);

            Assert.That(model.ReloadProgress, Is.EqualTo(0f));
            Assert.That(changes, Is.EqualTo(new[] { HudField.ReloadProgress }));
        }

        [Test]
        public void SetReloadProgressRejectsInvalidValuesAndKeepsTheShownOne()
        {
            var model = new HudModel();
            model.SetReloadProgress(0.25f, 0.5f, 0.65f);
            var changes = Record(model);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => model.SetReloadProgress(-0.1f, 0.5f, 0.65f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => model.SetReloadProgress(1.1f, 0.5f, 0.65f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => model.SetReloadProgress(float.NaN, 0.5f, 0.65f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => model.SetReloadProgress(0.5f, -0.1f, 0.65f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => model.SetReloadProgress(0.5f, 0.5f, 1.1f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => model.SetReloadProgress(0.5f, 0.7f, 0.65f));

            Assert.That(model.ReloadProgress, Is.EqualTo(0.25f));
            Assert.That(changes, Is.Empty);
        }

        private static Weapon NewActiveReloadWeapon(ActiveReload activeReload)
        {
            var d = GameRules.Default;
            var a = ActionRules.Default;
            var rules = new GameRules(
                d.FriendlyFire,
                d.RespawnMode,
                d.WaveIntervalSeconds,
                d.RegenDelaySeconds,
                d.RegenPerSecond,
                d.DeathBody,
                d.DeathCamera,
                d.InfiniteReserve,
                d.ReserveAmmoMultiplier,
                d.Destruction,
                new ActionRules(a.Downed, a.DownedBleedoutSeconds, a.DownedHealthFraction, a.ReviveSeconds, a.Cover, a.BlindFire, activeReload));
            var spec = new WeaponSpec(
                _rifleId, "Rifle", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0f, 2f, 25, 100f, DamageFalloff.None,
                activeReloadWindowStart: 0.5f,
                activeReloadWindowLength: 0.25f,
                activeReloadFailSeconds: 0.5f);
            var weapon = new Weapon(spec, rules);
            Assert.That(weapon.TryFire(), Is.True);
            return weapon;
        }

        [Test]
        public void RefreshReloadProgressFollowsTheBoundWeaponWhileItReloads()
        {
            var model = new HudModel();
            var weapon = NewActiveReloadWeapon(ActiveReload.On);
            model.BindWeapon(weapon);
            var changes = Record(model);

            model.RefreshReloadProgress();
            Assert.That(model.ReloadProgress, Is.Null, "not reloading");
            Assert.That(changes, Is.Empty);

            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(0.5f);
            model.RefreshReloadProgress();
            Assert.That(model.ReloadProgress, Is.EqualTo(0.25f));
            Assert.That(model.ReloadWindowStart, Is.EqualTo(0.5f));
            Assert.That(model.ReloadWindowEnd, Is.EqualTo(0.75f));

            weapon.Tick(1.5f);
            Assert.That(model.ReloadProgress, Is.Null, "the finished reload clears it without waiting for the next refresh");
        }

        [Test]
        public void AJudgedReloadKeepsTheProgressButHidesTheWindow()
        {
            var model = new HudModel();
            var weapon = NewActiveReloadWeapon(ActiveReload.On);
            model.BindWeapon(weapon);
            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(0.5f);

            Assert.That(weapon.TryActiveReload(out var result), Is.True);
            Assert.That(result, Is.EqualTo(ActiveReloadResult.Fail));
            model.RefreshReloadProgress();

            Assert.That(model.ReloadProgress, Is.EqualTo(0.2f), "the fail stretches the reload to 2.5 seconds");
            Assert.That(model.ReloadWindowStart, Is.EqualTo(0f));
            Assert.That(model.ReloadWindowEnd, Is.EqualTo(0f));
        }

        [Test]
        public void ASuccessfulActiveReloadClearsTheProgressAtOnce()
        {
            var model = new HudModel();
            var weapon = NewActiveReloadWeapon(ActiveReload.On);
            model.BindWeapon(weapon);
            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(1.25f);
            model.RefreshReloadProgress();
            Assert.That(model.ReloadProgress, Is.EqualTo(0.625f));

            Assert.That(weapon.TryActiveReload(out var result), Is.True);

            Assert.That(result, Is.EqualTo(ActiveReloadResult.Success));
            Assert.That(model.ReloadProgress, Is.Null);
            Assert.That(model.MagazineAmmo, Is.EqualTo(30));
            Assert.That(model.ReserveAmmo, Is.EqualTo(89));
            Assert.That(model.IsReloading, Is.False);
        }

        [Test]
        public void RefreshReloadProgressShowsNothingWhenTheRuleIsOffOrNoWeaponIsBound()
        {
            var model = new HudModel();
            var weapon = NewActiveReloadWeapon(ActiveReload.Off);
            model.BindWeapon(weapon);
            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(0.5f);

            model.RefreshReloadProgress();
            Assert.That(model.ReloadProgress, Is.Null, "the rule is off");

            var active = NewActiveReloadWeapon(ActiveReload.On);
            model.BindWeapon(active);
            Assert.That(active.TryStartReload(), Is.True);
            model.RefreshReloadProgress();
            Assert.That(model.ReloadProgress, Is.EqualTo(0f));

            model.BindWeapon(null);
            Assert.That(model.ReloadProgress, Is.Null, "unbinding the weapon clears it");
        }

        [Test]
        public void SettersRejectInvalidValues()
        {
            var model = new HudModel();

            Assert.Throws<System.ArgumentOutOfRangeException>(() => model.SetHealth(-1, 100));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => model.SetHealth(10, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => model.SetAmmo(-1, 0, false));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => model.SetScores(Team.Alpha, -1, 0));
            Assert.That(model.CurrentHealth, Is.Null);
        }

        [Test]
        public void UnbindingTheNoticeClearsItAndStopsListening()
        {
            var model = new HudModel();
            var source = new FakeNoticeSource();
            model.BindNotice(source);
            source.Set(HudNotice.CheckpointRestart(3));

            model.BindNotice(null);
            Assert.That(model.Notice, Is.EqualTo(HudNotice.None));

            source.Set(HudNotice.CheckpointRestart(1));
            Assert.That(model.Notice, Is.EqualTo(HudNotice.None));

            model.BindNotice(source);
            model.UnbindAll();
            var changes = Record(model);
            source.Set(HudNotice.None);
            Assert.That(changes, Is.Empty);
            Assert.That(model.Notice, Is.EqualTo(HudNotice.CheckpointRestart(1)), "UnbindAll keeps the notice");
        }

        [Test]
        public void BindingNoSourceKeepsANoticeSetDirectly()
        {
            var model = new HudModel();
            model.SetRespawnNotice(RespawnMode.Individual, 4);
            var changes = Record(model);

            model.BindNotice(null);

            Assert.That(model.Notice, Is.EqualTo(HudNotice.Respawn(4)));
            Assert.That(changes, Is.Empty);
        }

        [Test]
        public void ANoticeSourceOtherThanTheCampaignIsShownAndLetGo()
        {
            var model = new HudModel();
            var source = new FakeNoticeSource { Notice = HudNotice.CheckpointSaved };
            var changes = Record(model);

            model.BindNotice(source);
            Assert.That(model.Notice, Is.EqualTo(HudNotice.CheckpointSaved), "binding shows the source's notice at once");
            Assert.That(source.Subscribers, Is.EqualTo(1));

            source.Set(HudNotice.CheckpointRestart(2));
            Assert.That(model.Notice, Is.EqualTo(HudNotice.CheckpointRestart(2)), "a change from the source reaches the model");

            model.BindNotice(null);
            Assert.That(source.Subscribers, Is.Zero, "rebinding stops listening to the old source");
            Assert.That(model.Notice, Is.EqualTo(HudNotice.None));

            model.BindNotice(source);
            model.UnbindAll();
            Assert.That(source.Subscribers, Is.Zero, "UnbindAll stops listening to the source");
            Assert.That(changes, Is.EqualTo(new[] { HudField.Notice, HudField.Notice, HudField.Notice, HudField.Notice }));
        }

        private sealed class FakeNoticeSource : IHudNoticeSource
        {
            private System.Action<HudNotice> _changed;

            public HudNotice Notice { get; set; }

            public int Subscribers => _changed == null ? 0 : _changed.GetInvocationList().Length;

            public event System.Action<HudNotice> Changed
            {
                add => _changed += value;
                remove => _changed -= value;
            }

            public void Set(HudNotice notice)
            {
                Notice = notice;
                _changed?.Invoke(notice);
            }
        }
    }
}
