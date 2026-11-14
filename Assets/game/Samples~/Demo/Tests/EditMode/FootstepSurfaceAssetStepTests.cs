using System;
using System.Linq;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="FootstepSurfaceAssetStep"/> を一時フォルダに走らせて、材質ごとの音とカタログ・作り直したときの GUID・一括実行に入ることを確かめる。
    /// リポジトリの Assets/game/Audio と Assets/game/Presentation は触らない。
    /// </summary>
    public sealed class FootstepSurfaceAssetStepTests
    {
        private string _folder;
        private string _audioFolder;
        private string _catalogPath;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__FootstepSurfaceAssetStepTests_" + Guid.NewGuid().ToString("N");
            _audioFolder = _folder + "/Audio/Footsteps";
            _catalogPath = _folder + "/Resources/Presentation/FootstepSurfaceCatalog.asset";
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
        public void DefaultPathsAreWhereTheCatalogIsLoadedFrom()
        {
            Assert.That(FootstepSurfaceAssetStep.CatalogPath,
                Is.EqualTo(DemoSample.Root + "/Presentation/Resources/Presentation/FootstepSurfaceCatalog.asset"));
            Assert.That(FootstepSurfaceAssetStep.CatalogPath, Does.EndWith("/Resources/" + FootstepSurfaceCatalog.ResourcePath + ".asset"),
                "CharacterAudio reads the catalog with Resources.Load");
            Assert.That(FootstepSurfaceAssetStep.AudioFolder, Is.EqualTo(DemoSample.Root + "/Audio/Footsteps"));
        }

        [Test]
        public void TheBatchEntryRunsTheStepAfterThePresentationCatalog()
        {
            var steps = PresentationAssetSteps.Discover();
            var names = steps.Select(step => step.GetType()).ToList();

            Assert.That(names, Has.Member(typeof(FootstepSurfaceAssetStep)));
            Assert.That(names.IndexOf(typeof(FootstepSurfaceAssetStep)), Is.GreaterThan(names.IndexOf(typeof(PresentationCatalogAssetStep))));
            Assert.That(new FootstepSurfaceAssetStep().Order, Is.GreaterThan(PresentationCatalogAssetStep.StepOrder));
        }

        [Test]
        public void SurfacesStartWithDefaultAndCoverTheIssueMaterials()
        {
            Assert.That(FootstepSurfaceAssetStep.Surfaces(), Is.EqualTo(new[] { SurfaceMaterial.DefaultId, "concrete", "metal", "wood" }));
            Assert.Throws<ArgumentException>(() => FootstepSurfaceAssetStep.SoundSpecsFor("glass"));
        }

        [Test]
        public void EverySurfaceSoundsDifferent()
        {
            var renders = FootstepSurfaceAssetStep.Surfaces()
                .Select(id => Convert.ToBase64String(ProceduralAudio.Render(FootstepSurfaceAssetStep.SoundSpecsFor(id).Footsteps[0])))
                .ToList();

            Assert.That(renders.Distinct().Count(), Is.EqualTo(renders.Count));

            var variants = FootstepSurfaceAssetStep.SoundSpecsFor("concrete").Footsteps
                .Select(spec => Convert.ToBase64String(ProceduralAudio.Render(spec)))
                .ToList();
            Assert.That(variants, Has.Count.EqualTo(FootstepSurfaceAssetStep.FootstepVariants));
            Assert.That(variants.Distinct().Count(), Is.EqualTo(variants.Count), "variants differ so picks do not sound repeated");
        }

        [Test]
        public void BuildWritesTheClipsAndAValidCatalog()
        {
            var catalog = FootstepSurfaceAssetStep.BuildAt(_audioFolder, _catalogPath);

            Assert.That(catalog, Is.SameAs(AssetDatabase.LoadAssetAtPath<FootstepSurfaceCatalog>(_catalogPath)));
            Assert.DoesNotThrow(() => catalog.Validate());

            foreach (var id in FootstepSurfaceAssetStep.Surfaces())
            {
                Assert.That(catalog.TryGetSurface(id, out var sounds), Is.True, id);
                Assert.That(sounds.Footsteps, Has.Count.EqualTo(FootstepSurfaceAssetStep.FootstepVariants), id);
                Assert.That(sounds.Landings, Has.Count.EqualTo(1), id);
                Assert.That(sounds.Footsteps.All(clip => clip != null), Is.True, id);
                Assert.That(sounds.Footsteps[0].name, Is.EqualTo(FootstepSurfaceAssetStep.FootstepSoundName(id, 0)));
                Assert.That(sounds.Landings[0].name, Is.EqualTo(FootstepSurfaceAssetStep.LandingSoundName(id)));
                Assert.That(
                    AssetDatabase.GetAssetPath(sounds.Footsteps[0]), Is.EqualTo($"{_audioFolder}/{FootstepSurfaceAssetStep.FootstepSoundName(id, 0)}.wav"));
            }

            var into = new System.Collections.Generic.List<AudioClip>();
            Assert.That(FootstepSurfaceCatalog.CollectClips(catalog, null, "metal", FootstepCueKind.Footstep, into), Is.EqualTo(FootstepClipSource.Surface));
            Assert.That(into.All(clip => clip.name.StartsWith("Footstep_metal_", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void RebuildingKeepsTheGuidsAndTheContents()
        {
            FootstepSurfaceAssetStep.BuildAt(_audioFolder, _catalogPath);
            var catalogGuid = AssetDatabase.AssetPathToGUID(_catalogPath);
            var clipPath = $"{_audioFolder}/{FootstepSurfaceAssetStep.FootstepSoundName("wood", 1)}.wav";
            var clipGuid = AssetDatabase.AssetPathToGUID(clipPath);
            var bytes = System.IO.File.ReadAllBytes(clipPath);

            var rebuilt = FootstepSurfaceAssetStep.BuildAt(_audioFolder, _catalogPath);

            Assert.That(AssetDatabase.AssetPathToGUID(_catalogPath), Is.EqualTo(catalogGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(clipPath), Is.EqualTo(clipGuid));
            Assert.That(System.IO.File.ReadAllBytes(clipPath), Is.EqualTo(bytes));
            Assert.That(rebuilt.Surfaces, Has.Count.EqualTo(FootstepSurfaceAssetStep.Surfaces().Count));
        }

        [Test]
        public void BuildRejectsPathsOutsideAssets()
        {
            Assert.Throws<ArgumentException>(() => FootstepSurfaceAssetStep.BuildAt("Packages/Footsteps", _catalogPath));
            Assert.Throws<ArgumentException>(() => FootstepSurfaceAssetStep.BuildAt(_audioFolder + "/../x", _catalogPath));
        }
    }
}
