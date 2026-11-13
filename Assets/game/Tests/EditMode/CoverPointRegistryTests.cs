using System;
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
    /// CoverPointRegistry が印 (Fps.Runtime.CoverPoint) から selector を作り、敵どうしで点を共有させることを確かめる。
    /// 印は NavMesh の無い遠く (x = 500) に置くので、位置は寄せられずにそのまま使われる。
    /// </summary>
    public sealed class CoverPointRegistryTests
    {
        private static readonly Vector3 Origin = new Vector3(500f, 0f, 500f);

        private GameObject _root;
        private CoverPointRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("CoverPoints");
            _registry = _root.AddComponent<CoverPointRegistry>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void SelectorHoldsThePointsInOrderWithTheirFacingAndHeight()
        {
            var low = Marker("Low", new Vector3(1f, 0f, 0f), Vector3.back, CoverHeight.Low);
            var high = Marker("High", new Vector3(-1f, 0f, 0f), Vector3.left, CoverHeight.High);
            _registry.SetPoints(new[] { low, high });

            var selector = _registry.Selector;

            Assert.That(selector, Is.Not.Null);
            Assert.That(selector.Points.Count, Is.EqualTo(2));
            Assert.That(selector.Points[0].Position, Is.EqualTo((Origin + new Vector3(1f, 0f, 0f)).ToNumerics()));
            Assert.That(selector.Points[0].Height, Is.EqualTo(CoverHeight.Low));
            Assert.That(selector.Points[0].Facing.Z, Is.EqualTo(-1f).Within(1e-5f));
            Assert.That(selector.Points[1].Height, Is.EqualTo(CoverHeight.High));
            Assert.That(selector.Points[1].Facing.X, Is.EqualTo(-1f).Within(1e-5f));
            Assert.That(_registry.MarkerAt(0), Is.SameAs(low));
            Assert.That(_registry.MarkerAt(1), Is.SameAs(high));
            Assert.That(_registry.MarkerAt(2), Is.Null);
            Assert.That(_registry.MarkerAt(-1), Is.Null);
            Assert.That(_registry.Selector, Is.SameAs(selector), "built once");
        }

        [Test]
        public void PeekPositionFollowsTheMarker()
        {
            var high = Marker("High", Vector3.zero, Vector3.back, CoverHeight.High);
            high.SetPeekOffsetWorld(new Vector3(1.5f, 0f, 0f));
            _registry.SetPoints(new[] { high });

            Assert.That(Vector3.Distance(high.PeekPosition, Origin + new Vector3(1.5f, 0f, 0f)), Is.LessThan(1e-4f));
            Assert.That(_registry.Selector, Is.Not.Null);
            Assert.That(Vector3.Distance(_registry.PeekPositionAt(0).Value, high.PeekPosition), Is.LessThan(1e-4f));
            Assert.That(_registry.PeekPositionAt(1).HasValue, Is.False);
        }

        [Test]
        public void InvalidMarkersAreReportedAndSkipped()
        {
            var good = Marker("Good", Vector3.zero, Vector3.back, CoverHeight.Low);
            var upright = Marker("Upright", Vector3.right, Vector3.back, CoverHeight.Low);
            upright.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            _registry.SetPoints(new[] { upright, null, good });

            LogAssert.Expect(LogType.Error, new Regex(@"^\[Cover\] cover point 'Upright' is invalid"));
            var selector = _registry.Selector;

            Assert.That(_registry.Points.Count, Is.EqualTo(2), "null entries are dropped");
            Assert.That(selector.Points.Count, Is.EqualTo(1));
            Assert.That(_registry.MarkerAt(0), Is.SameAs(good), "indices follow the usable points");
        }

        [Test]
        public void NoUsablePointsMeansNoSelector()
        {
            Assert.That(_registry.Selector, Is.Null);
            Assert.That(_registry.IsOccupied(null), Is.False);
        }

        [Test]
        public void InvalidSettingsAreReportedAndGiveNoSelector()
        {
            _registry.SetPoints(new[] { Marker("A", Vector3.zero, Vector3.back, CoverHeight.Low) });
            _registry.MaxDistance = 0f;

            LogAssert.Expect(LogType.Error, new Regex(@"^\[Cover\] 'CoverPoints' has invalid settings"));
            Assert.That(_registry.Selector, Is.Null);

            _registry.MaxDistance = CoverPointSelector.DefaultMaxDistance;
            Assert.That(_registry.Selector, Is.Not.Null, "fixing the settings rebuilds the selector");
        }

        [Test]
        public void EnemiesSharingTheRegistryDoNotPickTheSamePoint()
        {
            var near = Marker("Near", new Vector3(0f, 0f, 1f), Vector3.back, CoverHeight.Low);
            var far = Marker("Far", new Vector3(0f, 0f, 3f), Vector3.back, CoverHeight.Low);
            _registry.SetPoints(new[] { near, far });
            var selector = _registry.Selector;
            var self = Origin.ToNumerics();
            var threat = (Origin + new Vector3(0f, 0f, -10f)).ToNumerics();
            var first = new object();
            var second = new object();

            var firstPick = selector.Select(self, threat, first);
            Assert.That(selector.TryClaim(firstPick, first), Is.True);
            var secondPick = selector.Select(self, threat, second);

            Assert.That(_registry.MarkerAt(firstPick), Is.SameAs(near));
            Assert.That(_registry.MarkerAt(secondPick), Is.SameAs(far));
            Assert.That(_registry.IsOccupied(near), Is.True);
            Assert.That(_registry.IsOccupied(far), Is.False);
        }

        [Test]
        public void ChangingThePointsRebuildsTheSelector()
        {
            _registry.SetPoints(new[] { Marker("A", Vector3.zero, Vector3.back, CoverHeight.Low) });
            var before = _registry.Selector;

            _registry.SetPoints(new[]
            {
                Marker("B", Vector3.zero, Vector3.back, CoverHeight.Low),
                Marker("C", Vector3.right, Vector3.back, CoverHeight.Low),
            });

            Assert.That(_registry.Selector, Is.Not.SameAs(before));
            Assert.That(_registry.Selector.Points.Count, Is.EqualTo(2));
            Assert.Throws<ArgumentNullException>(() => _registry.SetPoints(null));
        }

        private Fps.Runtime.CoverPoint Marker(string name, Vector3 offset, Vector3 facing, CoverHeight height)
        {
            var marker = new GameObject(name).AddComponent<Fps.Runtime.CoverPoint>();
            marker.transform.SetParent(_root.transform, false);
            marker.transform.SetPositionAndRotation(Origin + offset, Quaternion.LookRotation(facing));
            marker.Height = height;
            return marker;
        }
    }
}
