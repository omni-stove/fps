using System.Collections.Generic;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary><see cref="SurfaceImpactCatalog"/> の置き場所・材質の引き当てと default への寄せ方・検査を確かめる。</summary>
    public sealed class SurfaceImpactCatalogTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void ItLoadsFromThePresentationCatalogFolder()
        {
            Assert.That(SurfaceImpactCatalog.ResourcePath, Is.EqualTo("Presentation/SurfaceImpactCatalog"));
            Assert.That(SurfaceImpactCatalog.ResourcePath, Is.EqualTo(PresentationCatalog.CatalogResourcePath(SurfaceImpactCatalog.CatalogName)));
        }

        [Test]
        public void ResolveFindsTheMaterialThenFallsBackToDefault()
        {
            var metal = Entry("metal");
            var fallback = Entry(SurfaceMaterial.DefaultId);
            var catalog = Catalog(metal, fallback);

            Assert.That(catalog.Resolve("metal"), Is.SameAs(metal));
            Assert.That(catalog.Resolve("  metal "), Is.SameAs(metal), "ids are trimmed like SurfaceMaterial ids");
            Assert.That(catalog.Resolve("glass"), Is.SameAs(fallback));
            Assert.That(catalog.Resolve(null), Is.SameAs(fallback));
            Assert.That(catalog.Resolve("Metal"), Is.SameAs(fallback), "ids are case sensitive");
        }

        [Test]
        public void ResolveReturnsNullWithoutTheMaterialOrADefault()
        {
            var catalog = Catalog(Entry("metal"));

            Assert.That(catalog.Resolve("wood"), Is.Null);
            Assert.That(catalog.TryGetSurface("wood", out var assets), Is.False);
            Assert.That(assets, Is.Null);
            Assert.That(Catalog().Resolve("metal"), Is.Null);
        }

        [Test]
        public void ResolveDoesNotFillEmptySlotsFromDefault()
        {
            var bare = new SurfaceImpactAssets("metal", null, 0.1f, null, null);
            var catalog = Catalog(bare, Entry(SurfaceMaterial.DefaultId));

            Assert.That(catalog.Resolve("metal"), Is.SameAs(bare));
        }

        [Test]
        public void EntriesNormalizeTheirMaterialId()
        {
            Assert.That(new SurfaceImpactAssets(" wood ", null, 0.1f, null, null).MaterialId, Is.EqualTo("wood"));
            Assert.That(new SurfaceImpactAssets(null, null, 0.1f, null, null).MaterialId, Is.EqualTo(SurfaceMaterial.DefaultId));
            Assert.That(new SurfaceImpactAssets().MaterialId, Is.EqualTo(SurfaceMaterial.DefaultId));
        }

        [Test]
        public void ACompleteCatalogPassesValidation()
        {
            Assert.DoesNotThrow(() => Catalog(Entry(SurfaceMaterial.DefaultId), Entry("metal")).Validate());
        }

        [Test]
        public void ValidationListsEveryProblem()
        {
            var catalog = Catalog(
                Entry("metal"),
                Entry("metal"),
                new SurfaceImpactAssets("wood", null, 0f, null, null),
                null);

            var thrown = Assert.Throws<PresentationCatalogException>(() => catalog.Validate());

            Assert.That(thrown.Message, Does.Contain("surface #1 ('metal') uses a material id already used"));
            Assert.That(thrown.Message, Does.Contain("surface #2 ('wood') decal is missing"));
            Assert.That(thrown.Message, Does.Contain("surface #2 ('wood') effect is missing"));
            Assert.That(thrown.Message, Does.Contain("surface #2 ('wood') sound is missing"));
            Assert.That(thrown.Message, Does.Contain("surface #2 ('wood') decal size"));
            Assert.That(thrown.Message, Does.Contain("surface #3 is empty"));
            Assert.That(thrown.Message, Does.Contain("has no 'default' surface"));
        }

        [Test]
        public void AnEmptyCatalogFailsValidation()
        {
            var thrown = Assert.Throws<PresentationCatalogException>(() => Catalog().Validate());

            Assert.That(thrown.Message, Does.Contain("has no surfaces"));
        }

        [Test]
        public void SetSurfacesCopiesTheListAndNullClearsIt()
        {
            var entries = new List<SurfaceImpactAssets> { Entry("metal") };
            var catalog = Catalog();
            catalog.SetSurfaces(entries);
            entries.Clear();

            Assert.That(catalog.Surfaces.Count, Is.EqualTo(1));

            catalog.SetSurfaces(null);
            Assert.That(catalog.Surfaces, Is.Empty);
        }

        private SurfaceImpactCatalog Catalog(params SurfaceImpactAssets[] entries)
        {
            var catalog = Track(ScriptableObject.CreateInstance<SurfaceImpactCatalog>());
            catalog.name = "TestSurfaceImpactCatalog";
            catalog.SetSurfaces(entries);
            return catalog;
        }

        private SurfaceImpactAssets Entry(string materialId)
        {
            var decal = Track(new Material(Shader.Find("Hidden/InternalErrorShader")));
            var effect = Track(new GameObject(materialId + "Hit"));
            var sound = Track(AudioClip.Create(materialId + "Hit", 441, 1, 44100, false));
            return new SurfaceImpactAssets(materialId, decal, 0.1f, effect, sound);
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
