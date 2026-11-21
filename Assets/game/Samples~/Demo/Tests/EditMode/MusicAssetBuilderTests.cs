using System;
using System.IO;
using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    // リポジトリの Audio と Presentation を触らないよう、一時フォルダに走らせる。
    public sealed class MusicAssetBuilderTests
    {
        private string _folder;
        private string _audioFolder;
        private string _catalogPath;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__MusicAssetBuilderTests_" + Guid.NewGuid().ToString("N");
            _audioFolder = _folder + "/Audio/Music";
            _catalogPath = _folder + "/Presentation/Resources/Presentation/MusicCatalog.asset";
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(_folder))
            {
                AssetDatabase.DeleteAsset(_folder);
            }
        }

        [Test]
        public void TheCatalogGoesWhereMusicCatalogLoadLooks()
        {
            Assert.That(
                MusicAssetBuilder.CatalogPath,
                Is.EqualTo(DemoSample.Root + "/Presentation/Resources/Presentation/MusicCatalog.asset"));
            Assert.That(MusicAssetBuilder.CatalogPath, Does.EndWith("/Resources/" + MusicCatalog.ResourcePath + ".asset"));
            Assert.That(MusicAssetBuilder.AudioFolder, Is.EqualTo(PresentationAssetBuilder.AudioFolder + "/Music"));
        }

        [Test]
        public void TheStepRunsFromTheBatchEntry()
        {
            var steps = PresentationAssetSteps.Discover().ToList();

            var step = steps.OfType<MusicAssetStep>().Single();
            Assert.That(step.Name, Is.EqualTo("Music"));
            Assert.That(step.Order, Is.GreaterThan(PresentationCatalogAssetStep.StepOrder));
        }

        [Test]
        public void EveryCueHasItsOwnUniquelyNamedClip()
        {
            var names = MusicAssetBuilder.SoundSpecs().Select(spec => spec.Name).ToList();

            Assert.That(names, Is.Unique);
            Assert.That(names, Is.EquivalentTo(new[]
            {
                MusicAssetBuilder.MenuSound,
                MusicAssetBuilder.MatchSound,
                MusicAssetBuilder.VictorySound,
                MusicAssetBuilder.DefeatSound,
                MusicAssetBuilder.MatchEndSound,
            }));
        }

        // 1 周の長さを拍の整数倍にしておかないと、くり返したときにつなぎ目で拍がずれる
        [Test]
        public void TheLoopsLastAWholeNumberOfBeats()
        {
            foreach (var name in new[] { MusicAssetBuilder.MenuSound, MusicAssetBuilder.MatchSound })
            {
                var spec = MusicAssetBuilder.SoundSpecs().Single(s => s.Name == name);
                var beats = spec.LengthSeconds / MusicAssetBuilder.BeatSeconds;

                Assert.That(spec.LengthSeconds, Is.EqualTo(MusicAssetBuilder.LoopSeconds), name);
                Assert.That(beats, Is.EqualTo(Mathf.Round(beats)).Within(1e-4f), name);
            }
        }

        [Test]
        public void TheMenuAndTheMatchSoundDifferent()
        {
            var menu = ProceduralAudio.Synthesize(MusicAssetBuilder.SoundSpecs().Single(s => s.Name == MusicAssetBuilder.MenuSound));
            var match = ProceduralAudio.Synthesize(MusicAssetBuilder.SoundSpecs().Single(s => s.Name == MusicAssetBuilder.MatchSound));

            Assert.That(menu, Has.Length.EqualTo(match.Length));
            Assert.That(menu, Is.Not.EqualTo(match));
            Assert.That(menu.Select(s => Mathf.Abs(s)).Max(), Is.GreaterThan(0.5f));
            Assert.That(match.Select(s => Mathf.Abs(s)).Max(), Is.GreaterThan(0.5f));
        }

        [Test]
        public void BuildWritesTheClipsAndAValidCatalog()
        {
            var catalog = MusicAssetBuilder.BuildAt(_audioFolder, _catalogPath);

            Assert.That(catalog, Is.SameAs(AssetDatabase.LoadAssetAtPath<MusicCatalog>(_catalogPath)));
            Assert.DoesNotThrow(() => catalog.Validate());
            Assert.That(AssetDatabase.GetAssetPath(catalog.Menu), Is.EqualTo(_audioFolder + "/" + MusicAssetBuilder.MenuSound + ".wav"));
            Assert.That(AssetDatabase.GetAssetPath(catalog.Match), Is.EqualTo(_audioFolder + "/" + MusicAssetBuilder.MatchSound + ".wav"));
            Assert.That(AssetDatabase.GetAssetPath(catalog.Victory), Is.EqualTo(_audioFolder + "/" + MusicAssetBuilder.VictorySound + ".wav"));
            Assert.That(AssetDatabase.GetAssetPath(catalog.Defeat), Is.EqualTo(_audioFolder + "/" + MusicAssetBuilder.DefeatSound + ".wav"));
            Assert.That(AssetDatabase.GetAssetPath(catalog.MatchEnd), Is.EqualTo(_audioFolder + "/" + MusicAssetBuilder.MatchEndSound + ".wav"));
            Assert.That(catalog.ClipFor(MusicCue.Menu), Is.Not.SameAs(catalog.ClipFor(MusicCue.Match)), "the menu and the match play different music");
            Assert.That(catalog.Volume, Is.EqualTo(MusicAssetBuilder.Volume));
            Assert.That(catalog.FadeSeconds, Is.EqualTo(MusicAssetBuilder.FadeSeconds));
            Assert.That(catalog.LoopMatchEnd, Is.False);
        }

        [Test]
        public void RebuildingKeepsEveryFileAndGuidUnchanged()
        {
            MusicAssetBuilder.BuildAt(_audioFolder, _catalogPath);
            var paths = new[] { _catalogPath }
                .Concat(MusicAssetBuilder.SoundSpecs().Select(spec => _audioFolder + "/" + spec.Name + ".wav"))
                .ToArray();
            var guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            var bytes = paths.Select(path => File.ReadAllBytes(ToFullPath(path))).ToArray();

            MusicAssetBuilder.BuildAt(_audioFolder, _catalogPath);

            Assert.That(paths.Select(AssetDatabase.AssetPathToGUID).ToArray(), Is.EqualTo(guids));
            for (var i = 0; i < paths.Length; i++)
            {
                Assert.That(File.ReadAllBytes(ToFullPath(paths[i])), Is.EqualTo(bytes[i]), paths[i]);
            }
        }

        [Test]
        public void RejectsFoldersOutsideAssets()
        {
            Assert.Throws<ArgumentException>(() => MusicAssetBuilder.BuildAt("Packages/x", _catalogPath));
            Assert.Throws<ArgumentException>(() => MusicAssetBuilder.BuildAt(_audioFolder, "../x.asset"));
            Assert.That(AssetDatabase.IsValidFolder(_folder), Is.False, "nothing is written when the paths are wrong");
        }

        private static string ToFullPath(string assetPath)
        {
            return Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, assetPath);
        }
    }
}
