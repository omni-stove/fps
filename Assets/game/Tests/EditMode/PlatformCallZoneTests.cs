using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class PlatformCallZoneTests
    {
        private const string PlayerTag = "Player";

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
        public void APlayerEnteringCallsThePlatformToTheZonesStop()
        {
            var elevator = Elevator();
            var zone = Zone("Bottom", elevator, 1);
            var player = Body("Player", PlayerTag);

            LogAssert.Expect(LogType.Log, MovingPlatformLog.Called(elevator.name, 1, PlatformRouteKind.OnCall, true));
            Assert.That(zone.HandleEnter(player), Is.True);
            Assert.That(elevator.Route.Calls, Is.EqualTo(new[] { 1 }));

            elevator.Step(10f);
            Assert.That(elevator.Route.To, Is.EqualTo(1), "the elevator came to the zone's stop");
            Assert.That(elevator.transform.position.y, Is.EqualTo(4f).Within(1e-4f));

            LogAssert.Expect(LogType.Log, MovingPlatformLog.Called(elevator.name, 1, PlatformRouteKind.OnCall, false));
            Assert.That(zone.HandleEnter(player), Is.False, "calling it to where it already is does nothing");
        }

        [Test]
        public void OnlyThePlayerCallsIt()
        {
            var elevator = Elevator();
            var zone = Zone("Bottom", elevator, 1);
            var crate = Body("Crate", "Untagged");

            Assert.That(zone.HandleEnter(crate), Is.False);
            Assert.That(zone.HandleEnter(null), Is.False);
            Assert.That(elevator.Route.Calls, Is.Empty);

            zone.PlayerTag = string.Empty;
            Assert.That(zone.HandleEnter(Body("Player", PlayerTag)), Is.False, "no tag means no caller");
        }

        [Test]
        public void ItBuildsATriggerBoxOnlyWhenItHasNoCollider()
        {
            var zone = Zone("Built", null, 0);
            zone.Size = new Vector3(3f, 2f, 1f);

            var built = zone.EnsureTrigger();
            Assert.That(built, Is.InstanceOf<BoxCollider>());
            Assert.That(built.isTrigger, Is.True);
            Assert.That(((BoxCollider)built).size, Is.EqualTo(new Vector3(3f, 2f, 1f)));
            Assert.That(zone.EnsureTrigger(), Is.SameAs(built), "a second call keeps the one box");
            Assert.That(zone.GetComponents<Collider>(), Has.Length.EqualTo(1));

            var placed = new GameObject("Placed");
            _created.Add(placed);
            var sphere = placed.AddComponent<SphereCollider>();
            var own = placed.AddComponent<PlatformCallZone>();
            Assert.That(own.EnsureTrigger(), Is.SameAs(sphere), "a placed collider is used as the zone");
            Assert.That(sphere.isTrigger, Is.True, "the player walks through it");
        }

        [Test]
        public void ItInitializesAPlatformThatHasNotWokenYet()
        {
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(slab);
            slab.name = "Asleep";
            var platform = slab.AddComponent<MovingPlatform>();
            platform.Settings = OnCallSettings();
            var zone = Zone("Bottom", platform, 1);

            LogAssert.Expect(LogType.Log, MovingPlatformLog.Called(slab.name, 1, PlatformRouteKind.OnCall, true));
            Assert.That(zone.Request(), Is.True);
            Assert.That(platform.IsInitialized, Is.True);
        }

        [Test]
        public void AMissingPlatformOrAStopPastTheEndIsReported()
        {
            var empty = Zone("Empty", null, 0);
            LogAssert.Expect(LogType.Warning, MovingPlatformLog.CallZoneWithoutPlatform(empty.name));
            Assert.That(empty.Request(), Is.False);

            var elevator = Elevator();
            var far = Zone("Far", elevator, 5);
            LogAssert.Expect(LogType.Error, MovingPlatformLog.CallZoneStopOutOfRange(far.name, elevator.name, 5, 2));
            Assert.That(far.Request(), Is.False);
            Assert.That(elevator.Route.Calls, Is.Empty);

            Assert.Throws<ArgumentOutOfRangeException>(() => far.Stop = -1);
            Assert.That(far.Stop, Is.EqualTo(5));
        }

        [Test]
        public void TheDefaultsCoverAStandingPlayer()
        {
            var zone = Zone("Defaults", null, 0);

            Assert.That(zone.Size, Is.EqualTo(PlatformCallZone.DefaultSize));
            Assert.That(PlatformCallZone.DefaultSize.y, Is.GreaterThanOrEqualTo(GameBootstrap.PlayerHeight));
            Assert.That(zone.PlayerTag, Is.EqualTo(GameBootstrap.PlayerTag));
        }

        private MovingPlatform Elevator()
        {
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(slab);
            slab.name = "Elevator";
            var platform = slab.AddComponent<MovingPlatform>();
            platform.Configure(OnCallSettings(), new[] { Vector3.zero, new Vector3(0f, 4f, 0f) });
            return platform;
        }

        private MovingPlatformSettings OnCallSettings()
        {
            var settings = ScriptableObject.CreateInstance<MovingPlatformSettings>();
            _created.Add(settings);
            settings.Kind = PlatformRouteKind.OnCall;
            settings.Speed = 2f;
            settings.WaitSeconds = 0f;
            settings.SpinDegreesPerSecond = 0f;
            settings.DepartWhenBoarded = true;
            return settings;
        }

        private PlatformCallZone Zone(string name, MovingPlatform platform, int stop)
        {
            var zoneObject = new GameObject(name);
            _created.Add(zoneObject);
            var zone = zoneObject.AddComponent<PlatformCallZone>();
            zone.Platform = platform;
            zone.Stop = stop;
            return zone;
        }

        private Collider Body(string name, string tag)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _created.Add(body);
            body.name = name;
            body.tag = tag;
            return body.GetComponent<Collider>();
        }
    }
}
