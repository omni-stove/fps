using System.Collections.Generic;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Tests.EditMode
{
    public sealed class DestructiblePieceVisualTests
    {
        private const string ModelName = "Model";

        private Scene _scene;
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _scene = TestScenes.NewAdditive();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, true);
            }
        }

        [Test]
        public void ThePrefabIsReadFromTheSamplePrefabsFolder()
        {
            Assert.That(DestructiblePieceVisual.PrefabPath, Is.EqualTo(DemoSample.Root + "/Prefabs/DestructiblePiece.prefab"));
        }

        [Test]
        public void APrefabSavedAtThePathIsLoaded()
        {
            var path = DestructiblePieceVisual.PrefabPath;
            // ゲームが置いた見た目を消さないよう、空いているときだけ置いて確かめる。
            Assume.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Null, path + " is already set by the game");

            var source = Prefab("Brick");
            var saved = PrefabUtility.SaveAsPrefabAsset(source, path);
            try
            {
                Assert.That(saved, Is.Not.Null);
                Assert.That(DestructiblePieceVisual.Load(), Is.SameAs(saved));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }

            Assert.That(DestructiblePieceVisual.Load(), Is.Null);
        }

        [Test]
        public void TheHutUsesThePrefabWhenOneIsSet()
        {
            var hut = DestructionTestSceneBuilder.CreateHut(_scene, true, Prefab("Brick"));

            Assert.That(hut.Pieces, Has.Count.EqualTo(DestructionTestSceneBuilder.PieceCount));
            AssertPrefabLook(hut.Pieces);
        }

        [Test]
        public void TheHutKeepsThePlaceholderCubesWithoutAPrefab()
        {
            var hut = DestructionTestSceneBuilder.CreateHut(_scene, true);

            AssertPlaceholderLook(hut.Pieces);
        }

        [Test]
        public void TheActorsPassThePrefabToTheHut()
        {
            var launcher = Track(ScriptableObject.CreateInstance<WeaponDefinition>());
            launcher.name = "Launcher";
            WeaponAssetBuilder.Configure(launcher, WeaponAssetBuilder.LauncherSpec());
            var rules = Track(ScriptableObject.CreateInstance<GameRulesDefinition>());
            rules.name = "Rules";
            GameRulesAssetBuilder.Configure(rules, GameRulesAssetBuilder.RangeRules());

            var hut = DestructionTestSceneBuilder.CreateActors(_scene, new[] { launcher }, rules, Prefab("Brick"));

            AssertPrefabLook(hut.Pieces);
        }

        [Test]
        public void HordeFortificationsUseThePrefabWhenOneIsSet()
        {
            var placement = HordeSceneBuilder.Fortifications[0];

            var fortification = HordeSceneBuilder.CreateFortification(Root("Base"), placement, true, null, Prefab("Sandbag"));

            var pieces = fortification.GetComponentsInChildren<DestructiblePiece>(true);
            Assert.That(pieces, Has.Length.EqualTo(placement.Length * HordeSceneBuilder.PieceRows));
            AssertPrefabLook(pieces);
            foreach (var piece in pieces)
            {
                Assert.That(piece.transform.localScale, Is.EqualTo(HordeSceneBuilder.PieceSize), "the prefab is scaled to the piece");
            }
        }

        [Test]
        public void HordeFortificationsKeepThePlaceholderCubesWithoutAPrefab()
        {
            var fortification = HordeSceneBuilder.CreateFortification(Root("Base"), HordeSceneBuilder.Fortifications[0], true, null);

            AssertPlaceholderLook(fortification.GetComponentsInChildren<DestructiblePiece>(true));
        }

        public static void AssertPrefabLook(IEnumerable<DestructiblePiece> pieces)
        {
            foreach (var piece in pieces)
            {
                Assert.That(piece.GetComponent<MeshFilter>(), Is.Null, piece.name + " has no placeholder cube");
                Assert.That(piece.Collider, Is.InstanceOf<BoxCollider>(), piece.name + " keeps its box for hits and support");
                var visual = piece.transform.Find(DestructiblePiece.VisualName);
                Assert.That(visual, Is.Not.Null, piece.name);
                Assert.That(visual.Find(ModelName), Is.Not.Null, piece.name + " shows the prefab");
            }
        }

        public static void AssertPlaceholderLook(IEnumerable<DestructiblePiece> pieces)
        {
            foreach (var piece in pieces)
            {
                Assert.That(piece.GetComponent<MeshFilter>(), Is.Not.Null, piece.name + " is the placeholder cube");
                Assert.That(piece.transform.Find(DestructiblePiece.VisualName), Is.Null, piece.name);
            }
        }

        private Transform Root(string name)
        {
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, _scene);
            return root.transform;
        }

        private GameObject Prefab(string name)
        {
            var root = Track(new GameObject(name));
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.name = ModelName;
            model.transform.SetParent(root.transform, false);
            return root;
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
