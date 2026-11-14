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
    /// <see cref="HitFeedbackAssetStep"/> を一時フォルダに走らせて、生成物・作り直したときの同一性・一括実行での順番を確かめる。
    /// リポジトリの Assets/game/Presentation は触らない。
    /// </summary>
    public sealed class HitFeedbackAssetStepTests
    {
        private string _folder;
        private string _bloodFolder;
        private string _catalogPath;
        private string _materialPath;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__HitFeedbackAssetStepTests_" + Guid.NewGuid().ToString("N");
            _bloodFolder = _folder + "/Blood";
            _catalogPath = _folder + "/Resources/Presentation/HitFeedbackCatalog.asset";
            _materialPath = _folder + "/ParticleAlpha.mat";
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
        public void DefaultPathsAreWhereTheCatalogIsLoaded()
        {
            Assert.That(HitFeedbackAssetStep.CatalogPath, Is.EqualTo("Assets/game/Presentation/Resources/Presentation/HitFeedbackCatalog.asset"));
            Assert.That(HitFeedbackAssetStep.CatalogPath, Does.EndWith("/Resources/" + HitFeedbackCatalog.ResourcePath + ".asset"));
            Assert.That(HitFeedbackAssetStep.Folder, Is.EqualTo("Assets/game/Presentation/Blood"));
        }

        [Test]
        public void TheStepRunsAfterThePresentationCatalogFromTheBatchEntry()
        {
            var steps = PresentationAssetSteps.Discover();
            var catalogIndex = steps.ToList().FindIndex(step => step is PresentationCatalogAssetStep);
            var bloodIndex = steps.ToList().FindIndex(step => step is HitFeedbackAssetStep);

            Assert.That(bloodIndex, Is.GreaterThan(catalogIndex), "the sprays borrow the particle material the presentation catalog step makes");
            Assert.That(new HitFeedbackAssetStep().Order, Is.GreaterThan(PresentationCatalogAssetStep.StepOrder));
        }

        [Test]
        public void EveryZoneHasASpray()
        {
            var zones = HitFeedbackAssetStep.SprayEntries().Select(entry => entry.Zone).ToList();

            Assert.That(zones, Is.EquivalentTo(Enum.GetValues(typeof(HitZone)).Cast<HitZone>()));
            Assert.That(HitFeedbackAssetStep.SprayEntries().Select(entry => entry.Effect).Distinct().Count(), Is.EqualTo(zones.Count));
        }

        [Test]
        public void BuildMakesAValidCatalogWithRedSpraysAndARedCircleDecal()
        {
            var catalog = Build();

            Assert.That(catalog, Is.SameAs(AssetDatabase.LoadAssetAtPath<HitFeedbackCatalog>(_catalogPath)));
            Assert.DoesNotThrow(() => catalog.Validate());

            foreach (var entry in HitFeedbackAssetStep.SprayEntries())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{_bloodFolder}/{entry.Effect}.prefab");
                Assert.That(catalog.SprayFor(entry.Zone), Is.SameAs(prefab), entry.Effect);
                var particles = prefab.GetComponent<ParticleSystem>();
                Assert.That(particles, Is.Not.Null, entry.Effect);
                var color = particles.main.startColor.color;
                Assert.That(color.r, Is.GreaterThan(color.g * 4f).And.GreaterThan(color.b * 4f), $"{entry.Effect} is red");
                Assert.That(prefab.GetComponent<ParticleSystemRenderer>().sharedMaterial,
                    Is.SameAs(AssetDatabase.LoadAssetAtPath<Material>(_materialPath)), entry.Effect);
            }

            Assert.That(catalog.DefaultSpray, Is.SameAs(catalog.SprayFor(HitZone.Body)));

            var decal = catalog.WallDecal;
            Assert.That(decal, Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>($"{_bloodFolder}/{HitFeedbackAssetStep.WallDecalPrefab}.prefab")));
            Assert.That(decal.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
            Assert.That(decal.GetComponent<Collider>(), Is.Null, "a decal must not stop bullets");
            var texture = (Texture2D)decal.GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_BaseMap");
            Assert.That(texture, Is.Not.Null);
            var center = texture.GetPixel(texture.width / 2, texture.height / 2);
            var corner = texture.GetPixel(0, 0);
            Assert.That(center.r, Is.GreaterThan(center.g * 4f), "the circle is red");
            Assert.That(center.a, Is.EqualTo(1f).Within(0.01f));
            Assert.That(corner.a, Is.Zero.Within(0.01f), "outside the circle is clear");
        }

        [Test]
        public void RebuildingKeepsTheGuids()
        {
            Build();
            var catalogGuid = AssetDatabase.AssetPathToGUID(_catalogPath);
            var decalGuid = AssetDatabase.AssetPathToGUID($"{_bloodFolder}/{HitFeedbackAssetStep.WallDecalPrefab}.prefab");
            var headGuid = AssetDatabase.AssetPathToGUID($"{_bloodFolder}/{HitFeedbackAssetStep.HeadSprayEffect}.prefab");

            var catalog = HitFeedbackAssetStep.BuildAt(_bloodFolder, _catalogPath, _materialPath);

            Assert.That(AssetDatabase.AssetPathToGUID(_catalogPath), Is.EqualTo(catalogGuid));
            Assert.That(AssetDatabase.AssetPathToGUID($"{_bloodFolder}/{HitFeedbackAssetStep.WallDecalPrefab}.prefab"), Is.EqualTo(decalGuid));
            Assert.That(AssetDatabase.AssetPathToGUID($"{_bloodFolder}/{HitFeedbackAssetStep.HeadSprayEffect}.prefab"), Is.EqualTo(headGuid));
            Assert.DoesNotThrow(() => catalog.Validate());
        }

        [Test]
        public void BuildNeedsTheParticleMaterialFirst()
        {
            var e = Assert.Throws<InvalidOperationException>(() => HitFeedbackAssetStep.BuildAt(_bloodFolder, _catalogPath, _materialPath));

            Assert.That(e.Message, Does.Contain(_materialPath));
            Assert.That(AssetDatabase.LoadAssetAtPath<HitFeedbackCatalog>(_catalogPath), Is.Null);
        }

        [Test]
        public void DecalPixelsAreAnOpaqueRedCircleFadingToClear()
        {
            var pixels = HitFeedbackAssetStep.DecalPixels();
            var size = HitFeedbackAssetStep.DecalTextureSize;

            Assert.That(pixels, Has.Length.EqualTo(size * size));
            var center = pixels[((size / 2) * size) + (size / 2)];
            Assert.That(center.a, Is.EqualTo(255));
            Assert.That(center.r, Is.EqualTo(HitFeedbackAssetStep.DecalColor.r));
            Assert.That(pixels[0].a, Is.Zero);
            Assert.That(pixels[pixels.Length - 1].a, Is.Zero);
        }

        private HitFeedbackCatalog Build()
        {
            SceneBuildHelpers.EnsureFolder(_folder);
            var material = new Material(Shader.Find(PresentationAssetBuilder.ParticleShaderName));
            AssetDatabase.CreateAsset(material, _materialPath);
            return HitFeedbackAssetStep.BuildAt(_bloodFolder, _catalogPath, _materialPath);
        }
    }
}
