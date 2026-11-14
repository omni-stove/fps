using System;
using System.Collections.Generic;
using System.IO;
using Fps.Runtime;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Fps.Editor
{
    /// <summary>
    /// 仮の音とエフェクトを手続きで作り、それをまとめた <see cref="PresentationCatalog"/> を作るか上書きする。
    /// 音は <see cref="ProceduralAudio"/> でノイズと減衰する包絡線から合成し、<see cref="AudioFolder"/> に WAV で書き出す。
    /// エフェクトは ParticleSystem をコードで設定したプレハブにして、使う材質・テクスチャと一緒に <see cref="PresentationFolder"/> に置く。
    /// 既存のアセットは GUID を保って中身だけ更新し、同じコードからは同じファイルができる (2 回走らせても差分が出ない)。
    /// Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.PresentationAssetBuilder.Build</code>
    /// 演出の仮素材をまとめて作る入口 (<see cref="PresentationAssetSteps.BuildAll"/>) からは <see cref="PresentationCatalogAssetStep"/> として呼ばれる。
    /// </summary>
    public static class PresentationAssetBuilder
    {
        public static string AudioFolder => FpsKitRoot.OutputPath("Audio");
        public static string PresentationFolder => FpsKitRoot.OutputPath("Presentation");
        /// <summary>
        /// カタログの置き場所。実行時に <see cref="PresentationCatalog.Load"/> で読めるよう、Resources の
        /// <see cref="PresentationCatalog.ResourcePath"/> に置く。
        /// </summary>
        public static string CatalogPath => PresentationFolder + "/Resources/" + PresentationCatalog.ResourcePath + ".asset";

        public const string MenuPath = "Fps/Build Presentation Assets";
        public const string LogTag = "[PresentationAssetBuilder]";

        public const string RifleFireSound = "RifleFire";
        public const string RifleReloadSound = "RifleReload";
        public const string HandgunFireSound = "HandgunFire";
        public const string HandgunReloadSound = "HandgunReload";
        public const string LauncherFireSound = "LauncherFire";
        public const string LauncherReloadSound = "LauncherReload";
        public const string ImpactSound = "Impact";
        public const string FootstepSound = "Footstep";
        public const string LandingSound = "Landing";
        public const string HurtVoiceSound = "HurtVoice";
        public const string ExplosionSound = "Explosion";

        public const string RifleMuzzleFlashEffect = "RifleMuzzleFlash";
        public const string HandgunMuzzleFlashEffect = "HandgunMuzzleFlash";
        public const string LauncherMuzzleFlashEffect = "LauncherMuzzleFlash";
        public const string ImpactSparkEffect = "ImpactSpark";
        public const string TracerEffect = "Tracer";
        public const string ExplosionEffect = "ExplosionEffect";

        public const string SoftDotTextureName = "SoftDot";
        public const string AdditiveMaterialName = "ParticleAdditive";
        public const string AlphaMaterialName = "ParticleAlpha";

        /// <summary>エフェクトの材質に使う URP のパーティクル用シェーダー。</summary>
        public const string ParticleShaderName = "Universal Render Pipeline/Particles/Unlit";

        /// <summary>曳光弾の速さ (m/s)。寿命と掛けたものが届く距離になる。</summary>
        public const float TracerSpeed = 400f;

        /// <summary>曳光弾の寿命 (秒)。rifle の射程 100m を飛び切る長さにしている。</summary>
        public const float TracerLifetime = 0.25f;

        private const int SoftDotSize = 64;

        // URP の BaseShaderGUI の列挙と同じ値。材質をインスペクターを通さずに作るので、ここで直接書く。
        private const float SurfaceTransparent = 1f;
        private const float BlendAlpha = 0f;
        private const float BlendAdditive = 2f;

        /// <summary>
        /// 武器ごとの音とマズルフラッシュの組み合わせ。武器の Id は <see cref="WeaponAssetBuilder"/> の固定値で、
        /// カタログには入らない launcher も含める (建造物の破壊の確認で撃つため)。
        /// </summary>
        public static IReadOnlyList<(string WeaponId, string Fire, string Reload, string MuzzleFlash)> WeaponEntries()
        {
            return new[]
            {
                (WeaponAssetBuilder.RifleId, RifleFireSound, RifleReloadSound, RifleMuzzleFlashEffect),
                (WeaponAssetBuilder.HandgunId, HandgunFireSound, HandgunReloadSound, HandgunMuzzleFlashEffect),
                (WeaponAssetBuilder.LauncherId, LauncherFireSound, LauncherReloadSound, LauncherMuzzleFlashEffect),
            };
        }

        /// <summary>書き出すすべての音の設計図。名前がそのままファイル名 (.wav) になる。</summary>
        public static IReadOnlyList<SoundSpec> SoundSpecs()
        {
            return new[]
            {
                new SoundSpec(RifleFireSound, 1101u, 0.45f, 0.9f, new[]
                {
                    new SoundLayer(0f, 0.05f, 0.0005f, 0.012f, 1f, 9000f),
                    new SoundLayer(0f, 0.3f, 0.001f, 0.06f, 0.8f, 3000f, 0.6f, 150f, 55f),
                    new SoundLayer(0.02f, 0.4f, 0.01f, 0.14f, 0.35f, 900f),
                }),
                new SoundSpec(RifleReloadSound, 1102u, 1.9f, 0.7f, new[]
                {
                    Click(0f, 1600f),
                    Slide(0.35f, 0.3f),
                    Click(0.4f, 1200f),
                    Slide(1.0f, 0.25f),
                    Click(1.15f, 1300f),
                    Click(1.6f, 1900f),
                    Click(1.68f, 1500f),
                }),
                new SoundSpec(HandgunFireSound, 1201u, 0.35f, 0.85f, new[]
                {
                    new SoundLayer(0f, 0.04f, 0.0005f, 0.008f, 1f, 11000f),
                    new SoundLayer(0f, 0.2f, 0.001f, 0.035f, 0.7f, 4500f, 0.45f, 260f, 110f),
                    new SoundLayer(0.015f, 0.3f, 0.01f, 0.08f, 0.25f, 1500f),
                }),
                new SoundSpec(HandgunReloadSound, 1202u, 1.4f, 0.7f, new[]
                {
                    Click(0f, 2000f),
                    Click(0.5f, 1500f),
                    Slide(0.9f, 0.15f),
                    Click(1.0f, 1600f),
                    Click(1.25f, 2300f),
                }),
                new SoundSpec(LauncherFireSound, 1301u, 0.9f, 0.9f, new[]
                {
                    new SoundLayer(0f, 0.3f, 0.002f, 0.07f, 0.6f, 600f, 0.9f, 90f, 40f),
                    new SoundLayer(0.02f, 0.85f, 0.12f, 0.25f, 0.8f, 1800f),
                }),
                new SoundSpec(LauncherReloadSound, 1302u, 2.8f, 0.7f, new[]
                {
                    Slide(0.1f, 0.6f),
                    Click(0.9f, 700f),
                    Slide(1.6f, 0.4f),
                    Click(2.3f, 900f),
                    Click(2.45f, 1100f),
                }),
                new SoundSpec(ImpactSound, 2001u, 0.25f, 0.8f, new[]
                {
                    new SoundLayer(0f, 0.1f, 0.0003f, 0.01f, 1f, 8000f),
                    new SoundLayer(0f, 0.2f, 0.001f, 0.04f, 0.4f, 2500f, 0.35f, 700f, 350f),
                }),
                new SoundSpec(FootstepSound, 2101u, 0.18f, 0.6f, new[]
                {
                    new SoundLayer(0f, 0.15f, 0.004f, 0.03f, 1f, 500f, 0.3f, 90f, 60f),
                    new SoundLayer(0f, 0.05f, 0.001f, 0.01f, 0.3f, 3000f),
                }),
                new SoundSpec(LandingSound, 2102u, 0.45f, 0.75f, new[]
                {
                    new SoundLayer(0f, 0.4f, 0.003f, 0.09f, 1f, 350f, 0.6f, 75f, 40f),
                    new SoundLayer(0f, 0.1f, 0.001f, 0.02f, 0.4f, 2000f),
                }),
                new SoundSpec(HurtVoiceSound, 2201u, 0.45f, 0.8f, new[]
                {
                    new SoundLayer(0f, 0.4f, 0.025f, 0.16f, 0f, 0f, 0.8f, 300f, 170f, ToneWaveform.Saw),
                    new SoundLayer(0f, 0.4f, 0.025f, 0.16f, 0f, 0f, 0.3f, 600f, 340f),
                    new SoundLayer(0f, 0.4f, 0.02f, 0.14f, 0.25f, 1800f),
                }),
                new SoundSpec(ExplosionSound, 2301u, 2.4f, 0.95f, new[]
                {
                    new SoundLayer(0f, 0.08f, 0.0005f, 0.02f, 1f, 9000f),
                    new SoundLayer(0f, 2.3f, 0.004f, 0.45f, 1.2f, 450f, 0.7f, 55f, 28f),
                    new SoundLayer(0.05f, 2.3f, 0.06f, 0.8f, 0.5f, 180f),
                }),
            };
        }

        /// <summary>
        /// <see cref="CatalogPath"/> のカタログを読み、<see cref="PresentationCatalog.Validate"/> に通ることを確かめて返す。
        /// 無ければ (または別の型なら) 先に <see cref="Build"/> を走らせるよう例外を投げる。
        /// </summary>
        public static PresentationCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PresentationCatalog>(CatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    $"presentation catalog '{CatalogPath}' was not found; run {MenuPath} (Fps.Editor.PresentationAssetBuilder.Build) first");
            }

            catalog.Validate();
            return catalog;
        }

        [MenuItem(MenuPath)]
        public static void Build()
        {
            BuildAt(AudioFolder, PresentationFolder, CatalogPath);
        }

        /// <summary>
        /// audioFolder に音を、presentationFolder にエフェクトのプレハブと材質を、catalogPath にそれをまとめたカタログを
        /// 作るか上書きして保存し、カタログを返す。フォルダは Assets/ 以下で、無ければ作る。
        /// </summary>
        public static PresentationCatalog BuildAt(string audioFolder, string presentationFolder, string catalogPath)
        {
            audioFolder = ValidateFolderPath(audioFolder, nameof(audioFolder));
            presentationFolder = ValidateFolderPath(presentationFolder, nameof(presentationFolder));
            catalogPath = SceneBuildHelpers.ValidateAssetPath(catalogPath);

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

                clips.Add(spec.Name, WriteSound($"{audioFolder}/{spec.Name}.wav", spec));
            }

            var softDot = BuildSoftDot($"{presentationFolder}/{SoftDotTextureName}.asset");
            var additive = BuildParticleMaterial($"{presentationFolder}/{AdditiveMaterialName}.mat", softDot, BlendAdditive);
            var alpha = BuildParticleMaterial($"{presentationFolder}/{AlphaMaterialName}.mat", softDot, BlendAlpha);

            var effects = new Dictionary<string, GameObject>(StringComparer.Ordinal)
            {
                [RifleMuzzleFlashEffect] = SavePrefab(presentationFolder, RifleMuzzleFlashEffect,
                    root => AddMuzzleFlash(root, additive, 0.35f, 0.5f, new Color(1f, 0.78f, 0.4f), 0.05f, 2)),
                [HandgunMuzzleFlashEffect] = SavePrefab(presentationFolder, HandgunMuzzleFlashEffect,
                    root => AddMuzzleFlash(root, additive, 0.22f, 0.32f, new Color(1f, 0.85f, 0.5f), 0.04f, 2)),
                [LauncherMuzzleFlashEffect] = SavePrefab(presentationFolder, LauncherMuzzleFlashEffect,
                    root => AddMuzzleFlash(root, additive, 0.6f, 0.9f, new Color(1f, 0.6f, 0.25f), 0.08f, 3)),
                [ImpactSparkEffect] = SavePrefab(presentationFolder, ImpactSparkEffect, root => AddImpactSpark(root, additive)),
                [TracerEffect] = SavePrefab(presentationFolder, TracerEffect, root => AddTracer(root, additive)),
                [ExplosionEffect] = SavePrefab(presentationFolder, ExplosionEffect, root => AddExplosion(root, additive, alpha)),
            };

            var weapons = new List<WeaponPresentationAssets>();
            foreach (var entry in WeaponEntries())
            {
                weapons.Add(new WeaponPresentationAssets(entry.WeaponId, clips[entry.Fire], clips[entry.Reload], effects[entry.MuzzleFlash]));
            }

            var catalog = SceneBuildHelpers.CreateOrUpdateAsset<PresentationCatalog>(catalogPath, c =>
            {
                // 既存のアセットを書き換える前に、同じ中身の一時インスタンスで検査する (不正ならメモリ上の中身も汚さない)。
                var probe = ScriptableObject.CreateInstance<PresentationCatalog>();
                try
                {
                    probe.name = Path.GetFileNameWithoutExtension(catalogPath);
                    Configure(probe, weapons, clips, effects);
                    probe.Validate();
                }
                finally
                {
                    Object.DestroyImmediate(probe);
                }

                Configure(c, weapons, clips, effects);
            });

            Debug.Log($"{LogTag} saved {clips.Count} sound(s) to '{audioFolder}', {effects.Count} effect(s) to '{presentationFolder}' and '{catalogPath}'");
            return catalog;
        }

        private static void Configure(
            PresentationCatalog catalog,
            IEnumerable<WeaponPresentationAssets> weapons,
            IReadOnlyDictionary<string, AudioClip> clips,
            IReadOnlyDictionary<string, GameObject> effects)
        {
            catalog.SetWeapons(weapons);
            catalog.ImpactSound = clips[ImpactSound];
            catalog.ImpactSpark = effects[ImpactSparkEffect];
            catalog.Tracer = effects[TracerEffect];
            catalog.Footstep = clips[FootstepSound];
            catalog.Landing = clips[LandingSound];
            catalog.HurtVoice = clips[HurtVoiceSound];
            catalog.ExplosionSound = clips[ExplosionSound];
            catalog.Explosion = effects[ExplosionEffect];
        }

        private static SoundLayer Click(float start, float pitchHz)
        {
            return new SoundLayer(start, 0.06f, 0.0003f, 0.008f, 0.9f, 7000f, 0.5f, pitchHz, pitchHz * 0.85f);
        }

        private static SoundLayer Slide(float start, float duration)
        {
            return new SoundLayer(start, duration, duration * 0.3f, duration * 0.35f, 0.35f, 2500f);
        }

        internal static string ValidateFolderPath(string path, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("folder path must not be empty", parameterName);
            }

            var normalized = path.Trim().Replace('\\', '/').TrimEnd('/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException($"folder path '{path}' must be under Assets/", parameterName);
            }

            foreach (var segment in normalized.Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == "..")
                {
                    throw new ArgumentException($"folder path '{path}' must not contain empty, '.' or '..' segments", parameterName);
                }
            }

            return normalized;
        }

        private static string ToFullPath(string assetPath)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot ?? string.Empty, assetPath);
        }

        internal static AudioClip WriteSound(string path, SoundSpec spec)
        {
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null && !(existing is AudioClip))
            {
                throw new InvalidOperationException($"'{path}' is a {existing.GetType().Name}, not an AudioClip; move it away first");
            }

            var bytes = ProceduralAudio.Render(spec);
            var fullPath = ToFullPath(path);

            // 中身が同じなら書かない。更新日時が変わるだけでも再インポートが走るため。
            var changed = !File.Exists(fullPath) || !BytesEqual(File.ReadAllBytes(fullPath), bytes);
            if (changed)
            {
                File.WriteAllBytes(fullPath, bytes);
            }

            if (changed || existing == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                throw new InvalidOperationException($"'{path}' was written but did not import as an AudioClip");
            }

            return clip;
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (var i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static Texture2D BuildSoftDot(string path)
        {
            var pixels = new Color32[SoftDotSize * SoftDotSize];
            var center = (SoftDotSize - 1) * 0.5f;
            for (var y = 0; y < SoftDotSize; y++)
            {
                for (var x = 0; x < SoftDotSize; x++)
                {
                    var dx = (x - center) / center;
                    var dy = (y - center) / center;
                    var falloff = Mathf.Clamp01(1f - Mathf.Sqrt((dx * dx) + (dy * dy)));
                    pixels[(y * SoftDotSize) + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(falloff * falloff * 255f));
                }
            }

            return WriteTexture(path, SoftDotTextureName, SoftDotSize, pixels, true);
        }

        /// <summary>
        /// size × size の RGBA32 の pixels (下の行から) を path の Texture2D のアセットに書いて保存する。既存のアセットは GUID を保って中身だけ替える。
        /// path に別の型のアセットがあれば例外を投げる。
        /// </summary>
        internal static Texture2D WriteTexture(string path, string textureName, int size, Color32[] pixels, bool mipChain)
        {
            if (size < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, "must be at least 1");
            }

            if (pixels == null || pixels.Length != size * size)
            {
                throw new ArgumentException($"texture '{path}' needs {size * size} pixels", nameof(pixels));
            }

            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null && !(existing is Texture2D))
            {
                throw new InvalidOperationException($"'{path}' is a {existing.GetType().Name}, not a Texture2D; move it away first");
            }

            var texture = (Texture2D)existing;
            var created = texture == null;
            if (created)
            {
                texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain) { name = textureName };
            }
            else if (texture.width != size || texture.height != size || texture.format != TextureFormat.RGBA32
                || (texture.mipmapCount > 1) != mipChain)
            {
                texture.Reinitialize(size, size, TextureFormat.RGBA32, mipChain);
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels32(pixels);
            texture.Apply(true, false);

            if (created)
            {
                AssetDatabase.CreateAsset(texture, path);
            }
            else
            {
                EditorUtility.SetDirty(texture);
            }

            AssetDatabase.SaveAssetIfDirty(texture);
            return texture;
        }

        private static Material BuildParticleMaterial(string path, Texture2D texture, float blend)
        {
            var shader = Shader.Find(ParticleShaderName);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    $"shader '{ParticleShaderName}' was not found; the effects need the Universal Render Pipeline package");
            }

            var fresh = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            fresh.SetTexture("_BaseMap", texture);
            fresh.SetColor("_BaseColor", Color.white);
            fresh.SetFloat("_Surface", SurfaceTransparent);
            fresh.SetFloat("_Blend", blend);
            fresh.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            fresh.SetFloat("_DstBlend", (float)(blend == BlendAdditive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            fresh.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            fresh.SetFloat("_DstBlendAlpha", (float)(blend == BlendAdditive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            fresh.SetFloat("_ZWrite", 0f);
            fresh.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            fresh.SetOverrideTag("RenderType", "Transparent");
            fresh.renderQueue = (int)RenderQueue.Transparent;

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

        private sealed class ParticleSettings
        {
            public float Duration = 0.5f;
            public Vector2 Lifetime = new Vector2(0.5f, 0.5f);
            public Vector2 Speed;
            public Vector2 Size = new Vector2(0.1f, 0.1f);
            public Color Color = Color.white;
            public float Gravity;
            public short Burst = 1;
            public int MaxParticles = 16;
            public bool WorldSpace;
            public bool RandomRotation;
            public ParticleSystemShapeType? Shape;
            public float ShapeAngle;
            public float ShapeRadius;
            public float SizeStart = 1f;
            public float SizeEnd = 1f;
            public Gradient Fade;
            public bool Stretched;
            public float LengthScale = 1f;
            public float VelocityScale;
            public Material Material;
            public bool DestroyOnStop;
        }

        private static void AddMuzzleFlash(GameObject root, Material material, float minSize, float maxSize, Color color, float lifetime, short burst)
        {
            AddParticles(root, new ParticleSettings
            {
                Duration = 0.1f,
                Lifetime = new Vector2(lifetime, lifetime),
                Size = new Vector2(minSize, maxSize),
                Color = color,
                Burst = burst,
                MaxParticles = burst,
                RandomRotation = true,
                SizeStart = 1f,
                SizeEnd = 0.3f,
                Fade = Fade(Color.white, Color.white, 1f, 0f),
                Material = material,
                DestroyOnStop = true,
            });
        }

        private static void AddImpactSpark(GameObject root, Material material)
        {
            AddParticles(root, new ParticleSettings
            {
                Duration = 0.2f,
                Lifetime = new Vector2(0.15f, 0.35f),
                Speed = new Vector2(3f, 8f),
                Size = new Vector2(0.02f, 0.04f),
                Color = new Color(1f, 0.8f, 0.4f),
                Gravity = 1f,
                Burst = 14,
                MaxParticles = 14,
                WorldSpace = true,
                Shape = ParticleSystemShapeType.Cone,
                ShapeAngle = 35f,
                ShapeRadius = 0.01f,
                Fade = Fade(Color.white, new Color(1f, 0.5f, 0.2f), 1f, 0f),
                Stretched = true,
                LengthScale = 1f,
                VelocityScale = 0.02f,
                Material = material,
                DestroyOnStop = true,
            });
        }

        private static void AddTracer(GameObject root, Material material)
        {
            AddParticles(root, new ParticleSettings
            {
                Duration = TracerLifetime,
                Lifetime = new Vector2(TracerLifetime, TracerLifetime),
                Speed = new Vector2(TracerSpeed, TracerSpeed),
                Size = new Vector2(0.025f, 0.025f),
                Color = new Color(1f, 0.85f, 0.5f),
                Burst = 1,
                MaxParticles = 1,
                WorldSpace = true,
                Fade = Fade(Color.white, Color.white, 1f, 0.6f),
                Stretched = true,
                LengthScale = 1f,
                VelocityScale = 0.01f,
                Material = material,
                DestroyOnStop = true,
            });
        }

        // 煙を親にするのは、いちばん長く残る系が止まったときに全体を消すため (子が先に消されない)。
        private static void AddExplosion(GameObject root, Material additive, Material alpha)
        {
            AddParticles(root, new ParticleSettings
            {
                Duration = 0.5f,
                Lifetime = new Vector2(1.5f, 2.5f),
                Speed = new Vector2(0.5f, 2f),
                Size = new Vector2(1.5f, 3f),
                Color = new Color(0.35f, 0.33f, 0.3f, 0.6f),
                Gravity = -0.05f,
                Burst = 12,
                MaxParticles = 12,
                WorldSpace = true,
                RandomRotation = true,
                Shape = ParticleSystemShapeType.Sphere,
                ShapeRadius = 0.5f,
                SizeStart = 0.6f,
                SizeEnd = 1.4f,
                Fade = Fade(Color.white, Color.white, 1f, 0f),
                Material = alpha,
                DestroyOnStop = true,
            });

            var fireball = new GameObject("Fireball");
            fireball.transform.SetParent(root.transform, false);
            AddParticles(fireball, new ParticleSettings
            {
                Duration = 0.3f,
                Lifetime = new Vector2(0.3f, 0.6f),
                Speed = new Vector2(3f, 8f),
                Size = new Vector2(0.8f, 1.6f),
                Color = new Color(1f, 0.55f, 0.15f),
                Burst = 25,
                MaxParticles = 25,
                WorldSpace = true,
                RandomRotation = true,
                Shape = ParticleSystemShapeType.Sphere,
                ShapeRadius = 0.3f,
                SizeStart = 0.7f,
                SizeEnd = 1.3f,
                Fade = Fade(new Color(1f, 1f, 0.8f), new Color(0.6f, 0.15f, 0.05f), 1f, 0f),
                Material = additive,
            });

            var sparks = new GameObject("Sparks");
            sparks.transform.SetParent(root.transform, false);
            AddParticles(sparks, new ParticleSettings
            {
                Duration = 0.3f,
                Lifetime = new Vector2(0.4f, 0.9f),
                Speed = new Vector2(8f, 16f),
                Size = new Vector2(0.03f, 0.06f),
                Color = new Color(1f, 0.75f, 0.35f),
                Gravity = 1f,
                Burst = 30,
                MaxParticles = 30,
                WorldSpace = true,
                Shape = ParticleSystemShapeType.Sphere,
                ShapeRadius = 0.2f,
                Fade = Fade(Color.white, new Color(1f, 0.4f, 0.1f), 1f, 0f),
                Stretched = true,
                LengthScale = 1f,
                VelocityScale = 0.02f,
                Material = additive,
            });
        }

        private static Gradient Fade(Color startColor, Color endColor, float startAlpha, float endAlpha)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(startColor, 0f), new GradientColorKey(endColor, 1f) },
                new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(endAlpha, 1f) });
            return gradient;
        }

        private static void AddParticles(GameObject target, ParticleSettings settings)
        {
            var system = target.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // 新しい系には毎回ちがう種が入り、プレハブを作り直すたびに差分が出るので固定する。
            // 再生のたびの種は useAutoRandomSeed で選ばれるので、見た目は毎回ばらつく。
            system.useAutoRandomSeed = false;
            system.randomSeed = 0;
            system.useAutoRandomSeed = true;

            var main = system.main;
            main.duration = settings.Duration;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(settings.Lifetime.x, settings.Lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(settings.Speed.x, settings.Speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(settings.Size.x, settings.Size.y);
            main.startRotation = settings.RandomRotation ? new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f) : new ParticleSystem.MinMaxCurve(0f);
            main.startColor = settings.Color;
            main.gravityModifier = settings.Gravity;
            main.simulationSpace = settings.WorldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = settings.MaxParticles;
            main.stopAction = settings.DestroyOnStop ? ParticleSystemStopAction.Destroy : ParticleSystemStopAction.None;

            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, settings.Burst) });

            // 形を切ると、粒は原点から +Z へまっすぐ出る (曳光弾はこれを使う)。
            var shape = system.shape;
            shape.enabled = settings.Shape.HasValue;
            if (settings.Shape.HasValue)
            {
                shape.shapeType = settings.Shape.Value;
                shape.angle = settings.ShapeAngle;
                shape.radius = settings.ShapeRadius;
            }

            var sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = !Mathf.Approximately(settings.SizeStart, 1f) || !Mathf.Approximately(settings.SizeEnd, 1f);
            if (sizeOverLifetime.enabled)
            {
                var multiplier = Mathf.Max(settings.SizeStart, settings.SizeEnd);
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                    multiplier, AnimationCurve.Linear(0f, settings.SizeStart / multiplier, 1f, settings.SizeEnd / multiplier));
            }

            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = settings.Fade != null;
            if (settings.Fade != null)
            {
                colorOverLifetime.color = new ParticleSystem.MinMaxGradient(settings.Fade);
            }

            var renderer = target.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = settings.Stretched ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            renderer.lengthScale = settings.LengthScale;
            renderer.velocityScale = settings.VelocityScale;
            renderer.sharedMaterial = settings.Material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        /// <summary>
        /// folder/name.prefab に build で組んだプレハブを保存して返す。既にあって組み方 (子の名前と並び・部品の型) が同じなら、
        /// 読み込んだ中身に値だけを貼り付けて保存する。作り直すとプレハブ内の fileID が毎回変わり、差分が出るため。
        /// 組み方が変わったときだけ丸ごと上書きする (アセットの GUID は保たれる)。
        /// </summary>
        internal static GameObject SavePrefab(string folder, string name, Action<GameObject> build)
        {
            var path = $"{folder}/{name}.prefab";
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null && !(existing is GameObject))
            {
                throw new InvalidOperationException($"'{path}' is a {existing.GetType().Name}, not a prefab; move it away first");
            }

            var source = new GameObject(name);
            try
            {
                build(source);

                if (existing != null)
                {
                    var contents = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        if (SameStructure(source, contents))
                        {
                            CopyValues(source, contents);
                            SavePrefabAsset(contents, path);
                            return LoadPrefab(path);
                        }
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(contents);
                    }
                }

                SavePrefabAsset(source, path);
                return LoadPrefab(path);
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        private static void SavePrefabAsset(GameObject root, string path)
        {
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path, out var success);
            if (!success || saved == null)
            {
                throw new InvalidOperationException($"failed to save the prefab '{path}'");
            }
        }

        private static GameObject LoadPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                throw new InvalidOperationException($"the prefab '{path}' was saved but could not be loaded");
            }

            return prefab;
        }

        private static bool SameStructure(GameObject a, GameObject b)
        {
            var aComponents = a.GetComponents<Component>();
            var bComponents = b.GetComponents<Component>();
            if (aComponents.Length != bComponents.Length || a.transform.childCount != b.transform.childCount)
            {
                return false;
            }

            for (var i = 0; i < aComponents.Length; i++)
            {
                if (aComponents[i] == null || bComponents[i] == null || aComponents[i].GetType() != bComponents[i].GetType())
                {
                    return false;
                }
            }

            for (var i = 0; i < a.transform.childCount; i++)
            {
                var aChild = a.transform.GetChild(i).gameObject;
                var bChild = b.transform.GetChild(i).gameObject;
                if (aChild.name != bChild.name || !SameStructure(aChild, bChild))
                {
                    return false;
                }
            }

            return true;
        }

        private static void CopyValues(GameObject source, GameObject target)
        {
            target.layer = source.layer;
            target.SetActive(source.activeSelf);

            // Transform は値を貼ると親子の参照まで写ってしまうので、位置・回転・大きさだけを写す。
            target.transform.localPosition = source.transform.localPosition;
            target.transform.localRotation = source.transform.localRotation;
            target.transform.localScale = source.transform.localScale;

            var sourceComponents = source.GetComponents<Component>();
            var targetComponents = target.GetComponents<Component>();
            for (var i = 0; i < sourceComponents.Length; i++)
            {
                if (sourceComponents[i] is Transform)
                {
                    continue;
                }

                if (!ComponentUtility.CopyComponent(sourceComponents[i]) || !ComponentUtility.PasteComponentValues(targetComponents[i]))
                {
                    throw new InvalidOperationException(
                        $"failed to copy {sourceComponents[i].GetType().Name} of '{source.name}' into the existing prefab");
                }
            }

            for (var i = 0; i < source.transform.childCount; i++)
            {
                CopyValues(source.transform.GetChild(i).gameObject, target.transform.GetChild(i).gameObject);
            }
        }
    }
}
