using System.Collections.Generic;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Fps.Net.Tests
{
    public sealed class HeadlessPresentationTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _created.Clear();
        }

        private GameObject Create(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            else
            {
                _created.Add(go);
            }

            return go;
        }

        [Test]
        public void DisablesCameraAndAudioIncludingInactiveChildren()
        {
            var root = Create("Root");
            var camera = Create("Camera", root.transform).AddComponent<Camera>();
            var listener = camera.gameObject.AddComponent<AudioListener>();
            var inactive = Create("Inactive", root.transform);
            inactive.SetActive(false);
            var source = inactive.AddComponent<AudioSource>();

            var disabled = HeadlessPresentation.DisableUnder(root);

            Assert.That(disabled, Is.EqualTo(3));
            Assert.That(camera.enabled, Is.False);
            Assert.That(listener.enabled, Is.False);
            Assert.That(source.enabled, Is.False);
        }

        [Test]
        public void DisablesThePresentationComponents()
        {
            var root = Create("Root");
            var weapon = root.AddComponent<WeaponPresentation>();
            var audio = root.AddComponent<CharacterAudio>();
            var inactive = Create("Inactive", root.transform);
            inactive.SetActive(false);
            var animator = inactive.AddComponent<CharacterAnimator>();
            var grenade = root.AddComponent<GrenadeThrower>();

            var disabled = HeadlessPresentation.DisableUnder(root);

            Assert.That(disabled, Is.EqualTo(3));
            Assert.That(weapon.enabled, Is.False);
            Assert.That(audio.enabled, Is.False);
            Assert.That(animator.enabled, Is.False);
            Assert.That(grenade.enabled, Is.True, "throwing is gameplay, not presentation");
        }

        [Test]
        public void DisablesTheShellEjectorAndDryFireSound()
        {
            var root = Create("Root");
            var shells = root.AddComponent<ShellEjector>();
            var dryFire = root.AddComponent<DryFireSound>();

            var disabled = HeadlessPresentation.DisableUnder(root);

            Assert.That(disabled, Is.EqualTo(2));
            Assert.That(shells.enabled, Is.False);
            Assert.That(dryFire.enabled, Is.False);
        }

        [Test]
        public void DisablesTheRigsAnimator()
        {
            // NetSceneBuilder が NetPlayer のプレハブに置くリグと同じく、本体の子の根に Animator がある
            var root = Create("NetPlayer");
            var rig = Create(CharacterAnimator.RigObjectName, root.transform).AddComponent<Animator>();

            var disabled = HeadlessPresentation.DisableUnder(root);

            Assert.That(disabled, Is.EqualTo(1));
            Assert.That(rig.enabled, Is.False);
        }

        [Test]
        public void KeepsOtherComponentsAndCountsOnlyNewlyDisabled()
        {
            var root = Create("Root");
            var light = root.AddComponent<Light>();
            var camera = root.AddComponent<Camera>();
            camera.enabled = false;

            var disabled = HeadlessPresentation.DisableUnder(root);

            Assert.That(disabled, Is.Zero);
            Assert.That(light.enabled, Is.True);
        }

        [Test]
        public void ToleratesNullAndInvalidScene()
        {
            Assert.That(HeadlessPresentation.DisableUnder(null), Is.Zero);
            Assert.That(HeadlessPresentation.DisableIn(default), Is.Zero);
        }
    }
}
