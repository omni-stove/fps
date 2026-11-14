using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class PingControllerSharedTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void ShowsTheServersPingsAndRefusesToPlaceItsOwn()
        {
            var controller = Controller();
            var shared = new SharedPingMarkers();
            shared.Place(4, PingKind.Location, new System.Numerics.Vector3(1f, 2f, 3f), false, 5f, 0f);

            LogAssert.Expect(LogType.Log, PingLog.Shared());
            controller.ShowShared(shared);

            Assert.That(controller.IsShared, Is.True);
            Assert.That(controller.UsesPing, Is.True);
            Assert.That(controller.Markers, Is.SameAs(shared.Markers));
            Assert.That(controller.TryGetMarker(4, out var marker), Is.True);
            Assert.That(marker.Position, Is.EqualTo(new System.Numerics.Vector3(1f, 2f, 3f)));

            LogAssert.Expect(LogType.Log, PingLog.Refused(PingRefusal.Remote));
            Assert.That(controller.TryPingAlong(Vector3.zero, Vector3.forward, out _), Is.False);
            Assert.That(controller.LastRefusal, Is.EqualTo(PingRefusal.Remote));
        }

        [Test]
        public void TickTellsTheViewOnlyWhenTheServersPingsChanged()
        {
            var controller = Controller();
            var shared = new SharedPingMarkers();
            controller.ShowShared(shared);
            var changes = 0;
            controller.Changed += _ => changes++;

            controller.Tick(0f);
            Assert.That(changes, Is.EqualTo(0));

            shared.Place(1, PingKind.Enemy, System.Numerics.Vector3.Zero, true, 5f, 0f);
            controller.Tick(0f);
            controller.Tick(0f);
            Assert.That(changes, Is.EqualTo(1));

            // 寿命はサーバー (と SharedPingMarkers) が決めるので、Tick では消さない
            controller.Tick(100f);
            Assert.That(controller.Markers.Count, Is.EqualTo(1));
        }

        [Test]
        public void ConfiguringForOfflinePlayStopsShowingTheServersPings()
        {
            var controller = Controller();
            var shared = new SharedPingMarkers();
            shared.Place(1, PingKind.Location, System.Numerics.Vector3.Zero, false, 5f, 0f);
            controller.ShowShared(shared);

            controller.Configure(GameRules.Default.WithActions(GameRules.Default.Actions.WithPing(Fps.Core.Ping.On)));

            Assert.That(controller.IsShared, Is.False);
            Assert.That(controller.Markers, Is.Empty);
            Assert.That(shared.Count, Is.EqualTo(1));
        }

        [Test]
        public void ClearingEmptiesTheServersPingsShownHere()
        {
            var controller = Controller();
            var shared = new SharedPingMarkers();
            shared.Place(1, PingKind.Location, System.Numerics.Vector3.Zero, false, 5f, 0f);
            controller.ShowShared(shared);

            LogAssert.Expect(LogType.Log, PingLog.Cleared(1));
            controller.ClearMarkers();

            Assert.That(controller.Markers, Is.Empty);
            Assert.Throws<System.ArgumentNullException>(() => controller.ShowShared(null));
        }

        private PingController Controller()
        {
            var go = new GameObject("SharedPinger");
            _objects.Add(go);
            return go.AddComponent<PingController>();
        }
    }
}
