using System;
using NUnit.Framework;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class ExtractionZoneTests
    {
        private GameObject _go;
        private ExtractionZone _zone;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Exit");
            _go.transform.position = new Vector3(0f, 0f, 10f);
            _zone = _go.AddComponent<ExtractionZone>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void ContainsMeasuresOnTheGround()
        {
            Assert.That(_zone.Radius, Is.EqualTo(ExtractionZone.DefaultRadius));
            Assert.That(_zone.Contains(new Vector3(0f, 5f, 12.9f)), Is.True, "height does not matter");
            Assert.That(_zone.Contains(new Vector3(0f, 0f, 13.1f)), Is.False);

            _zone.Radius = 5f;
            Assert.That(_zone.Contains(new Vector3(0f, 0f, 14.9f)), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => _zone.Radius = 0f);
        }

        [Test]
        public void ThePlaceholderIsAFlatMarkerYouCanWalkOver()
        {
            _zone.Build();

            var visual = _go.transform.Find(ExtractionZone.VisualName);
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.GetComponent<Collider>(), Is.Null);
            Assert.That(visual.localScale, Is.EqualTo(new Vector3(6f, ExtractionZone.PlaceholderThickness, 6f)));
            Assert.That(_zone.IsOpen, Is.False);

            _zone.SetOpen(true);
            Assert.That(_zone.IsOpen, Is.True);
            Assert.That(visual.gameObject.activeSelf, Is.False, "the closed look hides once the point opens");

            var open = _go.transform.Find(ExtractionZone.VisualName + "-Open");
            Assert.That(open, Is.Not.Null);
            Assert.That(open.gameObject.activeSelf, Is.True);
            Assert.That(open.GetComponent<Collider>(), Is.Null);
            Assert.That(open.localScale, Is.EqualTo(visual.localScale));
        }

        [Test]
        public void AClosedLookAloneStillGetsAnOpenPlaceholder()
        {
            var closedLook = new GameObject("ClosedLook");
            try
            {
                _zone.ClosedVisualPrefab = closedLook;
                _zone.Build();

                _zone.SetOpen(true);
                var closed = _go.transform.Find(ExtractionZone.VisualName);
                var open = _go.transform.Find(ExtractionZone.VisualName + "-Open");
                Assert.That(closed.gameObject.activeSelf, Is.False);
                Assert.That(open, Is.Not.Null);
                Assert.That(open.gameObject.activeSelf, Is.True);
                Assert.That(open.GetComponent<Collider>(), Is.Null);
                Assert.That(open.localScale, Is.EqualTo(new Vector3(6f, ExtractionZone.PlaceholderThickness, 6f)));
            }
            finally
            {
                Object.DestroyImmediate(closedLook);
            }
        }

        [Test]
        public void AnOpenLookReplacesTheClosedOne()
        {
            var openLook = new GameObject("OpenLook");
            try
            {
                _zone.OpenVisualPrefab = openLook;
                _zone.Build();

                _zone.SetOpen(true);
                var closed = _go.transform.Find(ExtractionZone.VisualName);
                var open = _go.transform.Find(ExtractionZone.VisualName + "-Open");
                Assert.That(closed.gameObject.activeSelf, Is.False);
                Assert.That(open.gameObject.activeSelf, Is.True);

                _zone.SetOpen(false);
                Assert.That(closed.gameObject.activeSelf, Is.True);
                Assert.That(open.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(openLook);
            }
        }

        [Test]
        public void ThePointIdIsNeverNull()
        {
            Assert.That(_zone.PointId, Is.Empty);
            _zone.PointId = null;
            Assert.That(_zone.PointId, Is.Empty);
        }
    }
}
