using System;
using System.Linq;
using System.Numerics;
using Fps.Core.Navigation;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class MeshNavigationTests
    {
        private const float _tolerance = 1e-4f;
        private const int _allAreas = ~0;

        private static readonly Vector3 _ladderBottom = new Vector3(2f, 0f, 3.5f);
        private static readonly Vector3 _ladderTop = new Vector3(2f, 3f, 6.5f);

        [Test]
        public void ARoundACornerBendsAtTheInnerCorner()
        {
            var navigation = new NavMeshBuilder().Quad(0f, 0f, 8f, 2f).Quad(8f, 0f, 10f, 2f).Quad(8f, 2f, 10f, 10f).Navigation();
            var path = new NavPath();

            Assert.That(navigation.CalculatePath(new Vector3(1f, 0f, 1f), new Vector3(9f, 0f, 9f), _allAreas, path), Is.True);

            Assert.That(path.Status, Is.EqualTo(NavPathStatus.Complete));
            AssertPoints(path, new Vector3(1f, 0f, 1f), new Vector3(8f, 0f, 2f), new Vector3(9f, 0f, 9f));
            Assert.That(path.Points.All(p => !p.StartsLink), Is.True);
        }

        [Test]
        public void TheCornerIsTheSameWhenTrianglesWindClockwiseAsUnityBakesThem()
        {
            var navigation = new NavMeshBuilder().Clockwise().Quad(0f, 0f, 8f, 2f).Quad(8f, 0f, 10f, 2f).Quad(8f, 2f, 10f, 10f).Navigation();
            var path = new NavPath();

            Assert.That(navigation.CalculatePath(new Vector3(9f, 0f, 9f), new Vector3(1f, 0f, 1f), _allAreas, path), Is.True);

            Assert.That(path.Status, Is.EqualTo(NavPathStatus.Complete));
            AssertPoints(path, new Vector3(9f, 0f, 9f), new Vector3(8f, 0f, 2f), new Vector3(1f, 0f, 1f));
        }

        [Test]
        public void AStraightRunAcrossManyTrianglesHasNoCorners()
        {
            var builder = new NavMeshBuilder();
            for (var x = 0; x < 10; x++)
            {
                builder.Quad(x, 0f, x + 1, 2f);
            }

            var path = new NavPath();
            builder.Navigation().CalculatePath(new Vector3(0.5f, 0f, 0.3f), new Vector3(9.5f, 0f, 1.7f), _allAreas, path);

            Assert.That(path.Status, Is.EqualTo(NavPathStatus.Complete));
            AssertPoints(path, new Vector3(0.5f, 0f, 0.3f), new Vector3(9.5f, 0f, 1.7f));
        }

        [Test]
        public void EdgesThatMeetInATJunctionStillConnect()
        {
            // 右の 2 枚の辺は、左の 1 枚の辺の半分ずつにしか重ならない (Unity のタイルの境目で起きる形)。
            var navigation = new NavMeshBuilder().Quad(0f, 0f, 4f, 4f).Quad(4f, 0f, 6f, 2f).Quad(4f, 2f, 6f, 4f).Navigation();
            var path = new NavPath();

            navigation.CalculatePath(new Vector3(1f, 0f, 1f), new Vector3(5f, 0f, 3.5f), _allAreas, path);

            Assert.That(path.Status, Is.EqualTo(NavPathStatus.Complete));
            AssertPoints(path, new Vector3(1f, 0f, 1f), new Vector3(5f, 0f, 3.5f));
        }

        [Test]
        public void AnUnconnectedIslandHasNoPathAndThePathStopsAsCloseAsItCanGet()
        {
            var navigation = new NavMeshBuilder().Quad(0f, 0f, 4f, 4f).Quad(10f, 0f, 14f, 4f).Navigation();
            var path = new NavPath();

            var found = navigation.CalculatePath(new Vector3(1f, 0f, 1f), new Vector3(12f, 0f, 2f), _allAreas, path);

            Assert.That(found, Is.True, "a partial path is still a path, as with Unity's NavMesh.CalculatePath");
            Assert.That(path.Status, Is.EqualTo(NavPathStatus.Partial), "the island cannot be reached");
            AssertPoints(path, new Vector3(1f, 0f, 1f), new Vector3(4f, 0f, 2f));
        }

        [Test]
        public void ADestinationOffTheMeshGetsAPartialPathToTheNearestReachablePoint()
        {
            var navigation = new NavMeshBuilder().Quad(0f, 0f, 4f, 4f).Navigation();
            var path = new NavPath();

            navigation.CalculatePath(new Vector3(1f, 0f, 1f), new Vector3(2f, 0f, 30f), _allAreas, path);

            Assert.That(path.Status, Is.EqualTo(NavPathStatus.Partial));
            AssertPoints(path, new Vector3(1f, 0f, 1f), new Vector3(2f, 0f, 4f));
        }

        [Test]
        public void AStartOffTheMeshHasNoPath()
        {
            var navigation = new NavMeshBuilder().Quad(0f, 0f, 4f, 4f).Navigation();
            var path = new NavPath();
            path.Add(Vector3.One);

            Assert.That(navigation.CalculatePath(new Vector3(20f, 0f, 20f), new Vector3(1f, 0f, 1f), _allAreas, path), Is.False);

            Assert.That(path.Status, Is.EqualTo(NavPathStatus.Invalid));
            Assert.That(path.Points, Is.Empty);
        }

        [Test]
        public void TheStartAndDestinationAreMovedOntoTheMesh()
        {
            var navigation = new NavMeshBuilder().Quad(0f, 0f, 4f, 4f).Navigation();
            var path = new NavPath();

            navigation.CalculatePath(new Vector3(1f, 0.5f, 1f), new Vector3(3f, 0.9f, 3f), _allAreas, path);

            Assert.That(path.Status, Is.EqualTo(NavPathStatus.Complete));
            AssertPoints(path, new Vector3(1f, 0f, 1f), new Vector3(3f, 0f, 3f));
        }

        [Test]
        public void ThePathUpAndDownTheLadderCrossesItsLink()
        {
            var navigation = LadderMesh(true);
            var path = new NavPath();

            navigation.CalculatePath(new Vector3(2f, 0f, 1f), new Vector3(2f, 3f, 9f), _allAreas, path);

            Assert.That(path.Status, Is.EqualTo(NavPathStatus.Complete));
            AssertPoints(path, new Vector3(2f, 0f, 1f), _ladderBottom, _ladderTop, new Vector3(2f, 3f, 9f));
            Assert.That(path.Points.Select(p => p.LinkId), Is.EqualTo(new[] { NavPathPoint.NoLink, 0, NavPathPoint.NoLink, NavPathPoint.NoLink }));
            Assert.That(navigation.Links[path.Points[1].LinkId].Kind, Is.EqualTo(NavLinkKind.Ladder));

            navigation.CalculatePath(new Vector3(3f, 3f, 9f), new Vector3(1f, 0f, 1f), _allAreas, path);

            Assert.That(path.Status, Is.EqualTo(NavPathStatus.Complete));
            AssertPoints(path, new Vector3(3f, 3f, 9f), _ladderTop, _ladderBottom, new Vector3(1f, 0f, 1f));
            Assert.That(path.Points[1].LinkId, Is.EqualTo(0), "coming down starts the link at its top end");
        }

        [Test]
        public void AOneWayLinkCannotBeTakenBackwards()
        {
            var navigation = LadderMesh(false);
            var path = new NavPath();

            navigation.CalculatePath(new Vector3(2f, 3f, 9f), new Vector3(2f, 0f, 1f), _allAreas, path);

            Assert.That(path.Status, Is.EqualTo(NavPathStatus.Partial));
            Assert.That(path.Points.Any(p => p.StartsLink), Is.False);
        }

        [Test]
        public void ALinkInAnAreaOutsideTheMaskIsNotTaken()
        {
            var navigation = new NavMeshBuilder()
                .Quad(0f, 0f, 4f, 4f)
                .Quad(0f, 6f, 4f, 10f, 3f)
                .Link(_ladderBottom, _ladderTop, area: 3)
                .Navigation();
            var path = new NavPath();

            navigation.CalculatePath(new Vector3(2f, 0f, 1f), new Vector3(2f, 3f, 9f), ~(1 << 3), path);

            Assert.That(path.Status, Is.EqualTo(NavPathStatus.Partial));
        }

        [Test]
        public void ALinkWhoseEndIsOffTheMeshIsNotConnected()
        {
            var navigation = new NavMeshBuilder()
                .Quad(0f, 0f, 4f, 4f)
                .Quad(0f, 6f, 4f, 10f, 3f)
                .Link(_ladderBottom, new Vector3(2f, 3f, 30f))
                .Navigation();

            Assert.That(navigation.IsLinkConnected(0), Is.False);
        }

        [Test]
        public void ExpensiveAreasAreWalkedAround()
        {
            var navigation = new NavMeshBuilder()
                .Quad(0f, 0f, 4f, 2f).Quad(4f, 0f, 6f, 2f).Quad(6f, 0f, 10f, 2f)
                .Quad(0f, 2f, 4f, 8f).Quad(4f, 2f, 6f, 8f, area: 2).Quad(6f, 2f, 10f, 8f)
                .AreaCost(2, 50f)
                .Navigation();
            var path = new NavPath();
            var from = new Vector3(2f, 0f, 6f);
            var to = new Vector3(8f, 0f, 6f);

            navigation.CalculatePath(from, to, _allAreas, path);
            Assert.That(path.Status, Is.EqualTo(NavPathStatus.Complete));
            AssertPoints(path, from, new Vector3(4f, 0f, 2f), new Vector3(6f, 0f, 2f), to);

            navigation.CalculatePath(from, to, ~(1 << 2), path);
            AssertPoints(path, from, new Vector3(4f, 0f, 2f), new Vector3(6f, 0f, 2f), to);
        }

        [Test]
        public void AreasOutsideTheMaskAreNotWalked()
        {
            var navigation = new NavMeshBuilder().Quad(0f, 0f, 4f, 2f).Quad(4f, 0f, 6f, 2f, area: 5).Quad(6f, 0f, 10f, 2f).Navigation();
            var path = new NavPath();

            navigation.CalculatePath(new Vector3(1f, 0f, 1f), new Vector3(9f, 0f, 1f), ~(1 << 5), path);

            Assert.That(path.Status, Is.EqualTo(NavPathStatus.Partial));
            AssertPoints(path, new Vector3(1f, 0f, 1f), new Vector3(4f, 0f, 1f));
        }

        [Test]
        public void SamplePositionReturnsTheNearestPointOnTheMesh()
        {
            var navigation = LadderMesh(true);

            Assert.That(navigation.SamplePosition(new Vector3(2f, 1f, 2f), 2f, _allAreas, out var nearest), Is.True);
            AssertNear(nearest, new Vector3(2f, 0f, 2f));

            Assert.That(navigation.SamplePosition(new Vector3(2f, 3.2f, 5f), 2f, _allAreas, out nearest), Is.True);
            AssertNear(nearest, new Vector3(2f, 3f, 6f));

            Assert.That(navigation.SamplePosition(new Vector3(-3f, 0f, 2f), 2f, _allAreas, out nearest), Is.False);
            AssertNear(nearest, new Vector3(-3f, 0f, 2f));

            Assert.That(navigation.SamplePosition(new Vector3(2f, 1f, 2f), 0f, _allAreas, out _), Is.False);
            Assert.That(navigation.SamplePosition(new Vector3(2f, 1f, 2f), 2f, 0, out _), Is.False);
            Assert.That(navigation.SamplePosition(new Vector3(-30f, 0f, 2f), float.PositiveInfinity, _allAreas, out nearest), Is.True);
            AssertNear(nearest, new Vector3(0f, 0f, 2f));
        }

        [Test]
        public void ThePathIsTheSameAfterReadingTheFileBack()
        {
            var file = new NavMeshBuilder()
                .Quad(0f, 0f, 4f, 4f)
                .Quad(0f, 6f, 4f, 10f, 3f)
                .Link(_ladderBottom, _ladderTop)
                .Build();
            NavMeshFile read;
            using (var stream = new System.IO.MemoryStream())
            {
                file.Write(stream);
                stream.Position = 0;
                read = NavMeshFile.Read(stream);
            }

            var path = new NavPath();
            new MeshNavigation(read).CalculatePath(new Vector3(2f, 0f, 1f), new Vector3(2f, 3f, 9f), _allAreas, path);

            Assert.That(path.Status, Is.EqualTo(NavPathStatus.Complete));
            Assert.That(path.Points[1].LinkId, Is.EqualTo(0));
        }

        [Test]
        public void ConstructorRejectsBadArguments()
        {
            var file = new NavMeshBuilder().Quad(0f, 0f, 1f, 1f).Build();

            Assert.Throws<ArgumentNullException>(() => _ = new MeshNavigation(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new MeshNavigation(file, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new MeshNavigation(file, float.NaN));
            Assert.Throws<ArgumentNullException>(() => new MeshNavigation(file).CalculatePath(Vector3.Zero, Vector3.One, _allAreas, null));
        }

        [Test]
        public void AnEmptyMeshHasNoPathAndNoSample()
        {
            var navigation = new MeshNavigation(new NavMeshFile(Array.Empty<Vector3>(), Array.Empty<NavTriangle>()));
            var path = new NavPath();

            Assert.That(navigation.SamplePosition(Vector3.Zero, 10f, _allAreas, out _), Is.False);
            Assert.That(navigation.CalculatePath(Vector3.Zero, Vector3.One, _allAreas, path), Is.False);
        }

        [Test]
        public void ALargeGridFindsTheWayAroundAWall()
        {
            var builder = new NavMeshBuilder();
            for (var x = 0; x < 20; x++)
            {
                for (var z = 0; z < 20; z++)
                {
                    if (x != 10 || z >= 18)
                    {
                        builder.Quad(x, z, x + 1, z + 1);
                    }
                }
            }

            // 細かい三角形の上では、三角形の並びの選び方で最短から少し外れることがある (Unity の NavMesh の経路も同じ)。
            var from = new Vector3(5f, 0f, 1f);
            var to = new Vector3(15f, 0f, 1f);
            var path = new NavPath();
            builder.Navigation().CalculatePath(from, to, _allAreas, path);

            var wallTopLeft = new Vector3(10f, 0f, 18f);
            var wallTopRight = new Vector3(11f, 0f, 18f);
            var shortest = Vector3.Distance(from, wallTopLeft) + 1f + Vector3.Distance(wallTopRight, to);
            Assert.That(path.Status, Is.EqualTo(NavPathStatus.Complete));
            Assert.That(path.Points.Select(p => p.Position), Does.Contain(wallTopLeft).And.Contain(wallTopRight));
            Assert.That(path.Length(), Is.InRange(shortest - _tolerance, shortest * 1.05f));
        }

        private static MeshNavigation LadderMesh(bool bidirectional)
        {
            return new NavMeshBuilder()
                .Quad(0f, 0f, 4f, 4f)
                .Quad(0f, 6f, 4f, 10f, 3f)
                .Link(_ladderBottom, _ladderTop, bidirectional)
                .Navigation();
        }

        private static void AssertPoints(NavPath path, params Vector3[] expected)
        {
            var actual = path.Points.Select(p => p.Position).ToArray();
            Assert.That(actual, Has.Length.EqualTo(expected.Length), "points: " + string.Join(" ", actual));
            for (var i = 0; i < expected.Length; i++)
            {
                AssertNear(actual[i], expected[i], $"point {i} of " + string.Join(" ", actual));
            }
        }

        private static void AssertNear(Vector3 actual, Vector3 expected, string message = null)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(_tolerance), message ?? $"{actual} != {expected}");
        }
    }
}
