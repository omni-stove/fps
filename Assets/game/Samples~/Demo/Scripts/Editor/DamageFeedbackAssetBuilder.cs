using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Editor
{
    public static class DamageFeedbackAssetBuilder
    {
        public const string LogTag = "[DamageFeedbackAssetBuilder]";

        public const string HeartbeatSound = "Heartbeat";
        public const string BreathSound = "BreathRecovery";
        public const string VignetteTextureName = "DamageVignette";

        // 画面に引き伸ばすので、なめらかなグラデーションに足りる大きさにする。
        public const int VignetteSize = 256;

        public const float VignetteInnerRadius = 0.55f;

        public const float VignetteOuterRadius = 1.15f;

        public static readonly Color32 VignetteColor = new Color32(200, 16, 16, 255);

        // 実行時に DamageFeedbackCatalog.Load で読めるよう、Resources の下に置く。
        public static string CatalogPath => PresentationAssetBuilder.PresentationFolder + "/Resources/" + DamageFeedbackCatalog.ResourcePath + ".asset";

        public static IReadOnlyList<SoundSpec> SoundSpecs()
        {
            return new[]
            {
                // 「ドッ・クン」の 2 打を 1 周期にし、くり返し鳴らして心拍 (約 70 回/分) にする。後ろは無音なのでつなぎ目で途切れない。
                new SoundSpec(HeartbeatSound, 3101u, 0.85f, 0.9f, new[]
                {
                    new SoundLayer(0f, 0.16f, 0.006f, 0.05f, 0f, 0f, 1f, 62f, 44f),
                    new SoundLayer(0f, 0.08f, 0.003f, 0.02f, 0.25f, 180f),
                    new SoundLayer(0.26f, 0.13f, 0.005f, 0.04f, 0f, 0f, 0.7f, 74f, 52f),
                    new SoundLayer(0.26f, 0.06f, 0.003f, 0.015f, 0.15f, 200f),
                }),

                new SoundSpec(BreathSound, 3201u, 2.9f, 0.6f, new[]
                {
                    new SoundLayer(0f, 0.5f, 0.22f, 0.3f, 0.55f, 2200f),
                    new SoundLayer(0.6f, 0.75f, 0.06f, 0.3f, 1f, 1100f),
                    new SoundLayer(1.5f, 0.45f, 0.2f, 0.28f, 0.35f, 2000f),
                    new SoundLayer(2.05f, 0.75f, 0.08f, 0.3f, 0.55f, 900f),
                }),
            };
        }

        public static Color32[] VignettePixels(int size)
        {
            if (size < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, "must be at least 2");
            }

            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var u = (((x + 0.5f) / size) * 2f) - 1f;
                    var v = (((y + 0.5f) / size) * 2f) - 1f;
                    var radius = Mathf.Sqrt((u * u) + (v * v));
                    var t = Mathf.Clamp01((radius - VignetteInnerRadius) / (VignetteOuterRadius - VignetteInnerRadius));
                    var alpha = t * t * (3f - (2f * t));
                    pixels[(y * size) + x] = new Color32(VignetteColor.r, VignetteColor.g, VignetteColor.b, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            return pixels;
        }

        public static DamageFeedbackCatalog LoadCatalog()
        {
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<DamageFeedbackCatalog>(CatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    $"damage feedback catalog '{CatalogPath}' was not found; run {PresentationAssetSteps.MenuPath} (Shooter.Editor.PresentationAssetSteps.BuildAll) first");
            }

            catalog.Validate();
            return catalog;
        }

        public static DamageFeedbackCatalog Build()
        {
            return BuildAt(PresentationAssetBuilder.AudioFolder, PresentationAssetBuilder.PresentationFolder, CatalogPath);
        }

        public static DamageFeedbackCatalog BuildAt(string audioFolder, string presentationFolder, string catalogPath)
        {
            audioFolder = PresentationAssetBuilder.ValidateFolderPath(audioFolder, nameof(audioFolder));
            presentationFolder = PresentationAssetBuilder.ValidateFolderPath(presentationFolder, nameof(presentationFolder));
            catalogPath = SceneBuildHelpers.ValidateAssetPath(catalogPath);

            SceneBuildHelpers.EnsureFolder(audioFolder);
            SceneBuildHelpers.EnsureFolder(presentationFolder);
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(catalogPath)?.Replace('\\', '/'));

            var clips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
            foreach (var spec in SoundSpecs())
            {
                clips.Add(spec.Name, PresentationAssetBuilder.WriteSound($"{audioFolder}/{spec.Name}.wav", spec));
            }

            var vignette = PresentationAssetBuilder.WriteTexture(
                $"{presentationFolder}/{VignetteTextureName}.asset", VignetteTextureName, VignetteSize, VignettePixels(VignetteSize), false);

            var catalog = SceneBuildHelpers.CreateOrUpdateAsset<DamageFeedbackCatalog>(catalogPath, c =>
            {
                // 既存のアセットを書き換える前に、同じ中身の一時インスタンスで検査する (不正ならメモリ上の中身も汚さない)。
                var probe = ScriptableObject.CreateInstance<DamageFeedbackCatalog>();
                try
                {
                    probe.name = Path.GetFileNameWithoutExtension(catalogPath);
                    Configure(probe, vignette, clips);
                    probe.Validate();
                }
                finally
                {
                    Object.DestroyImmediate(probe);
                }

                Configure(c, vignette, clips);
            });

            Debug.Log($"{LogTag} saved {clips.Count} sound(s) to '{audioFolder}', the vignette to '{presentationFolder}' and '{catalogPath}'");
            return catalog;
        }

        private static void Configure(DamageFeedbackCatalog catalog, Texture2D vignette, IReadOnlyDictionary<string, AudioClip> clips)
        {
            catalog.Vignette = vignette;
            catalog.Tint = Color.white;
            catalog.SetSettings(DamageFeedbackSettings.Default);
            catalog.Heartbeat = clips[HeartbeatSound];
            catalog.HeartbeatVolume = 0.6f;
            catalog.Breath = clips[BreathSound];
            catalog.BreathVolume = 0.5f;
        }
    }
}
