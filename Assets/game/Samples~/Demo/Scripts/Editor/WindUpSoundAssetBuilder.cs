using System;
using System.Collections.Generic;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    public static class WindUpSoundAssetBuilder
    {
        public const string MenuPath = "Shooter/Build Wind-Up Sounds";
        public const string LogTag = "[WindUpSoundAssetBuilder]";

        public const string ChargeRifleId = "0a90a121-a287-4aae-8fcf-885ddf59da79";
        public const string MinigunId = "4d8e2a6f-1b3c-4e5d-9f7a-8c0b2d4e6f13";
        public const string BoltSniperId = "5b2af293-a0d8-45cb-af68-8c63628c80d1";

        public const string SpinLoopSound = "MinigunSpin";
        public const string ChargeLoopSound = "ChargeHum";
        public const string ChargeReadySound = "ChargeReady";
        public const string BoltCycleSound = "BoltCycle";

        public const float SpinStartVolume = 0.1f;
        public const float SpinFullVolume = 0.6f;

        // 1 周の頭と尻で波形がずれるとループの継ぎ目で鳴るので、ノイズを主にして音程の成分は控えめにする。
        public static IReadOnlyList<SoundSpec> SoundSpecs()
        {
            return new[]
            {
                new SoundSpec(SpinLoopSound, 3101u, 1f, 0.3f, new[]
                {
                    new SoundLayer(0f, 1f, 0f, 1000f, 0.6f, 900f, 0.15f, 110f, 110f, ToneWaveform.Saw),
                    new SoundLayer(0f, 1f, 0f, 1000f, 0f, 0f, 0.15f, 220f, 220f),
                }),
                new SoundSpec(ChargeLoopSound, 3201u, 1f, 0.4f, new[]
                {
                    new SoundLayer(0f, 1f, 0f, 1000f, 0.15f, 3000f, 0.6f, 440f, 440f),
                    new SoundLayer(0f, 1f, 0f, 1000f, 0f, 0f, 0.25f, 880f, 880f),
                }),
                new SoundSpec(ChargeReadySound, 3202u, 0.35f, 0.6f, new[]
                {
                    new SoundLayer(0f, 0.35f, 0.002f, 0.12f, 0f, 0f, 0.8f, 1320f, 1760f),
                    new SoundLayer(0f, 0.1f, 0.001f, 0.02f, 0.3f, 8000f),
                }),
                new SoundSpec(BoltCycleSound, 3301u, 0.8f, 0.6f, new[]
                {
                    new SoundLayer(0.25f, 0.08f, 0.0003f, 0.015f, 1f, 5000f, 0.3f, 1800f, 1400f),
                    new SoundLayer(0.55f, 0.1f, 0.0003f, 0.02f, 1f, 4000f, 0.35f, 1300f, 1000f),
                }),
            };
        }

        [MenuItem(MenuPath)]
        public static void Build()
        {
            BuildAt(PresentationAssetBuilder.AudioFolder, WeaponAssetBuilder.LoadRangeWeapons());
        }

        public static void BuildAt(string audioFolder, WeaponCatalogAsset weapons)
        {
            if (weapons == null)
            {
                throw new ArgumentNullException(nameof(weapons));
            }

            audioFolder = PresentationAssetBuilder.ValidateFolderPath(audioFolder, nameof(audioFolder));
            var minigun = Find(weapons, MinigunId);
            var chargeRifle = Find(weapons, ChargeRifleId);
            var boltSniper = Find(weapons, BoltSniperId);

            SceneBuildHelpers.EnsureFolder(audioFolder);
            var clips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
            foreach (var spec in SoundSpecs())
            {
                clips.Add(spec.Name, PresentationAssetBuilder.WriteSound($"{audioFolder}/{spec.Name}.wav", spec));
            }

            minigun.SpinUp.Sound.Loop = clips[SpinLoopSound];
            minigun.SpinUp.Sound.StartVolume = SpinStartVolume;
            minigun.SpinUp.Sound.FullVolume = SpinFullVolume;
            chargeRifle.Charge.Sound.Loop = clips[ChargeLoopSound];
            chargeRifle.Charge.Sound.ReadySound = clips[ChargeReadySound];
            boltSniper.Cycle.Sound = clips[BoltCycleSound];

            foreach (var definition in new[] { minigun, chargeRifle, boltSniper })
            {
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssetIfDirty(definition);
            }

            Debug.Log($"{LogTag} saved {clips.Count} sound(s) to '{audioFolder}' and wired them to the minigun, charge rifle and bolt sniper");
        }

        private static WeaponDefinition Find(WeaponCatalogAsset weapons, string weaponId)
        {
            var definition = WeaponAssetBuilder.FindInCatalog(weapons, nameof(WindUpSoundAssetBuilder), weaponId);
            if (definition.SpinUp.Enabled || definition.Charge.Enabled || definition.Cycle.Enabled)
            {
                return definition;
            }

            throw new InvalidOperationException($"weapon '{definition.name}' ({weaponId}) has no spin-up, charge or cycle; it has no wind-up to sound");
        }
    }
}
