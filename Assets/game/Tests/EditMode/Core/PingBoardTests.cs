using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class PingBoardTests
    {
        private static readonly Vector3 _here = new Vector3(1f, 2f, 3f);
        private static readonly Vector3 _there = new Vector3(4f, 5f, 6f);

        [Test]
        public void SpecRejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PingSpec(lifetimeSeconds: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PingSpec(lifetimeSeconds: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PingSpec(lifetimeSeconds: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PingSpec(maxMarkers: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PingSpec(maxDistance: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PingSpec(maxDistance: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PingSpec(enemyAimToleranceDegrees: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PingSpec(enemyAimToleranceDegrees: 90f));
            Assert.Throws<ArgumentNullException>(() => new PingBoard(null));
        }

        [Test]
        public void TheDefaultSpecUsesTheDefaults()
        {
            Assert.That(PingSpec.Default.LifetimeSeconds, Is.EqualTo(PingSpec.DefaultLifetimeSeconds));
            Assert.That(PingSpec.Default.MaxMarkers, Is.EqualTo(PingSpec.DefaultMaxMarkers));
            Assert.That(PingSpec.Default.MaxDistance, Is.EqualTo(PingSpec.DefaultMaxDistance));
            Assert.That(PingSpec.Default.EnemyAimToleranceDegrees, Is.EqualTo(PingSpec.DefaultEnemyAimToleranceDegrees));
        }

        [Test]
        public void PlacingAMarkerNumbersItAndSetsItsLifetime()
        {
            var board = new PingBoard(new PingSpec(lifetimeSeconds: 5f));

            var location = board.Place(PingHit.Location(_here), null, 10f);
            var enemy = board.Place(PingHit.Enemy(0, _there), 42, 11f);

            Assert.That(location.Marker.Id, Is.EqualTo(1));
            Assert.That(location.Marker.Kind, Is.EqualTo(PingKind.Location));
            Assert.That(location.Marker.TargetKey, Is.Null);
            Assert.That(location.Marker.IsTracking, Is.False);
            Assert.That(location.Marker.Position, Is.EqualTo(_here));
            Assert.That(location.Marker.PlacedAt, Is.EqualTo(10f));
            Assert.That(location.Marker.ExpiresAt, Is.EqualTo(15f));
            Assert.That(location.Refreshed, Is.False);
            Assert.That(location.Replaced, Is.Null);

            Assert.That(enemy.Marker.Id, Is.EqualTo(2));
            Assert.That(enemy.Marker.Kind, Is.EqualTo(PingKind.Enemy));
            Assert.That(enemy.Marker.TargetKey, Is.EqualTo(42));
            Assert.That(enemy.Marker.IsTracking, Is.True);
            Assert.That(board.Count, Is.EqualTo(2));
            Assert.That(board.IsMarked(42), Is.True);
            Assert.That(board.IsMarked(7), Is.False);
        }

        [Test]
        public void PlacingRejectsMismatchedHits()
        {
            var board = new PingBoard(PingSpec.Default);

            Assert.Throws<ArgumentException>(() => board.Place(PingHit.None, null, 0f));
            Assert.Throws<ArgumentException>(() => board.Place(PingHit.Enemy(0, _here), null, 0f));
            Assert.Throws<ArgumentException>(() => board.Place(PingHit.Location(_here), 3, 0f));
            Assert.Throws<ArgumentException>(() => board.Place(PingHit.Location(new Vector3(float.NaN, 0f, 0f)), null, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => board.Place(PingHit.Location(_here), null, float.NaN));
            Assert.That(board.Count, Is.Zero);
        }

        [Test]
        public void PlacingBeyondTheLimitReplacesTheOldest()
        {
            var board = new PingBoard(new PingSpec(maxMarkers: 2));
            board.Place(PingHit.Location(_here), null, 0f);
            board.Place(PingHit.Location(_there), null, 1f);

            var third = board.Place(PingHit.Location(Vector3.Zero), null, 2f);

            Assert.That(third.Replaced.HasValue, Is.True);
            Assert.That(third.Replaced.Value.Id, Is.EqualTo(1));
            Assert.That(board.Count, Is.EqualTo(2));
            Assert.That(board.Markers[0].Id, Is.EqualTo(2));
            Assert.That(board.Markers[1].Id, Is.EqualTo(3));
            Assert.That(board.TryGet(1, out _), Is.False);
        }

        [Test]
        public void MarkingTheSameEnemyAgainRefreshesItInsteadOfAddingOne()
        {
            var board = new PingBoard(new PingSpec(lifetimeSeconds: 5f, maxMarkers: 2));
            var first = board.Place(PingHit.Enemy(0, _here), 42, 0f);
            board.Place(PingHit.Location(_there), null, 1f);
            board.Track(first.Marker.Id, true, false, Vector3.Zero, out _);

            var again = board.Place(PingHit.Enemy(0, _there), 42, 3f);

            Assert.That(again.Refreshed, Is.True);
            Assert.That(again.Replaced, Is.Null);
            Assert.That(again.Marker.Id, Is.EqualTo(first.Marker.Id));
            Assert.That(again.Marker.Position, Is.EqualTo(_there));
            Assert.That(again.Marker.IsTracking, Is.True, "marking again picks the enemy up again");
            Assert.That(again.Marker.ExpiresAt, Is.EqualTo(8f));
            Assert.That(board.Count, Is.EqualTo(2));
            Assert.That(board.Markers[1].Id, Is.EqualTo(first.Marker.Id), "the refreshed marker becomes the newest");
        }

        [Test]
        public void AnEnemyMarkerFollowsWhileVisibleAndStaysWhereItWasLost()
        {
            var board = new PingBoard(PingSpec.Default);
            var id = board.Place(PingHit.Enemy(0, _here), 42, 0f).Marker.Id;

            Assert.That(board.Track(id, true, true, _here, out _), Is.EqualTo(PingTrackResult.Unchanged));
            Assert.That(board.Track(id, true, true, _there, out var moved), Is.EqualTo(PingTrackResult.Moved));
            Assert.That(moved.Position, Is.EqualTo(_there));

            Assert.That(board.Track(id, true, false, Vector3.Zero, out var lost), Is.EqualTo(PingTrackResult.Lost));
            Assert.That(lost.IsTracking, Is.False);
            Assert.That(lost.Position, Is.EqualTo(_there), "a lost marker stays where the enemy was last seen");

            Assert.That(board.Track(id, true, true, new Vector3(9f, 9f, 9f), out var after), Is.EqualTo(PingTrackResult.Unchanged));
            Assert.That(after.Position, Is.EqualTo(_there), "seeing the enemy again does not move a lost marker");
            Assert.That(board.IsMarked(42), Is.True, "a lost marker still marks the enemy");
        }

        [Test]
        public void AnEnemyMarkerIsRemovedWhenTheEnemyIsGone()
        {
            var board = new PingBoard(PingSpec.Default);
            var id = board.Place(PingHit.Enemy(0, _here), 42, 0f).Marker.Id;
            board.Track(id, true, false, Vector3.Zero, out _);

            Assert.That(board.Track(id, false, false, Vector3.Zero, out var gone), Is.EqualTo(PingTrackResult.Gone));
            Assert.That(gone.Id, Is.EqualTo(id));
            Assert.That(board.Count, Is.Zero);
            Assert.That(board.IsMarked(42), Is.False);
            Assert.That(board.Track(id, true, true, _here, out _), Is.EqualTo(PingTrackResult.Missing));
        }

        [Test]
        public void TrackingLeavesLocationMarkersAlone()
        {
            var board = new PingBoard(PingSpec.Default);
            var id = board.Place(PingHit.Location(_here), null, 0f).Marker.Id;

            Assert.That(board.Track(id, false, false, Vector3.Zero, out var marker), Is.EqualTo(PingTrackResult.Unchanged));
            Assert.That(marker.Position, Is.EqualTo(_here));
            Assert.That(board.Count, Is.EqualTo(1));
        }

        [Test]
        public void TrackingRejectsANonFinitePositionWhileVisible()
        {
            var board = new PingBoard(PingSpec.Default);
            var id = board.Place(PingHit.Enemy(0, _here), 42, 0f).Marker.Id;

            Assert.Throws<ArgumentOutOfRangeException>(() => board.Track(id, true, true, new Vector3(float.NaN, 0f, 0f), out _));
        }

        [Test]
        public void MarkersExpireAfterTheirLifetime()
        {
            var board = new PingBoard(new PingSpec(lifetimeSeconds: 4f));
            board.Place(PingHit.Location(_here), null, 0f);
            board.Place(PingHit.Enemy(0, _there), 42, 2f);
            var expired = new List<PingMarker>();

            Assert.That(board.Expire(3.99f, expired), Is.Zero);
            Assert.That(board.Markers[0].RemainingSeconds(3f), Is.EqualTo(1f).Within(1e-5f));

            Assert.That(board.Expire(4f, expired), Is.EqualTo(1));
            Assert.That(expired[0].Id, Is.EqualTo(1));
            Assert.That(board.Count, Is.EqualTo(1));

            Assert.That(board.Expire(10f, expired), Is.EqualTo(1));
            Assert.That(expired[1].Id, Is.EqualTo(2));
            Assert.That(expired[1].RemainingSeconds(10f), Is.Zero);
            Assert.That(board.Count, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => board.Expire(float.PositiveInfinity, null));
        }

        [Test]
        public void ClearingRemovesEverythingAndKeepsNumbering()
        {
            var board = new PingBoard(PingSpec.Default);
            board.Place(PingHit.Location(_here), null, 0f);
            board.Place(PingHit.Location(_there), null, 0f);
            var removed = new List<PingMarker>();

            Assert.That(board.Clear(removed), Is.EqualTo(2));
            Assert.That(removed.Count, Is.EqualTo(2));
            Assert.That(board.Count, Is.Zero);
            Assert.That(board.Place(PingHit.Location(_here), null, 1f).Marker.Id, Is.EqualTo(3), "numbers are not reused");
        }
    }
}
