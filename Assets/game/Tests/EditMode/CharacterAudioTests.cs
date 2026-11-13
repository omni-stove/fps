using System.Collections.Generic;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class CharacterAudioTests
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

        private CharacterAudio CreateAudio(PresentationCatalog catalog)
        {
            var body = new GameObject("Body");
            _created.Add(body);
            var audio = body.AddComponent<CharacterAudio>();
            audio.Catalog = catalog;
            return audio;
        }

        private PresentationCatalog CreateCatalog(bool withHurtVoice)
        {
            var catalog = ScriptableObject.CreateInstance<PresentationCatalog>();
            _created.Add(catalog);
            if (withHurtVoice)
            {
                var clip = AudioClip.Create("Hurt", 4410, 1, 44100, false);
                _created.Add(clip);
                catalog.HurtVoice = clip;
            }

            return catalog;
        }

        [Test]
        public void HurtVoiceWithoutCatalogStaysSilent()
        {
            var audio = CreateAudio(null);

            Assert.That(audio.PlayHurtVoice(), Is.False);
            Assert.That(audio.GetComponent<AudioSource>(), Is.Null, "no AudioSource until something plays");
        }

        [Test]
        public void HurtVoiceWithoutClipStaysSilent()
        {
            var audio = CreateAudio(CreateCatalog(withHurtVoice: false));

            Assert.That(audio.PlayHurtVoice(), Is.False);
            Assert.That(audio.GetComponent<AudioSource>(), Is.Null);
        }

        [Test]
        public void HurtVoicePlaysFromA3DSourceOnTheBody()
        {
            var audio = CreateAudio(CreateCatalog(withHurtVoice: true));

            Assert.That(audio.PlayHurtVoice(), Is.True);

            var source = audio.GetComponent<AudioSource>();
            Assert.That(source, Is.Not.Null);
            Assert.That(source.spatialBlend, Is.EqualTo(1f));
            Assert.That(source.playOnAwake, Is.False);
            Assert.That(source.rolloffMode, Is.EqualTo(AudioRolloffMode.Linear));
            Assert.That(source.maxDistance, Is.EqualTo(CharacterAudio.DefaultMaxDistance));
        }

        [Test]
        public void RapidHitsPlayTheHurtVoiceOnce()
        {
            var audio = CreateAudio(CreateCatalog(withHurtVoice: true));

            Assert.That(audio.PlayHurtVoice(), Is.True);
            Assert.That(audio.PlayHurtVoice(), Is.False);
            Assert.That(audio.GetComponents<AudioSource>().Length, Is.EqualTo(1));
        }
    }
}
