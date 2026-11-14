using System;
using System.Linq;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="SurfaceImpactAssetStep"/> を一時フォルダに走らせて、材質ごとの仮素材とカタログ・作り直したときの同一性・銃痕の画素を確かめる。
    /// リポジトリの Assets/game/Audio と Assets/game/Presentation は触らない。
    /// </summary>
    public sealed class SurfaceImpactAssetStepTests
    {
        private const string BaseMap = "_BaseMap";

        private string _folder;
        private string _audioFolder;
        private string _assetFolder;
        private string _catalogPath;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__SurfaceImpactAssetStepTests_" + Guid.NewGuid().ToString("N");
            _audioFolder = _folder + "/Audio";
            _assetFolder = _folder + "/SurfaceImpact";
            _catalogPath = _folder + "/Resources/Presentation/SurfaceImpactCatalog.asset";
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
        public void TheStepIsDiscoveredAfterThePresentationCatalogStep()
        {
            var steps = PresentationAssetSteps.Discover();

            Assert.That(steps.Count(step => step is SurfaceImpactAssetStep), Is.EqualTo(1));
            Assert.That(new SurfaceImpactAssetStep().Order, Is.GreaterThan(PresentationCatalogAssetStep.StepOrder));
            Assert.That(steps[0], Is.InstanceOf<PresentationCatalogAssetStep>());
        }

        [Test]
        public void TheCatalogGoesWhereSurfaceImpactCatalogLoadLooks()
        {
            Assert.That(
                SurfaceImpactAssetStep.CatalogPath,
                Is.EqualTo("Assets/game/Presentation/Resources/Presentation/SurfaceImpactCatalog.asset"));
            Assert.That(SurfaceImpactAssetStep.CatalogPath, Does.EndWith("/Resources/" + SurfaceImpactCatalog.ResourcePath + ".asset"));
        }

        [Test]
        public void SpecsCoverDefaultConcreteMetalAndWoodOnce()
        {
            var ids = SurfaceImpactAssetStep.Specs().Select(spec => spec.MaterialId).ToArray();

            Assert.That(ids, Is.EquivalentTo(new[]
            {
                SurfaceMaterial.DefaultId, SurfaceImpactAssetStep.ConcreteId, SurfaceImpactAssetStep.MetalId, SurfaceImpactAssetStep.WoodId,
            }));
            Assert.That(SurfaceImpactAssetStep.Specs().Select(spec => spec.Sound.Name), Is.Unique);
            Assert.That(SurfaceImpactAssetStep.Specs().Select(spec => spec.RimColor), Is.Unique, "each material has its own colour");
        }

        [Test]
        public void BuildMakesAValidCatalogWithEveryMaterial()
        {
            var catalog = Build();

            Assert.That(catalog, Is.SameAs(AssetDatabase.LoadAssetAtPath<SurfaceImpactCatalog>(_catalogPath)));
            Assert.DoesNotThrow(() => catalog.Validate());

            foreach (var spec in SurfaceImpactAssetStep.Specs())
            {
                Assert.That(catalog.TryGetSurface(spec.MaterialId, out var assets), Is.True, spec.MaterialId);
                Assert.That(AssetDatabase.GetAssetPath(assets.Decal), Is.EqualTo($"{_assetFolder}/{spec.DecalMaterialName}.mat"));
                Assert.That(AssetDatabase.GetAssetPath(assets.Decal.GetTexture(BaseMap)),Is.EqualTo($"{_assetFolder}/{spec.DecalTextureName}.asset"));
                Assert.That(AssetDatabase.GetAssetPath(assets.Effect), Is.EqualTo($"{_assetFolder}/{spec.EffectName}.prefab"));
                Assert.That(AssetDatabase.GetAssetPath(assets.Sound), Is.EqualTo($"{_audioFolder}/{spec.Sound.Name}.wav"));
                Assert.That(assets.DecalSize, Is.EqualTo(spec.DecalSize));
                Assert.That(assets.Effect.GetComponentInChildren<ParticleSystem>(), Is.Not.Null, spec.EffectName);
            }
        }

        [Test]
        public void RebuildingKeepsTheSameAssets()
        {
            var guids = Guids(Build());
            var catalogGuid = AssetDatabase.AssetPathToGUID(_catalogPath);

            var rebuilt = Guids(Build());

            Assert.That(AssetDatabase.AssetPathToGUID(_catalogPath), Is.EqualTo(catalogGuid));
            Assert.That(rebuilt, Is.EqualTo(guids));
            Assert.That(guids, Has.None.Empty);
        }

        [Test]
        public void DecalPixelsHaveAnOpaqueDarkHoleAndTransparentCorners()
        {
            const int size = 64;
            var rim = new Color(0.8f, 0.6f, 0.4f);
            var hole = new Color(0.05f, 0.05f, 0.05f);

            var pixels = SurfaceImpactAssetStep.DecalPixels(size, rim, hole, 7u);

            Assert.That(pixels.Length, Is.EqualTo(size * size));
            var centre = pixels[((size / 2) * size) + (size / 2)];
            Assert.That(centre.a, Is.EqualTo(255));
            Assert.That(centre.r, Is.LessThan(40), "the hole is dark");
            foreach (var index in new[] { 0, size - 1, (size - 1) * size, (size * size) - 1, size / 2, ((size / 2) * size) })
            {
                Assert.That(pixels[index].a, Is.Zero, $"pixel {index} at the edge is transparent");
            }

            Assert.That(SurfaceImpactAssetStep.DecalPixels(size, rim, hole, 7u), Is.EqualTo(pixels), "the same seed gives the same pixels");
            Assert.That(SurfaceImpactAssetStep.DecalPixels(size, rim, hole, 8u), Is.Not.EqualTo(pixels), "another seed changes the rim");
            Assert.Throws<ArgumentOutOfRangeException>(() => SurfaceImpactAssetStep.DecalPixels(1, rim, hole, 7u));
        }

        [Test]
        public void BuildRejectsFoldersOutsideAssets()
        {
            Assert.Throws<ArgumentException>(() => SurfaceImpactAssetStep.BuildAt("Packages/x", _assetFolder, _catalogPath));
            Assert.Throws<ArgumentException>(() => SurfaceImpactAssetStep.BuildAt(_audioFolder, _assetFolder, "/tmp/catalog.asset"));
            Assert.That(AssetDatabase.IsValidFolder(_folder), Is.False, "nothing is written when a path is wrong");
        }

        private SurfaceImpactCatalog Build()
        {
            return SurfaceImpactAssetStep.BuildAt(_audioFolder, _assetFolder, _catalogPath);
        }

        // 材質ごとの銃痕の材質・テクスチャ・エフェクト・音の GUID を、定義順に並べる。
        private static string[] Guids(SurfaceImpactCatalog catalog)
        {
            return catalog.Surfaces
                .SelectMany(s => new UnityEngine.Object[] { s.Decal, s.Decal.GetTexture(BaseMap), s.Effect, s.Sound })
                .Select(o => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(o)))
                .ToArray();
        }
    }
}
