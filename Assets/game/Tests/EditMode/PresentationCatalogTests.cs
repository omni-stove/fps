using System.Collections.Generic;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary><see cref="PresentationCatalog"/> の武器 Id での引き当てと検査を、メモリ上のインスタンスで確かめる。</summary>
    public sealed class PresentationCatalogTests
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
        public void TryGetWeaponFindsTheEntryByWeaponId()
        {
            var catalog = CompleteCatalog();
            var rifle = catalog.Weapons[0];
            var handgun = catalog.Weapons[1];

            Assert.That(catalog.TryGetWeapon("rifle-id", out var found), Is.True);
            Assert.That(found, Is.SameAs(rifle));
            Assert.That(catalog.TryGetWeapon("handgun-id", out found), Is.True);
            Assert.That(found, Is.SameAs(handgun));
        }

        [Test]
        public void TryGetWeaponReturnsFalseForUnknownNullOrDifferentlyCasedIds()
        {
            var catalog = CompleteCatalog();

            Assert.That(catalog.TryGetWeapon("missing", out var found), Is.False);
            Assert.That(found, Is.Null);
            Assert.That(catalog.TryGetWeapon(null, out found), Is.False);
            Assert.That(found, Is.Null);
            Assert.That(catalog.TryGetWeapon("RIFLE-ID", out found), Is.False);
        }

        [Test]
        public void TryGetWeaponSkipsEmptyEntries()
        {
            var catalog = CompleteCatalog();
            catalog.SetWeapons(new[] { null, Weapon("rifle-id") });

            Assert.That(catalog.TryGetWeapon("rifle-id", out var found), Is.True);
            Assert.That(found, Is.Not.Null);
        }

        [Test]
        public void WeaponIdIsTrimmed()
        {
            Assert.That(new WeaponPresentationAssets("  rifle-id \n", null, null, null).WeaponId, Is.EqualTo("rifle-id"));
            Assert.That(new WeaponPresentationAssets(null, null, null, null).WeaponId, Is.Empty);
        }

        [Test]
        public void ValidatePassesWhenEverythingIsFilled()
        {
            Assert.DoesNotThrow(() => CompleteCatalog().Validate());
        }

        [Test]
        public void ValidateListsEveryMissingReference()
        {
            var catalog = Track(ScriptableObject.CreateInstance<PresentationCatalog>());
            catalog.name = "Empty";

            var e = Assert.Throws<PresentationCatalogException>(() => catalog.Validate());

            Assert.That(e.Message, Does.Contain("'Empty'"));
            Assert.That(e.Message, Does.Contain("has no weapons"));
            foreach (var what in new[] { "impact sound", "impact spark", "tracer", "footstep", "landing", "hurt voice", "explosion sound", "explosion is missing" })
            {
                Assert.That(e.Message, Does.Contain(what));
            }
        }

        [Test]
        public void ValidateRejectsDuplicateEmptyAndIncompleteWeapons()
        {
            var catalog = CompleteCatalog();
            catalog.SetWeapons(new[]
            {
                Weapon("rifle-id"),
                Weapon("rifle-id"),
                Weapon(" "),
                null,
                new WeaponPresentationAssets("bare", null, null, null),
            });

            var e = Assert.Throws<PresentationCatalogException>(() => catalog.Validate());

            Assert.That(e.Message, Does.Contain("weapon #1 ('rifle-id') uses a weapon id already used"));
            Assert.That(e.Message, Does.Contain("weapon #2 has no weapon id"));
            Assert.That(e.Message, Does.Contain("weapon #3 is empty"));
            Assert.That(e.Message, Does.Contain("weapon #4 ('bare') fire sound is missing"));
            Assert.That(e.Message, Does.Contain("weapon #4 ('bare') reload sound is missing"));
            Assert.That(e.Message, Does.Contain("weapon #4 ('bare') muzzle flash is missing"));
        }

        [Test]
        public void ValidateCatchesDestroyedReferences()
        {
            var catalog = CompleteCatalog();
            Object.DestroyImmediate(catalog.Tracer);

            var e = Assert.Throws<PresentationCatalogException>(() => catalog.Validate());

            Assert.That(e.Message, Does.Contain("tracer is missing"));
        }

        [Test]
        public void SetWeaponsCopiesTheListAndNullEmptiesIt()
        {
            var catalog = CompleteCatalog();
            var source = new List<WeaponPresentationAssets> { Weapon("a") };

            catalog.SetWeapons(source);
            source.Add(Weapon("b"));
            Assert.That(catalog.Weapons, Has.Count.EqualTo(1));

            catalog.SetWeapons(null);
            Assert.That(catalog.Weapons, Is.Empty);
        }

        private PresentationCatalog CompleteCatalog()
        {
            var catalog = Track(ScriptableObject.CreateInstance<PresentationCatalog>());
            catalog.name = "Complete";
            catalog.SetWeapons(new[] { Weapon("rifle-id"), Weapon("handgun-id") });
            catalog.ImpactSound = Clip("Impact");
            catalog.ImpactSpark = Effect("ImpactSpark");
            catalog.Tracer = Effect("Tracer");
            catalog.Footstep = Clip("Footstep");
            catalog.Landing = Clip("Landing");
            catalog.HurtVoice = Clip("HurtVoice");
            catalog.ExplosionSound = Clip("Explosion");
            catalog.Explosion = Effect("Explosion");
            return catalog;
        }

        private WeaponPresentationAssets Weapon(string id)
        {
            return new WeaponPresentationAssets(id, Clip(id + "Fire"), Clip(id + "Reload"), Effect(id + "Flash"));
        }

        private AudioClip Clip(string clipName)
        {
            return Track(AudioClip.Create(clipName, 16, 1, 44100, false));
        }

        private GameObject Effect(string effectName)
        {
            return Track(new GameObject(effectName));
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
