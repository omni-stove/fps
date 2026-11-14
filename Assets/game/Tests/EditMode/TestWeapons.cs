using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// テストで使う武器の値。ゲームの武器 (サンプルの Weapons) の値を変えてもテストの前提が動かないよう、テストの側に持つ。
    /// </summary>
    public static class TestWeapons
    {
        public const string RifleId = "6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83";
        public const string HandgunId = "b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45";
        public const string LauncherId = "e7a3c5d1-2f84-4b69-a0d3-5c8e1b7f2a96";

        public const string RifleNameKey = "weapon.rifle";
        public const string HandgunNameKey = "weapon.handgun";
        public const string LauncherNameKey = "weapon.launcher";

        public const float RifleAdsFieldOfView = 45f;
        public const float HandgunAdsFieldOfView = 55f;
        public const float LauncherExplosionRadius = 4f;

        /// <summary>全自動の小銃 (30/90 発、0.1 秒、リロード 2 秒、25 ダメージ、射程 100m、20〜60m で 0.5 倍まで減衰)。</summary>
        public static WeaponSpec RifleSpec()
        {
            return new WeaponSpec(
                RifleId,
                RifleNameKey,
                WeaponCategory.AssaultRifle,
                FireMode.Auto,
                30,
                90,
                0.1f,
                2f,
                25,
                100f,
                new DamageFalloff(20f, 60f, 0.5f),
                adsFieldOfView: RifleAdsFieldOfView,
                blindFireSpreadDegrees: 8f,
                activeReloadWindowStart: 0.5f,
                activeReloadWindowLength: 0.15f,
                activeReloadFailSeconds: 0.6f,
                recoilPitchDegrees: 0.5f,
                recoilYawDegrees: 0.2f,
                recoilRecoveryPerSecond: 10f,
                hipSpreadDegrees: 3f,
                adsSpreadDegrees: 0.5f,
                moveSpreadDegrees: 3f);
        }

        /// <summary>単発の拳銃 (12/48 発、0.2 秒、リロード 1.5 秒、34 ダメージ、射程 60m、15〜40m で 0.6 倍まで減衰)。</summary>
        public static WeaponSpec HandgunSpec()
        {
            return new WeaponSpec(
                HandgunId,
                HandgunNameKey,
                WeaponCategory.Handgun,
                FireMode.Semi,
                12,
                48,
                0.2f,
                1.5f,
                34,
                60f,
                new DamageFalloff(15f, 40f, 0.6f),
                adsFieldOfView: HandgunAdsFieldOfView,
                blindFireSpreadDegrees: 12f,
                activeReloadWindowStart: 0.45f,
                activeReloadWindowLength: 0.2f,
                activeReloadFailSeconds: 0.4f,
                recoilPitchDegrees: 2.5f,
                recoilYawDegrees: 0.6f,
                recoilRecoveryPerSecond: 60f,
                hipSpreadDegrees: 2f,
                adsSpreadDegrees: 0.8f,
                moveSpreadDegrees: 1.5f);
        }

        /// <summary>爆発する発射機 (1/4 発、0.5 秒、リロード 3 秒、120 ダメージ、射程 150m、減衰なし、半径 4m)。</summary>
        public static WeaponSpec LauncherSpec()
        {
            return new WeaponSpec(
                LauncherId,
                LauncherNameKey,
                WeaponCategory.Launcher,
                FireMode.Semi,
                1,
                4,
                0.5f,
                3f,
                120,
                150f,
                DamageFalloff.None,
                explosionRadius: LauncherExplosionRadius);
        }

        /// <summary>スロット 0 に Handgun 以外のすべての種別で rifle、スロット 1 に Handgun で handgun を入れたロードアウト。</summary>
        public static LoadoutSlotDefinition[] LoadoutSlots(WeaponDefinition rifle, WeaponDefinition handgun)
        {
            var primary = new List<WeaponCategory>();
            foreach (WeaponCategory category in Enum.GetValues(typeof(WeaponCategory)))
            {
                if (category != WeaponCategory.Handgun)
                {
                    primary.Add(category);
                }
            }

            return new[]
            {
                new LoadoutSlotDefinition(primary.ToArray(), rifle),
                new LoadoutSlotDefinition(new[] { WeaponCategory.Handgun }, handgun),
            };
        }
    }
}
