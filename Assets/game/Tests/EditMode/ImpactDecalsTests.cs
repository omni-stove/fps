using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="ImpactDecals"/> が銃痕を面に沿って浮かせて貼り、キャラには貼らず、上限を超えたら古いものから剥がしてログに出すことを確かめる。
    /// </summary>
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
