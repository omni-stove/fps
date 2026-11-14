using System.Collections.Generic;
using System.IO;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using NumericsVector2 = System.Numerics.Vector2;
using NumericsVector3 = System.Numerics.Vector3;
using Object = UnityEngine.Object;

namespace Fps.Net.Tests
{
    public sealed class NetMapWorldTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();
        private readonly List<string> _files = new List<string>();
        private Scene _scene;

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
            foreach (var file in _files)
            {
                File.Delete(file);
            }

            _files.Clear();
            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, true);
                _scene = default;
            }
        }

        [Test]
        public void LoadsTheMapFileIntoACollisionWorld()
        {
            var ground = CreateBox("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(10f, 1f, 10f));
            var path = WriteMap(MapCollisionExporter.Collect(new[] { ground }));
            _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            Assert.That(NetMapWorld.TryLoad(_scene, path, out var world, out var error), Is.True, error);

            Assert.That(world.ColliderCount, Is.EqualTo(1));
            Assert.That(world.Collision.Raycast(new NumericsVector3(0f, 5f, 0f), -NumericsVector3.UnitY, 10f, ~0, out var hit), Is.True);
            Assert.That(hit.Point.Y, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void MissingMapFileTellsHowToExportIt()
        {
            _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var path = Path.Combine(Path.GetTempPath(), "missing-" + System.Guid.NewGuid().ToString("N") + FpsMapFile.Extension);

            Assert.That(NetMapWorld.TryLoad(_scene, path, out var world, out var error), Is.False);

            Assert.That(world, Is.Null);
            StringAssert.Contains(path, error);
            StringAssert.Contains("make export-server-data", error);
        }

        [Test]
        public void MapFilesAreReadFromServerDataInTheEditor()
        {
            Assert.That(MapCollisionFiles.Directory, Is.EqualTo(MapCollisionExporter.OutputDirectory));
            Assert.That(
                MapCollisionFiles.PathOf("NetTest"),
                Is.EqualTo(Path.Combine(MapCollisionExporter.OutputDirectory, "NetTest" + MapCollisionExporter.FileExtension)));
        }

        [Test]
        public void BrokenPiecesStopBlockingAndRestoredPiecesBlockAgain()
        {
            var root = new GameObject("Building");
            _created.Add(root);
            root.AddComponent<DestructibleStructure>();
            var piece = DestructiblePiece.Create(root.transform, "Wall", new Vector3(0f, 1f, 2f), new Vector3(2f, 2f, 0.2f), 0, grounded: true, carveObstacle: false);
            var roots = new[] { root };
            var world = new NetMapWorld("Test", MapCollisionExporter.Collect(roots), roots);
            var origin = new NumericsVector3(0f, 1f, 0f);

            Assert.That(world.PieceCount, Is.EqualTo(1));
            Assert.That(world.Collision.Raycast(origin, NumericsVector3.UnitZ, 5f, ~0, out _), Is.True, "an intact piece blocks");

            piece.SetBroken(true);
            world.SyncDestruction();
            Assert.That(world.Collision.Raycast(origin, NumericsVector3.UnitZ, 5f, ~0, out _), Is.False, "a broken piece lets players through");

            piece.SetBroken(false);
            world.SyncDestruction();
            Assert.That(world.Collision.Raycast(origin, NumericsVector3.UnitZ, 5f, ~0, out _), Is.True, "a restored piece blocks again");
        }

        [Test]
        public void MotorStateSurvivesTheTripThroughCore()
        {
            var cover = new NetCoverState(
                new CoverSnapshot(CoverPhase.Peeking, CoverHeight.Low, new NumericsVector3(0f, 0f, -1f), 0.4f, 0.2f, 0.1f, 1f),
                0.8f,
                CoverEdge.Left,
                0.3f);
            var dodge = new NetDodgeState(new DodgeSnapshot(true, new NumericsVector2(0f, 1f), 0.1f, 0.25f));
            var path = new VaultPath(
                new[] { NumericsVector3.Zero, new NumericsVector3(0f, 1f, 0.2f), new NumericsVector3(0f, 1f, 1f), new NumericsVector3(0f, 0f, 1.5f) },
                VaultProbe.Speed);
            var vault = new NetVaultState(path, 0.05f);
            var state = new MotorState(new Vector3(1f, 2f, 3f), new Vector3(0.5f, -1f, 2f), true, true, cover, dodge, vault);

            var back = MotorState.FromCore(state.ToCore());

            Assert.That(back.Position, Is.EqualTo(state.Position));
            Assert.That(back.Velocity, Is.EqualTo(state.Velocity));
            Assert.That(back.IsGrounded, Is.True);
            Assert.That(back.IsCrouched, Is.True);
            Assert.That(back.Cover, Is.EqualTo(cover));
            Assert.That(back.Dodge, Is.EqualTo(dodge));
            Assert.That(back.Vault, Is.EqualTo(vault));
            Assert.That(MotorState.FromCore(new MotorState(Vector3.zero, Vector3.zero, false, false).ToCore()).Vault.IsVaulting, Is.False);
        }

        [Test]
        public void StepsLikeTheCoreSimulationAndCopiesTheResultToTheTransform()
        {
            var ground = CreateBox("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f));
            var roots = new[] { ground };
            var map = MapCollisionExporter.Collect(roots);
            var start = new Vector3(0f, 1.1f, 0f);
            var player = new GameObject("Player");
            _created.Add(player);
            player.transform.position = start;
            var controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.zero;

            var spec = MotorSpec.Default;
            var mover = CapsuleMoverSpec.Default;
            var net = new NetMotorSimulation(
                controller, new PlayerMotor(spec), NetStance.FromHierarchy(controller, 1.2f), new BvhWorldCollision(map), mover,
                NetworkPlayerMotor.TickSeconds, NetworkPlayerMotor.KillPlaneY);
            var core = new CharacterMotorSimulation(
                new CharacterBody(new BvhWorldCollision(map), mover, new StanceGeometry(2f, 0f, NetPlayer.EyeHeight, 1.2f, 0.5f)),
                new PlayerMotor(spec),
                NetworkPlayerMotor.TickSeconds,
                NetworkPlayerMotor.KillPlaneY);

            var netState = new MotorState(start, Vector3.zero, false, false);
            var coreState = netState.ToCore();
            for (var tick = 0; tick < 90; tick++)
            {
                var crouch = tick >= 60;
                var input = new MotorInput(new Vector2(0f, 1f), 0.3f, tick < 30, tick == 40, crouch);
                netState = net.Step(netState, input);
                coreState = core.Step(coreState, input.ToCore());

                Assert.That(netState.Position, Is.EqualTo(NetMath.ToUnity(coreState.Position)), "tick " + tick);
                Assert.That(netState.IsCrouched, Is.EqualTo(coreState.IsCrouched), "tick " + tick);
                Assert.That(player.transform.position, Is.EqualTo(netState.Position), "tick " + tick);
                Assert.That(controller.height, Is.EqualTo(netState.IsCrouched ? 1.2f : 2f), "tick " + tick);
            }

            Assert.That(netState.IsGrounded, Is.True);
            Assert.That(netState.IsCrouched, Is.True);
            Assert.That(netState.Position.z, Is.GreaterThan(3f));
        }

        private GameObject CreateBox(string name, Vector3 position, Vector3 scale)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;
            _created.Add(box);
            return box;
        }

        private string WriteMap(WorldMap map)
        {
            var path = Path.Combine(Path.GetTempPath(), "netmapworld-" + System.Guid.NewGuid().ToString("N") + FpsMapFile.Extension);
            using (var stream = File.Create(path))
            {
                FpsMapFile.Write(stream, map);
            }

            _files.Add(path);
            return path;
        }
    }
}
