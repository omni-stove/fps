using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
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
        public void RevealingUsesTheGivenLifetimeAndDoesNotCountTowardTheLimit()
        {
            var board = new PingBoard(new PingSpec(lifetimeSeconds: 5f, maxMarkers: 1));
            var location = board.Place(PingHit.Location(_here), null, 0f);

            var first = board.Reveal(42, _there, 1f, 30f);
            var second = board.Reveal(43, _here, 1f, 30f);

            Assert.That(first.Marker.Id, Is.EqualTo(2));
            Assert.That(first.Marker.Kind, Is.EqualTo(PingKind.Enemy));
            Assert.That(first.Marker.TargetKey, Is.EqualTo(42));
            Assert.That(first.Marker.IsTracking, Is.True);
            Assert.That(first.Marker.ExpiresAt, Is.EqualTo(31f));
            Assert.That(first.Refreshed, Is.False);
            Assert.That(first.Replaced, Is.Null);
            Assert.That(second.Replaced, Is.Null);
            Assert.That(board.Count, Is.EqualTo(3), "revealed marks do not push out the placed one");
            Assert.That(board.IsRevealed(first.Marker.Id), Is.True);
            Assert.That(board.IsRevealed(location.Marker.Id), Is.False);

            var next = board.Place(PingHit.Location(_there), null, 2f);
            Assert.That(next.Replaced.HasValue, Is.True);
            Assert.That(next.Replaced.Value.Id, Is.EqualTo(location.Marker.Id), "only placed marks make room for placed marks");
            Assert.That(board.Count, Is.EqualTo(3));
            Assert.That(board.IsMarked(42), Is.True);
            Assert.That(board.IsMarked(43), Is.True);
        }

        [Test]
        public void RevealingAMarkedEnemyRefreshesItWithoutShorteningIt()
        {
            var board = new PingBoard(new PingSpec(lifetimeSeconds: 5f));
            var placed = board.Place(PingHit.Enemy(0, _here), 42, 0f);
            board.Track(placed.Marker.Id, true, false, Vector3.Zero, out _);

            var revealed = board.Reveal(42, _there, 1f, 30f);

            Assert.That(revealed.Refreshed, Is.True);
            Assert.That(revealed.Marker.Id, Is.EqualTo(placed.Marker.Id));
            Assert.That(revealed.Marker.IsTracking, Is.True, "revealing picks a lost enemy up again");
            Assert.That(revealed.Marker.Position, Is.EqualTo(_there));
            Assert.That(revealed.Marker.ExpiresAt, Is.EqualTo(31f));
            Assert.That(board.IsRevealed(placed.Marker.Id), Is.True);

            var pingedAgain = board.Place(PingHit.Enemy(0, _here), 42, 2f);
            Assert.That(pingedAgain.Marker.ExpiresAt, Is.EqualTo(31f), "pinging a revealed enemy keeps the longer lifetime");
            Assert.That(board.IsRevealed(placed.Marker.Id), Is.True);
            Assert.That(board.Count, Is.EqualTo(1));
        }

        [Test]
        public void ARevealedMarkerIsForgottenWhenItGoes()
        {
            var board = new PingBoard(PingSpec.Default);
            var expiring = board.Reveal(42, _here, 0f, 10f).Marker.Id;
            var dying = board.Reveal(43, _there, 0f, 10f).Marker.Id;

            Assert.That(board.Track(dying, false, false, Vector3.Zero, out _), Is.EqualTo(PingTrackResult.Gone));
            Assert.That(board.IsRevealed(dying), Is.False);

            Assert.That(board.Expire(9.99f, null), Is.Zero);
            Assert.That(board.Expire(10f, null), Is.EqualTo(1));
            Assert.That(board.IsRevealed(expiring), Is.False);

            var cleared = board.Reveal(44, _here, 11f, 10f).Marker.Id;
            board.Clear(null);
            Assert.That(board.IsRevealed(cleared), Is.False);
        }

        [Test]
        public void RevealingRejectsInvalidValues()
        {
            var board = new PingBoard(PingSpec.Default);

            Assert.Throws<ArgumentOutOfRangeException>(() => board.Reveal(42, _here, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => board.Reveal(42, _here, 0f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => board.Reveal(42, _here, 0f, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => board.Reveal(42, _here, float.NaN, 5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => board.Reveal(42, new Vector3(float.NaN, 0f, 0f), 0f, 5f));
            Assert.That(board.Count, Is.Zero);
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
