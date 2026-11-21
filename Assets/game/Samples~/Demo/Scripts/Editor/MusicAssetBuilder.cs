using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Editor
{
    public static class MusicAssetBuilder
    {
        public const string LogTag = "[MusicAssetBuilder]";

        public const string MenuSound = "MusicMenu";
        public const string MatchSound = "MusicMatch";
        public const string VictorySound = "MusicVictory";
        public const string DefeatSound = "MusicDefeat";
        public const string MatchEndSound = "MusicMatchEnd";

        public const string MusicSubfolder = "Music";

        // 4 小節 (4 拍子・120 拍/分) で 1 周するので、くり返しても拍がずれない。
        public const float LoopSeconds = 8f;

        public const float BeatSeconds = 0.5f;

        public const float StingerSeconds = 3.5f;

        public const float Volume = 0.5f;

        public const float FadeSeconds = 1.2f;

        // ラ・ファ・ド・ソ (Am-F-C-G) の和音を 2 秒ずつ。根音と 3 度・5 度の Hz。
        private static readonly float[][] _progression =
        {
            new[] { 220.00f, 261.63f, 329.63f },
            new[] { 174.61f, 220.00f, 261.63f },
            new[] { 130.81f, 164.81f, 196.00f },
            new[] { 196.00f, 246.94f, 293.66f },
        };

        public static string AudioFolder => PresentationAssetBuilder.AudioFolder + "/" + MusicSubfolder;

        // 実行時に MusicCatalog.Load で読めるよう、Resources の下に置く。
        public static string CatalogPath => PresentationAssetBuilder.PresentationFolder + "/Resources/" + MusicCatalog.ResourcePath + ".asset";

        public static IReadOnlyList<SoundSpec> SoundSpecs()
        {
            return new[]
            {
                new SoundSpec(MenuSound, 4101u, LoopSeconds, 0.8f, MenuLayers()),
                new SoundSpec(MatchSound, 4201u, LoopSeconds, 0.85f, MatchLayers()),
                new SoundSpec(VictorySound, 4301u, StingerSeconds, 0.8f, VictoryLayers()),
                new SoundSpec(DefeatSound, 4401u, StingerSeconds, 0.8f, DefeatLayers()),
                new SoundSpec(MatchEndSound, 4501u, StingerSeconds, 0.75f, MatchEndLayers()),
            };
        }

        public static MusicCatalog LoadCatalog()
        {
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<MusicCatalog>(CatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    $"music catalog '{CatalogPath}' was not found; run {PresentationAssetSteps.MenuPath} (Shooter.Editor.PresentationAssetSteps.BuildAll) first");
            }

            catalog.Validate();
            return catalog;
        }

        public static MusicCatalog Build()
        {
            return BuildAt(AudioFolder, CatalogPath);
        }

        public static MusicCatalog BuildAt(string audioFolder, string catalogPath)
        {
            audioFolder = PresentationAssetBuilder.ValidateFolderPath(audioFolder, nameof(audioFolder));
            catalogPath = SceneBuildHelpers.ValidateAssetPath(catalogPath);

            SceneBuildHelpers.EnsureFolder(audioFolder);
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(catalogPath)?.Replace('\\', '/'));

            var clips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
            foreach (var spec in SoundSpecs())
            {
                clips.Add(spec.Name, PresentationAssetBuilder.WriteSound($"{audioFolder}/{spec.Name}.wav", spec));
            }

            var catalog = SceneBuildHelpers.CreateOrUpdateAsset<MusicCatalog>(catalogPath, c =>
            {
                // 既存のアセットを書き換える前に、同じ中身の一時インスタンスで検査する (不正ならメモリ上の中身も汚さない)。
                var probe = ScriptableObject.CreateInstance<MusicCatalog>();
                try
                {
                    probe.name = Path.GetFileNameWithoutExtension(catalogPath);
                    Configure(probe, clips);
                    probe.Validate();
                }
                finally
                {
                    Object.DestroyImmediate(probe);
                }

                Configure(c, clips);
            });

            Debug.Log($"{LogTag} saved {clips.Count} music clip(s) to '{audioFolder}' and '{catalogPath}'");
            return catalog;
        }

        private static void Configure(MusicCatalog catalog, IReadOnlyDictionary<string, AudioClip> clips)
        {
            catalog.Menu = clips[MenuSound];
            catalog.Match = clips[MatchSound];
            catalog.Victory = clips[VictorySound];
            catalog.Defeat = clips[DefeatSound];
            catalog.MatchEnd = clips[MatchEndSound];
            catalog.Volume = Volume;
            catalog.FadeSeconds = FadeSeconds;
            catalog.LoopMatchEnd = false;
        }

        private static IReadOnlyList<SoundLayer> MenuLayers()
        {
            var layers = new List<SoundLayer>();
            var bar = LoopSeconds / _progression.Length;
            for (var i = 0; i < _progression.Length; i++)
            {
                var start = i * bar;
                foreach (var hz in _progression[i])
                {
                    layers.Add(new SoundLayer(start, bar, 0.4f, 1.6f, 0f, 0f, 0.22f, hz, hz));
                }

                var eighth = BeatSeconds / 2f;
                var steps = (int)Math.Round(bar / eighth);
                for (var step = 0; step < steps; step++)
                {
                    var hz = _progression[i][step % _progression[i].Length] * 2f;
                    layers.Add(Note(start + (step * eighth), eighth, hz, 0.18f));
                }
            }

            return layers;
        }

        private static IReadOnlyList<SoundLayer> MatchLayers()
        {
            var layers = new List<SoundLayer>();
            var beats = (int)Math.Round(LoopSeconds / BeatSeconds);
            for (var beat = 0; beat < beats; beat++)
            {
                var start = beat * BeatSeconds;
                layers.Add(new SoundLayer(start, 0.2f, 0.002f, 0.09f, 0f, 0f, 1f, 130f, 45f));
                layers.Add(new SoundLayer(start + (BeatSeconds / 2f), 0.06f, 0.001f, 0.02f, 0.35f, 9000f));
            }

            var bar = LoopSeconds / _progression.Length;
            var eighth = BeatSeconds / 2f;
            for (var i = 0; i < _progression.Length; i++)
            {
                var root = _progression[i][0] / 2f;
                var steps = (int)Math.Round(bar / eighth);
                for (var step = 0; step < steps; step++)
                {
                    layers.Add(Note((i * bar) + (step * eighth), eighth * 0.9f, root, 0.35f, ToneWaveform.Saw));
                }

                foreach (var hz in _progression[i])
                {
                    layers.Add(new SoundLayer(i * bar, bar, 0.05f, 0.8f, 0f, 0f, 0.08f, hz * 2f, hz * 2f, ToneWaveform.Saw));
                }
            }

            return layers;
        }

        private static IReadOnlyList<SoundLayer> VictoryLayers()
        {
            return Stinger(new[] { 261.63f, 329.63f, 392.00f, 523.25f }, new[] { 261.63f, 329.63f, 392.00f, 523.25f });
        }

        private static IReadOnlyList<SoundLayer> DefeatLayers()
        {
            return Stinger(new[] { 329.63f, 311.13f, 293.66f, 220.00f }, new[] { 110.00f, 220.00f, 261.63f, 329.63f });
        }

        // 引き分けや観戦で流すので、長調とも短調とも聞こえない掛留の和音 (レ・ソ・ラ) にする。
        private static IReadOnlyList<SoundLayer> MatchEndLayers()
        {
            return Stinger(new[] { 293.66f, 392.00f }, new[] { 146.83f, 293.66f, 392.00f, 440.00f });
        }

        private static IReadOnlyList<SoundLayer> Stinger(IReadOnlyList<float> run, IReadOnlyList<float> chord)
        {
            const float step = 0.18f;
            var layers = new List<SoundLayer>();
            for (var i = 0; i < run.Count; i++)
            {
                layers.Add(Note(i * step, step * 1.5f, run[i], 0.4f));
            }

            var held = run.Count * step;
            foreach (var hz in chord)
            {
                layers.Add(new SoundLayer(held, StingerSeconds - held, 0.02f, 1.1f, 0f, 0f, 0.3f, hz, hz));
            }

            return layers;
        }

        private static SoundLayer Note(float start, float duration, float hz, float gain, ToneWaveform waveform = ToneWaveform.Sine)
        {
            return new SoundLayer(start, duration, 0.008f, duration * 0.6f, 0f, 0f, gain, hz, hz, waveform);
        }
    }
}
