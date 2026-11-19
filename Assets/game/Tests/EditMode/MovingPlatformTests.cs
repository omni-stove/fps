using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class MovingPlatformTests
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
        public void TheDefaultsComeFromTheCoreSpec()
        {
            var fresh = ScriptableObject.CreateInstance<MovingPlatformSettings>();
            _created.Add(fresh);

            Assert.That(MovingPlatformSettings.Resolve(null), Is.SameAs(MovingPlatformSpec.Default));
            Assert.That(fresh.Kind, Is.EqualTo(MovingPlatformSpec.DefaultKind));
            Assert.That(fresh.Speed, Is.EqualTo(MovingPlatformSpec.DefaultSpeed));
            Assert.That(fresh.WaitSeconds, Is.EqualTo(MovingPlatformSpec.DefaultWaitSeconds));
            Assert.That(fresh.SpinDegreesPerSecond, Is.EqualTo(MovingPlatformSpec.DefaultSpinDegreesPerSecond));
            Assert.That(fresh.DepartWhenBoarded, Is.EqualTo(MovingPlatformSpec.DefaultDepartWhenBoarded));
        }

        [Test]
        public void SteppingMovesAlongTheStopsFromWhereItWasPlacedAndReportsTheMotion()
        {
            var platform = NewPlatform(new Vector3(1f, 2f, 3f), Quaternion.identity);
            platform.Configure(Settings(PlatformRouteKind.PingPong, 2f, 0f), new[] { Vector3.zero, new Vector3(0f, 0f, 4f) });

            platform.Step(0.5f);

            Assert.That(platform.transform.position.x, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(platform.transform.position.y, Is.EqualTo(2f).Within(1e-5f));
            Assert.That(platform.transform.position.z, Is.EqualTo(4f).Within(1e-5f));
            Assert.That(platform.StepCount, Is.EqualTo(1));
            Assert.That(platform.LastStepSeconds, Is.EqualTo(0.5f));
            Assert.That(platform.LastMotion.Pivot.Z, Is.EqualTo(3f).Within(1e-5f));
            Assert.That(platform.LastMotion.Translation.Z, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(platform.LastMotion.YawDelta, Is.Zero);
        }

        [Test]
        public void TheStopsFollowTheRotationItWasPlacedWith()
        {
            var platform = NewPlatform(Vector3.zero, Quaternion.Euler(0f, 90f, 0f));
            platform.Configure(Settings(PlatformRouteKind.PingPong, 4f, 0f), new[] { Vector3.zero, new Vector3(0f, 0f, 4f) });

            platform.Step(1f);

            Assert.That(platform.transform.position.x, Is.EqualTo(4f).Within(1e-4f));
            Assert.That(platform.transform.position.z, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(platform.WorldPoint(1).x, Is.EqualTo(4f).Within(1e-4f));
        }

        [Test]
        public void ASpinningPlatformReportsTheTurnInRadians()
        {
            var settings = Settings(PlatformRouteKind.PingPong, 0f, 0f);
            settings.SpinDegreesPerSecond = 90f;
            var platform = NewPlatform(Vector3.zero, Quaternion.identity);
            platform.Configure(settings, new[] { Vector3.zero });

            platform.Step(0.5f);

            Assert.That(platform.LastMotion.YawDelta, Is.EqualTo(Mathf.PI / 4f).Within(1e-5f));
            Assert.That(platform.transform.eulerAngles.y, Is.EqualTo(45f).Within(1e-3f));
        }

        [Test]
        public void ItBecomesAKinematicBody()
        {
            var platform = NewPlatform(Vector3.zero, Quaternion.identity);
            platform.Configure(null, new[] { Vector3.zero });

            var body = platform.GetComponent<Rigidbody>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body.isKinematic, Is.True);
            Assert.That(body.useGravity, Is.False);
        }

        [Test]
        public void WithoutAColliderItBuildsADefaultSlab()
        {
            var root = new GameObject("BarePlatform");
            _created.Add(root);
            var platform = root.AddComponent<MovingPlatform>();
            LogAssert.Expect(LogType.Warning, MovingPlatformLog.BuiltDefaultShape("BarePlatform"));

            platform.Configure(null, new[] { Vector3.zero });

            var slab = root.transform.Find(MovingPlatform.SlabName);
            Assert.That(slab, Is.Not.Null);
            Assert.That(slab.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(slab.localScale, Is.EqualTo(MovingPlatform.DefaultSlabSize));
        }

        [Test]
        public void ASlabPrefabIsUsedForTheLookAndTheBoxIsAddedForTheFeet()
        {
            var prefab = new GameObject("SlabLook");
            _created.Add(prefab);
            var root = new GameObject("PrefabPlatform");
            _created.Add(root);
            var platform = root.AddComponent<MovingPlatform>();
            platform.SlabPrefab = prefab;
            platform.SlabSize = new Vector3(2f, 0.2f, 2f);
            LogAssert.Expect(LogType.Warning, MovingPlatformLog.BuiltDefaultShape("PrefabPlatform"));

            platform.Configure(null, new[] { Vector3.zero });

            Assert.That(root.transform.Find(MovingPlatform.SlabName), Is.Not.Null);
            Assert.That(root.GetComponent<BoxCollider>().size, Is.EqualTo(new Vector3(2f, 0.2f, 2f)));
        }

        [Test]
        public void BadSettingsAreReportedAndKeepThePlatformUninitialized()
        {
            var settings = Settings(PlatformRouteKind.PingPong, -1f, 0f);
            var platform = NewPlatform(Vector3.zero, Quaternion.identity);
            platform.Settings = settings;
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(MovingPlatformLog.LogTag) + ".*設定値が不正です"));

            Assert.That(platform.TryInitialize(), Is.False);
            Assert.That(platform.IsInitialized, Is.False);

            platform.Step(1f);
            Assert.That(platform.StepCount, Is.Zero, "an uninitialized platform does not move");
        }

        [Test]
        public void OnlyAnElevatorAnswersACall()
        {
            var shuttle = NewPlatform(Vector3.zero, Quaternion.identity);
            shuttle.Configure(Settings(PlatformRouteKind.PingPong, 1f, 0f), new[] { Vector3.zero, Vector3.up });
            var elevator = NewPlatform(Vector3.zero, Quaternion.identity);
            elevator.Configure(Settings(PlatformRouteKind.OnCall, 2f, 0f), new[] { Vector3.zero, new Vector3(0f, 4f, 0f) });
            LogAssert.Expect(LogType.Log, MovingPlatformLog.Called(shuttle.name, 1, PlatformRouteKind.PingPong, false));
            LogAssert.Expect(LogType.Log, MovingPlatformLog.Called(elevator.name, 1, PlatformRouteKind.OnCall, true));

            Assert.That(shuttle.Call(1), Is.False);
            Assert.That(elevator.Call(1), Is.True);

            elevator.Step(1f);
            Assert.That(elevator.transform.position.y, Is.EqualTo(2f).Within(1e-4f));
        }

        [Test]
        public void BoardingAnIdleElevatorSendsItToTheNextStop()
        {
            var elevator = NewPlatform(Vector3.zero, Quaternion.identity);
            elevator.Configure(Settings(PlatformRouteKind.OnCall, 2f, 0f), new[] { Vector3.zero, new Vector3(0f, 4f, 0f) });
            var rider = new object();

            elevator.Board(rider);
            elevator.Board(rider);
            elevator.Step(2f);

            Assert.That(elevator.RiderCount, Is.EqualTo(1));
            Assert.That(elevator.IsRiddenBy(rider), Is.True);
            Assert.That(elevator.transform.position.y, Is.EqualTo(4f).Within(1e-4f));

            elevator.Leave(rider);
            elevator.Step(5f);
            Assert.That(elevator.RiderCount, Is.Zero);
            Assert.That(elevator.transform.position.y, Is.EqualTo(4f).Within(1e-4f), "leaving does not call it");
        }

        [Test]
        public void BoardingDoesNotSendItWhenTheSettingsSayNo()
        {
            var settings = Settings(PlatformRouteKind.OnCall, 2f, 0f);
            settings.DepartWhenBoarded = false;
            var elevator = NewPlatform(Vector3.zero, Quaternion.identity);
            elevator.Configure(settings, new[] { Vector3.zero, new Vector3(0f, 4f, 0f) });

            elevator.Board(new object());
            elevator.Step(2f);

            Assert.That(elevator.transform.position.y, Is.Zero);
        }

        [Test]
        public void ObstructingTurnsBackAndCarriesNobody()
        {
            var platform = NewPlatform(Vector3.zero, Quaternion.identity);
            platform.Configure(Settings(PlatformRouteKind.PingPong, 2f, 0f), new[] { Vector3.zero, new Vector3(0f, 4f, 0f) });
            platform.Step(1f);
            platform.Step(0.5f);
            var count = platform.StepCount;
            LogAssert.Expect(LogType.Log, MovingPlatformLog.Reversed(platform.name, 1, 0));

            platform.Obstruct();

            Assert.That(platform.transform.position.y, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(platform.LastMotion.IsStill, Is.True);
            Assert.That(platform.StepCount, Is.EqualTo(count + 1));
            Assert.That(platform.Route.To, Is.EqualTo(0));
        }

        private MovingPlatform NewPlatform(Vector3 position, Quaternion rotation)
        {
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(slab);
            slab.name = "Platform" + _created.Count;
            slab.transform.SetPositionAndRotation(position, rotation);
            return slab.AddComponent<MovingPlatform>();
        }

        private MovingPlatformSettings Settings(PlatformRouteKind kind, float speed, float wait)
        {
            var settings = ScriptableObject.CreateInstance<MovingPlatformSettings>();
            _created.Add(settings);
            settings.Kind = kind;
            settings.Speed = speed;
            settings.WaitSeconds = wait;
            settings.SpinDegreesPerSecond = 0f;
            return settings;
        }
    }
}
