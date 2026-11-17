using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Editor
{
    public sealed class FootstepSurfaceAssetStep : IPresentationAssetStep
    {
        public const string LogTag = "[FootstepSurfaceAssetStep]";

        // PresentationCatalog の手順 (PresentationCatalogAssetStep.StepOrder) より後にする。
        public const int StepOrder = 100;

        public const string ConcreteId = "concrete";
        public const string MetalId = "metal";
        public const string WoodId = "wood";

        // 同じ音の連続を避けて選べるよう、材質ごとに足音を複数作る。
        public const int FootstepVariants = 3;

        // 同じ材質でも少しずつ違う音にするため、種類ごとに音程をずらす。
        private static readonly float[] VariantPitch = { 1f, 0.93f, 1.08f };

        public static string AudioFolder => PresentationAssetBuilder.AudioFolder + "/Footsteps";

        // 実行時に FootstepSurfaceCatalog.Load で読めるよう Resources の下に置く。
        public static string CatalogPath =>
            PresentationAssetBuilder.PresentationFolder + "/Resources/" + FootstepSurfaceCatalog.ResourcePath + ".asset";

        public string Name => "FootstepSurfaceCatalog";

        public int Order => StepOrder;

        public void Build()
        {
            BuildAt(AudioFolder, CatalogPath);
        }

        public static IReadOnlyList<string> Surfaces()
        {
            return new[] { SurfaceMaterial.DefaultId, ConcreteId, MetalId, WoodId };
        }

        public static string FootstepSoundName(string materialId, int variant)
        {
            return "Footstep_" + materialId + "_" + (variant + 1).ToString(CultureInfo.InvariantCulture);
        }

        public static string LandingSoundName(string materialId)
        {
            return "Landing_" + materialId;
        }

        public static (IReadOnlyList<SoundSpec> Footsteps, SoundSpec Landing) SoundSpecsFor(string materialId)
        {
            var index = IndexOf(materialId);
            var footsteps = new SoundSpec[FootstepVariants];
            for (var v = 0; v < FootstepVariants; v++)
            {
                var seed = 3000u + ((uint)index * 100u) + (uint)v;
                footsteps[v] = new SoundSpec(FootstepSoundName(materialId, v), seed, FootstepLength(materialId), 0.6f, FootstepLayers(materialId, VariantPitch[v]));
            }

            var landing = new SoundSpec(LandingSoundName(materialId), 3000u + ((uint)index * 100u) + 50u, LandingLength(materialId), 0.75f, LandingLayers(materialId));
            return (footsteps, landing);
        }

        public static FootstepSurfaceCatalog BuildAt(string audioFolder, string catalogPath)
        {
            audioFolder = PresentationAssetBuilder.ValidateFolderPath(audioFolder, nameof(audioFolder));
            catalogPath = SceneBuildHelpers.ValidateAssetPath(catalogPath);

            SceneBuildHelpers.EnsureFolder(audioFolder);
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(catalogPath)?.Replace('\\', '/'));

            var surfaces = new List<FootstepSurfaceSounds>();
            var written = 0;
            foreach (var materialId in Surfaces())
            {
                var specs = SoundSpecsFor(materialId);
                var footsteps = new List<AudioClip>();
                foreach (var spec in specs.Footsteps)
                {
                    footsteps.Add(PresentationAssetBuilder.WriteSound($"{audioFolder}/{spec.Name}.wav", spec));
                    written++;
                }

                var landing = PresentationAssetBuilder.WriteSound($"{audioFolder}/{specs.Landing.Name}.wav", specs.Landing);
                written++;
                surfaces.Add(new FootstepSurfaceSounds(materialId, footsteps, new[] { landing }));
            }

            var catalog = SceneBuildHelpers.CreateOrUpdateAsset<FootstepSurfaceCatalog>(catalogPath, c =>
            {
                // 既存のアセットを書き換える前に、同じ中身の一時インスタンスで検査する (不正ならメモリ上の中身も汚さない)。
                var probe = ScriptableObject.CreateInstance<FootstepSurfaceCatalog>();
                try
                {
                    probe.name = Path.GetFileNameWithoutExtension(catalogPath);
                    probe.SetSurfaces(surfaces);
                    probe.Validate();
                }
                finally
                {
                    Object.DestroyImmediate(probe);
                }

                c.SetSurfaces(surfaces);
            });

            Debug.Log($"{LogTag} saved {written} sound(s) for {surfaces.Count} surface(s) to '{audioFolder}' and '{catalogPath}'");
            return catalog;
        }

        private static int IndexOf(string materialId)
        {
            var surfaces = Surfaces();
            for (var i = 0; i < surfaces.Count; i++)
            {
                if (string.Equals(surfaces[i], materialId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            throw new ArgumentException($"unknown footstep surface '{materialId}'", nameof(materialId));
        }

        private static float FootstepLength(string materialId)
        {
            return materialId == MetalId ? 0.35f : 0.2f;
        }

        private static float LandingLength(string materialId)
        {
            return materialId == MetalId ? 0.7f : 0.45f;
        }

        private static SoundLayer[] FootstepLayers(string materialId, float pitch)
        {
            switch (materialId)
            {
                case ConcreteId:
                    return new[]
                    {
                        new SoundLayer(0f, 0.12f, 0.002f, 0.02f, 1f, 1800f * pitch, 0.2f, 120f * pitch, 80f * pitch),
                        new SoundLayer(0f, 0.04f, 0.0005f, 0.006f, 0.5f, 6000f * pitch),
                    };
                case MetalId:
                    return new[]
                    {
                        new SoundLayer(0f, 0.05f, 0.0005f, 0.01f, 0.8f, 4000f * pitch),
                        new SoundLayer(0f, 0.34f, 0.001f, 0.09f, 0f, 0f, 0.5f, 820f * pitch, 790f * pitch),
                        new SoundLayer(0f, 0.25f, 0.001f, 0.05f, 0f, 0f, 0.3f, 1370f * pitch, 1330f * pitch),
                    };
                case WoodId:
                    return new[]
                    {
                        new SoundLayer(0f, 0.15f, 0.002f, 0.04f, 0.6f, 900f * pitch, 0.6f, 230f * pitch, 160f * pitch),
                        new SoundLayer(0.01f, 0.06f, 0.002f, 0.015f, 0.25f, 2500f * pitch),
                    };
                default:
                    // 既存の PresentationCatalog の足音と同じ作り
                    return new[]
                    {
                        new SoundLayer(0f, 0.15f, 0.004f, 0.03f, 1f, 500f * pitch, 0.3f, 90f * pitch, 60f * pitch),
                        new SoundLayer(0f, 0.05f, 0.001f, 0.01f, 0.3f, 3000f * pitch),
                    };
            }
        }

        private static SoundLayer[] LandingLayers(string materialId)
        {
            switch (materialId)
            {
                case ConcreteId:
                    return new[]
                    {
                        new SoundLayer(0f, 0.35f, 0.002f, 0.06f, 1f, 1200f, 0.4f, 90f, 50f),
                        new SoundLayer(0f, 0.08f, 0.0005f, 0.015f, 0.6f, 5000f),
                    };
                case MetalId:
                    return new[]
                    {
                        new SoundLayer(0f, 0.1f, 0.0005f, 0.02f, 1f, 3500f),
                        new SoundLayer(0f, 0.68f, 0.001f, 0.2f, 0f, 0f, 0.6f, 560f, 530f),
                        new SoundLayer(0f, 0.5f, 0.001f, 0.12f, 0f, 0f, 0.35f, 930f, 900f),
                    };
                case WoodId:
                    return new[]
                    {
                        new SoundLayer(0f, 0.4f, 0.003f, 0.08f, 0.7f, 700f, 0.7f, 160f, 100f),
                        new SoundLayer(0.02f, 0.15f, 0.003f, 0.03f, 0.3f, 2200f),
                    };
                default:
                    // 既存の PresentationCatalog の着地音と同じ作り
                    return new[]
                    {
                        new SoundLayer(0f, 0.4f, 0.003f, 0.09f, 1f, 350f, 0.6f, 75f, 40f),
                        new SoundLayer(0f, 0.1f, 0.001f, 0.02f, 0.4f, 2000f),
                    };
            }
        }
    }
}
