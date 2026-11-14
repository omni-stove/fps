using System.Collections.Generic;
using System.IO;
using Fps.Core.Navigation;
using Fps.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Fps.Net.Tests
{
    public sealed class ServerMapNavMeshTests
    {
        private const int PairCount = 500;
        private const int Seed = 584;
        private const float LengthRatio = 0.05f;
        private const float LengthSlack = 0.5f;

        private Scene _scene;
        private bool _opened;

        public static IEnumerable<string> Maps => ServerBuilder.ServerScenePaths();

        [TearDown]
        public void TearDown()
        {
            // 開いたマップの NavMesh が残ると、後のテストの NavMesh に混ざる。
            if (_opened)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                _opened = false;
            }
        }

        [TestCaseSource(nameof(Maps))]
        public void TheCommittedFileIsTheSceneNavMeshAndCoreFindsTheSamePathsAsUnity(string scenePath)
        {
            OpenAlone(scenePath);
            var file = ReadCommitted(scenePath);

            var captured = NavMeshExporter.Capture(_scene);
            Assert.That(file.Vertices, Is.EqualTo(captured.Vertices), "run make export-server-data and commit the result");
            Assert.That(file.Triangles, Is.EqualTo(captured.Triangles), "run make export-server-data and commit the result");
            Assert.That(file.AreaCosts, Is.EqualTo(captured.AreaCosts), "run make export-server-data and commit the result");
            Assert.That(file.Links, Is.EqualTo(captured.Links), "run make export-server-data and commit the result");

            var report = NavMeshParity.Compare(file, PairCount, Seed, LengthRatio, LengthSlack);

            TestContext.WriteLine(scenePath + ": " + report);
            Assert.That(report.Pairs, Is.EqualTo(PairCount));
            Assert.That(report.Mismatches, Is.Empty, report + "\n" + string.Join("\n", report.Mismatches));
        }

        private static NavMeshFile ReadCommitted(string scenePath)
        {
            var path = Path.Combine(ServerDataExporter.OutputDirectory, NavMeshExporter.MapNameOf(scenePath) + NavMeshFile.Extension);
            Assert.That(File.Exists(path), Is.True, path + " is missing; run make export-server-data and commit it");
            using (var stream = File.OpenRead(path))
            {
                return NavMeshFile.Read(stream);
            }
        }

        // 生成物のマップ (NetArena01 など) は make build-server か make export-server-data が作るまで無いので、その間は比べられない。
        private void OpenAlone(string scenePath)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                Assert.Ignore(scenePath + " has not been generated; run make export-server-data");
            }

            _scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            _opened = true;
        }
    }
}
