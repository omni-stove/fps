using System;
using System.IO;
using System.Linq;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="DamageFeedbackAssetBuilder"/> を一時フォルダに走らせて、生成物・作り直したときの同一性と、一括実行の手順として集まることを確かめる。
    /// リポジトリの Assets/game/Audio と Assets/game/Presentation は触らない。
    /// </summary>
    public sealed class DamageFeedbackAssetBuilderTests
    {
        private string _folder;
        private string _audioFolder;
        private string _presentationFolder;
        private string _catalogPath;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__DamageFeedbackAssetBuilderTests_" + Guid.NewGuid().ToString("N");
            _audioFolder = _folder + "/Audio";
            _presentationFolder = _folder + "/Presentation";
            _catalogPath = _presentationFolder + "/Resources/Presentation/DamageFeedbackCatalog.asset";
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
        public void TheCatalogGoesWhereDamageFeedbackCatalogLoadLooks()
        {
            Assert.That(
                DamageFeedbackAssetBuilder.CatalogPath,
                Is.EqualTo(DemoSample.Root + "/Presentation/Resources/Presentation/DamageFeedbackCatalog.asset"));
            Assert.That(DamageFeedbackAssetBuilder.CatalogPath, Does.EndWith("/Resources/" + DamageFeedbackCatalog.ResourcePath + ".asset"));
        }

        [Test]
        public void TheStepRunsFromTheBatchEntryAfterTheMainCatalog()
        {
            var steps = PresentationAssetSteps.Discover().ToList();

            var step = steps.OfType<DamageFeedbackAssetStep>().Single();
            Assert.That(step.Order, Is.GreaterThan(PresentationCatalogAssetStep.StepOrder));
            Assert.That(step.Name, Is.EqualTo("DamageFeedback"));
            Assert.That(steps.IndexOf(step), Is.GreaterThan(steps.FindIndex(s => s is PresentationCatalogAssetStep)));
        }

        [Test]
        public void SoundNamesDoNotClashWithTheOtherPresentationSounds()
        {
            var ours = DamageFeedbackAssetBuilder.SoundSpecs().Select(spec => spec.Name).ToList();
            var others = PresentationAssetBuilder.SoundSpecs().Select(spec => spec.Name);

            Assert.That(ours, Is.Unique);
            Assert.That(ours.Intersect(others, StringComparer.OrdinalIgnoreCase), Is.Empty, "both write into the same audio folder");
        }

        [Test]
        public void TheHeartbeatEndsInSilenceSoItLoopsWithoutAClick()
        {
            var heartbeat = DamageFeedbackAssetBuilder.SoundSpecs().Single(spec => spec.Name == DamageFeedbackAssetBuilder.HeartbeatSound);

            var samples = ProceduralAudio.Synthesize(heartbeat);

            var tail = samples.Skip(samples.Length - (ProceduralAudio.DefaultSampleRate / 10));
            Assert.That(tail.Select(s => Mathf.Abs(s)).Max(), Is.LessThan(0.01f));
            Assert.That(samples.Select(s => Mathf.Abs(s)).Max(), Is.GreaterThan(0.5f));
        }

        [Test]
        public void TheVignetteIsClearInTheMiddleAndRedAtTheEdges()
        {
            const int size = 64;
            var pixels = DamageFeedbackAssetBuilder.VignettePixels(size);

            Assert.That(pixels, Has.Length.EqualTo(size * size));
            var center = pixels[((size / 2) * size) + (size / 2)];
            var corner = pixels[0];
            var sideMiddle = pixels[((size / 2) * size) + 0];

            Assert.That(center.a, Is.Zero, "the middle of the screen stays clear");
            Assert.That(corner.a, Is.EqualTo(255), "the corners are fully red");
            Assert.That(sideMiddle.a, Is.GreaterThan(128), "the edges are mostly red");
            Assert.That(sideMiddle.a, Is.LessThan(corner.a));
            Assert.That(corner.r, Is.GreaterThan(corner.g));
            Assert.That(corner.r, Is.GreaterThan(corner.b));
            Assert.Throws<ArgumentOutOfRangeException>(() => DamageFeedbackAssetBuilder.VignettePixels(1));
        }

        [Test]
        public void BuildWritesTheVignetteTheSoundsAndAValidCatalog()
        {
            var catalog = DamageFeedbackAssetBuilder.BuildAt(_audioFolder, _presentationFolder, _catalogPath);

            Assert.That(catalog, Is.SameAs(AssetDatabase.LoadAssetAtPath<DamageFeedbackCatalog>(_catalogPath)));
            Assert.DoesNotThrow(() => catalog.Validate());
            Assert.That(AssetDatabase.GetAssetPath(catalog.Vignette), Is.EqualTo(_presentationFolder + "/" + DamageFeedbackAssetBuilder.VignetteTextureName + ".asset"));
            Assert.That(catalog.Vignette.width, Is.EqualTo(DamageFeedbackAssetBuilder.VignetteSize));
            Assert.That(AssetDatabase.GetAssetPath(catalog.Heartbeat), Is.EqualTo(_audioFolder + "/" + DamageFeedbackAssetBuilder.HeartbeatSound + ".wav"));
            Assert.That(AssetDatabase.GetAssetPath(catalog.Breath), Is.EqualTo(_audioFolder + "/" + DamageFeedbackAssetBuilder.BreathSound + ".wav"));
            Assert.That(catalog.ToSettings().LowHealthThreshold, Is.EqualTo(Fps.Core.DamageFeedbackSettings.Default.LowHealthThreshold));
        }

        [Test]
        public void RebuildingKeepsEveryFileAndGuidUnchanged()
        {
            DamageFeedbackAssetBuilder.BuildAt(_audioFolder, _presentationFolder, _catalogPath);
            var paths = new[]
            {
                _catalogPath,
                _presentationFolder + "/" + DamageFeedbackAssetBuilder.VignetteTextureName + ".asset",
                _audioFolder + "/" + DamageFeedbackAssetBuilder.HeartbeatSound + ".wav",
                _audioFolder + "/" + DamageFeedbackAssetBuilder.BreathSound + ".wav",
            };
            var guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            var bytes = paths.Select(path => File.ReadAllBytes(ToFullPath(path))).ToArray();

            DamageFeedbackAssetBuilder.BuildAt(_audioFolder, _presentationFolder, _catalogPath);

            Assert.That(paths.Select(AssetDatabase.AssetPathToGUID).ToArray(), Is.EqualTo(guids));
            for (var i = 0; i < paths.Length; i++)
            {
                Assert.That(File.ReadAllBytes(ToFullPath(paths[i])), Is.EqualTo(bytes[i]), paths[i]);
            }
        }

        [Test]
        public void RejectsFoldersOutsideAssets()
        {
            Assert.Throws<ArgumentException>(() => DamageFeedbackAssetBuilder.BuildAt("Packages/x", _presentationFolder, _catalogPath));
            Assert.Throws<ArgumentException>(() => DamageFeedbackAssetBuilder.BuildAt(_audioFolder, "../x", _catalogPath));
            Assert.That(AssetDatabase.IsValidFolder(_folder), Is.False, "nothing is written when the paths are wrong");
        }

        private static string ToFullPath(string assetPath)
        {
            return Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, assetPath);
        }
    }
}
