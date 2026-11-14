using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class ShrinkingZoneTests
    {
        private readonly List<Object> _created = new List<Object>();
        private ShrinkingZone _zone;
        private List<Damageable> _targets;

        [SetUp]
        public void SetUp()
        {
            var zoneObject = new GameObject("Zone");
            _created.Add(zoneObject);
            _zone = zoneObject.AddComponent<ShrinkingZone>();
            _zone.ShowsBoundary = false;
            _zone.BeginsOnStart = false;
            _zone.UseRandomSeed = false;
            _zone.Seed = 1;
            _zone.InitialRadius = 20f;
            _zone.DamageIntervalSeconds = 1f;

            // 10 秒待ち、10 秒で半径 10 へ。外では毎秒 5。
            _zone.SetPhases(new[] { new ShrinkingZone.PhaseSettings(10f, 10f, 10f, 5f) });
            _targets = new List<Damageable>();
            _zone.SetTargets(_targets);
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
        }

        private Damageable Target(string id, Vector3 position)
        {
            var target = new GameObject(id);
            _created.Add(target);
            target.transform.position = position;
            var damageable = target.AddComponent<Damageable>();
            damageable.Initialize(id, 100);
            _targets.Add(damageable);
            return damageable;
        }

        [Test]
        public void DoesNothingUntilBegun()
        {
            var outside = Target("Outside", new Vector3(50f, 0f, 0f));

            _zone.Tick(5f);

            Assert.That(_zone.IsRunning, Is.False);
            Assert.That(outside.Health.Current, Is.EqualTo(100));
            Assert.That(_zone.HudLine.IsEmpty, Is.True);
            Assert.That(_zone.SecondsUntilShrink, Is.Zero);
        }

        [Test]
        public void DamagesOnlyThoseOutsideTheCircleEachInterval()
        {
            var inside = Target("Inside", new Vector3(5f, 0f, 0f));
            var outside = Target("Outside", new Vector3(30f, 100f, 0f));
            Assert.That(_zone.Begin(), Is.True);

            _zone.Tick(0.5f);
            Assert.That(outside.Health.Current, Is.EqualTo(100));

            LogAssert.Expect(LogType.Log, "[Zone] outside target=Outside");
            _zone.Tick(0.5f);
            Assert.That(outside.Health.Current, Is.EqualTo(95));
            Assert.That(inside.Health.Current, Is.EqualTo(100));
            Assert.That(_zone.Outside, Is.EquivalentTo(new[] { outside }));

            _zone.Tick(2f);
            Assert.That(outside.Health.Current, Is.EqualTo(85));
        }

        [Test]
        public void CarriesFractionalDamageOver()
        {
            _zone.SetPhases(new[] { new ShrinkingZone.PhaseSettings(10f, 10f, 10f, 0.4f) });
            var outside = Target("Outside", new Vector3(30f, 0f, 0f));
            _zone.Begin();

            _zone.Tick(2f);
            Assert.That(outside.Health.Current, Is.EqualTo(100));

            _zone.Tick(1f);
            Assert.That(outside.Health.Current, Is.EqualTo(99));
        }

        [Test]
        public void StopsDamagingAfterReturningInside()
        {
            var target = Target("Runner", new Vector3(30f, 0f, 0f));
            _zone.Begin();
            _zone.Tick(1f);
            Assert.That(target.Health.Current, Is.EqualTo(95));

            target.transform.position = Vector3.zero;
            LogAssert.Expect(LogType.Log, "[Zone] inside target=Runner");
            _zone.Tick(1f);

            Assert.That(target.Health.Current, Is.EqualTo(95));
            Assert.That(_zone.Outside, Is.Empty);
        }

        [Test]
        public void LogsAndReportsEachStageChange()
        {
            var stages = new List<ZoneStage>();
            _zone.StageChanged += state => stages.Add(state.Stage);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Zone\] start center=\(0\.00, 0\.00\) radius=20\.00 phases=1 seed=1 total=20\.0$"));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Zone\] phase=1/1 stage=Waiting radius=20\.00 "));
            _zone.Begin();
            Assert.That(_zone.SecondsUntilShrink, Is.EqualTo(10f));
            Assert.That(Localizer.Resolve(_zone.HudLine), Is.EqualTo("エリア縮小まで 00:10"));

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Zone\] phase=1/1 stage=Shrinking radius=17\.50 "));
            _zone.Tick(12.5f);
            Assert.That(_zone.State.Radius, Is.EqualTo(17.5f).Within(1e-4f));
            Assert.That(_zone.SecondsUntilShrink, Is.Zero);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Zone\] phase=1/1 stage=Closed radius=10\.00 "));
            _zone.Tick(10f);

            Assert.That(stages, Is.EqualTo(new[] { ZoneStage.Waiting, ZoneStage.Shrinking, ZoneStage.Closed }));
            Assert.That(Localizer.Resolve(_zone.HudLine), Is.EqualTo("最終エリア"));
        }

        [Test]
        public void SameSeedGivesTheSameCircles()
        {
            _zone.Begin(42);
            var first = _zone.Schedule.TargetCenter(0);
            _zone.Begin(42);

            Assert.That(_zone.Schedule.TargetCenter(0), Is.EqualTo(first));
            Assert.That(_zone.Schedule.Seed, Is.EqualTo(42));
        }

        [Test]
        public void RefusesToBeginWithInvalidPhases()
        {
            _zone.SetPhases(new[] { new ShrinkingZone.PhaseSettings(1f, 1f, 50f, 1f) });

            LogAssert.Expect(LogType.Error, new Regex(@"^\[Zone\] エリアを組み立てられません"));
            Assert.That(_zone.Begin(), Is.False);
            Assert.That(_zone.IsRunning, Is.False);

            _zone.SetPhases(new ShrinkingZone.PhaseSettings[0]);
            LogAssert.Expect(LogType.Error, new Regex(@"^\[Zone\] エリアを組み立てられません"));
            Assert.That(_zone.Begin(), Is.False);
        }

        [Test]
        public void StopEndsDamage()
        {
            var outside = Target("Outside", new Vector3(30f, 0f, 0f));
            _zone.Begin();
            LogAssert.Expect(LogType.Log, "[Zone] stop");
            _zone.Stop();

            _zone.Tick(5f);

            Assert.That(outside.Health.Current, Is.EqualTo(100));
            Assert.That(_zone.IsRunning, Is.False);
        }

        [Test]
        public void BoundaryFollowsTheCircle()
        {
            _zone.ShowsBoundary = true;
            _zone.Begin();

            var boundary = _zone.Boundary;
            Assert.That(boundary, Is.Not.Null);
            Assert.That(boundary.name, Is.EqualTo(ShrinkingZone.BoundaryName));
            Assert.That(boundary.TryGetComponent<Collider>(out _), Is.False);
            Assert.That(boundary.transform.localScale.x, Is.EqualTo(20f).Within(1e-4f));

            _zone.Tick(15f);

            var center = _zone.State.Center;
            Assert.That(boundary.transform.localScale.x, Is.EqualTo(15f).Within(1e-4f));
            Assert.That(boundary.transform.position.x, Is.EqualTo(center.X).Within(1e-4f));
            Assert.That(boundary.transform.position.z, Is.EqualTo(center.Y).Within(1e-4f));
            _created.Add(boundary.GetComponent<Renderer>().sharedMaterial);
            _created.Add(boundary.GetComponent<MeshFilter>().sharedMesh);
        }

        [Test]
        public void ShorterShrinkSecondsCloseTheCircleFaster()
        {
            _zone.Begin();
            _zone.Tick(12.5f);
            var slow = _zone.State.Radius;

            _zone.SetPhases(new[] { new ShrinkingZone.PhaseSettings(10f, 5f, 10f, 5f) });
            _zone.Begin();
            _zone.Tick(12.5f);

            Assert.That(slow, Is.EqualTo(17.5f).Within(1e-4f), "10 s to shrink: a quarter of the way after 2.5 s");
            Assert.That(_zone.State.Radius, Is.EqualTo(15f).Within(1e-4f), "5 s to shrink: half way after 2.5 s");
        }

        [Test]
        public void TheBoundaryHeightIsTheConfiguredValue()
        {
            Assert.That(_zone.BoundaryHeight, Is.EqualTo(ShrinkingZone.DefaultBoundaryHeight));
            _zone.ShowsBoundary = true;
            _zone.BoundaryHeight = 12f;

            _zone.Begin();

            var boundary = _zone.Boundary;
            Assert.That(boundary, Is.Not.Null);
            Assert.That(boundary.transform.localScale.y, Is.EqualTo(12f).Within(1e-4f));
            _created.Add(boundary.GetComponent<Renderer>().sharedMaterial);
            _created.Add(boundary.GetComponent<MeshFilter>().sharedMesh);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void TheBoundaryHeightRejectsUnusableValues(float value)
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _zone.BoundaryHeight = value);
            Assert.That(_zone.BoundaryHeight, Is.EqualTo(ShrinkingZone.DefaultBoundaryHeight));
        }

        [Test]
        public void TubeMeshIsOpenAndUnitSized()
        {
            var mesh = ShrinkingZone.BuildTubeMesh(8);
            _created.Add(mesh);

            Assert.That(mesh.vertexCount, Is.EqualTo(18));
            Assert.That(mesh.triangles.Length, Is.EqualTo(48));
            Assert.That(mesh.bounds.extents.x, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(mesh.bounds.extents.y, Is.EqualTo(0.5f).Within(1e-4f));
        }
    }
}
