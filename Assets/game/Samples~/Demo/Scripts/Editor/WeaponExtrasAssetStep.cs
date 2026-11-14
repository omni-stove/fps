using System;
using System.Collections.Generic;
using System.IO;
using Fps.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Editor
{
    /// <summary>
    /// 薬莢と空撃ちの仮素材 (小さい円柱の薬莢のプレハブ・手続き生成の薬莢の落下音とカチッという空撃ちの音) と、それをまとめた
    /// <see cref="WeaponExtrasCatalog"/> を作るか上書きする手順。<see cref="PresentationAssetSteps.BuildAll"/> から実行される。
    /// 武器は <see cref="PresentationAssetBuilder.WeaponEntries"/> のすべての Id に既定の素材を入れる。
    /// 既存のアセットは GUID を保って中身だけ更新する (<see cref="PresentationAssetBuilder"/> と同じ書き出し方)。
    /// </summary>
    public sealed class WeaponExtrasAssetStep : IPresentationAssetStep
    {
        public const int StepOrder = PresentationCatalogAssetStep.StepOrder + 10;
        public const string LogTag = "[WeaponExtrasAssetStep]";

        public const string ShellDropSound = "ShellDrop";
        public const string HeavyShellDropSound = "HeavyShellDrop";
        public const string DryFireClickSound = "DryFire";

        public const string RifleShellPrefab = "RifleShell";
        public const string HandgunShellPrefab = "HandgunShell";
        public const string LauncherShellPrefab = "LauncherShell";

        public const string ShellMaterialName = "ShellBrass";

        /// <summary>薬莢の材質に使う URP のシェーダー。</summary>
        public const string LitShaderName = "Universal Render Pipeline/Lit";

        /// <summary>薬莢の重さ (kg)。軽いので、跳ねたり転がったりしやすい。</summary>
        public const float ShellMass = 0.01f;

        public static string CatalogPath =>
            PresentationAssetBuilder.PresentationFolder + "/Resources/" + WeaponExtrasCatalog.ResourcePath + ".asset";

        public string Name => "WeaponExtras";

        public int Order => StepOrder;

        public void Build()
        {
            BuildAt(PresentationAssetBuilder.AudioFolder, PresentationAssetBuilder.PresentationFolder, CatalogPath);
        }

        /// <summary>
        /// 武器ごとの薬莢のプレハブと落下音・空撃ちの音の組み合わせ。武器の Id は <see cref="PresentationAssetBuilder.WeaponEntries"/> と同じ並び。
        /// </summary>
        public static IReadOnlyList<(string WeaponId, string Shell, string LandSound, string DryFire)> WeaponEntries()
        {
            return new[]
            {
                (WeaponAssetBuilder.RifleId, RifleShellPrefab, ShellDropSound, DryFireClickSound),
                (WeaponAssetBuilder.HandgunId, HandgunShellPrefab, ShellDropSound, DryFireClickSound),
                (WeaponAssetBuilder.LauncherId, LauncherShellPrefab, HeavyShellDropSound, DryFireClickSound),
            };
        }

        /// <summary>薬莢のプレハブの名前と、円柱の直径・長さ (m)。</summary>
        public static IReadOnlyList<(string Name, float Diameter, float Length)> ShellShapes()
        {
            return new[]
            {
                (RifleShellPrefab, 0.01f, 0.045f),
                (HandgunShellPrefab, 0.009f, 0.019f),
                (LauncherShellPrefab, 0.04f, 0.1f),
            };
        }

        /// <summary>書き出すすべての音の設計図。名前がそのままファイル名 (.wav) になる。</summary>
        public static IReadOnlyList<SoundSpec> SoundSpecs()
        {
            return new[]
            {
                new SoundSpec(ShellDropSound, 2401u, 0.4f, 0.5f, new[]
                {
                    new SoundLayer(0f, 0.25f, 0.0003f, 0.05f, 0.3f, 9000f, 0.6f, 4300f, 4200f),
                    new SoundLayer(0f, 0.25f, 0.0003f, 0.04f, 0f, 0f, 0.35f, 6900f, 6800f),
                    new SoundLayer(0.11f, 0.2f, 0.0003f, 0.03f, 0.2f, 9000f, 0.4f, 4350f, 4250f),
                    new SoundLayer(0.19f, 0.15f, 0.0003f, 0.02f, 0.1f, 9000f, 0.25f, 4400f, 4300f),
                }),
                new SoundSpec(HeavyShellDropSound, 2402u, 0.55f, 0.6f, new[]
                {
                    new SoundLayer(0f, 0.35f, 0.0005f, 0.08f, 0.4f, 5000f, 0.6f, 1900f, 1800f),
                    new SoundLayer(0f, 0.35f, 0.0005f, 0.06f, 0f, 0f, 0.3f, 3100f, 3000f),
                    new SoundLayer(0.16f, 0.3f, 0.0005f, 0.05f, 0.25f, 5000f, 0.35f, 1950f, 1850f),
                }),
                new SoundSpec(DryFireClickSound, 2501u, 0.12f, 0.6f, new[]
                {
                    new SoundLayer(0f, 0.05f, 0.0002f, 0.006f, 1f, 6000f, 0.4f, 2400f, 1800f),
                    new SoundLayer(0.035f, 0.05f, 0.0002f, 0.008f, 0.5f, 4000f, 0.3f, 1500f, 1200f),
                }),
            };
        }

        /// <summary>
        /// <see cref="CatalogPath"/> のカタログを読み、<see cref="WeaponExtrasCatalog.Validate"/> に通ることを確かめて返す。
        /// 無ければ (または別の型なら) 先に <see cref="PresentationAssetSteps.BuildAll"/> を走らせるよう例外を投げる。
        /// </summary>
        public static WeaponExtrasCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<WeaponExtrasCatalog>(CatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    $"weapon extras catalog '{CatalogPath}' was not found; run {PresentationAssetSteps.MenuPath} (Fps.Editor.PresentationAssetSteps.BuildAll) first");
            }

            catalog.Validate();
            return catalog;
        }

        /// <summary>
        /// <see cref="PresentationAssetBuilder.WeaponEntries"/> の武器の Id のうち、<see cref="WeaponEntries"/> に無いもの。
        /// 武器を足したのにここへ足し忘れたら <see cref="BuildAt"/> が止まる。
        /// </summary>
        public static IReadOnlyList<string> MissingWeaponIds()
        {
            var covered = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in WeaponEntries())
            {
                covered.Add(entry.WeaponId);
            }

            var missing = new List<string>();
            foreach (var entry in PresentationAssetBuilder.WeaponEntries())
            {
                if (!covered.Contains(entry.WeaponId))
                {
                    missing.Add(entry.WeaponId);
                }
            }

            return missing;
        }

        /// <summary>
        /// audioFolder に音を、presentationFolder に薬莢のプレハブと材質を、catalogPath にそれをまとめたカタログを
        /// 作るか上書きして保存し、カタログを返す。フォルダは Assets/ 以下で、無ければ作る。
        /// </summary>
        public static WeaponExtrasCatalog BuildAt(string audioFolder, string presentationFolder, string catalogPath)
        {
            audioFolder = PresentationAssetBuilder.ValidateFolderPath(audioFolder, nameof(audioFolder));
            presentationFolder = PresentationAssetBuilder.ValidateFolderPath(presentationFolder, nameof(presentationFolder));
            catalogPath = SceneBuildHelpers.ValidateAssetPath(catalogPath);

            var missing = MissingWeaponIds();
            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    $"weapon(s) {string.Join(", ", missing)} have sounds in {nameof(PresentationAssetBuilder)} but no shell casing or dry-fire sound; add them to {nameof(WeaponEntries)}");
            }

            SceneBuildHelpers.EnsureFolder(audioFolder);
            SceneBuildHelpers.EnsureFolder(presentationFolder);
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(catalogPath)?.Replace('\\', '/'));

            var clips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
            foreach (var spec in SoundSpecs())
            {
                if (clips.ContainsKey(spec.Name))
                {
                    throw new InvalidOperationException($"sound name '{spec.Name}' is used more than once");
                }

                clips.Add(spec.Name, PresentationAssetBuilder.WriteSound($"{audioFolder}/{spec.Name}.wav", spec));
            }

            var material = BuildShellMaterial($"{presentationFolder}/{ShellMaterialName}.mat");
            var cylinder = BuiltinCylinderMesh();
            var shells = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (var shape in ShellShapes())
            {
                shells.Add(shape.Name, PresentationAssetBuilder.SavePrefab(presentationFolder, shape.Name,
                    root => BuildShell(root, cylinder, material, shape.Diameter, shape.Length)));
            }

            var weapons = new List<WeaponExtrasAssets>();
            foreach (var entry in WeaponEntries())
            {
                weapons.Add(new WeaponExtrasAssets(entry.WeaponId, shells[entry.Shell], clips[entry.LandSound], clips[entry.DryFire]));
            }

            var catalog = SceneBuildHelpers.CreateOrUpdateAsset<WeaponExtrasCatalog>(catalogPath, c =>
            {
                // 既存のアセットを書き換える前に、同じ中身の一時インスタンスで検査する (不正ならメモリ上の中身も汚さない)。
                var probe = ScriptableObject.CreateInstance<WeaponExtrasCatalog>();
                try
                {
                    probe.name = Path.GetFileNameWithoutExtension(catalogPath);
                    probe.SetWeapons(weapons);
                    probe.Validate();
                }
                finally
                {
                    Object.DestroyImmediate(probe);
                }

                c.SetWeapons(weapons);
            });

            Debug.Log($"{LogTag} saved {clips.Count} sound(s) to '{audioFolder}', {shells.Count} shell casing(s) to '{presentationFolder}' and '{catalogPath}'");
            return catalog;
        }

        private static void BuildShell(GameObject root, Mesh cylinder, Material material, float diameter, float length)
        {
            // Unity の円柱は直径 1・高さ 2 なので、高さは半分で割る。
            root.transform.localScale = new Vector3(diameter, length * 0.5f, diameter);
            root.AddComponent<MeshFilter>().sharedMesh = cylinder;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 1;
            collider.radius = 0.5f;
            collider.height = 2f;

            var body = root.AddComponent<Rigidbody>();
            body.mass = ShellMass;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private static Mesh BuiltinCylinderMesh()
        {
            // 組み込みの円柱のメッシュは名前で引くと版によって名前が違うので、円柱を一度作って借りる。
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            try
            {
                var mesh = probe.GetComponent<MeshFilter>().sharedMesh;
                if (mesh == null)
                {
                    throw new InvalidOperationException("the built-in cylinder has no mesh");
                }

                return mesh;
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        private static Material BuildShellMaterial(string path)
        {
            var shader = Shader.Find(LitShaderName);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    $"shader '{LitShaderName}' was not found; the shell casings need the Universal Render Pipeline package");
            }

            var fresh = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            fresh.SetColor("_BaseColor", new Color(0.8f, 0.62f, 0.26f));
            fresh.SetFloat("_Metallic", 0.9f);
            fresh.SetFloat("_Smoothness", 0.65f);

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
    }
}
