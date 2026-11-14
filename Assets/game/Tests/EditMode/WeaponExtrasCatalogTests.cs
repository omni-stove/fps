using System.Linq;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="WeaponExtrasCatalog"/> の引き方と検査、仮素材を作る <see cref="WeaponExtrasAssetStep"/> の組み合わせを確かめる。
    /// 素材を書き出す <see cref="WeaponExtrasAssetStep.Build"/> は呼ばない。
    /// </summary>
    public sealed class WeaponExtrasCatalogTests
    {
        private WeaponExtrasCatalog _catalog;
        private GameObject _shell;
        private AudioClip _clip;

        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<WeaponExtrasCatalog>();
            _catalog.name = "WeaponExtrasCatalog";
            _shell = new GameObject("Shell");
            _clip = AudioClip.Create("Click", 441, 1, 44100, false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_catalog);
            Object.DestroyImmediate(_shell);
            Object.DestroyImmediate(_clip);
        }

        [Test]
        public void TheCatalogLivesBesideTheOtherPresentationCatalogs()
        {
            Assert.That(WeaponExtrasCatalog.ResourcePath, Is.EqualTo("Presentation/WeaponExtrasCatalog"));
            Assert.That(WeaponExtrasAssetStep.CatalogPath, Does.EndWith("/Resources/Presentation/WeaponExtrasCatalog.asset"));
        }

        [Test]
        public void TryGetWeaponMatchesTheIdExactly()
        {
            _catalog.SetWeapons(new[] { new WeaponExtrasAssets("  rifle  ", _shell, _clip, _clip) });

            Assert.That(_catalog.TryGetWeapon("rifle", out var found), Is.True);
            Assert.That(found.ShellPrefab, Is.SameAs(_shell));
            Assert.That(_catalog.TryGetWeapon("Rifle", out _), Is.False);
            Assert.That(_catalog.TryGetWeapon(null, out var none), Is.False);
            Assert.That(none, Is.Null);
        }

        [Test]
        public void ValidateListsEveryProblem()
        {
            _catalog.SetWeapons(new[]
            {
                new WeaponExtrasAssets("rifle", _shell, _clip, _clip),
                new WeaponExtrasAssets("rifle", null, null, null),
                new WeaponExtrasAssets(string.Empty, _shell, _clip, _clip),
                null,
            });

            var thrown = Assert.Throws<PresentationCatalogException>(() => _catalog.Validate());

            Assert.That(thrown.Message, Does.Contain("weapon #1 ('rifle') uses a weapon id already used"));
            Assert.That(thrown.Message, Does.Contain("shell prefab is missing"));
            Assert.That(thrown.Message, Does.Contain("shell landing sound is missing"));
            Assert.That(thrown.Message, Does.Contain("dry-fire sound is missing"));
            Assert.That(thrown.Message, Does.Contain("weapon #2 has no weapon id"));
            Assert.That(thrown.Message, Does.Contain("weapon #3 is empty"));
        }

        [Test]
        public void ValidateRejectsAnEmptyCatalogAndAcceptsAFullOne()
        {
            Assert.Throws<PresentationCatalogException>(() => _catalog.Validate());

            _catalog.SetWeapons(new[] { new WeaponExtrasAssets("rifle", _shell, _clip, _clip) });
            Assert.DoesNotThrow(() => _catalog.Validate());

            _catalog.SetWeapons(null);
            Assert.That(_catalog.Weapons, Is.Empty);
        }

        [Test]
        public void TheStepFillsEveryWeaponWithDefaults()
        {
            Assert.That(WeaponExtrasAssetStep.MissingWeaponIds(), Is.Empty);

            var ids = WeaponExtrasAssetStep.WeaponEntries().Select(entry => entry.WeaponId).ToList();
            Assert.That(ids, Is.Unique);
            Assert.That(ids, Is.EquivalentTo(PresentationAssetBuilder.WeaponEntries().Select(entry => entry.WeaponId)));

            var shells = WeaponExtrasAssetStep.ShellShapes().Select(shape => shape.Name).ToList();
            var sounds = WeaponExtrasAssetStep.SoundSpecs().Select(spec => spec.Name).ToList();
            Assert.That(sounds, Is.Unique);
            Assert.That(sounds.Intersect(PresentationAssetBuilder.SoundSpecs().Select(spec => spec.Name)), Is.Empty,
                "the sounds share the audio folder with the presentation catalog's sounds");
            foreach (var entry in WeaponExtrasAssetStep.WeaponEntries())
            {
                Assert.That(shells, Does.Contain(entry.Shell), entry.WeaponId);
                Assert.That(sounds, Does.Contain(entry.LandSound), entry.WeaponId);
                Assert.That(sounds, Does.Contain(entry.DryFire), entry.WeaponId);
            }

            foreach (var shape in WeaponExtrasAssetStep.ShellShapes())
            {
                Assert.That(shape.Diameter, Is.GreaterThan(0f), shape.Name);
                Assert.That(shape.Length, Is.GreaterThan(shape.Diameter), shape.Name);
            }
        }

        [Test]
        public void TheStepRunsAfterThePresentationCatalogFromTheBatchEntry()
        {
            var steps = PresentationAssetSteps.Discover();

            Assert.That(steps.Count(step => step is WeaponExtrasAssetStep), Is.EqualTo(1));
            Assert.That(steps[0], Is.InstanceOf<PresentationCatalogAssetStep>());
            Assert.That(new WeaponExtrasAssetStep().Order, Is.GreaterThan(PresentationCatalogAssetStep.StepOrder));
        }
    }
}
