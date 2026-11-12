using System.Collections.Generic;
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
