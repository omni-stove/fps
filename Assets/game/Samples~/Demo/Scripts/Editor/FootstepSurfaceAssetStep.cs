using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Fps.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Editor
{
    /// <summary>
    /// 床の材質ごとの足音と着地音の仮素材を手続きで作り (<see cref="ProceduralAudio"/>)、<see cref="FootstepSurfaceCatalog"/> にまとめる手順。
    /// 材質は <see cref="Surfaces"/> の default / concrete / metal / wood で、足音は材質ごとに <see cref="FootstepVariants"/> 種類
    /// (同じ音の連続を避けて選ぶため)、着地音は 1 種類。音は <see cref="AudioFolder"/> に WAV で、カタログは <see cref="CatalogPath"/> に置く。
    /// 既存のアセットは GUID を保って中身だけ更新し、同じコードからは同じファイルができる。
    /// <see cref="PresentationAssetSteps.BuildAll"/> から呼ばれる。
    /// </summary>
    public sealed class FootstepSurfaceAssetStep : IPresentationAssetStep
    {
        public const string LogTag = "[FootstepSurfaceAssetStep]";

        /// <summary>この手順の <see cref="Order"/>。PresentationCatalog の手順 (<see cref="PresentationCatalogAssetStep.StepOrder"/>) より後にする。</summary>
        public const int StepOrder = 100;

        public const string ConcreteId = "concrete";
        public const string MetalId = "metal";
        public const string WoodId = "wood";

        /// <summary>材質ごとの足音の種類の数。</summary>
        public const int FootstepVariants = 3;

        /// <summary>足音の種類ごとに音程に掛ける倍率。同じ材質でも少しずつ違う音にする。</summary>
        private static readonly float[] VariantPitch = { 1f, 0.93f, 1.08f };

        public static string AudioFolder => PresentationAssetBuilder.AudioFolder + "/Footsteps";

        /// <summary>カタログの置き場所。実行時に <see cref="FootstepSurfaceCatalog.Load"/> で読めるよう Resources の下に置く。</summary>
        public static string CatalogPath =>
            PresentationAssetBuilder.PresentationFolder + "/Resources/" + FootstepSurfaceCatalog.ResourcePath + ".asset";

        public string Name => "FootstepSurfaceCatalog";

        public int Order => StepOrder;

        public void Build()
        {
            BuildAt(AudioFolder, CatalogPath);
        }

        /// <summary>作る材質の Id。先頭は <see cref="SurfaceMaterial.DefaultId"/>。</summary>
        public static IReadOnlyList<string> Surfaces()
        {
            return new[] { SurfaceMaterial.DefaultId, ConcreteId, MetalId, WoodId };
        }

        /// <summary>materialId の variant 番目 (0 から) の足音のファイル名 (拡張子なし)。</summary>
        public static string FootstepSoundName(string materialId, int variant)
        {
            return "Footstep_" + materialId + "_" + (variant + 1).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>materialId の着地音のファイル名 (拡張子なし)。</summary>
        public static string LandingSoundName(string materialId)
        {
            return "Landing_" + materialId;
        }

        /// <summary>materialId の足音 (<see cref="FootstepVariants"/> 個) と着地音 (1 個) の設計図。</summary>
        /// <exception cref="ArgumentException">materialId が <see cref="Surfaces"/> に無い。</exception>
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

        /// <summary>
        /// audioFolder に足音と着地音を、catalogPath にそれをまとめたカタログを作るか上書きして保存し、カタログを返す。
        /// フォルダは Assets/ 以下で、無ければ作る。カタログは書き込む前に <see cref="FootstepSurfaceCatalog.Validate"/> に通す。
        /// </summary>
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
                    // 硬い面: 低音の胴鳴りを抑え、高めのノイズの擦れを短く立てる
                    return new[]
                    {
                        new SoundLayer(0f, 0.12f, 0.002f, 0.02f, 1f, 1800f * pitch, 0.2f, 120f * pitch, 80f * pitch),
                        new SoundLayer(0f, 0.04f, 0.0005f, 0.006f, 0.5f, 6000f * pitch),
                    };
                case MetalId:
                    // 金属: 短い打撃のあとに、非整数倍の 2 つの音程が尾を引いて鳴る
                    return new[]
                    {
                        new SoundLayer(0f, 0.05f, 0.0005f, 0.01f, 0.8f, 4000f * pitch),
                        new SoundLayer(0f, 0.34f, 0.001f, 0.09f, 0f, 0f, 0.5f, 820f * pitch, 790f * pitch),
                        new SoundLayer(0f, 0.25f, 0.001f, 0.05f, 0f, 0f, 0.3f, 1370f * pitch, 1330f * pitch),
                    };
                case WoodId:
                    // 木: 中低域のこもった打音と、軽いきしみ
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
