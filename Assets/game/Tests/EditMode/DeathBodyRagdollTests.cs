using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class DeathBodyRagdollTests
    {
        private const string BodyId = "ragdoll-body";
        private const int MaxHealth = 50;

        private static readonly Vector3 Home = new Vector3(2f, 0f, 4f);

        private readonly List<Object> _spawned = new List<Object>();
        private GameObject _body;
        private Transform _hips;
        private Transform _torso;
        private Damageable _damageable;
        private DeathBodyPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject(BodyId);
            _body.transform.position = Home;
            _damageable = _body.AddComponent<Damageable>();
            _damageable.Initialize(BodyId, MaxHealth);

            _hips = new GameObject("Hips").transform;
            _hips.SetParent(_body.transform, false);
            _hips.localPosition = new Vector3(0f, 1f, 0f);
            AddCube("PelvisMesh", _hips, Vector3.zero, new Vector3(0.3f, 0.2f, 0.2f));

            _torso = new GameObject("Torso").transform;
            _torso.SetParent(_hips, false);
            _torso.localPosition = new Vector3(0f, 0.1f, 0f);
            AddCube("TorsoMesh", _torso, new Vector3(0f, 0.3f, 0f), new Vector3(0.4f, 0.6f, 0.2f));

            _presenter = DeathBodyPresenter.Attach(_body);
        }

        [TearDown]
        public void TearDown()
        {
            var shell = _presenter != null ? _presenter.Shell : null;
            if (_body != null)
            {
                Object.DestroyImmediate(_body);
            }

            if (shell != null)
            {
                Object.DestroyImmediate(shell);
            }

            foreach (var spawned in _spawned)
            {
                if (spawned != null)
                {
                    Object.DestroyImmediate(spawned);
                }
            }

            _spawned.Clear();
        }

        [Test]
        public void ACatalogBuildsOneBodyPerBoneJoinedToItsParent()
        {
            var catalog = NewCatalog();
            _presenter.RagdollCatalog = catalog;
            _presenter.Bind(_damageable, DeathBody.Collapse);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathBody\] collapse id=ragdoll-body ragdoll bones=2 parts=2 push=\(-?0,-1\) speed=1 damage=0 settle=2s lifetime=5s$"));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathBody\] push id=ragdoll-body ragdoll push=\(-?0,-1\) speed=6 damage=50$"));

            _damageable.Kill();

            Assert.That(_presenter.IsRagdoll, Is.True);
            var shell = _presenter.Shell;
            Assert.That(shell.transform.parent, Is.Null);
            Assert.That(shell.GetComponent<Rigidbody>(), Is.Null, "the root does not move as one piece");

            var hips = shell.transform.Find("Hips");
            var torso = shell.transform.Find("Hips/Torso");
            Assert.That(hips, Is.Not.Null);
            Assert.That(torso, Is.Not.Null);
            Assert.That(Vector3.Distance(hips.position, _hips.position), Is.LessThan(1e-5f), "bones start where the body died");
            Assert.That(Vector3.Distance(torso.position, _torso.position), Is.LessThan(1e-5f));

            var hipsBody = hips.GetComponent<Rigidbody>();
            var torsoBody = torso.GetComponent<Rigidbody>();
            Assert.That(hipsBody.mass, Is.EqualTo(10f));
            Assert.That(torsoBody.mass, Is.EqualTo(15f));
            Assert.That(hips.GetComponent<BoxCollider>(), Is.Not.Null);
            var capsule = torso.GetComponent<CapsuleCollider>();
            Assert.That(capsule, Is.Not.Null);
            Assert.That(capsule.direction, Is.EqualTo(1));
            Assert.That(capsule.height, Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(capsule.radius, Is.EqualTo(0.2f).Within(1e-5f));

            Assert.That(hips.GetComponent<CharacterJoint>(), Is.Null, "the root bone has nothing to hang from");
            var joint = torso.GetComponent<CharacterJoint>();
            Assert.That(joint, Is.Not.Null);
            Assert.That(joint.connectedBody, Is.SameAs(hipsBody));
            Assert.That(joint.axis, Is.EqualTo(Vector3.up));
            Assert.That(joint.swingAxis, Is.EqualTo(Vector3.right));
            Assert.That(joint.lowTwistLimit.limit, Is.EqualTo(-20f));
            Assert.That(joint.highTwistLimit.limit, Is.EqualTo(25f));
            Assert.That(joint.swing1Limit.limit, Is.EqualTo(30f));
            Assert.That(joint.swing2Limit.limit, Is.EqualTo(15f));

            Assert.That(shell.GetComponentsInChildren<MeshRenderer>(true), Has.Length.EqualTo(2));
            Assert.That(shell.transform.Find("Hips/Torso/TorsoMesh").GetComponent<MeshRenderer>(), Is.Not.Null, "parts follow their bone");
            foreach (var part in shell.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(part.gameObject.layer, Is.EqualTo(DeathBodyPresenter.ShellLayer), $"{part.name} is on the shell layer");
            }

            Assert.That(shell.GetComponentsInChildren<Damageable>(true), Is.Empty);
            Assert.That(_presenter.Ragdoll.Bodies, Has.Count.EqualTo(2));
            Assert.That(_presenter.Ragdoll.SettleSeconds, Is.EqualTo(catalog.SettleSeconds));
        }

        [Test]
        public void TheKillingShotPushesTheUpperBodyHarderAwayFromTheShooter()
        {
            _presenter.RagdollCatalog = NewCatalog();
            _presenter.Bind(_damageable, DeathBody.Collapse);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathBody\] collapse id=ragdoll-body ragdoll "));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathBody\] push id=ragdoll-body ragdoll push=\(1,0\) speed=6 damage=50$"));

            _damageable.TakeDamage(MaxHealth, Home + new Vector3(-5f, 1.5f, 0f));

            var hips = _presenter.Shell.transform.Find("Hips").GetComponent<Rigidbody>();
            var torso = _presenter.Shell.transform.Find("Hips/Torso").GetComponent<Rigidbody>();
            Assert.That(torso.linearVelocity.normalized.x, Is.EqualTo(1f).Within(1e-3f), "falls away from the shooter");
            Assert.That(torso.linearVelocity.y, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(torso.linearVelocity.magnitude, Is.EqualTo(6f).Within(1e-3f), "base 1 + 0.1 per damage");
            Assert.That(hips.linearVelocity.magnitude, Is.EqualTo(6f * _presenter.RagdollCatalog.FootSpeedRatio).Within(1e-3f), "the lowest bone is pushed less so the body topples");
        }

        [Test]
        public void HarderHitsPushFasterUpToTheCap()
        {
            var catalog = NewCatalog();

            Assert.That(catalog.SpeedFor(0), Is.EqualTo(1f));
            Assert.That(catalog.SpeedFor(20), Is.EqualTo(3f).Within(1e-5f));
            Assert.That(catalog.SpeedFor(-30), Is.EqualTo(1f), "negative damage does not pull the body");
            Assert.That(catalog.SpeedFor(10000), Is.EqualTo(catalog.MaxSpeed));
        }

        [Test]
        public void TheRagdollFreezesAfterItsSettleTime()
        {
            _presenter.RagdollCatalog = NewCatalog();
            _presenter.Mode = DeathBody.Collapse;
            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathBody\] collapse id=ragdoll-body ragdoll "));
            _presenter.Present(Vector3.forward, 10);
            var ragdoll = _presenter.Ragdoll;

            Assert.That(ragdoll.Tick(1.9f), Is.False);
            Assert.That(ragdoll.IsFrozen, Is.False);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathBody\] ragdoll frozen id=ragdoll-body bones=2 after=2\.1s$"));
            Assert.That(ragdoll.Tick(0.2f), Is.True);

            Assert.That(ragdoll.IsFrozen, Is.True);
            foreach (var body in ragdoll.Bodies)
            {
                Assert.That(body.isKinematic, Is.True, $"{body.name} stops moving");
            }

            Assert.That(ragdoll.Tick(1f), Is.False, "freezes only once");
            Assert.That(ragdoll.Push(Vector3.right, 3f), Is.False, "a frozen body is not pushed again");
            Assert.That(_presenter.HasShell, Is.True, "freezing keeps the body until its lifetime ends");
        }

        [Test]
        public void ClearRemovesTheRagdoll()
        {
            _presenter.RagdollCatalog = NewCatalog();
            _presenter.Mode = DeathBody.Collapse;
            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathBody\] collapse id=ragdoll-body ragdoll "));
            var shell = _presenter.Present(null);

            LogAssert.Expect(LogType.Log, "[DeathBody] removed id=ragdoll-body reason=respawn");
            Assert.That(_presenter.Clear(DeathBodyPresenter.ReasonRespawn), Is.True);

            Assert.That(shell == null, Is.True);
            Assert.That(_presenter.IsRagdoll, Is.False);
            Assert.That(_presenter.Ragdoll, Is.Null);
        }

        [Test]
        public void AMissingBoneFallsBackToOnePiece()
        {
            var catalog = NewCatalog(Bone("Hips", RagdollShape.Box, 10f, -20f, 25f), Bone("Spine", RagdollShape.Box, 5f, -20f, 25f));
            _presenter.RagdollCatalog = catalog;
            _presenter.Mode = DeathBody.Collapse;
            LogAssert.Expect(LogType.Log, "[DeathBody] ragdoll skipped id=ragdoll-body reason=bone 'Spine' not found under 'ragdoll-body'");
            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathBody\] collapse id=ragdoll-body parts=2 push="));

            var shell = _presenter.Present(Vector3.left);

            Assert.That(shell, Is.Not.Null);
            Assert.That(_presenter.IsRagdoll, Is.False);
            Assert.That(shell.GetComponent<Rigidbody>(), Is.Not.Null);
            Assert.That(shell.GetComponentsInChildren<Rigidbody>(true), Has.Length.EqualTo(1));
            Assert.That(shell.GetComponentsInChildren<CharacterJoint>(true), Is.Empty);
        }

        [Test]
        public void AnInvalidCatalogWarnsAndFallsBackToOnePiece()
        {
            var catalog = NewCatalog(Bone("Hips", RagdollShape.Box, 10f, -20f, 25f), Bone("Torso", RagdollShape.Box, -1f, 30f, 10f));
            _presenter.RagdollCatalog = catalog;
            _presenter.Mode = DeathBody.Collapse;
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[DeathBody\] ragdoll skipped id=ragdoll-body reason=invalid catalog: .*mass must be positive.*low twist is greater than high twist"));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathBody\] collapse id=ragdoll-body parts=2 "));

            Assert.That(_presenter.Present(null), Is.Not.Null);
            Assert.That(_presenter.IsRagdoll, Is.False);
        }

        [Test]
        public void TurningTheRagdollOffAlwaysCollapsesAsOnePiece()
        {
            _presenter.RagdollCatalog = NewCatalog();
            _presenter.UseRagdoll = false;
            _presenter.Mode = DeathBody.Collapse;
            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathBody\] collapse id=ragdoll-body parts=2 "));

            _presenter.Present(null);

            Assert.That(_presenter.IsRagdoll, Is.False);
        }

        [Test]
        public void SkinnedPartsBendWithTheCopiedBones()
        {
            var skin = new GameObject("Skin");
            skin.transform.SetParent(_body.transform, false);
            var skinned = skin.AddComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(0f, 1f, 0f), new Vector3(0.1f, 1.2f, 0f), new Vector3(0f, 1.4f, 0f) },
                triangles = new[] { 0, 1, 2 },
                boneWeights = new[]
                {
                    new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                    new BoneWeight { boneIndex0 = 0, weight0 = 0.5f, boneIndex1 = 1, weight1 = 0.5f },
                    new BoneWeight { boneIndex0 = 1, weight0 = 1f },
                },
                bindposes = new[] { _hips.worldToLocalMatrix * _body.transform.localToWorldMatrix, _torso.worldToLocalMatrix * _body.transform.localToWorldMatrix },
            };
            _spawned.Add(mesh);
            skinned.sharedMesh = mesh;
            skinned.bones = new[] { _hips, _torso };
            skinned.rootBone = _hips;

            _presenter.RagdollCatalog = NewCatalog();
            _presenter.Mode = DeathBody.Collapse;
            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathBody\] collapse id=ragdoll-body ragdoll bones=2 parts=3 "));
            var shell = _presenter.Present(null);

            var copy = shell.transform.Find("Skin").GetComponent<SkinnedMeshRenderer>();
            Assert.That(copy, Is.Not.Null, "the skin is not baked into a rigid mesh");
            Assert.That(copy.sharedMesh, Is.SameAs(mesh));
            Assert.That(copy.bones, Has.Length.EqualTo(2));
            Assert.That(copy.bones[0], Is.SameAs(shell.transform.Find("Hips")));
            Assert.That(copy.bones[1], Is.SameAs(shell.transform.Find("Hips/Torso")));
            Assert.That(copy.rootBone, Is.SameAs(shell.transform.Find("Hips")));
        }

        [Test]
        public void ValidateListsEveryProblem()
        {
            var catalog = NewCatalog(
                Bone("Hips", RagdollShape.Box, 10f, -20f, 25f),
                Bone("Hips", RagdollShape.Box, 10f, -200f, 25f),
                new RagdollBone("Torso", RagdollShape.Capsule, Vector3.zero, new Vector3(0.1f, 0f, 0.1f), 1f, Vector3.up, Vector3.up, 0f, 0f, 200f, 0f));
            catalog.SettleSeconds = 0f;
            catalog.BaseSpeed = 5f;
            catalog.MaxSpeed = 1f;

            var error = Assert.Throws<RagdollCatalogException>(catalog.Validate);

            Assert.That(error.Message, Does.Contain("uses a bone name already used"));
            Assert.That(error.Message, Does.Contain("twist limits must be within"));
            Assert.That(error.Message, Does.Contain("size must be positive"));
            Assert.That(error.Message, Does.Contain("must not be parallel"));
            Assert.That(error.Message, Does.Contain("swing limits must be within"));
            Assert.That(error.Message, Does.Contain("settle seconds must be positive"));
            Assert.That(error.Message, Does.Contain("max speed is smaller than base speed"));

            catalog.SetBones(null);
            Assert.That(Assert.Throws<RagdollCatalogException>(catalog.Validate).Message, Does.Contain("has no bones"));
        }

        [Test]
        public void TheCatalogLivesAtThePresentationConvention()
        {
            Assert.That(RagdollCatalog.ResourcePath, Is.EqualTo(PresentationCatalog.CatalogResourcePath(RagdollCatalog.CatalogName)));
        }

        [Test]
        public void DeathRagdollRejectsInvalidInput()
        {
            var ragdoll = new GameObject("ragdoll").AddComponent<DeathRagdoll>();
            _spawned.Add(ragdoll.gameObject);

            Assert.Throws<ArgumentNullException>(() => ragdoll.Initialize("x", null, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => ragdoll.Initialize("x", Array.Empty<Rigidbody>(), 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => ragdoll.Initialize("x", Array.Empty<Rigidbody>(), float.PositiveInfinity));
            ragdoll.Initialize("x", Array.Empty<Rigidbody>(), 1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => ragdoll.Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => ragdoll.Push(Vector3.right, float.NaN));
        }

        private RagdollCatalog NewCatalog(params RagdollBone[] bones)
        {
            var catalog = ScriptableObject.CreateInstance<RagdollCatalog>();
            _spawned.Add(catalog);
            catalog.SetBones(bones.Length > 0
                ? bones
                : new[]
                {
                    Bone("Hips", RagdollShape.Box, 10f, 0f, 0f),
                    Bone("Torso", RagdollShape.Capsule, 15f, -20f, 25f),
                });
            catalog.SettleSeconds = 2f;
            catalog.BaseSpeed = 1f;
            catalog.SpeedPerDamage = 0.1f;
            catalog.MaxSpeed = 10f;
            return catalog;
        }

        private static RagdollBone Bone(string name, RagdollShape shape, float mass, float lowTwist, float highTwist)
        {
            return new RagdollBone(
                name, shape, new Vector3(0f, 0.3f, 0f), new Vector3(0.4f, 0.6f, 0.4f), mass, Vector3.up, Vector3.right, lowTwist, highTwist, 30f, 15f);
        }

        private static void AddCube(string name, Transform parent, Vector3 localPosition, Vector3 localScale)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localScale = localScale;
        }
    }
}
