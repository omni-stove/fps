using System.Collections.Generic;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="ShellEjector"/> が撃つたびに排莢口から薬莢を出し、最初の接地で落下音を 1 回だけ鳴らし、プールの上限と寿命を守ることを、
    /// メモリ上のカタログで確かめる (Edit Mode では OnEnable も物理も走らないので、Eject と Land を直接呼ぶ)。
    /// </summary>
    public sealed class ShellEjectorTests
    {
        private const string RifleId = "rifle-id";

        private readonly List<Object> _created = new List<Object>();
        private WeaponExtrasCatalog _catalog;
        private GameObject _shellPrefab;
        private AudioClip _landSound;

        [SetUp]
        public void SetUp()
        {
            _shellPrefab = Track(new GameObject("Shell"));
            _landSound = Clip("ShellDrop");
            _catalog = Track(ScriptableObject.CreateInstance<WeaponExtrasCatalog>());
            _catalog.SetWeapons(new[] { new WeaponExtrasAssets(RifleId, _shellPrefab, _landSound, Clip("DryFire")) });
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] == null)
                {
                    continue;
                }

                // Edit Mode では OnDestroy が走らないので、キャラの外に作ったプールの親はここで消す。
                if (_created[i] is GameObject body && body.TryGetComponent<ShellEjector>(out var ejector) && ejector.PoolRoot != null)
                {
                    Object.DestroyImmediate(ejector.PoolRoot.gameObject);
                }

                Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        [Test]
        public void AShotEjectsAShellFromTheDefaultPortToTheUpperRight()
        {
            var ejector = Ejector("Shooter");
            var expected = WeaponSockets.None.ShellEjectPose(Vector3.zero, Vector3.forward);

            LogAssert.Expect(LogType.Log, ShellEjector.FormatEjectLog(RifleId, expected.position));
            var shell = ejector.Eject(Shot(ejector.gameObject));

            Assert.That(shell, Is.Not.Null);
            Assert.That(shell.Root.activeSelf, Is.True);
            Assert.That(shell.Root.transform.parent, Is.SameAs(ejector.PoolRoot), "shells live outside the character");
            Assert.That(shell.Root.layer, Is.EqualTo(ShellEjector.ShellLayer), "shells do not block hitscan rays");
            Assert.That(Vector3.Distance(shell.Root.transform.position, expected.position), Is.LessThan(1e-4f));
            Assert.That(shell.Body, Is.Not.Null, "a Rigidbody is added when the prefab has none");
            Assert.That(shell.Colliders, Is.Not.Empty, "a collider is added when the prefab has none");
            Assert.That(shell.Root.GetComponent<ShellCasing>().Owner, Is.SameAs(ejector));
            Assert.That(Vector3.Dot(shell.Body.linearVelocity, Vector3.right), Is.GreaterThan(0f));
            Assert.That(shell.Body.linearVelocity.y, Is.GreaterThan(0f));
            Assert.That(shell.Landed, Is.False);
            Assert.That(_shellPrefab.GetComponent<Rigidbody>(), Is.Null, "the prefab is left as it is");
        }

        [Test]
        public void ShellsFlyFromTheWeaponModelsSocket()
        {
            var ejector = Ejector("Shooter");
            var presentation = ejector.gameObject.AddComponent<WeaponPresentation>();
            var model = Track(new GameObject("Model"));
            var socket = new GameObject(WeaponSockets.ShellEjectName);
            socket.transform.SetParent(model.transform, false);
            socket.transform.SetPositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f));
            presentation.WeaponModel = model.transform;

            LogAssert.Expect(LogType.Log, ShellEjector.FormatEjectLog(RifleId, socket.transform.position));
            var shell = ejector.Eject(Shot(ejector.gameObject));

            Assert.That(Vector3.Distance(shell.Root.transform.position, socket.transform.position), Is.LessThan(1e-4f));
        }

        [Test]
        public void TheFirstLandingPlaysTheSoundOnlyOnce()
        {
            var ejector = Ejector("Shooter");
            var shell = ejector.Eject(Shot(ejector.gameObject));

            LogAssert.Expect(LogType.Log, ShellEjector.FormatLandLog(RifleId, _landSound));
            Assert.That(ejector.Land(shell), Is.True);
            Assert.That(shell.Landed, Is.True);
            Assert.That(ejector.Land(shell), Is.False, "bouncing again stays quiet");
            Assert.That(ejector.Land(null), Is.False);

            ejector.Tick(Time.time + ShellEjector.ShellLifetimeSeconds + 1f);
            var again = ejector.Eject(Shot(ejector.gameObject));
            Assert.That(again, Is.SameAs(shell), "the hidden shell is reused");
            Assert.That(again.Landed, Is.False, "a reused shell lands again");
        }

        [Test]
        public void ShellsAreHiddenAfterTheirLifetime()
        {
            var ejector = Ejector("Shooter");
            var shell = ejector.Eject(Shot(ejector.gameObject));

            ejector.Tick(Time.time + ShellEjector.ShellLifetimeSeconds + 1f);

            Assert.That(ejector.PoolFor(_shellPrefab).ActiveCount, Is.Zero);
            Assert.That(shell.Root.activeSelf, Is.False);
            Assert.That(ejector.Land(shell), Is.False, "a hidden shell plays nothing");
        }

        [Test]
        public void ManyShotsStayWithinThePoolCapacity()
        {
            var ejector = Ejector("Shooter");

            for (var i = 0; i < 100; i++)
            {
                ejector.Eject(Shot(ejector.gameObject));
            }

            var pool = ejector.PoolFor(_shellPrefab);
            Assert.That(pool.Count, Is.EqualTo(ShellEjector.ShellCapacity));
            Assert.That(pool.ActiveCount, Is.EqualTo(ShellEjector.ShellCapacity));
            Assert.That(ejector.PoolRoot.childCount, Is.EqualTo(ShellEjector.ShellCapacity), "nothing is instantiated outside the pool");
        }

        [Test]
        public void APrefabWithItsOwnBodyKeepsIt()
        {
            var body = _shellPrefab.AddComponent<Rigidbody>();
            body.mass = 0.5f;
            _shellPrefab.AddComponent<CapsuleCollider>();
            var ejector = Ejector("Shooter");

            var shell = ejector.Eject(Shot(ejector.gameObject));

            Assert.That(shell.Root.GetComponents<Rigidbody>(), Has.Length.EqualTo(1));
            Assert.That(shell.Body.mass, Is.EqualTo(0.5f));
            Assert.That(shell.Root.GetComponents<Collider>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void AMissingCatalogOrShellEjectsNothing()
        {
            var ejector = Ejector("Shooter");
            ejector.Catalog = null;
            Assert.That(ejector.Eject(Shot(ejector.gameObject)), Is.Null);

            ejector.Catalog = _catalog;
            Assert.That(ejector.Eject(new ShotFiredInfo(ejector.gameObject, Vector3.zero, Vector3.forward, "unknown")), Is.Null);

            var empty = Track(ScriptableObject.CreateInstance<WeaponExtrasCatalog>());
            empty.SetWeapons(new[] { new WeaponExtrasAssets(RifleId, null, _landSound, null) });
            ejector.Catalog = empty;
            Assert.That(ejector.Eject(Shot(ejector.gameObject)), Is.Null);

            Assert.That(ejector.PoolRoot, Is.Null, "nothing was created");
        }

        [Test]
        public void FindForResolvesTheShootersBodyFromAChild()
        {
            var ejector = Ejector("Body");
            var child = Track(new GameObject("Eye"));
            child.transform.SetParent(ejector.transform, false);

            Assert.That(ShellEjector.FindFor(child), Is.SameAs(ejector));
            Assert.That(ShellEjector.FindFor(Track(new GameObject("Stranger"))), Is.Null);
            Assert.That(ShellEjector.FindFor(null), Is.Null);

            ejector.enabled = false;
            Assert.That(ShellEjector.FindFor(child), Is.Null, "a disabled ejector (dedicated server) ejects nothing");
        }

        [Test]
        public void EjectVelocityFollowsThePortAndGoesUp()
        {
            var velocity = ShellEjector.EjectVelocity(Quaternion.LookRotation(Vector3.right), Vector3.zero);

            Assert.That(velocity.x, Is.EqualTo(ShellEjector.EjectSpeed).Within(1e-4f));
            Assert.That(velocity.y, Is.EqualTo(ShellEjector.EjectUpSpeed).Within(1e-4f));
            Assert.That(velocity.z, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void LogsCarryTheTagWeaponAndSound()
        {
            Assert.That(ShellEjector.FormatEjectLog(RifleId, new Vector3(0.18f, -0.1f, 0.3f)),
                Is.EqualTo("[Presentation] shell eject weapon=rifle-id pos=(0.18,-0.10,0.30)"));
            Assert.That(ShellEjector.FormatLandLog(RifleId, _landSound), Is.EqualTo("[Presentation] shell land weapon=rifle-id sound=ShellDrop"));
            Assert.That(ShellEjector.FormatLandLog(RifleId, null), Is.EqualTo("[Presentation] shell land weapon=rifle-id sound=none"));
        }

        private ShellEjector Ejector(string bodyName)
        {
            var body = Track(new GameObject(bodyName));
            var ejector = body.AddComponent<ShellEjector>();
            ejector.Catalog = _catalog;
            return ejector;
        }

        private static ShotFiredInfo Shot(GameObject shooter)
        {
            return new ShotFiredInfo(shooter, Vector3.zero, Vector3.forward, RifleId);
        }

        private AudioClip Clip(string clipName)
        {
            return Track(AudioClip.Create(clipName, 4410, 1, 44100, false));
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
