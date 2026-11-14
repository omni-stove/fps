using System;
using System.Collections.Generic;
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
    /// <see cref="PresentationAssetBuilder"/> を一時フォルダに走らせて、生成物・作り直したときの同一性・カタログの引き当てを確かめる。
    /// リポジトリの Assets/game/Audio と Assets/game/Presentation は触らない。
    /// </summary>
    public sealed class PresentationAssetBuilderTests
    {
        private string _folder;
        private string _audioFolder;
        private string _presentationFolder;
        private string _catalogPath;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__PresentationAssetBuilderTests_" + Guid.NewGuid().ToString("N");
            _audioFolder = _folder + "/Audio";
            _presentationFolder = _folder + "/Presentation";
            _catalogPath = _presentationFolder + "/PresentationCatalog.asset";
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
        public void DefaultPathsAreUnderGame()
        {
            Assert.That(PresentationAssetBuilder.AudioFolder, Is.EqualTo(DemoSample.Root + "/Audio"));
            Assert.That(PresentationAssetBuilder.PresentationFolder, Is.EqualTo(DemoSample.Root + "/Presentation"));
            Assert.That(
                PresentationAssetBuilder.CatalogPath, Is.EqualTo(DemoSample.Root + "/Presentation/Resources/Presentation/PresentationCatalog.asset"));
        }

        [Test]
        public void CatalogPathIsWherePresentationCatalogLoadLooks()
        {
            Assert.That(
                PresentationAssetBuilder.CatalogPath,
                Does.EndWith("/Resources/" + PresentationCatalog.ResourcePath + ".asset"),
                "WeaponPresentation and HitMarkerView read the catalog with Resources.Load");
        }

        [Test]
        public void BuildCreatesTheCatalogFolderWhenItIsNested()
        {
            var nested = _presentationFolder + "/Resources/Presentation/PresentationCatalog.asset";

            var catalog = PresentationAssetBuilder.BuildAt(_audioFolder, _presentationFolder, nested);

            Assert.That(catalog, Is.SameAs(AssetDatabase.LoadAssetAtPath<PresentationCatalog>(nested)));
        }

        [Test]
        public void CatalogResolvesEveryWeaponToItsOwnSoundsAndFlash()
        {
            var catalog = Build();

            Assert.That(catalog, Is.SameAs(AssetDatabase.LoadAssetAtPath<PresentationCatalog>(_catalogPath)));
            Assert.DoesNotThrow(() => catalog.Validate());

            foreach (var entry in PresentationAssetBuilder.WeaponEntries())
            {
                Assert.That(catalog.TryGetWeapon(entry.WeaponId, out var assets), Is.True, entry.WeaponId);
                Assert.That(assets.Fire, Is.SameAs(Clip(entry.Fire)), entry.Fire);
                Assert.That(assets.Reload, Is.SameAs(Clip(entry.Reload)), entry.Reload);
                Assert.That(assets.MuzzleFlash, Is.SameAs(Prefab(entry.MuzzleFlash)), entry.MuzzleFlash);
            }

            Assert.That(catalog.TryGetWeapon(WeaponAssetBuilder.RifleId, out var rifle), Is.True);
            Assert.That(catalog.TryGetWeapon(WeaponAssetBuilder.HandgunId, out var handgun), Is.True);
            Assert.That(rifle.Fire, Is.Not.SameAs(handgun.Fire));
            Assert.That(catalog.TryGetWeapon("not-a-weapon", out _), Is.False);
        }

        [Test]
        public void CatalogHoldsTheSharedSoundsAndEffects()
        {
            var catalog = Build();

            Assert.That(catalog.ImpactSound, Is.SameAs(Clip(PresentationAssetBuilder.ImpactSound)));
            Assert.That(catalog.Footstep, Is.SameAs(Clip(PresentationAssetBuilder.FootstepSound)));
            Assert.That(catalog.Landing, Is.SameAs(Clip(PresentationAssetBuilder.LandingSound)));
            Assert.That(catalog.HurtVoice, Is.SameAs(Clip(PresentationAssetBuilder.HurtVoiceSound)));
            Assert.That(catalog.ExplosionSound, Is.SameAs(Clip(PresentationAssetBuilder.ExplosionSound)));
            Assert.That(catalog.ImpactSpark, Is.SameAs(Prefab(PresentationAssetBuilder.ImpactSparkEffect)));
            Assert.That(catalog.Tracer, Is.SameAs(Prefab(PresentationAssetBuilder.TracerEffect)));
            Assert.That(catalog.Explosion, Is.SameAs(Prefab(PresentationAssetBuilder.ExplosionEffect)));
        }

        [Test]
        public void WavFilesHoldTheRenderedSoundsAndImportAtTheirLength()
        {
            Build();

            foreach (var spec in PresentationAssetBuilder.SoundSpecs())
            {
                var path = $"{_audioFolder}/{spec.Name}.wav";
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(ProceduralAudio.Render(spec)), spec.Name);

                var clip = Clip(spec.Name);
                Assert.That(clip.channels, Is.EqualTo(1), spec.Name);
                Assert.That(clip.frequency, Is.EqualTo(ProceduralAudio.DefaultSampleRate), spec.Name);
                Assert.That(clip.length, Is.EqualTo(spec.LengthSeconds).Within(0.01f), spec.Name);
            }
        }

        [Test]
        public void EffectsArePlayOnceParticleSystemsThatCleanUpAfterThemselves()
        {
            Build();

            foreach (var name in new[]
            {
                PresentationAssetBuilder.RifleMuzzleFlashEffect,
                PresentationAssetBuilder.HandgunMuzzleFlashEffect,
                PresentationAssetBuilder.LauncherMuzzleFlashEffect,
                PresentationAssetBuilder.ImpactSparkEffect,
                PresentationAssetBuilder.TracerEffect,
                PresentationAssetBuilder.ExplosionEffect,
            })
            {
                var system = Prefab(name).GetComponent<ParticleSystem>();
                Assert.That(system, Is.Not.Null, name);
                Assert.That(system.main.loop, Is.False, name);
                Assert.That(system.main.playOnAwake, Is.True, name);
                Assert.That(system.main.stopAction, Is.EqualTo(ParticleSystemStopAction.Destroy), name);

                foreach (var renderer in Prefab(name).GetComponentsInChildren<ParticleSystemRenderer>())
                {
                    Assert.That(renderer.sharedMaterial, Is.Not.Null, $"{name}/{renderer.name}");
                    Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo(PresentationAssetBuilder.ParticleShaderName), $"{name}/{renderer.name}");
                }
            }

            var tracer = Prefab(PresentationAssetBuilder.TracerEffect).GetComponent<ParticleSystem>();
            Assert.That(tracer.shape.enabled, Is.False, "the tracer flies straight along +Z");
            Assert.That(tracer.main.startSpeed.constantMax, Is.EqualTo(PresentationAssetBuilder.TracerSpeed));
            Assert.That(Prefab(PresentationAssetBuilder.ExplosionEffect).GetComponentsInChildren<ParticleSystem>(), Has.Length.EqualTo(3));
        }

        [Test]
        public void RebuildingKeepsEveryFileAndGuidUnchanged()
        {
            var catalog = Build();
            var catalogGuid = AssetDatabase.AssetPathToGUID(_catalogPath);
            var rifleFlashGuid = AssetDatabase.AssetPathToGUID($"{_presentationFolder}/{PresentationAssetBuilder.RifleMuzzleFlashEffect}.prefab");
            var before = Snapshot();

            var rebuilt = Build();

            Assert.That(rebuilt, Is.SameAs(catalog));
            Assert.That(AssetDatabase.AssetPathToGUID(_catalogPath), Is.EqualTo(catalogGuid));
            Assert.That(
                AssetDatabase.AssetPathToGUID($"{_presentationFolder}/{PresentationAssetBuilder.RifleMuzzleFlashEffect}.prefab"),
                Is.EqualTo(rifleFlashGuid));

            var after = Snapshot();
            Assert.That(after.Keys, Is.EquivalentTo(before.Keys));
            foreach (var pair in before)
            {
                Assert.That(after[pair.Key], Is.EqualTo(pair.Value), $"{pair.Key} changed on the second build");
            }
        }

        [Test]
        public void LoadCatalogPointsAtTheBuilderWhenTheCatalogIsMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<PresentationCatalog>(PresentationAssetBuilder.CatalogPath) != null)
            {
                Assert.Ignore("the repository already has the generated presentation catalog");
            }

            var e = Assert.Throws<InvalidOperationException>(() => PresentationAssetBuilder.LoadCatalog());

            Assert.That(e.Message, Does.Contain("Fps.Editor.PresentationAssetBuilder.Build"));
        }

        [Test]
        public void RejectsFoldersOutsideAssets()
        {
            Assert.Throws<ArgumentException>(() => PresentationAssetBuilder.BuildAt("Library/Audio", _presentationFolder, _catalogPath));
            Assert.Throws<ArgumentException>(() => PresentationAssetBuilder.BuildAt(_audioFolder, "Assets/../Presentation", _catalogPath));
            Assert.Throws<ArgumentException>(() => PresentationAssetBuilder.BuildAt(_audioFolder, " ", _catalogPath));
            Assert.Throws<ArgumentException>(() => PresentationAssetBuilder.BuildAt(_audioFolder, _presentationFolder, _folder + "/Catalog.prefab"));
            Assert.That(AssetDatabase.IsValidFolder(_folder), Is.False, "nothing is written when a path is rejected");
        }

        [Test]
        public void RefusesToOverwriteAnAssetOfAnotherType()
        {
            SceneBuildHelpers.EnsureFolder(_presentationFolder);
            var other = ScriptableObject.CreateInstance<PresentationCatalog>();
            AssetDatabase.CreateAsset(other, $"{_presentationFolder}/{PresentationAssetBuilder.SoftDotTextureName}.asset");

            Assert.Throws<InvalidOperationException>(() => Build());
        }

        private PresentationCatalog Build()
        {
            return PresentationAssetBuilder.BuildAt(_audioFolder, _presentationFolder, _catalogPath);
        }

        private AudioClip Clip(string soundName)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{_audioFolder}/{soundName}.wav");
            Assert.That(clip, Is.Not.Null, soundName);
            return clip;
        }

        private GameObject Prefab(string effectName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{_presentationFolder}/{effectName}.prefab");
            Assert.That(prefab, Is.Not.Null, effectName);
            return prefab;
        }

        private Dictionary<string, byte[]> Snapshot()
        {
            AssetDatabase.SaveAssets();
            return Directory.GetFiles(_folder, "*", SearchOption.AllDirectories)
                .Select(path => path.Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToDictionary(path => path, File.ReadAllBytes);
        }
    }
}
