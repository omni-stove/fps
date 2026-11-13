using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ControlPointMapTests
    {
        private static ControlPointArea Area(int index, float x, float z, float radius = 5f, float height = 6f)
        {
            return new ControlPointArea(index, new Vector3(x, 0f, z), radius, height);
        }

        [Test]
        public void AreaContainsPointsInsideTheCylinderIncludingTheEdge()
        {
            var area = Area(0, 0f, 0f, radius: 5f, height: 6f);

            Assert.That(area.Contains(new Vector3(0f, 1f, 0f)), Is.True);
            Assert.That(area.Contains(new Vector3(3f, 0f, 4f)), Is.True, "exactly on the radius");
            Assert.That(area.Contains(new Vector3(0f, 3f, 0f)), Is.True, "exactly on the top");
            Assert.That(area.Contains(new Vector3(0f, -3f, 0f)), Is.True, "exactly on the bottom");
        }

        [Test]
        public void AreaExcludesPointsOutsideTheRadiusOrHeight()
        {
            var area = Area(0, 0f, 0f, radius: 5f, height: 6f);

            Assert.That(area.Contains(new Vector3(5.01f, 0f, 0f)), Is.False);
            Assert.That(area.Contains(new Vector3(0f, 3.01f, 0f)), Is.False);
            Assert.That(area.Contains(new Vector3(0f, -3.01f, 0f)), Is.False);
            Assert.That(area.Contains(new Vector3(float.NaN, 0f, 0f)), Is.False);
        }

        [Test]
        public void AreaRejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ControlPointArea(-1, Vector3.Zero, 5f, 6f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ControlPointArea(0, new Vector3(float.NaN, 0f, 0f), 5f, 6f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ControlPointArea(0, Vector3.Zero, 0f, 6f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ControlPointArea(0, Vector3.Zero, float.PositiveInfinity, 6f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ControlPointArea(0, Vector3.Zero, 5f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ControlPointArea(0, Vector3.Zero, 5f, float.NaN));
        }

        [Test]
        public void MapOrdersAreasByIndex()
        {
            var map = new ControlPointMap(new[] { Area(1, 20f, 0f), Area(0, 0f, 0f) });

            Assert.That(map.Count, Is.EqualTo(2));
            Assert.That(map[0].Center, Is.EqualTo(new Vector3(0f, 0f, 0f)));
            Assert.That(map[1].Center, Is.EqualTo(new Vector3(20f, 0f, 0f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = map[2]);
        }

        [Test]
        public void LocateReturnsThePointIndexOrNull()
        {
            var map = new ControlPointMap(new[] { Area(0, 0f, 0f), Area(1, 20f, 0f) });

            Assert.That(map.Locate(new Vector3(1f, 1f, 1f)), Is.EqualTo(0));
            Assert.That(map.Locate(new Vector3(19f, 1f, 0f)), Is.EqualTo(1));
            Assert.That(map.Locate(new Vector3(10f, 1f, 0f)), Is.Null);
        }

        [Test]
        public void LocatePicksTheNearestCenterWhereAreasOverlap()
        {
            var map = new ControlPointMap(new[] { Area(0, 0f, 0f), Area(1, 6f, 0f) });

            Assert.That(map.Locate(new Vector3(2.9f, 0f, 0f)), Is.EqualTo(0));
            Assert.That(map.Locate(new Vector3(3.1f, 0f, 0f)), Is.EqualTo(1));
            Assert.That(map.Locate(new Vector3(3f, 0f, 0f)), Is.EqualTo(0), "a tie goes to the lower index");
        }

        [Test]
        public void MapRejectsEmptyDuplicateOrMissingIndices()
        {
            Assert.Throws<ArgumentNullException>(() => new ControlPointMap(null));
            Assert.Throws<ArgumentException>(() => new ControlPointMap(Array.Empty<ControlPointArea>()));
            Assert.Throws<ArgumentException>(() => new ControlPointMap(new[] { Area(0, 0f, 0f), Area(0, 20f, 0f) }));
            Assert.Throws<ArgumentException>(() => new ControlPointMap(new[] { Area(0, 0f, 0f), Area(2, 20f, 0f) }));
            Assert.Throws<ArgumentException>(() => new ControlPointMap(new[] { Area(1, 0f, 0f) }));
        }
    }
}
