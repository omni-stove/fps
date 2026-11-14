using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SharedPingMarkersTests
    {
        private static readonly Vector3 _here = new Vector3(1f, 2f, 3f);
        private static readonly Vector3 _there = new Vector3(4f, 5f, 6f);

        [Test]
        public void PlacingUsesTheRemainingSecondsOnThisClock()
        {
            var markers = new SharedPingMarkers();

            Assert.That(markers.Place(4, PingKind.Enemy, _here, true, 5f, 100f), Is.True);

            Assert.That(markers.TryGet(4, out var marker), Is.True);
            Assert.That(marker.Kind, Is.EqualTo(PingKind.Enemy));
            Assert.That(marker.Position, Is.EqualTo(_here));
            Assert.That(marker.IsTracking, Is.True);
            Assert.That(marker.TargetKey, Is.Null);
            Assert.That(marker.PlacedAt, Is.EqualTo(100f));
            Assert.That(marker.ExpiresAt, Is.EqualTo(105f));
            Assert.That(markers.Version, Is.EqualTo(1));
        }

        [Test]
        public void InvalidNewsIsIgnored()
        {
            var markers = new SharedPingMarkers();

            Assert.That(markers.Place(1, (PingKind)9, _here, false, 5f, 0f), Is.False);
            Assert.That(markers.Place(1, PingKind.Location, new Vector3(float.NaN, 0f, 0f), false, 5f, 0f), Is.False);
            Assert.That(markers.Place(1, PingKind.Location, _here, false, 0f, 0f), Is.False);
            Assert.That(markers.Place(1, PingKind.Location, _here, false, float.PositiveInfinity, 0f), Is.False);
            Assert.That(markers.Place(1, PingKind.Location, _here, false, 5f, float.NaN), Is.False);
            Assert.That(markers.Count, Is.EqualTo(0));
            Assert.That(markers.Version, Is.EqualTo(0));
        }

        [Test]
        public void ALocationPingNeverTracks()
        {
            var markers = new SharedPingMarkers();
            markers.Place(1, PingKind.Location, _here, true, 5f, 0f);

            Assert.That(markers.Markers[0].IsTracking, Is.False);
            Assert.That(markers.Move(1, _there), Is.False);
        }

        [Test]
        public void PlacingTheSameIdAgainRefreshesItAsTheNewest()
        {
            var markers = new SharedPingMarkers();
            markers.Place(1, PingKind.Enemy, _here, true, 5f, 0f);
            markers.Place(2, PingKind.Location, _there, false, 5f, 0f);

            markers.Place(1, PingKind.Enemy, _there, true, 5f, 3f);

            Assert.That(markers.Count, Is.EqualTo(2));
            Assert.That(markers.Markers[1].Id, Is.EqualTo(1));
            Assert.That(markers.Markers[1].ExpiresAt, Is.EqualTo(8f));
        }

        [Test]
        public void MovesStopOnceThePingIsLost()
        {
            var markers = new SharedPingMarkers();
            markers.Place(1, PingKind.Enemy, _here, true, 5f, 0f);

            Assert.That(markers.Move(1, _there), Is.True);
            Assert.That(markers.Move(1, _there), Is.False);
            Assert.That(markers.Lose(1, _here), Is.True);
            Assert.That(markers.Markers[0].IsTracking, Is.False);
            Assert.That(markers.Markers[0].Position, Is.EqualTo(_here));

            // 見失った後に遅れて届いた位置は捨てる
            Assert.That(markers.Move(1, _there), Is.False);
            Assert.That(markers.Markers[0].Position, Is.EqualTo(_here));

            Assert.That(markers.Move(99, _there), Is.False);
            Assert.That(markers.Lose(99, _there), Is.False);
        }

        [Test]
        public void RemovingExpiringAndClearingBumpTheVersion()
        {
            var markers = new SharedPingMarkers();
            markers.Place(1, PingKind.Location, _here, false, 1f, 0f);
            markers.Place(2, PingKind.Location, _here, false, 3f, 0f);
            markers.Place(3, PingKind.Location, _here, false, 3f, 0f);
            var version = markers.Version;

            Assert.That(markers.Remove(2), Is.True);
            Assert.That(markers.Remove(2), Is.False);
            Assert.That(markers.Version, Is.EqualTo(version + 1));

            Assert.That(markers.Expire(0.5f), Is.EqualTo(0));
            Assert.That(markers.Version, Is.EqualTo(version + 1));
            Assert.That(markers.Expire(1f), Is.EqualTo(1));
            Assert.That(markers.Version, Is.EqualTo(version + 2));

            Assert.That(markers.Clear(), Is.EqualTo(1));
            Assert.That(markers.Clear(), Is.EqualTo(0));
            Assert.That(markers.Version, Is.EqualTo(version + 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => markers.Expire(float.NaN));
        }
    }
}
