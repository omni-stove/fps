using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Fps.Editor
{
    /// <summary>
    /// 血の演出 (<see cref="BloodPresenter"/>) の仮素材と <see cref="HitFeedbackCatalog"/> を作る手順。
    /// 部位ごとの血しぶき (赤い粒) と、壁の血痕 (赤い円のテクスチャを貼った Quad) を <see cref="Folder"/> に、カタログを <see cref="CatalogPath"/> に置く。
    /// 血しぶきは <see cref="PresentationCatalogAssetStep"/> が作る半透明の粒の材質 (<see cref="PresentationAssetBuilder.AlphaMaterialName"/>) を借りるので、それより後に実行する。
    /// 既存のアセットは GUID を保って中身だけ更新する。Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.HitFeedbackAssetStep.BuildDefault</code>
    /// </summary>
    public sealed class HitFeedbackAssetStep : IPresentationAssetStep
    {
        public const int StepOrder = PresentationCatalogAssetStep.StepOrder + 10;

        public const string MenuPath = "Fps/Build Hit Feedback Assets";
        public const string LogTag = "[HitFeedbackAssetStep]";

        public const string HeadSprayEffect = "BloodSprayHead";
        public const string BodySprayEffect = "BloodSprayBody";
        public const string LimbSprayEffect = "BloodSprayLimb";
        public const string WallDecalPrefab = "BloodDecal";
        public const string DecalTextureName = "BloodDecal";
        public const string DecalMaterialName = "BloodDecal";

        /// <summary>血痕のテクスチャの 1 辺 (px)。</summary>
        public const int DecalTextureSize = 64;

        /// <summary>血痕の円の中の色。</summary>
        public static readonly Color32 DecalColor = new Color32(120, 6, 6, 255);

        public static string Folder => PresentationAssetBuilder.PresentationFolder + "/Blood";

        /// <summary>カタログの置き場所。実行時に <see cref="HitFeedbackCatalog.Load"/> で読めるよう Resources の下に置く。</summary>
        public static string CatalogPath => PresentationAssetBuilder.PresentationFolder + "/Resources/" + HitFeedbackCatalog.ResourcePath + ".asset";

        /// <summary>血しぶきが借りる粒の材質の置き場所 (<see cref="PresentationAssetBuilder"/> の生成物)。</summary>
        public static string ParticleMaterialPath => PresentationAssetBuilder.PresentationFolder + "/" + PresentationAssetBuilder.AlphaMaterialName + ".mat";

        public string Name => "HitFeedbackCatalog";

        public int Order => StepOrder;

        /// <summary>部位ごとの血しぶきの名前。<see cref="HitZone"/> のすべての値を持つ。</summary>
        public static IReadOnlyList<(HitZone Zone, string Effect)> SprayEntries()
        {
            return new[]
            {
                (HitZone.Head, HeadSprayEffect),
                (HitZone.Body, BodySprayEffect),
                (HitZone.Limb, LimbSprayEffect),
            };
        }

        public void Build()
        {
            BuildDefault();
        }

        [MenuItem(MenuPath)]
        public static void BuildDefault()
        {
            BuildAt(Folder, CatalogPath, ParticleMaterialPath);
        }

        /// <summary>
        /// folder に血しぶきと血痕の素材を、catalogPath にそれをまとめたカタログを作るか上書きして保存し、カタログを返す。
        /// 血しぶきの粒には particleMaterialPath の材質を使う。フォルダは Assets/ 以下で、無ければ作る。
        /// </summary>
        /// <exception cref="InvalidOperationException">particleMaterialPath に材質が無い (先に <see cref="PresentationAssetBuilder.Build"/> を走らせる)。</exception>
        public static HitFeedbackCatalog BuildAt(string folder, string catalogPath, string particleMaterialPath)
        {
            folder = PresentationAssetBuilder.ValidateFolderPath(folder, nameof(folder));
            catalogPath = SceneBuildHelpers.ValidateAssetPath(catalogPath);
            particleMaterialPath = SceneBuildHelpers.ValidateAssetPath(particleMaterialPath);

            var particleMaterial = AssetDatabase.LoadAssetAtPath<Material>(particleMaterialPath);
            if (particleMaterial == null)
            {
                throw new InvalidOperationException(
                    $"particle material '{particleMaterialPath}' was not found; run {PresentationAssetBuilder.MenuPath} (Fps.Editor.PresentationAssetBuilder.Build) first");
            }

            SceneBuildHelpers.EnsureFolder(folder);
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(catalogPath)?.Replace('\\', '/'));

            var sprays = new Dictionary<string, GameObject>(StringComparer.Ordinal)
            {
                [HeadSprayEffect] = PresentationAssetBuilder.SavePrefab(folder, HeadSprayEffect,
                    root => AddSpray(root, particleMaterial, 22, new Vector2(2f, 5f), new Vector2(0.03f, 0.07f), 30f)),
                [BodySprayEffect] = PresentationAssetBuilder.SavePrefab(folder, BodySprayEffect,
                    root => AddSpray(root, particleMaterial, 14, new Vector2(1.5f, 3.5f), new Vector2(0.04f, 0.09f), 25f)),
                [LimbSprayEffect] = PresentationAssetBuilder.SavePrefab(folder, LimbSprayEffect,
                    root => AddSpray(root, particleMaterial, 8, new Vector2(1f, 2.5f), new Vector2(0.03f, 0.06f), 20f)),
            };

            var texture = PresentationAssetBuilder.WriteTexture($"{folder}/{DecalTextureName}.asset", DecalTextureSize, DecalPixels());
            var decalMaterial = PresentationAssetBuilder.BuildParticleMaterial(
                $"{folder}/{DecalMaterialName}.mat", texture, PresentationAssetBuilder.BlendAlpha);
            var decal = PresentationAssetBuilder.SavePrefab(folder, WallDecalPrefab, root => AddDecal(root, decalMaterial));

            var entries = new List<ZoneBloodSpray>();
            foreach (var entry in SprayEntries())
            {
                entries.Add(new ZoneBloodSpray(entry.Zone, sprays[entry.Effect]));
            }

            var catalog = SceneBuildHelpers.CreateOrUpdateAsset<HitFeedbackCatalog>(catalogPath, c =>
            {
                // 既存のアセットを書き換える前に、同じ中身の一時インスタンスで検査する (不正ならメモリ上の中身も汚さない)。
                var probe = ScriptableObject.CreateInstance<HitFeedbackCatalog>();
                try
                {
                    probe.name = Path.GetFileNameWithoutExtension(catalogPath);
                    Configure(probe, entries, sprays[BodySprayEffect], decal);
                    probe.Validate();
                }
                finally
                {
                    Object.DestroyImmediate(probe);
                }

                Configure(c, entries, sprays[BodySprayEffect], decal);
            });

            Debug.Log($"{LogTag} saved {sprays.Count} spray(s) and the wall decal to '{folder}' and '{catalogPath}'");
            return catalog;
        }

        /// <summary>
        /// 血痕のテクスチャの画素 (下の行から)。中心から半径の 0.8 までは <see cref="DecalColor"/> で不透明、そこから縁へ透明になる円。
        /// </summary>
        public static Color32[] DecalPixels()
        {
            var pixels = new Color32[DecalTextureSize * DecalTextureSize];
            var center = (DecalTextureSize - 1) * 0.5f;
            for (var y = 0; y < DecalTextureSize; y++)
            {
                for (var x = 0; x < DecalTextureSize; x++)
                {
                    var dx = (x - center) / center;
                    var dy = (y - center) / center;
                    var distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                    var alpha = Mathf.Clamp01((1f - distance) / 0.2f);
                    pixels[(y * DecalTextureSize) + x] = new Color32(
                        DecalColor.r, DecalColor.g, DecalColor.b, (byte)Mathf.RoundToInt(alpha * DecalColor.a));
                }
            }

            return pixels;
        }

        private static void Configure(HitFeedbackCatalog catalog, IEnumerable<ZoneBloodSpray> sprays, GameObject defaultSpray, GameObject decal)
        {
            catalog.SetSprays(sprays);
            catalog.DefaultSpray = defaultSpray;
            catalog.WallDecal = decal;
            catalog.WallDecalMaxDistance = HitFeedbackCatalog.DefaultWallDecalMaxDistance;
            catalog.WallDecalLifetime = HitFeedbackCatalog.DefaultWallDecalLifetime;
            catalog.WallDecalSize = HitFeedbackCatalog.DefaultWallDecalSize;
        }

        private static void AddSpray(GameObject root, Material material, short burst, Vector2 speed, Vector2 size, float angle)
        {
            PresentationAssetBuilder.AddParticles(root, new PresentationAssetBuilder.ParticleSettings
            {
                Duration = 0.1f,
                Lifetime = new Vector2(0.25f, 0.5f),
                Speed = speed,
                Size = size,
                Color = new Color(0.55f, 0.02f, 0.02f, 0.9f),
                Gravity = 1f,
                Burst = burst,
                MaxParticles = burst,
                WorldSpace = true,
                RandomRotation = true,
                Shape = ParticleSystemShapeType.Cone,
                ShapeAngle = angle,
                ShapeRadius = 0.02f,
                SizeStart = 1f,
                SizeEnd = 0.5f,
                Fade = PresentationAssetBuilder.Fade(Color.white, new Color(0.6f, 0.6f, 0.6f), 1f, 0f),
                Material = material,
                DestroyOnStop = true,
            });
        }

        // 粒ではなく Quad にするのは、壁に貼り付いたまま残し、向きを壁の面に合わせるため (URP の Decal Projector は描画の設定が要る)。
        private static void AddDecal(GameObject root, Material material)
        {
            var mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            if (mesh == null)
            {
                throw new InvalidOperationException("the built-in Quad mesh was not found");
            }

            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }
}
