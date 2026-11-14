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
    public sealed class ViewmodelAssetStep : IPresentationAssetStep
    {
        public const int StepOrder = 100;
        public const string LogTag = "[ViewmodelAssetStep]";
        public const string LitShaderName = "Universal Render Pipeline/Lit";
        public const string SkinMaterialName = "ViewmodelSkin";
        public const string MetalMaterialName = "ViewmodelMetal";
        public const string PrefabPrefix = "Viewmodel_";

        public const string GunName = "Gun";
        public const string HandsName = "Hands";

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

        public static IReadOnlyList<WeaponSpec> WeaponSpecs()
        {
            return new[] { WeaponAssetBuilder.RifleSpec(), WeaponAssetBuilder.HandgunSpec(), WeaponAssetBuilder.LauncherSpec() };
        }

        public static string PrefabName(WeaponCategory category)
        {
            return PrefabPrefix + category;
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
                default:
                    return ViewmodelEntry.DefaultOffset;
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
            var metal = BuildLitMaterial($"{folder}/{MetalMaterialName}.mat", MetalColor, 0.55f);

            var prefabs = new Dictionary<WeaponCategory, GameObject>();
            var entries = new List<ViewmodelEntry>();
            foreach (var spec in WeaponSpecs())
            {
                var category = spec.Category;
                if (!prefabs.TryGetValue(category, out var prefab))
                {
                    var shape = ShapeFor(category);
                    prefab = PresentationAssetBuilder.SavePrefab(folder, PrefabName(category), root => BuildViewmodel(root, shape, skin, metal));
                    prefabs.Add(category, prefab);
                }

                entries.Add(new ViewmodelEntry(spec.Id, prefab, OffsetFor(category), new ViewmodelSwayParameters(SwayFor(category))));
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

        public static void BuildViewmodel(GameObject root, GunShape shape, Material skin, Material metal)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var r = shape.ReceiverLength;
            var b = shape.BarrelLength;
            var w = shape.Width;
            var h = shape.Height;
            var barrelY = h * 0.15f;
            var gripZ = r * 0.3f;
            var gripY = (-h * 0.5f) - (h * 0.55f);

            var gun = AddGroup(root.transform, GunName);
            AddBox(gun, "Receiver", new Vector3(0f, 0f, r * 0.5f), new Vector3(w, h, r), metal);
            var bore = Mathf.Max(0.018f, Mathf.Min(w, h) * 0.45f);
            AddBox(gun, "Barrel", new Vector3(0f, barrelY, r + (b * 0.5f)), new Vector3(bore, bore, b), metal);
            AddBox(gun, "Grip", new Vector3(0f, gripY, gripZ), new Vector3(w * 0.8f, h * 1.1f, w * 0.9f), metal);
            AddSocket(gun, WeaponSockets.MuzzleName, new Vector3(0f, barrelY, r + b), Quaternion.identity);
            AddSocket(gun, WeaponSockets.ShellEjectName, new Vector3(w * 0.5f, h * 0.25f, r * 0.5f), Quaternion.Euler(0f, 90f, 0f));

            var hands = AddGroup(root.transform, HandsName);
            AddBox(hands, "Hand_R", new Vector3(w * 0.15f, gripY, gripZ - 0.01f), new Vector3(0.07f, 0.08f, 0.1f), skin);
            AddBox(hands, "Arm_R", new Vector3(0.03f, gripY - 0.05f, gripZ - 0.2f), new Vector3(0.07f, 0.07f, 0.32f), skin);
            if (shape.TwoHanded)
            {
                var foreZ = r * 0.85f;
                var foreY = (-h * 0.5f) - 0.03f;
                AddBox(hands, "Hand_L", new Vector3(0f, foreY, foreZ), new Vector3(0.07f, 0.07f, 0.1f), skin);
                AddBox(hands, "Arm_L", new Vector3(-0.08f, foreY - 0.04f, foreZ - 0.2f), new Vector3(0.07f, 0.07f, 0.32f), skin);
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

        private static Material BuildLitMaterial(string path, Color color, float smoothness)
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
