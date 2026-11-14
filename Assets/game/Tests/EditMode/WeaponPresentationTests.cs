using System;
using System.Collections.Generic;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="WeaponPresentation"/> が射撃・着弾・リロードで音とエフェクトを出し、プールの上限を超えて作らず使い回すことを、
    /// メモリ上のカタログで確かめる (Edit Mode では OnEnable が走らないので、PlayShot などを直接呼ぶ)。
    /// </summary>
    public sealed class WeaponPresentationTests
    {
        private const string RifleId = "rifle-id";

        private readonly List<Object> _created = new List<Object>();
        private PresentationCatalog _catalog;
        private GameObject _flash;
        private GameObject _spark;

        [SetUp]
        public void SetUp()
        {
            _flash = Effect("RifleFlash");
            _spark = Effect("Spark");
            var tracer = Effect("Tracer");
            tracer.GetComponent<ParticleSystemRenderer>().sharedMaterial = Track(new Material(Shader.Find("Hidden/InternalErrorShader")));

            _catalog = Track(ScriptableObject.CreateInstance<PresentationCatalog>());
            _catalog.SetWeapons(new[] { new WeaponPresentationAssets(RifleId, Clip("Fire"), Clip("Reload"), _flash) });
            _catalog.ImpactSound = Clip("Impact");
            _catalog.ImpactSpark = _spark;
            _catalog.Tracer = tracer;
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
                if (_created[i] is GameObject body && body.TryGetComponent<WeaponPresentation>(out var presenter) && presenter.PoolRoot != null)
                {
                    Object.DestroyImmediate(presenter.PoolRoot.gameObject);
                }

                Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        [Test]
        public void AShotPlaysTheMuzzleFlashTracerAndSound()
        {
            var presenter = Presenter("Shooter");

            presenter.PlayShot(Shot(presenter.gameObject));

            var flashes = presenter.EffectPoolFor(_flash);
            Assert.That(flashes, Is.Not.Null);
            Assert.That(flashes.ActiveCount, Is.EqualTo(1));
            Assert.That(flashes.Items[0].Root.activeSelf, Is.True);
            AssertNear(flashes.Items[0].Root.transform.position, WeaponPresentation.MuzzlePosition(Vector3.zero, Vector3.forward));
            Assert.That(presenter.Tracers.ActiveCount, Is.EqualTo(1));
            Assert.That(presenter.Tracers.Items[0].Line.gameObject.activeSelf, Is.True);
            Assert.That(presenter.BodySource, Is.Not.Null, "the shooter's own gunshot plays from its body");
        }

        [Test]
        public void ThePooledFlashDoesNotDestroyItselfWhenItStops()
        {
            var presenter = Presenter("Shooter");

            presenter.PlayShot(Shot(presenter.gameObject));

            foreach (var system in presenter.EffectPoolFor(_flash).Items[0].Systems)
            {
                Assert.That(system.main.stopAction, Is.EqualTo(ParticleSystemStopAction.None));
            }

            Assert.That(_flash.GetComponent<ParticleSystem>().main.stopAction, Is.EqualTo(ParticleSystemStopAction.Destroy), "the prefab is left as it is");
        }

        [Test]
        public void ManyShotsStayWithinThePoolCapacities()
        {
            var presenter = Presenter("Shooter");

            for (var i = 0; i < 100; i++)
            {
                presenter.PlayShot(Shot(null));
                presenter.PlayImpact(new ImpactInfo(new Vector3(0f, 0f, 20f), Vector3.back, null));
            }

            var flashes = presenter.EffectPoolFor(_flash);
            var sparks = presenter.EffectPoolFor(_spark);
            Assert.That(flashes.Count, Is.EqualTo(WeaponPresentation.MuzzleFlashCapacity));
            Assert.That(sparks.Count, Is.EqualTo(WeaponPresentation.ImpactSparkCapacity));
            Assert.That(presenter.Tracers.Count, Is.EqualTo(WeaponPresentation.TracerCapacity));
            Assert.That(presenter.Sounds.Count, Is.EqualTo(WeaponPresentation.SoundCapacity));
            Assert.That(
                presenter.PoolRoot.childCount,
                Is.EqualTo(flashes.Count + sparks.Count + presenter.Tracers.Count + presenter.Sounds.Count),
                "nothing is instantiated outside the pools");
        }

        [Test]
        public void FinishedEffectsAreHiddenAndReused()
        {
            var presenter = Presenter("Shooter");
            presenter.PlayShot(Shot(presenter.gameObject));
            var flash = presenter.EffectPoolFor(_flash).Items[0];

            presenter.Tick(Time.time + 1000f);

            Assert.That(presenter.EffectPoolFor(_flash).ActiveCount, Is.Zero);
            Assert.That(flash.Root.activeSelf, Is.False);
            Assert.That(presenter.Tracers.ActiveCount, Is.Zero);
            Assert.That(presenter.Tracers.Items[0].Line.gameObject.activeSelf, Is.False);

            presenter.PlayShot(Shot(presenter.gameObject));

            Assert.That(presenter.EffectPoolFor(_flash).Count, Is.EqualTo(1));
            Assert.That(presenter.EffectPoolFor(_flash).Items[0], Is.SameAs(flash));
            Assert.That(flash.Root.activeSelf, Is.True);
            Assert.That(presenter.Tracers.Count, Is.EqualTo(1));
        }

        [Test]
        public void TheLocalPlayersGunshotAndReloadAre2D()
        {
            var presenter = Presenter("Player");
            presenter.IsLocal = true;

            presenter.PlayShot(Shot(presenter.gameObject));

            Assert.That(presenter.BodySource.spatialBlend, Is.EqualTo(0f));
            Assert.That(presenter.Sounds, Is.Null, "no positioned source is needed for the player's own gunshot");

            presenter.PlayReload(RifleId);
            Assert.That(presenter.BodySource.spatialBlend, Is.EqualTo(0f));
        }

        [Test]
        public void AnotherCharactersGunshotIs3DFromItsBody()
        {
            var presenter = Presenter("Enemy");
            presenter.IsLocal = false;

            presenter.PlayShot(Shot(presenter.gameObject));

            Assert.That(presenter.BodySource.spatialBlend, Is.EqualTo(1f));
            Assert.That(presenter.BodySource.rolloffMode, Is.EqualTo(AudioRolloffMode.Logarithmic));
            Assert.That(presenter.BodySource.maxDistance, Is.EqualTo(WeaponPresentation.SoundMaxDistance));
        }

        [Test]
        public void AShotWithoutAKnownShooterIs3DAtItsOrigin()
        {
            var presenter = Presenter("Fallback");
            var origin = new Vector3(5f, 1f, -3f);

            presenter.PlayShot(new ShotFiredInfo(null, origin, Vector3.forward, RifleId));

            Assert.That(presenter.BodySource, Is.Null);
            Assert.That(presenter.Sounds.ActiveCount, Is.EqualTo(1));
            var source = presenter.Sounds.Items[0];
            AssertNear(source.transform.position, origin);
            Assert.That(source.spatialBlend, Is.EqualTo(1f));
            Assert.That(source.clip, Is.SameAs(_catalog.Weapons[0].Fire));
        }

        [Test]
        public void AnImpactPlaysASparkAnd3DSoundAtThePoint()
        {
            var presenter = Presenter("Shooter");
            var point = new Vector3(2f, 0f, 10f);

            presenter.PlayImpact(new ImpactInfo(point, Vector3.up, null));

            var spark = presenter.EffectPoolFor(_spark).Items[0];
            AssertNear(spark.Root.transform.position, point);
            AssertNear(spark.Root.transform.forward, Vector3.up, "the spark faces away from the surface");
            AssertNear(presenter.Sounds.Items[0].transform.position, point);
            Assert.That(presenter.Sounds.Items[0].clip, Is.SameAs(_catalog.ImpactSound));
            Assert.That(presenter.Sounds.Items[0].spatialBlend, Is.EqualTo(1f));
        }

        [Test]
        public void AnImpactRightAfterAShotStopsItsTracerAtThePoint()
        {
            var presenter = Presenter("Shooter");
            var point = new Vector3(0f, 0f, 12f);

            presenter.PlayShot(Shot(presenter.gameObject));
            presenter.PlayImpact(new ImpactInfo(point, Vector3.back, null));

            var tracer = presenter.Tracers.Items[0];
            Assert.That(tracer.To, Is.EqualTo(point));

            presenter.Tick(tracer.StartTime + WeaponPresentation.TracerSeconds(tracer.From, point) + 0.01f);
            Assert.That(presenter.Tracers.ActiveCount, Is.Zero, "the tracer ends at the impact, not at the full distance");
        }

        [Test]
        public void AnUnknownWeaponStillDrawsTheTracerButNoFlash()
        {
            var presenter = Presenter("Shooter");

            presenter.PlayShot(new ShotFiredInfo(presenter.gameObject, Vector3.zero, Vector3.forward, "unknown-" + Guid.NewGuid().ToString("N")));

            Assert.That(presenter.EffectPoolFor(_flash), Is.Null);
            Assert.That(presenter.BodySource, Is.Null);
            Assert.That(presenter.Tracers.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        public void WithoutACatalogNothingIsCreated()
        {
            var presenter = Presenter("Shooter");
            presenter.Catalog = null;

            presenter.PlayShot(Shot(presenter.gameObject));
            presenter.PlayImpact(new ImpactInfo(Vector3.forward, Vector3.back, null));
            presenter.PlayReload(RifleId);

            Assert.That(presenter.PoolRoot, Is.Null);
            Assert.That(presenter.BodySource, Is.Null);
        }

        [Test]
        public void TracerSegmentMovesAShortLineAlongThePathAndStopsAtTheEnd()
        {
            var from = Vector3.zero;
            var to = new Vector3(0f, 0f, 10f);
            var speed = WeaponPresentation.TracerSpeed;

            Assert.That(WeaponPresentation.TracerSegment(from, to, 0f, out var tail, out var head), Is.True);
            Assert.That(head, Is.EqualTo(from));
            Assert.That(tail, Is.EqualTo(from));

            Assert.That(WeaponPresentation.TracerSegment(from, to, 5f / speed, out tail, out head), Is.True);
            Assert.That(head.z, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(tail.z, Is.EqualTo(5f - WeaponPresentation.TracerLength).Within(1e-4f));

            Assert.That(WeaponPresentation.TracerSegment(from, to, 11f / speed, out tail, out head), Is.True);
            Assert.That(head.z, Is.EqualTo(10f).Within(1e-4f), "the head does not pass the end");

            Assert.That(WeaponPresentation.TracerSegment(from, to, (10f + WeaponPresentation.TracerLength) / speed, out _, out _), Is.False);
        }

        [Test]
        public void MuzzleSitsToTheRightBelowAndAheadOfTheEye()
        {
            var muzzle = WeaponPresentation.MuzzlePosition(Vector3.zero, Vector3.forward);

            Assert.That(muzzle.x, Is.EqualTo(WeaponPresentation.MuzzleOffset.x).Within(1e-5f));
            Assert.That(muzzle.y, Is.EqualTo(WeaponPresentation.MuzzleOffset.y).Within(1e-5f));
            Assert.That(muzzle.z, Is.EqualTo(WeaponPresentation.MuzzleOffset.z).Within(1e-5f));

            var straightUp = WeaponPresentation.MuzzlePosition(Vector3.zero, Vector3.up);
            AssertNear(straightUp, Vector3.up * WeaponPresentation.MuzzleOffset.z);
        }

        [Test]
        public void OwnShotUsesTheMuzzleSocketOfTheWeaponModel()
        {
            var presenter = Presenter("Shooter");
            var model = Track(new GameObject("RifleModel"));
            var socket = Track(new GameObject(WeaponSockets.MuzzleName));
            socket.transform.SetParent(model.transform, false);
            socket.transform.SetPositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f));
            presenter.WeaponModel = model.transform;

            presenter.PlayShot(Shot(presenter.gameObject));

            Assert.That(presenter.Sockets.Muzzle, Is.SameAs(socket.transform));
            var flash = presenter.EffectPoolFor(_flash).Items[0].Root.transform;
            AssertNear(flash.position, socket.transform.position);
            Assert.That(Quaternion.Angle(flash.rotation, socket.transform.rotation), Is.LessThan(0.01f));
            AssertNear(presenter.Tracers.Items[0].From, socket.transform.position, "the tracer starts at the socket");
        }

        [Test]
        public void OthersShotsAndModelsWithoutASocketUseTheDefaultMuzzle()
        {
            var presenter = Presenter("Shooter");
            var model = Track(new GameObject("RifleModel"));
            presenter.WeaponModel = model.transform;

            presenter.PlayShot(Shot(presenter.gameObject));

            Assert.That(presenter.Sockets.Muzzle, Is.Null);
            AssertNear(presenter.EffectPoolFor(_flash).Items[0].Root.transform.position, WeaponPresentation.MuzzlePosition(Vector3.zero, Vector3.forward));

            var socket = Track(new GameObject(WeaponSockets.MuzzleName));
            socket.transform.SetParent(model.transform, false);
            socket.transform.position = new Vector3(5f, 5f, 5f);
            presenter.WeaponModel = model.transform;
            presenter.Tick(Time.time + 1000f);

            presenter.PlayShot(Shot(null));

            AssertNear(
                presenter.EffectPoolFor(_flash).Items[0].Root.transform.position,
                WeaponPresentation.MuzzlePosition(Vector3.zero, Vector3.forward),
                "a shot whose shooter is unknown is not drawn from this body's weapon");
        }

        [Test]
        public void ClearingTheWeaponModelDropsTheSockets()
        {
            var presenter = Presenter("Shooter");
            var model = Track(new GameObject("RifleModel"));
            presenter.WeaponModel = model.transform;

            presenter.WeaponModel = null;

            Assert.That(presenter.WeaponModel, Is.Null);
            Assert.That(presenter.Sockets, Is.SameAs(WeaponSockets.None));
        }

        [Test]
        public void AnImpactLogsItsMaterialAndHitZone()
        {
            var presenter = Presenter("Shooter");

            LogAssert.Expect(LogType.Log, "[Presentation] impact material=default character=False zone=none");
            presenter.PlayImpact(new ImpactInfo(new Vector3(0f, 0f, 20f), Vector3.back, null));

            LogAssert.Expect(LogType.Log, "[Presentation] impact material=flesh character=True zone=Head");
            presenter.PlayImpact(new ImpactInfo(new Vector3(0f, 0f, 20f), Vector3.back, null, "flesh", true, Fps.Core.HitZone.Head));
        }

        [Test]
        public void FindForResolvesTheShootersBodyFromAChild()
        {
            var presenter = Presenter("Body");
            var child = Track(new GameObject("Eye"));
            child.transform.SetParent(presenter.transform, false);

            Assert.That(WeaponPresentation.FindFor(child), Is.SameAs(presenter));
            Assert.That(WeaponPresentation.FindFor(Track(new GameObject("Stranger"))), Is.Null);
            Assert.That(WeaponPresentation.FindFor(null), Is.Null);

            presenter.enabled = false;
            Assert.That(WeaponPresentation.FindFor(child), Is.Null, "a disabled presentation (dedicated server) plays nothing");
        }

        private WeaponPresentation Presenter(string bodyName)
        {
            var body = Track(new GameObject(bodyName));
            var presenter = body.AddComponent<WeaponPresentation>();
            presenter.Catalog = _catalog;
            return presenter;
        }

        private static void AssertNear(Vector3 actual, Vector3 expected, string message = null)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), message ?? $"expected {expected} but was {actual}");
        }

        private static ShotFiredInfo Shot(GameObject shooter)
        {
            return new ShotFiredInfo(shooter, Vector3.zero, Vector3.forward, RifleId);
        }

        private GameObject Effect(string effectName)
        {
            var effect = Track(new GameObject(effectName));
            var main = effect.AddComponent<ParticleSystem>().main;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.Destroy;
            return effect;
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
