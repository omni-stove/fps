using System;
using System.IO;
using System.Linq;
using Fps.Core;
using Fps.Core.Navigation;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using LadderComponent = Fps.Runtime.Traversal.Ladder;
using NumericsVector3 = System.Numerics.Vector3;

namespace Fps.Tests.EditMode
{
    public sealed class NavMeshExporterTests
    {
        private const int PairCount = 300;
        private const int Seed = 584;
        private const float LengthRatio = 0.05f;
        private const float LengthSlack = 0.5f;

        // 台は z = 2〜6、はしごは台の -Z 側の面 (z = 2) に立ち、つかまる人は +Z を向く。台の上へは、はしごでしか行けない。
        private static readonly Vector3 _towerCenter = new Vector3(0f, 1.5f, 4f);
        private static readonly Vector3 _towerSize = new Vector3(4f, 3f, 4f);
        private static readonly Vector3 _ground = new Vector3(0f, 0f, -6f);
        private static readonly Vector3 _top = new Vector3(0f, 3f, 4.5f);

        private Scene _scene;
        private LadderComponent _ladder;
        private NavMeshLink _walkLink;
        private string _temp;

        [SetUp]
        public void SetUp()
        {
            _scene = TestScenes.NewAdditive();
            var floor = SceneBuildHelpers.CreateStaticPlane(_scene, null, "Floor", Vector3.zero, new Vector3(3f, 1f, 3f));
            var surface = SceneBuildHelpers.AddNavMeshSurface(floor, CollectObjects.All);
            SceneBuildHelpers.CreateStaticBox(_scene, null, "Tower", _towerCenter, _towerSize);

            var ladderObject = new GameObject("Ladder");
            SceneManager.MoveGameObjectToScene(ladderObject, _scene);
            ladderObject.transform.SetPositionAndRotation(new Vector3(0f, 0f, 2f), Quaternion.LookRotation(Vector3.forward));
            _ladder = ladderObject.AddComponent<LadderComponent>();
            _ladder.Height = _towerSize.y;

            var linkObject = new GameObject("Gap Link");
            SceneManager.MoveGameObjectToScene(linkObject, _scene);
            linkObject.transform.SetPositionAndRotation(new Vector3(10f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
            linkObject.transform.localScale = new Vector3(3f, 3f, 3f);
            _walkLink = linkObject.AddComponent<NavMeshLink>();
            _walkLink.startPoint = new Vector3(0f, 0f, -1f);
            _walkLink.endPoint = new Vector3(0f, 0f, 1f);
            _walkLink.bidirectional = false;
            _walkLink.area = 2;

            SceneBuildHelpers.BakeNavMesh(surface);
            _temp = Path.Combine(Path.GetTempPath(), "fps-navmesh-exporter-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (_ladder != null)
            {
                _ladder.Unregister();
            }

            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, true);
            }

            if (Directory.Exists(_temp))
            {
                Directory.Delete(_temp, true);
            }
        }

        [Test]
        public void TheTrianglesAndAreasAreThoseOfTheBakedNavMesh()
        {
            var triangulation = NavMesh.CalculateTriangulation();

            var file = NavMeshExporter.Capture(_scene);

            Assert.That(file.Vertices, Has.Count.EqualTo(triangulation.vertices.Length));
            Assert.That(file.Triangles, Has.Count.EqualTo(triangulation.indices.Length / 3));
            for (var i = 0; i < file.Triangles.Count; i++)
            {
                Assert.That(file.Triangles[i].A, Is.EqualTo(triangulation.indices[i * 3]));
                Assert.That(file.Triangles[i].Area, Is.EqualTo(triangulation.areas[i]));
            }

            Assert.That(file.Vertices[0], Is.EqualTo(triangulation.vertices[0].ToNumerics()));
            for (var i = 0; i < NavMeshFile.AreaCount; i++)
            {
                Assert.That(file.AreaCosts[i], Is.EqualTo(NavMesh.GetAreaCost(i)), "area " + i);
            }
        }

        [Test]
        public void TheLadderBecomesATwoWayLadderLinkWithTheEndsItLinksInPlay()
        {
            var agentRadius = NavMesh.GetSettingsByID(NavMeshExporter.AgentTypeOf(_scene)).agentRadius;
            Assert.That(_ladder.TryGetLinkEnds(agentRadius, out var bottom, out var top), Is.True);

            var link = NavMeshExporter.Capture(_scene).Links.Single(l => l.Kind == NavLinkKind.Ladder);

            Assert.That(link.Start, Is.EqualTo(bottom.ToNumerics()));
            Assert.That(link.End, Is.EqualTo(top.ToNumerics()));
            Assert.That(link.Bidirectional, Is.True);
            Assert.That(link.Area, Is.Zero);
        }

        [Test]
        public void ANavMeshLinkKeepsItsDirectionAreaAndUnscaledWorldEnds()
        {
            var link = NavMeshExporter.Capture(_scene).Links.Single(l => l.Kind == NavLinkKind.Walk);

            AssertNear(link.Start, new NumericsVector3(9f, 0f, 0f));
            AssertNear(link.End, new NumericsVector3(11f, 0f, 0f));
            Assert.That(link.Bidirectional, Is.False);
            Assert.That(link.Area, Is.EqualTo(2));
        }

        [Test]
        public void DisabledLaddersAndLinksAreLeftOut()
        {
            _ladder.CreatesNavMeshLinks = false;
            _walkLink.enabled = false;

            Assert.That(NavMeshExporter.Capture(_scene).Links, Is.Empty);
        }

        [Test]
        public void TheWrittenFileReadsBackTheSame()
        {
            var file = NavMeshExporter.Capture(_scene);
            var path = Path.Combine(_temp, "maps", "Test" + NavMeshFile.Extension);

            NavMeshExporter.Write(file, path);
            NavMeshFile read;
            using (var stream = File.OpenRead(path))
            {
                read = NavMeshFile.Read(stream);
            }

            Assert.That(read.Vertices, Is.EqualTo(file.Vertices));
            Assert.That(read.Triangles, Is.EqualTo(file.Triangles));
            Assert.That(read.AreaCosts, Is.EqualTo(file.AreaCosts));
            Assert.That(read.Links, Is.EqualTo(file.Links));
        }

        [Test]
        public void CoreFindsTheSamePathsAsUnityIncludingUpTheLadder()
        {
            _walkLink.enabled = false;
            var file = NavMeshExporter.Capture(_scene);
            _ladder.Register();

            var report = NavMeshParity.Compare(file, PairCount, Seed, LengthRatio, LengthSlack);

            TestContext.WriteLine(report);
            Assert.That(report.Mismatches, Is.Empty, report.ToString());
            Assert.That(report.UnityComplete, Is.EqualTo(report.Pairs), "the ladder joins the platform to the ground");
            Assert.That(report.CoreWithLink, Is.GreaterThan(0), "some pairs climb the ladder");

            var path = new NavPath();
            Assert.That(new MeshNavigation(file).CalculatePath(_ground.ToNumerics(), _top.ToNumerics(), NavMesh.AllAreas, path), Is.True);
            Assert.That(path.Status, Is.EqualTo(NavPathStatus.Complete));
            Assert.That(path.Points.Any(p => p.LinkId != NavPathPoint.NoLink && file.Links[p.LinkId].Kind == NavLinkKind.Ladder), Is.True);
        }

        [Test]
        public void WithoutTheLadderTheyAgreeThePlatformCannotBeReached()
        {
            _walkLink.enabled = false;
            _ladder.CreatesNavMeshLinks = false;
            var file = NavMeshExporter.Capture(_scene);

            var report = NavMeshParity.Compare(file, PairCount, Seed, LengthRatio, LengthSlack);

            TestContext.WriteLine(report);
            Assert.That(report.Mismatches, Is.Empty, report.ToString());
            Assert.That(report.UnityComplete, Is.LessThan(report.Pairs), "some pairs have one end on the platform");
        }

        [Test]
        public void ATriangulationWhoseArraysDisagreeIsRejected()
        {
            var vertices = new[] { Vector3.zero, Vector3.right, Vector3.forward };

            Assert.That(() => NavMeshExporter.FromTriangulation(vertices, new[] { 0, 1 }, new int[0], null, null), Throws.ArgumentException);
            Assert.That(() => NavMeshExporter.FromTriangulation(vertices, new[] { 0, 1, 2 }, new[] { 0, 0 }, null, null), Throws.ArgumentException);
            Assert.That(() => NavMeshExporter.FromTriangulation(vertices, new[] { 0, 1, 3 }, new[] { 0 }, null, null), Throws.ArgumentException);
            Assert.That(NavMeshExporter.FromTriangulation(vertices, new[] { 0, 1, 2 }, new[] { 3 }, null, null).Triangles[0].Area, Is.EqualTo(3));
        }

        [Test]
        public void TheMapNameIsTheSceneFileName()
        {
            Assert.That(NavMeshExporter.MapNameOf("Assets/Scenes/NetArena01.unity"), Is.EqualTo("NetArena01"));
            Assert.That(() => NavMeshExporter.MapNameOf("Assets/Scenes/Bad Name.unity"), Throws.ArgumentException);
        }

        [Test]
        public void MapsComeFromTheArgumentsBeforeTheRegisteredOnes()
        {
            string[] Registered() => new[] { "Assets/Registered.unity" };

            Assert.That(
                ServerDataExporter.ResolveMaps(new[] { "Unity", "-serverMap", "Assets/A.unity", "-serverMap", "Assets/B.unity" }, Registered),
                Is.EqualTo(new[] { "Assets/A.unity", "Assets/B.unity" }));
            Assert.That(ServerDataExporter.ResolveMaps(new[] { "Unity" }, Registered), Is.EqualTo(new[] { "Assets/Registered.unity" }));
            Assert.That(() => ServerDataExporter.ResolveMaps(new[] { "Unity", "-serverMap" }, Registered), Throws.ArgumentException);
            Assert.That(() => ServerDataExporter.ResolveMaps(new[] { "Unity" }, null), Throws.InvalidOperationException);
        }

        private static void AssertNear(NumericsVector3 actual, NumericsVector3 expected)
        {
            Assert.That(NumericsVector3.Distance(actual, expected), Is.LessThan(1e-4f), $"{actual} is not {expected}");
        }
    }
}
