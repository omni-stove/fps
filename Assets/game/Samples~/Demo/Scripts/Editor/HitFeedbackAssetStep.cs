using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Shooter.Editor
{
    // 血しぶきは PresentationCatalogAssetStep が作る半透明の粒の材質 (PresentationAssetBuilder.AlphaMaterialName) を借りるので、それより後に実行する。
    public sealed class HitFeedbackAssetStep : IPresentationAssetStep
    {
        public const int StepOrder = PresentationCatalogAssetStep.StepOrder + 10;

        public const string MenuPath = "Shooter/Build Hit Feedback Assets";
        public const string LogTag = "[HitFeedbackAssetStep]";

        public const string HeadSprayEffect = "BloodSprayHead";
        public const string BodySprayEffect = "BloodSprayBody";
        public const string LimbSprayEffect = "BloodSprayLimb";
        public const string WeakPointSprayEffect = "BloodSprayWeakPoint";
        public const string WallDecalPrefab = "BloodDecal";
        public const string DecalTextureName = "BloodDecal";
        public const string DecalMaterialName = "BloodDecal";

        public const int DecalTextureSize = 64;

        public static readonly Color32 DecalColor = new Color32(120, 6, 6, 255);

        public static string Folder => PresentationAssetBuilder.PresentationFolder + "/Blood";

        // 実行時に HitFeedbackCatalog.Load で読めるよう Resources の下に置く。
        public static string CatalogPath => PresentationAssetBuilder.PresentationFolder + "/Resources/" + HitFeedbackCatalog.ResourcePath + ".asset";

        public static string ParticleMaterialPath => PresentationAssetBuilder.PresentationFolder + "/" + PresentationAssetBuilder.AlphaMaterialName + ".mat";

        public string Name => "HitFeedbackCatalog";

        public int Order => StepOrder;

        public static IReadOnlyList<(HitZone Zone, string Effect)> SprayEntries()
        {
            return new[]
            {
                (HitZone.Head, HeadSprayEffect),
                (HitZone.Body, BodySprayEffect),
                (HitZone.Limb, LimbSprayEffect),
                (HitZone.WeakPoint, WeakPointSprayEffect),
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

        public static HitFeedbackCatalog BuildAt(string folder, string catalogPath, string particleMaterialPath)
        {
            folder = PresentationAssetBuilder.ValidateFolderPath(folder, nameof(folder));
            catalogPath = SceneBuildHelpers.ValidateAssetPath(catalogPath);
            particleMaterialPath = SceneBuildHelpers.ValidateAssetPath(particleMaterialPath, ".mat");

            var particleMaterial = AssetDatabase.LoadAssetAtPath<Material>(particleMaterialPath);
            if (particleMaterial == null)
            {
                throw new InvalidOperationException(
                    $"particle material '{particleMaterialPath}' was not found; run {PresentationAssetBuilder.MenuPath} (Shooter.Editor.PresentationAssetBuilder.Build) first");
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
                [WeakPointSprayEffect] = PresentationAssetBuilder.SavePrefab(folder, WeakPointSprayEffect,
                    root => AddSpray(root, particleMaterial, 26, new Vector2(2.5f, 6f), new Vector2(0.04f, 0.08f), 35f)),
            };

            var texture = PresentationAssetBuilder.WriteTexture(
                $"{folder}/{DecalTextureName}.asset", DecalTextureName, DecalTextureSize, DecalPixels(), true);
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
