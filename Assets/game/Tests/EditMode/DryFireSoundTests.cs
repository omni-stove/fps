using System.Collections.Generic;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="DryFireSound"/> が武器の空撃ちの音を鳴らしてタグ付きのログを出し、カタログや音が無くても止まらないことを確かめる。
    /// </summary>
    public sealed class DryFireSoundTests
    {
        private const string RifleId = "rifle-id";

        private readonly List<Object> _created = new List<Object>();
        private WeaponExtrasCatalog _catalog;
        private AudioClip _click;

        [SetUp]
        public void SetUp()
        {
            _click = Track(AudioClip.Create("DryFire", 4410, 1, 44100, false));
            _catalog = Track(ScriptableObject.CreateInstance<WeaponExtrasCatalog>());
            _catalog.SetWeapons(new[] { new WeaponExtrasAssets(RifleId, null, null, _click) });
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void PlayClicksAndLogs()
        {
            var sound = Sound("Shooter");

            LogAssert.Expect(LogType.Log, DryFireSound.FormatLog(RifleId, _click));
            Assert.That(sound.Play(RifleId), Is.True);

            Assert.That(sound.Source, Is.Not.Null);
            Assert.That(sound.Source.spatialBlend, Is.EqualTo(1f), "someone else's click comes from their body");
        }

        [Test]
        public void TheLocalPlayersClickIs2D()
        {
            var sound = Sound("Player");
            sound.gameObject.AddComponent<WeaponPresentation>().IsLocal = true;

            LogAssert.Expect(LogType.Log, DryFireSound.FormatLog(RifleId, _click));
            Assert.That(sound.Play(RifleId), Is.True);

            Assert.That(sound.IsLocal, Is.True);
            Assert.That(sound.Source.spatialBlend, Is.Zero);
        }

        [Test]
        public void AMissingCatalogWeaponOrClipLogsWithoutPlaying()
        {
            var sound = Sound("Shooter");
            sound.Catalog = null;
            LogAssert.Expect(LogType.Log, DryFireSound.FormatLog(RifleId, null));
            Assert.That(sound.Play(RifleId), Is.False);

            sound.Catalog = _catalog;
            LogAssert.Expect(LogType.Log, DryFireSound.FormatLog("unknown", null));
            Assert.That(sound.Play("unknown"), Is.False);

            var silent = Track(ScriptableObject.CreateInstance<WeaponExtrasCatalog>());
            silent.SetWeapons(new[] { new WeaponExtrasAssets(RifleId, null, null, null) });
            sound.Catalog = silent;
            LogAssert.Expect(LogType.Log, DryFireSound.FormatLog(RifleId, null));
            Assert.That(sound.Play(RifleId), Is.False);

            Assert.That(sound.Source, Is.Null, "nothing was created");
        }

        [Test]
        public void TheLogCarriesTheTagWeaponAndSound()
        {
            Assert.That(DryFireSound.FormatLog(RifleId, _click), Is.EqualTo("[Presentation] dry-fire weapon=rifle-id sound=DryFire"));
            Assert.That(DryFireSound.FormatLog(null, null), Is.EqualTo("[Presentation] dry-fire weapon= sound=none"));
        }

        private DryFireSound Sound(string bodyName)
        {
            var body = Track(new GameObject(bodyName));
            var sound = body.AddComponent<DryFireSound>();
            sound.Catalog = _catalog;
            return sound;
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
