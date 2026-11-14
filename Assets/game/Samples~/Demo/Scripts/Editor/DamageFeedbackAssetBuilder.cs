using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Editor
{
    /// <summary>
    /// 被弾の画面効果と低体力の演出の仮素材 (端が赤く中央が透明な画像・心音・息切れの音) と <see cref="DamageFeedbackCatalog"/> を作るか上書きする。
    /// 音は <see cref="ProceduralAudio"/> で合成して <see cref="PresentationAssetBuilder.AudioFolder"/> に WAV で、画像は
    /// <see cref="PresentationAssetBuilder.PresentationFolder"/> に Texture2D のアセットで置く。
    /// 既存のアセットは GUID を保って中身だけ更新し、同じコードからは同じファイルができる (2 回走らせても差分が出ない)。
    /// 一括実行の入口 (<see cref="PresentationAssetSteps.BuildAll"/>) からは <see cref="DamageFeedbackAssetStep"/> として呼ばれる。
    /// </summary>
    public static class DamageFeedbackAssetBuilder
    {
        public const string LogTag = "[DamageFeedbackAssetBuilder]";

        public const string HeartbeatSound = "Heartbeat";
        public const string BreathSound = "BreathRecovery";
        public const string VignetteTextureName = "DamageVignette";

        /// <summary>画像の一辺 (px)。画面に引き伸ばすので、なめらかなグラデーションに足りる大きさにする。</summary>
        public const int VignetteSize = 256;

        /// <summary>この半径 (画像の中心から辺の中点までを 1 とする) より内側は透明。</summary>
        public const float VignetteInnerRadius = 0.55f;

        /// <summary>この半径より外側は <see cref="VignetteColor"/> の不透明度 1。</summary>
        public const float VignetteOuterRadius = 1.15f;

        /// <summary>画像の端の色 (不透明度は半径で決める)。</summary>
        public static readonly Color32 VignetteColor = new Color32(200, 16, 16, 255);

        /// <summary>
        /// カタログの置き場所。実行時に <see cref="DamageFeedbackCatalog.Load"/> で読めるよう、Resources の
        /// <see cref="DamageFeedbackCatalog.ResourcePath"/> に置く。
        /// </summary>
        public static string CatalogPath => PresentationAssetBuilder.PresentationFolder + "/Resources/" + DamageFeedbackCatalog.ResourcePath + ".asset";

        /// <summary>書き出す音の設計図。名前がそのままファイル名 (.wav) になる。</summary>
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

                // 吸って吐くを 2 回。吸う音は高め・弱め、吐く音は低め・強めのノイズで、2 回目は落ち着いて弱くする。
                new SoundSpec(BreathSound, 3201u, 2.9f, 0.6f, new[]
                {
                    new SoundLayer(0f, 0.5f, 0.22f, 0.3f, 0.55f, 2200f),
                    new SoundLayer(0.6f, 0.75f, 0.06f, 0.3f, 1f, 1100f),
                    new SoundLayer(1.5f, 0.45f, 0.2f, 0.28f, 0.35f, 2000f),
                    new SoundLayer(2.05f, 0.75f, 0.08f, 0.3f, 0.55f, 900f),
                }),
            };
        }

        /// <summary>
        /// size × size の画像の画素 (下の行から)。色は <see cref="VignetteColor"/> で、不透明度は中心からの距離で
        /// <see cref="VignetteInnerRadius"/> の 0 から <see cref="VignetteOuterRadius"/> の 1 へなめらかに上げる。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">size が 2 未満。</exception>
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

        /// <summary>
        /// <see cref="CatalogPath"/> のカタログを読み、<see cref="DamageFeedbackCatalog.Validate"/> に通ることを確かめて返す。
        /// 無ければ先に <see cref="PresentationAssetSteps.BuildAll"/> を走らせるよう例外を投げる。
        /// </summary>
        public static DamageFeedbackCatalog LoadCatalog()
        {
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<DamageFeedbackCatalog>(CatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    $"damage feedback catalog '{CatalogPath}' was not found; run {PresentationAssetSteps.MenuPath} (Fps.Editor.PresentationAssetSteps.BuildAll) first");
            }

            catalog.Validate();
            return catalog;
        }

        /// <summary>既定の場所 (<see cref="PresentationAssetBuilder.AudioFolder"/>・<see cref="PresentationAssetBuilder.PresentationFolder"/>・<see cref="CatalogPath"/>) に作る。</summary>
        public static DamageFeedbackCatalog Build()
        {
            return BuildAt(PresentationAssetBuilder.AudioFolder, PresentationAssetBuilder.PresentationFolder, CatalogPath);
        }

        /// <summary>
        /// audioFolder に心音と息切れの音を、presentationFolder に画像を、catalogPath にそれをまとめたカタログを
        /// 作るか上書きして保存し、カタログを返す。フォルダは Assets/ 以下で、無ければ作る。
        /// </summary>
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
