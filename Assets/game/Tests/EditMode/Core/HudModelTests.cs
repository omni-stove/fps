using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class HudModelTests
    {
        private const int _playerId = 0;
        private const string _rifleId = "00000000-0000-4000-8000-000000000001";

        private const string _handgunId = "00000000-0000-4000-8000-000000000002";

        private static Weapon NewWeapon(int magazine = 30, int reserve = 90, float fireInterval = 0f, float reloadSeconds = 1f)
        {
            return new Weapon(new WeaponSpec(_rifleId, "Rifle", WeaponCategory.AssaultRifle, FireMode.Auto, magazine, reserve, fireInterval, reloadSeconds, 25, 100f, DamageFalloff.None));
        }

        private static Weapon NewHandgun()
        {
            return new Weapon(new WeaponSpec(_handgunId, "Handgun", WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f, DamageFalloff.None));
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

            Assert.That(model.HealthText, Is.EqualTo(HudFormat.NoHealth));
            Assert.That(model.AmmoText, Is.EqualTo(HudFormat.NoAmmo));
            Assert.That(model.WeaponNameText, Is.EqualTo(HudFormat.NoWeaponName));
            Assert.That(model.TimeText, Is.EqualTo(HudFormat.NoTime));
        }

        [Test]
        public void BindingShowsTheCurrentValuesAtOnce()
        {
            var model = new HudModel();
            var changes = Record(model);

            model.BindHealth(new Health(100));
            model.BindWeapon(NewWeapon());
            model.BindMatch(new Match(180f, 1, _playerId));

            Assert.That(model.HealthText, Is.EqualTo("100"));
            Assert.That(model.AmmoText, Is.EqualTo("30 / 90"));
            Assert.That(model.WeaponNameText, Is.EqualTo("Rifle"));
            Assert.That(model.TimeText, Is.EqualTo("03:00"));
            Assert.That(changes, Is.EqualTo(new[] { HudField.Health, HudField.Ammo, HudField.WeaponName, HudField.Time }));
        }

        [Test]
        public void WeaponNameStartsEmptyAndFollowsTheBoundWeapon()
        {
            var model = new HudModel();
            Assert.That(model.WeaponNameText, Is.EqualTo(HudFormat.NoWeaponName));
            var changes = Record(model);

            model.BindWeapon(NewWeapon());
            Assert.That(model.WeaponNameText, Is.EqualTo("Rifle"));

            model.BindWeapon(NewHandgun());
            Assert.That(model.WeaponNameText, Is.EqualTo("Handgun"), "switching weapons rebinds and shows the new name");
            Assert.That(model.AmmoText, Is.EqualTo("12 / 48"));

            model.BindWeapon(null);
            Assert.That(model.WeaponNameText, Is.EqualTo(HudFormat.NoWeaponName));

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
            Assert.That(model.AmmoText, Is.EqualTo("2 / " + HudFormat.InfiniteReserve));

            weapon.TryFire();
            Assert.That(weapon.TryStartReload(), Is.True);
            model.SetAmmo(weapon.AmmoInMagazine, weapon.ReserveAmmo, weapon.IsReloading, weapon.HasInfiniteReserve);
            Assert.That(model.AmmoText, Is.EqualTo(HudFormat.Reloading + " / " + HudFormat.InfiniteReserve));

            weapon.Tick(1f);
            Assert.That(model.AmmoText, Is.EqualTo("2 / " + HudFormat.InfiniteReserve), "the reload refills from the infinite reserve");
        }

        [Test]
        public void WeaponNameDoesNotNotifyWhenTheNameStaysTheSame()
        {
            var model = new HudModel();
            model.BindWeapon(NewWeapon());
            var changes = Record(model);

            model.BindWeapon(NewWeapon(magazine: 20));

            Assert.That(model.WeaponNameText, Is.EqualTo("Rifle"));
            Assert.That(changes, Is.EqualTo(new[] { HudField.Ammo }));
        }

        [Test]
        public void SetWeaponNameTreatsNullAsEmptyAndCanBeCleared()
        {
            var model = new HudModel();
            var changes = Record(model);

            model.SetWeaponName("Handgun");
            Assert.That(model.WeaponNameText, Is.EqualTo("Handgun"));

            model.SetWeaponName(null);
            Assert.That(model.WeaponNameText, Is.EqualTo(HudFormat.NoWeaponName));

            model.SetWeaponName("Rifle");
            model.ClearWeaponName();
            model.ClearWeaponName();
            Assert.That(model.WeaponNameText, Is.EqualTo(HudFormat.NoWeaponName));

            Assert.That(changes, Is.EqualTo(new[] { HudField.WeaponName, HudField.WeaponName, HudField.WeaponName, HudField.WeaponName }));
        }

        [Test]
        public void UnbindAllKeepsTheWeaponName()
        {
            var model = new HudModel();
            model.BindWeapon(NewHandgun());

            model.UnbindAll();

            Assert.That(model.WeaponNameText, Is.EqualTo("Handgun"));
        }

        [Test]
        public void HealthUpdatesOnDamageAndHeal()
        {
            var model = new HudModel();
            var health = new Health(100);
            model.BindHealth(health);
            var changes = Record(model);

            health.TakeDamage(25);
            Assert.That(model.HealthText, Is.EqualTo("75"));

            health.Heal(10);
            Assert.That(model.HealthText, Is.EqualTo("85"));

            health.TakeDamage(500);
            Assert.That(model.HealthText, Is.EqualTo("0"));

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
            Assert.That(model.AmmoText, Is.EqualTo("28 / 90"));

            weapon.TryStartReload();
            weapon.Tick(0.5f);
            Assert.That(model.AmmoText, Is.EqualTo("28 / 90"), "the magazine is only refilled when the reload finishes");

            weapon.Tick(0.5f);
            Assert.That(model.AmmoText, Is.EqualTo("30 / 88"));

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
            Assert.That(model.TimeText, Is.EqualTo("01:05"));
            Assert.That(changes, Is.Empty);

            // 64.9 → 63.9: 01:04。
            match.Tick(1f);
            Assert.That(model.TimeText, Is.EqualTo("01:04"));
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
            Assert.That(model.TimeText, Is.EqualTo("00:00"));
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

            Assert.That(model.HealthText, Is.EqualTo("50"));
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

            Assert.That(model.HealthText, Is.EqualTo(HudFormat.NoHealth));
            Assert.That(model.AmmoText, Is.EqualTo(HudFormat.NoAmmo));
            Assert.That(model.WeaponNameText, Is.EqualTo(HudFormat.NoWeaponName));
            Assert.That(model.TimeText, Is.EqualTo(HudFormat.NoTime));
        }

        [Test]
        public void UnbindAllKeepsTheTextButStopsUpdates()
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
            Assert.That(model.HealthText, Is.EqualTo("100"));
            Assert.That(model.AmmoText, Is.EqualTo("30 / 90"));
            Assert.That(model.TimeText, Is.EqualTo("00:10"));
        }

        [Test]
        public void NoticeStartsEmptyAndFollowsTheCampaign()
        {
            var model = new HudModel();
            Assert.That(model.NoticeText, Is.EqualTo(HudFormat.NoNotice));

            var notice = new CampaignNotice();
            notice.ShowSaved(1f);
            var changes = Record(model);

            model.BindNotice(notice);
            Assert.That(model.NoticeText, Is.EqualTo(HudFormat.CheckpointSaved), "binding shows the current notice at once");

            notice.Tick(1f);
            Assert.That(model.NoticeText, Is.EqualTo(HudFormat.NoNotice));

            notice.ShowRestartCountdown(3f);
            notice.ShowRestartCountdown(2.5f);
            Assert.That(model.NoticeText, Is.EqualTo(HudFormat.CheckpointRestart(3)));

            Assert.That(changes, Is.EqualTo(new[] { HudField.Notice, HudField.Notice, HudField.Notice }));
        }

        [Test]
        public void SetHealthShowsTheValueAndWarnsWhenLow()
        {
            var model = new HudModel();
            var changes = Record(model);

            model.SetHealth(100, 100);
            Assert.That(model.HealthText, Is.EqualTo("100"));
            Assert.That(model.IsHealthLow, Is.False);

            model.SetHealth(20, 100);
            Assert.That(model.HealthText, Is.EqualTo("20"));
            Assert.That(model.IsHealthLow, Is.True);

            model.SetHealth(20, 100);
            model.ClearHealth();
            Assert.That(model.HealthText, Is.EqualTo(HudFormat.NoHealth));
            Assert.That(model.IsHealthLow, Is.False);

            Assert.That(changes, Is.EqualTo(new[] { HudField.Health, HudField.Health, HudField.Health }));
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
            Assert.That(model.AmmoText, Is.EqualTo("5 / 90"));
            Assert.That(model.IsReloading, Is.False);

            model.SetAmmo(5, 90, true);
            Assert.That(model.AmmoText, Is.EqualTo(HudFormat.Ammo(5, 90, true)));
            Assert.That(model.IsReloading, Is.True);

            model.SetAmmo(30, 65, false);
            Assert.That(model.AmmoText, Is.EqualTo("30 / 65"));
            Assert.That(model.IsReloading, Is.False);

            model.ClearAmmo();
            Assert.That(model.AmmoText, Is.EqualTo(HudFormat.NoAmmo));

            Assert.That(changes, Is.EqualTo(new[] { HudField.Ammo, HudField.Ammo, HudField.Ammo, HudField.Ammo }));
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

            Assert.That(model.TimeText, Is.EqualTo(HudFormat.NoTime));
            Assert.That(changes, Is.EqualTo(new[] { HudField.Time, HudField.Time, HudField.Time }));
        }

        [Test]
        public void ScoresStartHiddenAndPutTheOwnTeamFirst()
        {
            var model = new HudModel();
            Assert.That(model.FirstScoreTeam, Is.Null);
            Assert.That(model.SecondScoreTeam, Is.Null);
            Assert.That(model.FirstScoreText, Is.EqualTo(HudFormat.NoScore));
            Assert.That(model.SecondScoreText, Is.EqualTo(HudFormat.NoScore));
            var changes = Record(model);

            model.SetScores(Team.Bravo, 2, 5);
            Assert.That(model.FirstScoreTeam, Is.EqualTo(Team.Bravo));
            Assert.That(model.FirstScoreText, Is.EqualTo("BRAVO 5"));
            Assert.That(model.SecondScoreTeam, Is.EqualTo(Team.Alpha));
            Assert.That(model.SecondScoreText, Is.EqualTo("ALPHA 2"));

            model.SetScores(Team.Bravo, 2, 5);
            model.SetScores(Team.Alpha, 2, 5);
            Assert.That(model.FirstScoreText, Is.EqualTo("ALPHA 2"));
            Assert.That(model.SecondScoreText, Is.EqualTo("BRAVO 5"));

            model.ClearScores();
            Assert.That(model.FirstScoreTeam, Is.Null);
            Assert.That(model.FirstScoreText, Is.EqualTo(HudFormat.NoScore));

            Assert.That(changes, Is.EqualTo(new[] { HudField.Score, HudField.Score, HudField.Score }));
        }

        [Test]
        public void ResultShowsTheWinnerOrDrawUntilCleared()
        {
            var model = new HudModel();
            Assert.That(model.ResultText, Is.EqualTo(HudFormat.NoResult));
            var changes = Record(model);

            model.SetResult(Team.Alpha);
            Assert.That(model.ResultText, Is.EqualTo(HudFormat.Result(Team.Alpha)));

            model.SetResult(null);
            Assert.That(model.ResultText, Is.EqualTo(HudFormat.Draw));

            model.ClearResult();
            model.ClearResult();
            Assert.That(model.ResultText, Is.EqualTo(HudFormat.NoResult));

            Assert.That(changes, Is.EqualTo(new[] { HudField.Result, HudField.Result, HudField.Result }));
        }

        [Test]
        public void SetNoticeTreatsNullAsEmpty()
        {
            var model = new HudModel();
            model.SetNotice(HudFormat.Respawn(3));
            var changes = Record(model);

            model.SetNotice(null);

            Assert.That(model.NoticeText, Is.EqualTo(HudFormat.NoNotice));
            Assert.That(changes, Is.EqualTo(new[] { HudField.Notice }));
        }

        [Test]
        public void SetRespawnNoticeFollowsTheRespawnMode()
        {
            var model = new HudModel();
            var changes = Record(model);

            model.SetRespawnNotice(RespawnMode.Wave, 5);
            Assert.That(model.NoticeText, Is.EqualTo(HudFormat.Respawn(5)));

            model.SetRespawnNotice(RespawnMode.None, 0);
            Assert.That(model.NoticeText, Is.EqualTo(HudFormat.NoRespawnUntilNextMatch));

            // 表示が変わらなければ知らせない
            model.SetRespawnNotice(RespawnMode.None, 3);
            Assert.That(changes, Is.EqualTo(new[] { HudField.Notice, HudField.Notice }));
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

        [Test]
        public void SettersRejectInvalidValues()
        {
            var model = new HudModel();

            Assert.Throws<System.ArgumentOutOfRangeException>(() => model.SetHealth(-1, 100));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => model.SetHealth(10, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => model.SetAmmo(-1, 0, false));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => model.SetScores(Team.Alpha, -1, 0));
            Assert.That(model.HealthText, Is.EqualTo(HudFormat.NoHealth));
        }

        [Test]
        public void UnbindingTheNoticeClearsItAndStopsListening()
        {
            var model = new HudModel();
            var notice = new CampaignNotice();
            model.BindNotice(notice);
            notice.ShowRestartCountdown(3f);

            model.BindNotice(null);
            Assert.That(model.NoticeText, Is.EqualTo(HudFormat.NoNotice));

            notice.ShowRestartCountdown(1f);
            Assert.That(model.NoticeText, Is.EqualTo(HudFormat.NoNotice));

            model.BindNotice(notice);
            model.UnbindAll();
            var changes = Record(model);
            notice.Clear();
            Assert.That(changes, Is.Empty);
            Assert.That(model.NoticeText, Is.EqualTo(HudFormat.CheckpointRestart(1)), "UnbindAll keeps the text");
        }
    }
}
