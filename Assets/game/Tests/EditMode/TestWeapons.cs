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

        public const string EveryPartId = "5d4c3b2a-1f0e-4d9c-8b7a-6f5e4d3c2b1a";
        public const string EveryPartStandardAmmoId = "1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d";
        public const string EveryPartPiercingAmmoId = "2b3c4d5e-6f7a-4b8c-9d0e-1f2a3b4c5d6e";

        /// <summary>省略できる部品をすべて持つ武器。読み書きと、アセットへの写しで何も落ちないことを確かめるのに使う。</summary>
        public static WeaponSpec EveryPartSpec()
        {
            var burning = new StatusEffectSpec(3f, 0.5f, 4, "fx.burning");
            return new WeaponSpec(
                EveryPartId,
                "weapon.everything",
                WeaponCategory.Launcher,
                new TriggerSpec(
                    FireMode.Semi,
                    0.25f,
                    new BurstSpec(3, 0.05f),
                    new ChargeSpec(1.5f, 0.2f, 2.5f, firesWhenFull: true),
                    new SpinUpSpec(0.8f, 3f, 0.4f),
                    new CycleSpec(0.9f, leavesAds: true)),
                new AmmoSpec(1, 6, 0f, 0.3f, 0.2f, 0.7f, ReloadStyle.PerRound, 0.6f, AmmoKind.Heat, new HeatSpec(0.1f, 0.5f, 0.3f, 2f)),
                new RoundSpec(
                    RoundDelivery.Projectile,
                    300f,
                    pelletCount: 8,
                    pelletSpreadDegrees: 4f,
                    radius: 0.2f,
                    penetration: new PenetrationSpec(
                        2, 0.6f, 0.5f, new[] { new PenetrationMaterialSpec("metal", 0f), new PenetrationMaterialSpec("wood", 2f) }),
                    ricochet: new RicochetSpec(1, 20f, 0.5f),
                    projectile: new ProjectileSpec(40f, 0.5f, 6f, WorldHitResponse.Stick, "fx.bolt"),
                    homing: new HomingSpec(1f, 10f, 90f)),
                new ImpactSpec(
                    90,
                    new DamageFalloff(10f, 30f, 0.25f),
                    3f,
                    new ExplosionSpec(new ExplosionDamage(150, 6f, 1.5f), 200, DetonationTrigger.OnLifetimeEnd),
                    burning,
                    new AreaEffectSpec(4f, 8f, 1f, 5, burning, "fx.fire")),
                new AimSpec(1f, 0.2f, 0.5f, 3f, 1f, 20f, 0.25f, 6f, new[] { 30f, 15f }, new BreathHoldSpec(4f, 2f, 0.1f), 1.5f),
                new[]
                {
                    new AmmoTypeSpec(EveryPartStandardAmmoId, "ammo.everything.standard"),
                    new AmmoTypeSpec(
                        EveryPartPiercingAmmoId,
                        "ammo.everything.piercing",
                        new RoundSpec(RoundDelivery.Spray, 80f, spray: new SpraySpec(0.1f, 12f, "fx.flame")),
                        new ImpactSpec(40, structureRadius: 1f, explosion: new ExplosionSpec(new ExplosionDamage(20, 2f)))),
                    new AmmoTypeSpec(
                        "3c4d5e6f-7a8b-4c9d-8e0f-2a3b4c5d6e7f",
                        "ammo.everything.beam",
                        new RoundSpec(RoundDelivery.Beam, 60f, penetration: new PenetrationSpec(1, 0.5f, 0.2f), beam: new BeamSpec(0.05f, "fx.beam")),
                        new ImpactSpec(30, statusEffect: burning)),
                },
                shotNoiseRadius: 12f);
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
