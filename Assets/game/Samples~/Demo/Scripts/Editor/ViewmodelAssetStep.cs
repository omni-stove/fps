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
    public sealed class ViewmodelAssetStep : IPresentationAssetStep
    {
        public const int StepOrder = 100;
        public const string LogTag = "[ViewmodelAssetStep]";
        public const string LitShaderName = "Universal Render Pipeline/Lit";
        public const string SkinMaterialName = "ViewmodelSkin";
        public const string PaintMaterialPrefix = "WeaponPaint_";
        public const string PrefabPrefix = "Viewmodel_";

        public const string GunName = "Gun";
        public const string HandsName = "Hands";

        public const float SightPostHeight = 0.014f;

        public static readonly Color SkinColor = new Color(0.82f, 0.64f, 0.52f);
        public static readonly Color MetalColor = new Color(0.17f, 0.17f, 0.19f);

        public static string ViewmodelFolder => PresentationAssetBuilder.PresentationFolder + "/Viewmodels";

        public static string CatalogPath => PresentationAssetBuilder.PresentationFolder + "/Resources/" + ViewmodelCatalog.ResourcePath + ".asset";

        public string Name => ViewmodelCatalog.CatalogName;

        public int Order => StepOrder;

        public void Build()
        {
            BuildAt(ViewmodelFolder, CatalogPath);
        }

        public static IReadOnlyList<(string Name, WeaponSpec Spec)> Weapons() => WeaponAssetBuilder.AllWeaponSources();

        public static IReadOnlyList<WeaponSpec> WeaponSpecs()
        {
            var specs = new List<WeaponSpec>();
            foreach (var weapon in Weapons())
            {
                specs.Add(weapon.Spec);
            }

            return specs;
        }

        public static string PrefabName(string weaponName)
        {
            if (string.IsNullOrWhiteSpace(weaponName))
            {
                throw new ArgumentException("weapon name must not be empty", nameof(weaponName));
            }

            return PrefabPrefix + weaponName;
        }

        public static GunShape ShapeFor(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.AssaultRifle:
                    return new GunShape(0.32f, 0.28f, 0.05f, 0.08f, true);
                case WeaponCategory.SubmachineGun:
                    return new GunShape(0.26f, 0.12f, 0.05f, 0.08f, true);
                case WeaponCategory.Shotgun:
                    return new GunShape(0.3f, 0.42f, 0.055f, 0.075f, true);
                case WeaponCategory.MarksmanRifle:
                    return new GunShape(0.36f, 0.38f, 0.05f, 0.08f, true);
                case WeaponCategory.SniperRifle:
                    return new GunShape(0.4f, 0.5f, 0.05f, 0.08f, true);
                case WeaponCategory.LightMachineGun:
                    return new GunShape(0.42f, 0.36f, 0.07f, 0.1f, true);
                case WeaponCategory.Handgun:
                    return new GunShape(0.17f, 0.03f, 0.035f, 0.05f, false);
                case WeaponCategory.Launcher:
                    return new GunShape(0.5f, 0.25f, 0.11f, 0.11f, true);
                case WeaponCategory.Melee:
                    return new GunShape(0.12f, 0.5f, 0.03f, 0.03f, false);
                default:
                    throw new ArgumentOutOfRangeException(nameof(category), category, "unknown weapon category");
            }
        }

        public static Vector3 OffsetFor(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.Handgun:
                    return new Vector3(0.12f, -0.12f, 0.25f);
                case WeaponCategory.Launcher:
                    return new Vector3(0.17f, -0.17f, 0.12f);
                case WeaponCategory.Melee:
                    return new Vector3(0.18f, -0.2f, 0.2f);
                default:
                    return ViewmodelEntry.DefaultOffset;
            }
        }

        // 片手の銃は腕を伸ばして構えるので、照準の点を遠くする。
        public static Vector3 AdsOffsetFor(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.Handgun:
                    return new Vector3(0f, 0f, 0.32f);
                case WeaponCategory.Launcher:
                    return new Vector3(0f, 0f, 0.15f);
                default:
                    return ViewmodelEntry.DefaultAdsOffset;
            }
        }

        public static ViewmodelSwaySettings SwayFor(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.SubmachineGun:
                    return new ViewmodelSwaySettings(kickBackMeters: 0.02f, kickPitchDegrees: 1.2f, kickReturnPerSecond: 16f);
                case WeaponCategory.Shotgun:
                    return new ViewmodelSwaySettings(kickBackMeters: 0.06f, kickPitchDegrees: 5f, kickReturnPerSecond: 9f, maxKicks: 1f);
                case WeaponCategory.SniperRifle:
                    return new ViewmodelSwaySettings(kickBackMeters: 0.06f, kickPitchDegrees: 5f, kickReturnPerSecond: 8f, maxKicks: 1f);
                case WeaponCategory.LightMachineGun:
                    return new ViewmodelSwaySettings(bobHorizontalMeters: 0.016f, kickBackMeters: 0.025f, kickPitchDegrees: 1.5f);
                case WeaponCategory.Handgun:
                    return new ViewmodelSwaySettings(kickBackMeters: 0.02f, kickPitchDegrees: 4f, kickReturnPerSecond: 14f);
                case WeaponCategory.Melee:
                    return new ViewmodelSwaySettings(bobHorizontalMeters: 0.018f);
                case WeaponCategory.Launcher:
                    return new ViewmodelSwaySettings(
                        bobHorizontalMeters: 0.016f, kickBackMeters: 0.08f, kickPitchDegrees: 6f, kickReturnPerSecond: 6f, maxKicks: 1f);
                default:
                    return ViewmodelSwaySettings.Default;
            }
        }

        public static ViewmodelCatalog BuildAt(string folder, string catalogPath)
        {
            folder = PresentationAssetBuilder.ValidateFolderPath(folder, nameof(folder));
            catalogPath = SceneBuildHelpers.ValidateAssetPath(catalogPath);
            SceneBuildHelpers.EnsureFolder(folder);
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(catalogPath)?.Replace('\\', '/'));

            var skin = BuildLitMaterial($"{folder}/{SkinMaterialName}.mat", SkinColor, 0.2f);
            var paint = PaintCache(folder);

            var prefabs = new List<GameObject>();
            var entries = new List<ViewmodelEntry>();
            foreach (var (weaponName, spec) in Weapons())
            {
                var category = spec.Category;
                var look = WeaponLook.For(spec);
                var prefab = PresentationAssetBuilder.SavePrefab(folder, PrefabName(weaponName), root => BuildViewmodel(root, look, skin, paint));
                prefabs.Add(prefab);
                entries.Add(new ViewmodelEntry(
                    spec.Id, prefab, OffsetFor(category), new ViewmodelSwayParameters(SwayFor(category)), AdsOffsetFor(category)));
            }

            var catalog = SceneBuildHelpers.CreateOrUpdateAsset<ViewmodelCatalog>(catalogPath, c =>
            {
                // 既存のアセットを書き換える前に、同じ中身の一時インスタンスで検査する (不正ならメモリ上の中身も汚さない)。
                var probe = ScriptableObject.CreateInstance<ViewmodelCatalog>();
                try
                {
                    probe.name = Path.GetFileNameWithoutExtension(catalogPath);
                    probe.SetWeapons(entries);
                    probe.Validate();
                }
                finally
                {
                    Object.DestroyImmediate(probe);
                }

                c.SetWeapons(entries);
            });

            Debug.Log($"{LogTag} saved {prefabs.Count} viewmodel prefab(s) to '{folder}' and '{catalogPath}' for {entries.Count} weapon(s)");
            return catalog;
        }

        public static void BuildViewmodel(GameObject root, WeaponLook look, Material skin, Func<WeaponPaint, Color, Material> paint)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (look == null)
            {
                throw new ArgumentNullException(nameof(look));
            }

            var gun = AddGroup(root.transform, GunName);
            look.Build(gun, paint, castShadows: false);
            AddSocket(gun, WeaponSockets.MuzzleName, look.Muzzle, Quaternion.identity);
            AddSocket(gun, WeaponSockets.ShellEjectName, look.ShellEject, Quaternion.Euler(0f, 90f, 0f));
            AddSocket(gun, WeaponSockets.SightName, look.Sight, Quaternion.identity);

            var hands = AddGroup(root.transform, HandsName);
            var grip = look.RearGrip;
            AddBox(hands, "Hand_R", grip + new Vector3(0.005f, 0f, -0.01f), new Vector3(0.07f, 0.08f, 0.1f), skin);
            AddBox(hands, "Arm_R", new Vector3(0.03f, grip.y - 0.05f, grip.z - 0.2f), new Vector3(0.07f, 0.07f, 0.32f), skin);
            if (look.ForeGrip.HasValue)
            {
                var fore = look.ForeGrip.Value;
                AddBox(hands, "Hand_L", fore, new Vector3(0.07f, 0.07f, 0.1f), skin);
                AddBox(hands, "Arm_L", new Vector3(-0.08f, fore.y - 0.04f, fore.z - 0.2f), new Vector3(0.07f, 0.07f, 0.32f), skin);
            }
        }

        // 部品ごとに材質を作ると同じ色の .mat が部品の数だけできるので、塗りと色の組み合わせで 1 つにまとめる。
        public static Func<WeaponPaint, Color, Material> PaintCache(string folder)
        {
            var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
            return (paint, color) =>
            {
                var key = paint + "_" + ColorUtility.ToHtmlStringRGB(color);
                if (!materials.TryGetValue(key, out var material))
                {
                    material = BuildLitMaterial($"{folder}/{PaintMaterialPrefix}{key}.mat", color, SmoothnessOf(paint));
                    materials.Add(key, material);
                }

                return material;
            };
        }

        public static float SmoothnessOf(WeaponPaint paint)
        {
            switch (paint)
            {
                case WeaponPaint.Glass:
                    return 0.9f;
                case WeaponPaint.Furniture:
                    return 0.25f;
                case WeaponPaint.Accent:
                    return 0.4f;
                default:
                    return 0.55f;
            }
        }

        private static Transform AddGroup(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        private static void AddSocket(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var socket = new GameObject(name).transform;
            socket.SetParent(parent, false);
            socket.localPosition = position;
            socket.localRotation = rotation;
        }

        private static void AddBox(Transform parent, string name, Vector3 center, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;

            // 手元のモデルが壁や弾の Ray に当たらないよう、当たりは付けない。
            Object.DestroyImmediate(box.GetComponent<BoxCollider>());
            box.transform.SetParent(parent, false);
            box.transform.localPosition = center;
            box.transform.localScale = size;
            var renderer = box.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        internal static Material BuildLitMaterial(string path, Color color, float smoothness)
        {
            var shader = Shader.Find(LitShaderName);
            if (shader == null)
            {
                throw new InvalidOperationException($"shader '{LitShaderName}' was not found; the viewmodels need the Universal Render Pipeline package");
            }

            var fresh = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            fresh.SetColor("_BaseColor", color);
            fresh.SetFloat("_Smoothness", smoothness);

            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                return fresh;
            }

            if (!(existing is Material material))
            {
                Object.DestroyImmediate(fresh);
                throw new InvalidOperationException($"'{path}' is a {existing.GetType().Name}, not a Material; move it away first");
            }

            // 既存の材質に中身だけ写すと GUID が保たれ、プレハブからの参照が切れない。
            EditorUtility.CopySerialized(fresh, material);
            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        public readonly struct GunShape
        {
            public GunShape(float receiverLength, float barrelLength, float width, float height, bool twoHanded)
            {
                ReceiverLength = receiverLength;
                BarrelLength = barrelLength;
                Width = width;
                Height = height;
                TwoHanded = twoHanded;
            }

            public float ReceiverLength { get; }

            public float BarrelLength { get; }

            public float Width { get; }

            public float Height { get; }

            public bool TwoHanded { get; }

            public float TotalLength => ReceiverLength + BarrelLength;
        }
    }
}
