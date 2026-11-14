using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fps.Core;
using Fps.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Net.Tests
{
    // 失敗したら make export-server-data で書き出し直し、server-data/maps をコミットする
    public sealed class ServerDataMapsTests
    {
        private const int _probeCount = 4500;

        private Scene _scene;

        private static IEnumerable<string> MapScenes => BuildSceneList.ServerScenes;

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid() && SceneManager.sceneCount > 1)
            {
                EditorSceneManager.CloseScene(_scene, true);
            }
        }

        [Test]
        public void ServerDataHasOneFilePerMap()
        {
            var expected = BuildSceneList.ServerScenes.Select(s => MapCollisionExporter.MapName(s) + MapCollisionExporter.FileExtension);
            var actual = Directory.GetFiles(MapCollisionExporter.OutputDirectory, "*" + MapCollisionExporter.FileExtension).Select(Path.GetFileName);

            Assert.That(actual, Is.EquivalentTo(expected));
        }

        [TestCaseSource(nameof(MapScenes))]
        public void CommittedMapMatchesTheScene(string scenePath)
        {
            var sources = OpenAndCollect(scenePath, out var fresh);
            var committed = MapCollisionExporter.ReadFile(MapFile(scenePath));

            Assert.That(Bytes(committed), Is.EqualTo(Bytes(fresh)), MapFile(scenePath) + " is out of date with " + scenePath);
            Assert.That(sources, Is.Not.Empty);
        }

        [TestCaseSource(nameof(MapScenes))]
        public void CommittedMapRaycastsLikePhysics(string scenePath)
        {
            var sources = OpenAndCollect(scenePath, out _);
            var world = new BvhWorldCollision(MapCollisionExporter.ReadFile(MapFile(scenePath)));
            var probes = MapCollisionCheck.SampleProbes(MapCollisionCheck.BoundsOf(sources), _probeCount, 583);

            var mismatches = MapCollisionCheck.CompareRaycasts(world, probes, Physics.DefaultRaycastLayers, sources);

            Assert.That(mismatches, Is.Empty, mismatches.Count + " of " + probes.Count + " rays differ:\n" + string.Join("\n", mismatches.Take(20)));
        }

        private List<Collider> OpenAndCollect(string scenePath, out WorldMap map)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                Assert.Ignore(scenePath + " has not been generated; run make export-server-data to generate it and compare");
            }

            Assert.That(File.Exists(MapFile(scenePath)), Is.True, MapFile(scenePath) + " is missing");
            _scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            var sources = new List<Collider>();
            map = MapCollisionExporter.Collect(_scene.GetRootGameObjects(), sources);
            return sources;
        }

        private static string MapFile(string scenePath)
        {
            return Path.Combine(MapCollisionExporter.OutputDirectory, MapCollisionExporter.MapName(scenePath) + MapCollisionExporter.FileExtension);
        }

        private static byte[] Bytes(WorldMap map)
        {
            using (var stream = new MemoryStream())
            {
                FpsMapFile.Write(stream, map);
                return stream.ToArray();
            }
        }
    }
}
