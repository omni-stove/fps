using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="FootstepSurfaceCatalog"/> の引き方 (材質 → default → PresentationCatalog)・同じ音を続けない選び方・検査と、
    /// <see cref="CharacterAudio"/> が足元の材質で鳴らし分けることを確かめる。
    /// </summary>
    public sealed class FootstepSurfaceCatalogTests
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

        private AudioClip Clip(string name)
        {
            var clip = AudioClip.Create(name, 441, 1, 44100, false);
            _created.Add(clip);
            return clip;
        }

        private FootstepSurfaceCatalog Catalog(params FootstepSurfaceSounds[] surfaces)
        {
            var catalog = ScriptableObject.CreateInstance<FootstepSurfaceCatalog>();
            _created.Add(catalog);
            catalog.SetSurfaces(surfaces);
            return catalog;
        }

        private PresentationCatalog Legacy(AudioClip footstep, AudioClip landing)
        {
            var catalog = ScriptableObject.CreateInstance<PresentationCatalog>();
            _created.Add(catalog);
            catalog.Footstep = footstep;
            catalog.Landing = landing;
            return catalog;
        }

        private GameObject Create(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        private CharacterAudio CreateAudio(FootstepSurfaceCatalog surfaces, PresentationCatalog legacy)
        {
            var audio = Create("Body").AddComponent<CharacterAudio>();
            audio.SurfaceCatalog = surfaces;
            audio.Catalog = legacy;
            return audio;
        }

        private GameObject Floor(string materialId, Vector3 topCenter)
        {
            var floor = Create("Floor_" + materialId);
            floor.transform.position = topCenter + (Vector3.down * 0.5f);
            floor.AddComponent<BoxCollider>().size = new Vector3(4f, 1f, 4f);
            if (materialId != null)
            {
                floor.AddComponent<SurfaceMaterial>().Id = materialId;
            }

            return floor;
        }

        private static List<AudioClip> Collect(
            FootstepSurfaceCatalog catalog, PresentationCatalog legacy, string materialId, FootstepCueKind kind, out FootstepClipSource source)
        {
            var into = new List<AudioClip>();
            source = FootstepSurfaceCatalog.CollectClips(catalog, legacy, materialId, kind, into);
            return into;
        }

        [Test]
        public void ResourcePathFollowsThePresentationCatalogRule()
        {
            Assert.That(FootstepSurfaceCatalog.ResourcePath, Is.EqualTo("Presentation/FootstepSurfaceCatalog"));
        }

        [Test]
        public void CollectUsesTheMaterialsOwnClips()
        {
            var metal = new[] { Clip("m1"), Clip("m2") };
            var catalog = Catalog(
                new FootstepSurfaceSounds(SurfaceMaterial.DefaultId, new[] { Clip("d") }, new[] { Clip("dl") }),
                new FootstepSurfaceSounds("metal", metal, new[] { Clip("ml") }));

            var clips = Collect(catalog, null, " metal ", FootstepCueKind.Footstep, out var source);

            Assert.That(source, Is.EqualTo(FootstepClipSource.Surface));
            Assert.That(clips, Is.EqualTo(metal));
        }

        [Test]
        public void CollectFallsBackToDefaultForUnknownOrEmptyMaterials()
        {
            var defaultStep = Clip("d");
            var defaultLanding = Clip("dl");
            var catalog = Catalog(
                new FootstepSurfaceSounds(SurfaceMaterial.DefaultId, new[] { defaultStep }, new[] { defaultLanding }),
                new FootstepSurfaceSounds("wood", new[] { Clip("w") }, null));

            Assert.That(Collect(catalog, null, "glass", FootstepCueKind.Footstep, out var unknown), Is.EqualTo(new[] { defaultStep }));
            Assert.That(unknown, Is.EqualTo(FootstepClipSource.Default));

            // 材質の組はあっても着地音が空なら、着地だけ default を使う
            Assert.That(Collect(catalog, null, "wood", FootstepCueKind.Landing, out var empty), Is.EqualTo(new[] { defaultLanding }));
            Assert.That(empty, Is.EqualTo(FootstepClipSource.Default));

            Assert.That(Collect(catalog, null, null, FootstepCueKind.Footstep, out var unset), Is.EqualTo(new[] { defaultStep }));
            Assert.That(unset, Is.EqualTo(FootstepClipSource.Default));
        }

        [Test]
        public void CollectFallsBackToThePresentationCatalogWhenDefaultIsEmptyOrTheCatalogIsMissing()
        {
            var legacyStep = Clip("legacy");
            var legacyLanding = Clip("legacyLanding");
            var legacy = Legacy(legacyStep, legacyLanding);
            var emptyDefault = Catalog(new FootstepSurfaceSounds(SurfaceMaterial.DefaultId, null, null));

            Assert.That(Collect(emptyDefault, legacy, "metal", FootstepCueKind.Footstep, out var fromEmpty), Is.EqualTo(new[] { legacyStep }));
            Assert.That(fromEmpty, Is.EqualTo(FootstepClipSource.PresentationCatalog));

            Assert.That(Collect(null, legacy, "metal", FootstepCueKind.Landing, out var fromMissing), Is.EqualTo(new[] { legacyLanding }));
            Assert.That(fromMissing, Is.EqualTo(FootstepClipSource.PresentationCatalog));

            Assert.That(Collect(null, null, "metal", FootstepCueKind.Footstep, out var none), Is.Empty);
            Assert.That(none, Is.EqualTo(FootstepClipSource.None));
        }

        [Test]
        public void CollectSkipsDestroyedClips()
        {
            var destroyed = Clip("gone");
            var alive = Clip("alive");
            var catalog = Catalog(new FootstepSurfaceSounds(SurfaceMaterial.DefaultId, new[] { destroyed, alive }, null));
            Object.DestroyImmediate(destroyed);

            Assert.That(Collect(catalog, null, SurfaceMaterial.DefaultId, FootstepCueKind.Footstep, out _), Is.EqualTo(new[] { alive }));
        }

        [Test]
        public void CollectRejectsBadArguments()
        {
            Assert.Throws<ArgumentNullException>(
                () => FootstepSurfaceCatalog.CollectClips(null, null, "metal", FootstepCueKind.Footstep, null));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => FootstepSurfaceCatalog.CollectClips(null, null, "metal", FootstepCueKind.None, new List<AudioClip>()));
        }

        [Test]
        public void PickNeverRepeatsThePreviousClipWhenThereIsAChoice()
        {
            var clips = new[] { Clip("a"), Clip("b"), Clip("c") };

            for (var roll = 0; roll < 2; roll++)
            {
                var fixedRoll = roll;
                var picked = FootstepSurfaceCatalog.PickAvoidingRepeat(clips, clips[1], n =>
                {
                    Assert.That(n, Is.EqualTo(2), "the previous clip is left out of the roll");
                    return fixedRoll;
                });

                Assert.That(picked, Is.Not.SameAs(clips[1]));
            }

            Assert.That(FootstepSurfaceCatalog.PickAvoidingRepeat(clips, clips[0], _ => 0), Is.SameAs(clips[1]));
            Assert.That(FootstepSurfaceCatalog.PickAvoidingRepeat(clips, clips[0], _ => 1), Is.SameAs(clips[2]));
        }

        [Test]
        public void PickUsesEveryClipWhenThePreviousOneIsNotACandidate()
        {
            var clips = new[] { Clip("a"), Clip("b") };
            var other = Clip("other");

            Assert.That(FootstepSurfaceCatalog.PickAvoidingRepeat(clips, other, n => n - 1), Is.SameAs(clips[1]));
            Assert.That(FootstepSurfaceCatalog.PickAvoidingRepeat(clips, null, _ => 0), Is.SameAs(clips[0]));
        }

        [Test]
        public void PickWithOneOrNoClipNeedsNoRoll()
        {
            var only = Clip("only");
            Func<int, int> mustNotRoll = _ => throw new AssertionException("no roll is needed");

            Assert.That(FootstepSurfaceCatalog.PickAvoidingRepeat(new[] { only }, only, mustNotRoll), Is.SameAs(only),
                "a single clip repeats rather than going silent");
            Assert.That(FootstepSurfaceCatalog.PickAvoidingRepeat(Array.Empty<AudioClip>(), null, mustNotRoll), Is.Null);
        }

        [Test]
        public void PickRejectsBadArgumentsAndOutOfRangeRolls()
        {
            var clips = new[] { Clip("a"), Clip("b") };

            Assert.Throws<ArgumentNullException>(() => FootstepSurfaceCatalog.PickAvoidingRepeat(null, null, _ => 0));
            Assert.Throws<ArgumentNullException>(() => FootstepSurfaceCatalog.PickAvoidingRepeat(clips, null, null));
            Assert.Throws<InvalidOperationException>(() => FootstepSurfaceCatalog.PickAvoidingRepeat(clips, null, n => n));
            Assert.Throws<InvalidOperationException>(() => FootstepSurfaceCatalog.PickAvoidingRepeat(clips, null, _ => -1));
        }

        [Test]
        public void ValidateAcceptsACompleteCatalog()
        {
            var catalog = Catalog(
                new FootstepSurfaceSounds(SurfaceMaterial.DefaultId, new[] { Clip("d") }, new[] { Clip("dl") }),
                new FootstepSurfaceSounds("metal", new[] { Clip("m") }, null));

            Assert.DoesNotThrow(() => catalog.Validate());
        }

        [Test]
        public void ValidateListsEveryProblem()
        {
            var missing = Clip("missing");
            var catalog = Catalog(
                new FootstepSurfaceSounds("metal", new[] { Clip("m") }, null),
                new FootstepSurfaceSounds("metal", new[] { missing }, null),
                new FootstepSurfaceSounds("wood", null, null),
                null);
            catalog.name = "Broken";
            Object.DestroyImmediate(missing);

            var thrown = Assert.Throws<PresentationCatalogException>(() => catalog.Validate());

            Assert.That(thrown.Message, Does.Contain("Broken"));
            Assert.That(thrown.Message, Does.Contain("already used"));
            Assert.That(thrown.Message, Does.Contain("footstep #0 is missing"));
            Assert.That(thrown.Message, Does.Contain("'wood') has no footstep or landing clip"));
            Assert.That(thrown.Message, Does.Contain("surface #3 is empty"));
            Assert.That(thrown.Message, Does.Contain("no 'default' surface"));
        }

        [Test]
        public void ValidateRequiresBothDefaultSounds()
        {
            var catalog = Catalog(new FootstepSurfaceSounds(SurfaceMaterial.DefaultId, new[] { Clip("d") }, null));

            var thrown = Assert.Throws<PresentationCatalogException>(() => catalog.Validate());

            Assert.That(thrown.Message, Does.Contain("'default' surface has no landing clip"));
        }

        [Test]
        public void SurfaceSoundsNormalizeTheirMaterialId()
        {
            Assert.That(new FootstepSurfaceSounds("  wood ", null, null).MaterialId, Is.EqualTo("wood"));
            Assert.That(new FootstepSurfaceSounds(" ", null, null).MaterialId, Is.EqualTo(SurfaceMaterial.DefaultId));
            Assert.That(new FootstepSurfaceSounds().MaterialId, Is.EqualTo(SurfaceMaterial.DefaultId));
        }

        [Test]
        public void CharacterAudioPlaysTheSurfaceClipsAndLogsWhenTheSurfaceChanges()
        {
            var concrete = Clip("concrete");
            var metal = Clip("metal");
            var audio = CreateAudio(
                Catalog(
                    new FootstepSurfaceSounds(SurfaceMaterial.DefaultId, new[] { Clip("d") }, new[] { Clip("dl") }),
                    new FootstepSurfaceSounds("concrete", new[] { concrete }, null),
                    new FootstepSurfaceSounds("metal", new[] { metal }, null)),
                null);

            LogAssert.Expect(LogType.Log, new Regex(Regex.Escape(CharacterAudio.FootstepLogTag) + @" 'Body' surface \(none\) -> concrete .*'concrete' from Surface"));
            Assert.That(audio.PlayFootstep(FootstepCueKind.Footstep, 0.5f, "concrete"), Is.SameAs(concrete));
            Assert.That(audio.PlayFootstep(FootstepCueKind.Footstep, 0.5f, "concrete"), Is.SameAs(concrete), "the same surface logs nothing");

            LogAssert.Expect(LogType.Log, new Regex(Regex.Escape(CharacterAudio.FootstepLogTag) + " 'Body' surface concrete -> metal"));
            Assert.That(audio.PlayFootstep(FootstepCueKind.Footstep, 0.5f, "metal"), Is.SameAs(metal));
            Assert.That(audio.LastSurfaceId, Is.EqualTo("metal"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void CharacterAudioDoesNotRepeatAFootstepOnTheSameSurface()
        {
            var clips = new[] { Clip("a"), Clip("b") };
            var audio = CreateAudio(
                Catalog(new FootstepSurfaceSounds(SurfaceMaterial.DefaultId, clips, new[] { Clip("land") })),
                null);

            var previous = audio.PlayFootstep(FootstepCueKind.Footstep, 1f, SurfaceMaterial.DefaultId);
            for (var i = 0; i < 10; i++)
            {
                var next = audio.PlayFootstep(FootstepCueKind.Footstep, 1f, SurfaceMaterial.DefaultId);
                Assert.That(next, Is.Not.SameAs(previous));
                previous = next;
            }
        }

        [Test]
        public void CharacterAudioWithoutTheSurfaceCatalogPlaysThePresentationCatalogSounds()
        {
            var legacyStep = Clip("legacy");
            var legacyLanding = Clip("legacyLanding");
            var audio = CreateAudio(null, Legacy(legacyStep, legacyLanding));

            Assert.That(audio.PlayFootstep(FootstepCueKind.Footstep, 1f, "metal"), Is.SameAs(legacyStep));
            Assert.That(audio.PlayFootstep(FootstepCueKind.Landing, 1f, "metal"), Is.SameAs(legacyLanding));
            Assert.That(audio.GetComponent<AudioSource>(), Is.Not.Null);
        }

        [Test]
        public void CharacterAudioWithNoSoundsStaysSilent()
        {
            var audio = CreateAudio(null, null);

            Assert.That(audio.PlayFootstep(FootstepCueKind.Footstep, 1f, "metal"), Is.Null);
            Assert.That(audio.PlayFootstep(FootstepCueKind.None, 0f, "metal"), Is.Null);
            Assert.That(audio.GetComponent<AudioSource>(), Is.Null, "no AudioSource until something plays");
            Assert.That(audio.LastSurfaceId, Is.Null);
        }

        [Test]
        public void ProbeReadsTheFloorUnderTheBodyColliderAndSkipsTheBodyItself()
        {
            Floor("metal", new Vector3(0f, 0f, 0f));
            var audio = CreateAudio(null, null);

            // 原点が足元より上にある本体 (プレイヤーの CharacterController と同じ形)
            audio.transform.position = new Vector3(0f, 1f, 0f);
            var capsule = audio.gameObject.AddComponent<CapsuleCollider>();
            capsule.height = 2f;
            capsule.radius = 0.4f;

            // 足元の自分の当たり判定は床として拾わない
            var foot = Create("Foot");
            foot.transform.SetParent(audio.transform, false);
            foot.transform.localPosition = new Vector3(0f, -0.9f, 0f);
            foot.AddComponent<BoxCollider>().size = new Vector3(0.2f, 0.1f, 0.2f);
            Physics.SyncTransforms();

            Assert.That(audio.ProbeSurfaceId(), Is.EqualTo("metal"));
        }

        [Test]
        public void ProbeFollowsTheFloorAndFallsBackToDefault()
        {
            Floor("wood", new Vector3(10f, 0f, 0f));
            Floor(null, new Vector3(20f, 0f, 0f));
            var audio = CreateAudio(null, null);

            audio.transform.position = new Vector3(10f, 0f, 0f);
            Physics.SyncTransforms();
            Assert.That(audio.ProbeSurfaceId(), Is.EqualTo("wood"));

            audio.transform.position = new Vector3(20f, 0f, 0f);
            Physics.SyncTransforms();
            Assert.That(audio.ProbeSurfaceId(), Is.EqualTo(SurfaceMaterial.DefaultId), "a floor without SurfaceMaterial is default");

            audio.transform.position = new Vector3(10f, 5f, 0f);
            Physics.SyncTransforms();
            Assert.That(audio.ProbeSurfaceId(), Is.EqualTo(SurfaceMaterial.DefaultId), "nothing under the feet is default");
        }
    }
}
