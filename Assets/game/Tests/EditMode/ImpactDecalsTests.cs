using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class ImpactDecalsTests
    {
        private readonly List<Object> _created = new List<Object>();

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
        public void ADecalLiesOnTheSurfaceFacingAlongTheNormalSlightlyRaised()
        {
            var decals = Decals();
            var surface = Surface("concrete", 0.2f);
            var point = new Vector3(1f, 2f, 3f);
            var normal = new Vector3(0f, 0f, -2f);

            LogAssert.Expect(LogType.Log, "[Presentation] decal placed material=concrete surface=concrete active=1/" + ImpactDecals.DefaultCapacity);
            var decal = decals.Place(new ImpactInfo(point, normal, null, "concrete", false, null), surface, 0f);

            Assert.That(decal, Is.Not.Null);
            Assert.That(decal.Root.activeSelf, Is.True);
            Assert.That(decal.Root.transform.parent, Is.SameAs(decals.transform));
            AssertNear(decal.Root.transform.position, point + (Vector3.back * ImpactDecals.SurfaceOffset));
            AssertNear(decal.Root.transform.forward, Vector3.back, "the visible side faces away from the surface");
            AssertNear(decal.Root.transform.localScale, new Vector3(0.2f, 0.2f, 1f));
            Assert.That(decal.Renderer.sharedMaterial, Is.SameAs(surface.Decal));
            Assert.That(decal.Root.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(ImpactDecals.QuadMesh));
            Assert.That(decal.MaterialId, Is.EqualTo("concrete"));
        }

        [Test]
        public void TheQuadIsSeenFromItsPlusZSide()
        {
            var mesh = ImpactDecals.QuadMesh;
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;

            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                Assert.That(Vector3.Cross(b - a, c - a).normalized, Is.EqualTo(Vector3.forward), $"triangle #{i / 3}");
            }

            Assert.That(mesh.colors, Has.All.EqualTo(Color.white), "the particle shader multiplies the vertex colour");
        }

        [Test]
        public void TheRollTurnsTheDecalAroundTheNormal()
        {
            var normal = new Vector3(1f, 0f, 0f);

            var still = ImpactDecals.DecalPose(Vector3.zero, normal, 0f);
            var turned = ImpactDecals.DecalPose(Vector3.zero, normal, 90f);

            AssertNear(still.forward, normal);
            AssertNear(turned.forward, normal, "rolling keeps the decal on the surface");
            Assert.That(Vector3.Angle(still.up, turned.up), Is.EqualTo(90f).Within(0.01f));
        }

        [Test]
        public void FloorsCeilingsAndAMissingNormalStillGiveAValidPose()
        {
            foreach (var normal in new[] { Vector3.up, Vector3.down, Vector3.zero })
            {
                var pose = ImpactDecals.DecalPose(Vector3.zero, normal, 33f);
                var expected = normal == Vector3.zero ? Vector3.up : normal;

                Assert.That(float.IsNaN(pose.rotation.x), Is.False, $"normal {normal}");
                AssertNear(pose.forward, expected, $"normal {normal}");
                AssertNear(pose.position, expected * ImpactDecals.SurfaceOffset, $"normal {normal}");
            }
        }

        [Test]
        public void ACharacterImpactGetsNoDecalAndNoLog()
        {
            var decals = Decals();

            var decal = decals.Place(new ImpactInfo(Vector3.zero, Vector3.back, null, "flesh", true, HitZone.Body), Surface("flesh", 0.1f), 0f);

            Assert.That(decal, Is.Null);
            Assert.That(decals.Pool, Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ASurfaceWithoutADecalMaterialGetsNoDecal()
        {
            var decals = Decals();
            var bare = new SurfaceImpactAssets("metal", null, 0.1f, null, null);

            Assert.That(decals.Place(Impact("metal"), bare, 0f), Is.Null);
            Assert.That(decals.Place(Impact("metal"), null, 0f), Is.Null);
            Assert.That(decals.ActiveCount, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void PastTheCapacityTheOldestDecalIsReleasedAndReused()
        {
            var decals = Decals();
            decals.Capacity = 3;
            var surface = Surface("default", 0.1f);

            var first = decals.Place(Impact("wood"), surface, 0f);
            decals.Place(Impact("metal"), surface, 0f);
            decals.Place(Impact("concrete"), surface, 0f);

            LogAssert.Expect(LogType.Log, ImpactDecals.FormatEvictedLog("wood", 3));
            LogAssert.Expect(LogType.Log, ImpactDecals.FormatPlacedLog("default", "default", 3, 3));
            var fourth = decals.Place(Impact("default"), surface, 0f);

            Assert.That(fourth, Is.SameAs(first), "the oldest decal is taken over");
            Assert.That(fourth.MaterialId, Is.EqualTo("default"));
            Assert.That(decals.Pool.Count, Is.EqualTo(3));
            Assert.That(decals.ActiveCount, Is.EqualTo(3));
            Assert.That(decals.transform.childCount, Is.EqualTo(3), "nothing is created past the capacity");
        }

        [Test]
        public void EvictionLogsShowTheMaterialAndCapacity()
        {
            Assert.That(ImpactDecals.FormatEvictedLog("wood", 64), Is.EqualTo("[Presentation] decal released oldest material=wood capacity=64"));
            Assert.That(ImpactDecals.FormatPlacedLog(" ", "metal", 2, 64), Is.EqualTo("[Presentation] decal placed material=default surface=metal active=2/64"));
        }

        [Test]
        public void ChangingTheCapacityStartsOver()
        {
            var decals = Decals();
            var surface = Surface("default", 0.1f);
            decals.Place(Impact("default"), surface, 0f);

            decals.Capacity = 5;

            Assert.That(decals.Pool, Is.Null);
            Assert.That(decals.transform.childCount, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => decals.Capacity = 0);
        }

        [Test]
        public void ClearHidesEveryDecalAndKeepsThemForReuse()
        {
            var decals = Decals();
            var surface = Surface("default", 0.1f);
            var decal = decals.Place(Impact("default"), surface, 0f);

            decals.Clear();

            Assert.That(decals.ActiveCount, Is.Zero);
            Assert.That(decal.Root.activeSelf, Is.False);
            Assert.That(decals.Place(Impact("default"), surface, 0f), Is.SameAs(decal));
        }

        [Test]
        public void ADecalOnABrokenPieceIsReleasedAndOthersStay()
        {
            var decals = Decals();
            var surface = Surface("concrete", 0.1f);
            var structure = Track(new GameObject("Structure"));
            var piece = DestructiblePiece.Create(structure.transform, "Piece", Vector3.zero, Vector3.one, 0);
            var wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));

            var onPiece = decals.Place(new ImpactInfo(Vector3.zero, Vector3.back, piece.Collider, "concrete", false, null), surface, 0f);
            var onWall = decals.Place(new ImpactInfo(Vector3.zero, Vector3.back, wall.GetComponent<Collider>(), "concrete", false, null), surface, 0f);
            var loose = decals.Place(Impact("concrete"), surface, 0f);

            Assert.That(decals.ReleaseDetached(), Is.Zero, "nothing is released while every surface stands");

            piece.SetBroken(true);
            LogAssert.Expect(LogType.Log, ImpactDecals.FormatDetachedLog(1, 2, ImpactDecals.DefaultCapacity));
            Assert.That(decals.ReleaseDetached(), Is.EqualTo(1));

            Assert.That(onPiece.Root.activeSelf, Is.False);
            Assert.That(onWall.Root.activeSelf, Is.True);
            Assert.That(loose.Root.activeSelf, Is.True, "a decal placed without a collider is never released by this");
            Assert.That(decals.ActiveCount, Is.EqualTo(2));
        }

        [Test]
        public void ADecalIsReleasedWhenItsColliderIsDestroyedOrDeactivated()
        {
            var decals = Decals();
            var surface = Surface("wood", 0.1f);
            var destroyed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var hidden = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            var first = decals.Place(new ImpactInfo(Vector3.zero, Vector3.back, destroyed.GetComponent<Collider>(), "wood", false, null), surface, 0f);
            var second = decals.Place(new ImpactInfo(Vector3.zero, Vector3.back, hidden.GetComponent<Collider>(), "wood", false, null), surface, 0f);

            Object.DestroyImmediate(destroyed);
            hidden.SetActive(false);

            Assert.That(first.IsDetached, Is.True);
            Assert.That(second.IsDetached, Is.True);
            LogAssert.Expect(LogType.Log, ImpactDecals.FormatDetachedLog(2, 0, ImpactDecals.DefaultCapacity));
            Assert.That(decals.ReleaseDetached(), Is.EqualTo(2));
            Assert.That(decals.ActiveCount, Is.Zero);
        }

        [Test]
        public void ReusingADecalForAnotherSurfaceReplacesItsAnchor()
        {
            var decals = Decals();
            var surface = Surface("metal", 0.1f);
            var wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            var decal = decals.Place(new ImpactInfo(Vector3.zero, Vector3.back, wall.GetComponent<Collider>(), "metal", false, null), surface, 0f);

            decals.Clear();
            var reused = decals.Place(Impact("metal"), surface, 0f);
            wall.SetActive(false);

            Assert.That(reused, Is.SameAs(decal));
            Assert.That(reused.Anchor, Is.Null);
            Assert.That(reused.IsDetached, Is.False);
        }

        [Test]
        public void ADecalMovesAndTurnsWithTheObjectItHit()
        {
            var decals = Decals();
            var surface = Surface("metal", 0.2f);
            var crate = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            crate.transform.position = new Vector3(0f, 0f, 0.5f);
            var decal = decals.Place(new ImpactInfo(Vector3.zero, Vector3.back, crate.GetComponent<Collider>(), "metal", false, null), surface, 0f);
            var placed = decal.Root.transform;

            Assert.That(decals.FollowAnchors(), Is.Zero, "a still object leaves its decal alone");
            AssertNear(placed.position, Vector3.back * ImpactDecals.SurfaceOffset);

            crate.transform.position = new Vector3(3f, 1f, 0.5f);
            Assert.That(decals.FollowAnchors(), Is.EqualTo(1));
            AssertNear(placed.position, new Vector3(3f, 1f, -ImpactDecals.SurfaceOffset));
            AssertNear(placed.forward, Vector3.back);

            crate.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            decals.FollowAnchors();
            AssertNear(placed.position, new Vector3(2.5f - ImpactDecals.SurfaceOffset, 1f, 0.5f), "the decal turns with the face it is on");
            AssertNear(placed.forward, Vector3.left);
            AssertNear(placed.localScale, new Vector3(0.2f, 0.2f, 1f), "the decal keeps its own size");
        }

        [Test]
        public void TheGapToTheFaceStaysTheSameWhenTheObjectStretches()
        {
            var decals = Decals();
            var surface = Surface("wood", 0.1f);
            var plank = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            plank.transform.position = new Vector3(0f, 0f, 0.5f);
            var decal = decals.Place(new ImpactInfo(Vector3.zero, Vector3.back, plank.GetComponent<Collider>(), "wood", false, null), surface, 0f);

            plank.transform.localScale = new Vector3(1f, 1f, 4f);
            decals.FollowAnchors();

            AssertNear(decal.Root.transform.position, new Vector3(0f, 0f, -1.5f - ImpactDecals.SurfaceOffset));
        }

        [Test]
        public void ADecalWithoutAnObjectAndAReleasedDecalStayPut()
        {
            var decals = Decals();
            var surface = Surface("concrete", 0.1f);
            var door = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            var loose = decals.Place(new ImpactInfo(new Vector3(1f, 0f, 0f), Vector3.back, null, "concrete", false, null), surface, 0f);
            var onDoor = decals.Place(new ImpactInfo(Vector3.zero, Vector3.back, door.GetComponent<Collider>(), "concrete", false, null), surface, 0f);

            decals.Clear();
            var reused = decals.Place(new ImpactInfo(new Vector3(0f, 5f, 0f), Vector3.back, null, "concrete", false, null), surface, 0f);
            door.transform.position = new Vector3(9f, 0f, 0f);

            Assert.That(reused, Is.SameAs(onDoor).Or.SameAs(loose));
            Assert.That(decals.FollowAnchors(), Is.Zero, "a reused decal no longer follows the door it was on");
            AssertNear(reused.Root.transform.position, new Vector3(0f, 5f, -ImpactDecals.SurfaceOffset));
        }

        [Test]
        public void TheDetachedLogNamesTheCountAndWhatIsLeft()
        {
            Assert.That(ImpactDecals.FormatDetachedLog(3, 5, 64), Is.EqualTo("[Presentation] decal released detached count=3 active=5/64"));
        }

        [Test]
        public void ForReturnsOneSharedInstancePerScene()
        {
            var scene = SceneManager.GetActiveScene();

            var first = Track(ImpactDecals.For(scene).gameObject).GetComponent<ImpactDecals>();
            var second = ImpactDecals.For(scene);

            Assert.That(second, Is.SameAs(first));
            Assert.That(first.gameObject.scene, Is.EqualTo(scene));
            Assert.That(first.name, Is.EqualTo(ImpactDecals.ObjectName));
            Assert.That(ImpactDecals.For(default), Is.SameAs(first), "an unloaded scene falls back to the active scene");
        }

        private static ImpactInfo Impact(string materialId)
        {
            return new ImpactInfo(Vector3.zero, Vector3.back, null, materialId, false, null);
        }

        private ImpactDecals Decals()
        {
            return Track(new GameObject("Decals")).AddComponent<ImpactDecals>();
        }

        private SurfaceImpactAssets Surface(string materialId, float size)
        {
            var material = Track(new Material(Shader.Find("Hidden/InternalErrorShader")) { name = materialId });
            return new SurfaceImpactAssets(materialId, material, size, null, null);
        }

        private static void AssertNear(Vector3 actual, Vector3 expected, string message = null)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), message ?? $"expected {expected} but was {actual}");
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
