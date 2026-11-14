using System;
using System.Collections.Generic;
using System.IO;
using Fps.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Editor
{
    /// <summary>
    /// 材質ごとの着弾の仮素材 (銃痕のテクスチャと材質・着弾のエフェクト・着弾音) と <see cref="SurfaceImpactCatalog"/> を作る手順。
    /// 材質は <see cref="SurfaceMaterial.DefaultId"/>・<see cref="ConcreteId"/>・<see cref="MetalId"/>・<see cref="WoodId"/> の 4 つ (<see cref="Specs"/>)。
    /// 銃痕のテクスチャは材質ごとの色で手続きに描き、音は <see cref="ProceduralAudio"/> で合成する。粒の材質とテクスチャも自分のフォルダに作り、
    /// ほかの手順の生成物には頼らない。既存のアセットは GUID を保って中身だけ更新し、同じコードからは同じファイルができる。
    /// <see cref="PresentationAssetSteps.BuildAll"/> から実行される。
    /// </summary>
    public sealed class SurfaceImpactAssetStep : IPresentationAssetStep
    {
        public const string LogTag = "[SurfaceImpactAssetStep]";

        /// <summary>この手順の <see cref="Order"/>。</summary>
        public const int StepOrder = PresentationCatalogAssetStep.StepOrder + 10;

        public const string ConcreteId = "concrete";
        public const string MetalId = "metal";
        public const string WoodId = "wood";

        /// <summary>銃痕のテクスチャの一辺 (px)。</summary>
        public const int DecalTextureSize = 128;

        public const string DotTextureName = "ImpactDot";
        public const string DustMaterialName = "ImpactDust";
        public const string SparkMaterialName = "ImpactSparks";

        // 銃痕のテクスチャの形 (中心からの距離を、テクスチャの半分の幅を 1 として測る)。
        private const float HoleRadius = 0.24f;
        private const float RimRadius = 0.6f;
        private const float RimFeather = 0.06f;
        private const float HaloRadius = 0.95f;
        private const float HaloAlpha = 0.3f;

        private static readonly int[] WobbleFrequencies = { 3, 5, 7, 11 };
        private static readonly float[] WobbleAmplitudes = { 0.05f, 0.04f, 0.03f, 0.02f };

        public static string AudioFolder => FpsKitRoot.OutputPath("Audio/SurfaceImpact");

        public static string AssetFolder => FpsKitRoot.OutputPath("Presentation/SurfaceImpact");

        /// <summary>カタログの置き場所。実行時に <see cref="SurfaceImpactCatalog.Load"/> で読めるよう、Resources の <see cref="SurfaceImpactCatalog.ResourcePath"/> に置く。</summary>
        public static string CatalogPath => PresentationAssetBuilder.PresentationFolder + "/Resources/" + SurfaceImpactCatalog.ResourcePath + ".asset";

        public string Name => "SurfaceImpactCatalog";

        public int Order => StepOrder;

        public void Build()
        {
            BuildAt(AudioFolder, AssetFolder, CatalogPath);
        }

        /// <summary>材質ごとの仮素材の設計図。材質の Id は重複しない。</summary>
        public static IReadOnlyList<SurfaceImpactSpec> Specs()
        {
            return new[]
            {
                new SurfaceImpactSpec(
                    SurfaceMaterial.DefaultId, "Default", SurfaceImpactStyle.Dust, 0.12f,
                    new Color(0.36f, 0.36f, 0.36f), new Color(0.05f, 0.05f, 0.05f), new Color(0.55f, 0.55f, 0.55f, 0.7f), 3101u,
                    new SoundSpec("ImpactDefault", 3101u, 0.25f, 0.8f, new[]
                    {
                        new SoundLayer(0f, 0.1f, 0.0003f, 0.01f, 1f, 8000f),
                        new SoundLayer(0f, 0.2f, 0.001f, 0.04f, 0.4f, 2500f, 0.35f, 700f, 350f),
                    })),
                new SurfaceImpactSpec(
                    ConcreteId, "Concrete", SurfaceImpactStyle.Dust, 0.14f,
                    new Color(0.62f, 0.6f, 0.56f), new Color(0.08f, 0.075f, 0.07f), new Color(0.72f, 0.68f, 0.6f, 0.8f), 3102u,
                    new SoundSpec("ImpactConcrete", 3102u, 0.3f, 0.8f, new[]
                    {
                        new SoundLayer(0f, 0.08f, 0.0003f, 0.012f, 1f, 6000f),
                        new SoundLayer(0f, 0.25f, 0.001f, 0.06f, 0.6f, 1800f, 0.3f, 220f, 140f),
                    })),
                new SurfaceImpactSpec(
                    MetalId, "Metal", SurfaceImpactStyle.Sparks, 0.08f,
                    new Color(0.76f, 0.78f, 0.82f), new Color(0.16f, 0.16f, 0.18f), new Color(1f, 0.85f, 0.5f), 3103u,
                    new SoundSpec("ImpactMetal", 3103u, 0.6f, 0.75f, new[]
                    {
                        new SoundLayer(0f, 0.05f, 0.0002f, 0.006f, 0.8f, 10000f),
                        new SoundLayer(0f, 0.6f, 0.0005f, 0.18f, 0f, 0f, 0.7f, 2600f, 2450f),
                        new SoundLayer(0f, 0.6f, 0.0005f, 0.22f, 0f, 0f, 0.35f, 4100f, 3950f),
                    })),
                new SurfaceImpactSpec(
                    WoodId, "Wood", SurfaceImpactStyle.Chips, 0.12f,
                    new Color(0.55f, 0.38f, 0.2f), new Color(0.12f, 0.07f, 0.03f), new Color(0.6f, 0.42f, 0.22f), 3104u,
                    new SoundSpec("ImpactWood", 3104u, 0.25f, 0.8f, new[]
                    {
                        new SoundLayer(0f, 0.15f, 0.0005f, 0.03f, 0.7f, 2200f, 0.6f, 420f, 300f),
                        new SoundLayer(0f, 0.05f, 0.0003f, 0.008f, 0.4f, 5000f),
                    })),
            };
        }

        /// <summary>
        /// audioFolder に着弾音を、assetFolder に銃痕のテクスチャと材質・エフェクトのプレハブ・粒の材質を、catalogPath にそれをまとめたカタログを
        /// 作るか上書きして保存し、カタログを返す。フォルダは Assets/ 以下で、無ければ作る。カタログは書き換える前に
        /// <see cref="SurfaceImpactCatalog.Validate"/> に通す。
        /// </summary>
        public static SurfaceImpactCatalog BuildAt(string audioFolder, string assetFolder, string catalogPath)
        {
            audioFolder = PresentationAssetBuilder.ValidateFolderPath(audioFolder, nameof(audioFolder));
            assetFolder = PresentationAssetBuilder.ValidateFolderPath(assetFolder, nameof(assetFolder));
            catalogPath = SceneBuildHelpers.ValidateAssetPath(catalogPath);

            SceneBuildHelpers.EnsureFolder(audioFolder);
            SceneBuildHelpers.EnsureFolder(assetFolder);
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(catalogPath)?.Replace('\\', '/'));

            var dot = PresentationAssetBuilder.BuildSoftDot($"{assetFolder}/{DotTextureName}.asset");
            var dust = PresentationAssetBuilder.BuildParticleMaterial($"{assetFolder}/{DustMaterialName}.mat", dot, PresentationAssetBuilder.BlendAlpha);
            var sparks = PresentationAssetBuilder.BuildParticleMaterial($"{assetFolder}/{SparkMaterialName}.mat", dot, PresentationAssetBuilder.BlendAdditive);

            var surfaces = new List<SurfaceImpactAssets>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var spec in Specs())
            {
                if (!seen.Add(spec.MaterialId))
                {
                    throw new InvalidOperationException($"surface material id '{spec.MaterialId}' is used more than once");
                }

                var texture = PresentationAssetBuilder.WriteTexture(
                    $"{assetFolder}/{spec.DecalTextureName}.asset",
                    spec.DecalTextureName,
                    DecalTextureSize,
                    DecalPixels(DecalTextureSize, spec.RimColor, spec.HoleColor, spec.Seed),
                    true);
                var decal = PresentationAssetBuilder.BuildParticleMaterial(
                    $"{assetFolder}/{spec.DecalMaterialName}.mat", texture, PresentationAssetBuilder.BlendAlpha);
                var effect = PresentationAssetBuilder.SavePrefab(
                    assetFolder, spec.EffectName, root => AddImpactParticles(root, spec, spec.Style == SurfaceImpactStyle.Sparks ? sparks : dust));
                var sound = PresentationAssetBuilder.WriteSound($"{audioFolder}/{spec.Sound.Name}.wav", spec.Sound);
                surfaces.Add(new SurfaceImpactAssets(spec.MaterialId, decal, spec.DecalSize, effect, sound));
            }

            var catalog = SceneBuildHelpers.CreateOrUpdateAsset<SurfaceImpactCatalog>(catalogPath, c =>
            {
                // 既存のアセットを書き換える前に、同じ中身の一時インスタンスで検査する (不正ならメモリ上の中身も汚さない)。
                var probe = ScriptableObject.CreateInstance<SurfaceImpactCatalog>();
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

            Debug.Log($"{LogTag} saved {surfaces.Count} surface(s) ({string.Join(", ", seen)}) to '{assetFolder}', '{audioFolder}' and '{catalogPath}'");
            return catalog;
        }

        /// <summary>
        /// 一辺 size の銃痕のテクスチャの画素 (下の行から)。中心の穴を hole の色、そのまわりのささくれた縁を rim の色で塗り、
        /// 縁の外は薄い焦げを残して端に向かって透明にする (四隅と各辺の中点は透明)。縁の揺らぎと粒は seed で決まり、同じ引数なら同じ画素になる。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">size が 2 未満。</exception>
        public static Color32[] DecalPixels(int size, Color rim, Color hole, uint seed)
        {
            if (size < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, "must be at least 2");
            }

            var phases = new float[WobbleFrequencies.Length];
            for (var i = 0; i < phases.Length; i++)
            {
                phases[i] = Hash01(i, -1, seed) * Mathf.PI * 2f;
            }

            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - center) / center;
                    var dy = (y - center) / center;
                    var r = Mathf.Sqrt((dx * dx) + (dy * dy));
                    var angle = Mathf.Atan2(dy, dx);

                    var wobble = 0f;
                    for (var i = 0; i < phases.Length; i++)
                    {
                        wobble += WobbleAmplitudes[i] * Mathf.Sin((WobbleFrequencies[i] * angle) + phases[i]);
                    }

                    var holeRadius = HoleRadius + (wobble * 0.5f);
                    var rimRadius = RimRadius + wobble;

                    Color color;
                    float alpha;
                    if (r <= rimRadius)
                    {
                        color = Color.Lerp(hole, rim, SmoothStep(holeRadius, holeRadius + 0.12f, r));
                        alpha = Mathf.Lerp(1f, HaloAlpha, SmoothStep(rimRadius - RimFeather, rimRadius, r));
                    }
                    else
                    {
                        color = rim * 0.6f;
                        alpha = HaloAlpha * (1f - SmoothStep(rimRadius, HaloRadius, r));
                    }

                    var grain = 0.85f + (0.3f * Hash01(x, y, seed));
                    pixels[(y * size) + x] = new Color32(
                        ToByte(color.r * grain), ToByte(color.g * grain), ToByte(color.b * grain), ToByte(alpha));
                }
            }

            return pixels;
        }

        private static void AddImpactParticles(GameObject root, SurfaceImpactSpec spec, Material material)
        {
            PresentationAssetBuilder.ParticleSettings settings;
            switch (spec.Style)
            {
                case SurfaceImpactStyle.Sparks:
                    settings = new PresentationAssetBuilder.ParticleSettings
                    {
                        Duration = 0.2f,
                        Lifetime = new Vector2(0.15f, 0.4f),
                        Speed = new Vector2(4f, 10f),
                        Size = new Vector2(0.02f, 0.045f),
                        Gravity = 1f,
                        Burst = 18,
                        MaxParticles = 18,
                        ShapeAngle = 40f,
                        Fade = PresentationAssetBuilder.Fade(Color.white, new Color(1f, 0.45f, 0.15f), 1f, 0f),
                        Stretched = true,
                        LengthScale = 1f,
                        VelocityScale = 0.02f,
                    };
                    break;
                case SurfaceImpactStyle.Chips:
                    settings = new PresentationAssetBuilder.ParticleSettings
                    {
                        Duration = 0.2f,
                        Lifetime = new Vector2(0.4f, 0.8f),
                        Speed = new Vector2(2f, 5f),
                        Size = new Vector2(0.02f, 0.05f),
                        Gravity = 1.5f,
                        Burst = 12,
                        MaxParticles = 12,
                        ShapeAngle = 35f,
                        RandomRotation = true,
                        Fade = PresentationAssetBuilder.Fade(Color.white, Color.white, 1f, 0f),
                    };
                    break;
                default:
                    settings = new PresentationAssetBuilder.ParticleSettings
                    {
                        Duration = 0.3f,
                        Lifetime = new Vector2(0.5f, 0.9f),
                        Speed = new Vector2(0.6f, 2.2f),
                        Size = new Vector2(0.08f, 0.18f),
                        Gravity = 0.1f,
                        Burst = 10,
                        MaxParticles = 10,
                        ShapeAngle = 30f,
                        RandomRotation = true,
                        SizeStart = 0.6f,
                        SizeEnd = 1.4f,
                        Fade = PresentationAssetBuilder.Fade(Color.white, Color.white, 1f, 0f),
                    };
                    break;
            }

            settings.Color = spec.ParticleColor;
            settings.WorldSpace = true;
            settings.Shape = ParticleSystemShapeType.Cone;
            settings.ShapeRadius = 0.01f;
            settings.Material = material;
            settings.DestroyOnStop = true;
            PresentationAssetBuilder.AddParticles(root, settings);
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            var t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - (2f * t));
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
        }

        // System.Random は実装が変わると結果も変わり、作り直したテクスチャに差分が出るので、自前の整数のハッシュにする。
        private static float Hash01(int x, int y, uint seed)
        {
            unchecked
            {
                var h = ((uint)x * 73856093u) ^ ((uint)y * 19349663u) ^ (seed * 83492791u);
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return (h & 0xFFFFFFu) / 16777216f;
            }
        }
    }

    /// <summary>着弾のエフェクトの見た目の種類。</summary>
    public enum SurfaceImpactStyle
    {
        /// <summary>ゆっくり広がって消える土埃。</summary>
        Dust,

        /// <summary>速く飛んで尾を引く火花 (加算合成)。</summary>
        Sparks,

        /// <summary>重力で落ちる木片。</summary>
        Chips,
    }

    /// <summary>材質 1 つの着弾の仮素材の設計図。<see cref="SurfaceImpactAssetStep.Specs"/> の要素。</summary>
    public sealed class SurfaceImpactSpec
    {
        public SurfaceImpactSpec(
            string materialId,
            string label,
            SurfaceImpactStyle style,
            float decalSize,
            Color rimColor,
            Color holeColor,
            Color particleColor,
            uint seed,
            SoundSpec sound)
        {
            if (string.IsNullOrWhiteSpace(label) || label.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException($"label '{label}' must be a non-empty file name", nameof(label));
            }

            if (!(decalSize > 0f) || float.IsInfinity(decalSize))
            {
                throw new ArgumentOutOfRangeException(nameof(decalSize), decalSize, "must be a finite number > 0");
            }

            MaterialId = SurfaceMaterial.Normalize(materialId);
            Label = label;
            Style = style;
            DecalSize = decalSize;
            RimColor = rimColor;
            HoleColor = holeColor;
            ParticleColor = particleColor;
            Seed = seed;
            Sound = sound ?? throw new ArgumentNullException(nameof(sound));
        }

        /// <summary>材質の Id (<see cref="SurfaceMaterial.Id"/>)。</summary>
        public string MaterialId { get; }

        /// <summary>ファイル名に付ける名前 (例: Concrete)。</summary>
        public string Label { get; }

        public SurfaceImpactStyle Style { get; }

        /// <summary>銃痕の一辺の長さ (m)。</summary>
        public float DecalSize { get; }

        /// <summary>銃痕の縁の色。</summary>
        public Color RimColor { get; }

        /// <summary>銃痕の穴の色。</summary>
        public Color HoleColor { get; }

        /// <summary>着弾のエフェクトの粒の色。</summary>
        public Color ParticleColor { get; }

        /// <summary>銃痕のテクスチャの揺らぎと粒の種。</summary>
        public uint Seed { get; }

        /// <summary>着弾音の設計図。名前がそのままファイル名 (.wav) になる。</summary>
        public SoundSpec Sound { get; }

        public string DecalTextureName => Label + "BulletHoleTexture";

        public string DecalMaterialName => Label + "BulletHole";

        public string EffectName => Label + "Impact";
    }
}
