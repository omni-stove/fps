using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary><see cref="HitFeedbackCatalog"/> の置き場所・部位からの血しぶきの引き当て・検査を確かめる。</summary>
    public sealed class HitFeedbackCatalogTests
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
        public void ResourcePathFollowsThePresentationCatalogConvention()
        {
            Assert.That(HitFeedbackCatalog.ResourcePath, Is.EqualTo("Presentation/HitFeedbackCatalog"));
            Assert.That(HitFeedbackCatalog.ResourcePath, Is.EqualTo(PresentationCatalog.CatalogResourcePath(HitFeedbackCatalog.CatalogName)));
        }

        [Test]
        public void SprayForPicksTheZonesOwnSprayAndFallsBackToTheDefault()
        {
            var head = Track(new GameObject("Head"));
            var fallback = Track(new GameObject("Default"));
            var catalog = Catalog();
            catalog.SetSprays(new[] { new ZoneBloodSpray(HitZone.Head, head), new ZoneBloodSpray(HitZone.Limb, null) });
            catalog.DefaultSpray = fallback;

            Assert.That(catalog.SprayFor(HitZone.Head), Is.SameAs(head));
            Assert.That(catalog.SprayFor(HitZone.Body), Is.SameAs(fallback), "a zone without an entry uses the default");
            Assert.That(catalog.SprayFor(HitZone.Limb), Is.SameAs(fallback), "an entry with an empty slot uses the default");
            Assert.That(catalog.SprayFor(null), Is.SameAs(fallback), "an impact without a zone uses the default");
        }

        [Test]
        public void SprayForIsNullWhenNothingIsSet()
        {
            var catalog = Catalog();

            Assert.That(catalog.SprayFor(HitZone.Head), Is.Null);
            Assert.That(catalog.SprayFor(null), Is.Null);
        }

        [Test]
        public void ValidatePassesWhenEveryZoneAndTheDecalAreSet()
        {
            var catalog = Complete();

            Assert.DoesNotThrow(() => catalog.Validate());
        }

        [Test]
        public void ValidateListsEveryMissingPiece()
        {
            var catalog = Catalog();
            catalog.name = "Broken";
            catalog.SetSprays(new[] { new ZoneBloodSpray(HitZone.Head, null), null });

            var e = Assert.Throws<PresentationCatalogException>(() => catalog.Validate());

            Assert.That(e.Message, Does.Contain("'Broken'"));
            Assert.That(e.Message, Does.Contain("spray #0 (Head) effect is missing"));
            Assert.That(e.Message, Does.Contain("spray #1 is empty"));
            Assert.That(e.Message, Does.Contain("zone Body has no spray"));
            Assert.That(e.Message, Does.Contain("zone Limb has no spray"));
            Assert.That(e.Message, Does.Contain("default spray is missing"));
            Assert.That(e.Message, Does.Contain("wall decal is missing"));
        }

        [Test]
        public void ValidateRejectsADuplicatedZone()
        {
            var catalog = Complete();
            var extra = Track(new GameObject("Extra"));
            var sprays = new List<ZoneBloodSpray>(catalog.Sprays) { new ZoneBloodSpray(HitZone.Head, extra) };
            catalog.SetSprays(sprays);

            var e = Assert.Throws<PresentationCatalogException>(() => catalog.Validate());

            Assert.That(e.Message, Does.Contain("already used"));
        }

        [Test]
        public void TuningValuesStayPositive()
        {
            var catalog = Catalog();
            catalog.WallDecalMaxDistance = -1f;
            catalog.WallDecalLifetime = 0f;
            catalog.WallDecalSize = -3f;

            Assert.That(catalog.WallDecalMaxDistance, Is.GreaterThan(0f));
            Assert.That(catalog.WallDecalLifetime, Is.GreaterThan(0f));
            Assert.That(catalog.WallDecalSize, Is.GreaterThan(0f));
        }

        [Test]
        public void SetSpraysCopiesTheList()
        {
            var catalog = Catalog();
            var sprays = new List<ZoneBloodSpray> { new ZoneBloodSpray(HitZone.Head, Track(new GameObject("Head"))) };
            catalog.SetSprays(sprays);
            sprays.Clear();

            Assert.That(catalog.Sprays, Has.Count.EqualTo(1));

            catalog.SetSprays(null);
            Assert.That(catalog.Sprays, Is.Empty);
        }

        private HitFeedbackCatalog Complete()
        {
            var catalog = Catalog();
            catalog.SetSprays(new[]
            {
                new ZoneBloodSpray(HitZone.Head, Track(new GameObject("Head"))),
                new ZoneBloodSpray(HitZone.Body, Track(new GameObject("Body"))),
                new ZoneBloodSpray(HitZone.Limb, Track(new GameObject("Limb"))),
            });
            catalog.DefaultSpray = Track(new GameObject("Default"));
            catalog.WallDecal = Track(new GameObject("Decal"));
            return catalog;
        }

        private HitFeedbackCatalog Catalog()
        {
            return Track(ScriptableObject.CreateInstance<HitFeedbackCatalog>());
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
