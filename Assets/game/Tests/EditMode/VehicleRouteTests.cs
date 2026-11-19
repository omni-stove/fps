using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class VehicleRouteTests
    {
        private readonly List<Object> _created = new List<Object>();
        private VehicleRoute _route;

        [SetUp]
        public void SetUp()
        {
            _route = new GameObject("Route").AddComponent<VehicleRoute>();
            _created.Add(_route.gameObject);
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
        public void ARouteLoopsByDefault()
        {
            Assert.That(_route.Loop, Is.True);
        }

        [Test]
        public void WithoutListedPointsTheChildrenAreThePointsInOrder()
        {
            Child(_route.transform, new Vector3(0f, 0f, 10f));
            Child(_route.transform, new Vector3(10f, 0f, 10f));
            var positions = new List<System.Numerics.Vector3> { new System.Numerics.Vector3(9f, 9f, 9f) };

            _route.CopyPositions(positions);

            Assert.That(positions, Is.EqualTo(new[] { new System.Numerics.Vector3(0f, 0f, 10f), new System.Numerics.Vector3(10f, 0f, 10f) }));
        }

        [Test]
        public void ListedPointsWinOverChildrenAndRemovedOnesAreSkipped()
        {
            Child(_route.transform, new Vector3(5f, 0f, 5f));
            var a = Child(null, new Vector3(1f, 0f, 0f));
            var b = Child(null, new Vector3(2f, 0f, 0f));
            var c = Child(null, new Vector3(3f, 0f, 0f));
            _route.SetPoints(a, b, c);
            Object.DestroyImmediate(b.gameObject);
            var positions = new List<System.Numerics.Vector3>();

            _route.CopyPositions(positions);

            Assert.That(positions, Is.EqualTo(new[] { new System.Numerics.Vector3(1f, 0f, 0f), new System.Numerics.Vector3(3f, 0f, 0f) }));
        }

        [Test]
        public void CopyPointsGivesTheSameTransformsAsThePositions()
        {
            var first = Child(_route.transform, new Vector3(0f, 0f, 10f));
            var second = Child(_route.transform, new Vector3(10f, 0f, 10f));
            var points = new List<Transform> { _route.transform };

            _route.CopyPoints(points);

            Assert.That(points, Is.EqualTo(new[] { first, second }));
        }

        [Test]
        public void CopyPointsPrefersListedPointsAndSkipsRemovedOnes()
        {
            Child(_route.transform, new Vector3(5f, 0f, 5f));
            var a = Child(null, new Vector3(1f, 0f, 0f));
            var b = Child(null, new Vector3(2f, 0f, 0f));
            var c = Child(null, new Vector3(3f, 0f, 0f));
            _route.SetPoints(a, b, c);
            Object.DestroyImmediate(b.gameObject);
            var points = new List<Transform>();

            _route.CopyPoints(points);

            Assert.That(points, Is.EqualTo(new[] { a, c }));
        }

        [Test]
        public void SetPointsRejectsMissingPoints()
        {
            Assert.Throws<ArgumentNullException>(() => _route.SetPoints(null));
            Assert.Throws<ArgumentException>(() => _route.SetPoints(_route.transform, null));
            Assert.Throws<ArgumentNullException>(() => _route.CopyPositions(null));
            Assert.Throws<ArgumentNullException>(() => _route.CopyPoints(null));
        }

        private Transform Child(Transform parent, Vector3 position)
        {
            var point = new GameObject("Point").transform;
            _created.Add(point.gameObject);
            point.SetParent(parent, false);
            point.position = position;
            return point;
        }
    }
}
