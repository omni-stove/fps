using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class TeamPingRelayTests
    {
        private const int _alice = 1;
        private const int _bob = 2;
        private const int _enemy = 7;

        private static readonly Vector3 _here = new Vector3(1f, 2f, 3f);
        private static readonly Vector3 _there = new Vector3(4f, 5f, 6f);

        private readonly List<SharedPingChange> _changes = new List<SharedPingChange>();

        [SetUp]
        public void SetUp()
        {
            _changes.Clear();
        }

        [Test]
        public void SpecRejectsAnInvalidInterval()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PingSpec(minIntervalSeconds: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PingSpec(minIntervalSeconds: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PingSpec(minIntervalSeconds: float.PositiveInfinity));
            Assert.That(new PingSpec(minIntervalSeconds: 0f).MinIntervalSeconds, Is.EqualTo(0f));
            Assert.That(PingSpec.Default.MinIntervalSeconds, Is.EqualTo(PingSpec.DefaultMinIntervalSeconds));
            Assert.Throws<ArgumentNullException>(() => new TeamPingRelay(null));
        }

        [Test]
        public void PlacingGivesEachPingAnIdUniqueAcrossPlayers()
        {
            var relay = new TeamPingRelay(new PingSpec(lifetimeSeconds: 5f));

            Assert.That(relay.TryPlace(_alice, Team.Alpha, PingHit.Location(_here), null, 10f, _changes, out var first), Is.EqualTo(PingRefusal.None));
            Assert.That(relay.TryPlace(_bob, Team.Bravo, PingHit.Location(_there), null, 10f, _changes, out var second), Is.EqualTo(PingRefusal.None));

            Assert.That(first.Id, Is.Not.EqualTo(second.Id));
            Assert.That(first.PingerId, Is.EqualTo(_alice));
            Assert.That(first.Team, Is.EqualTo(Team.Alpha));
            Assert.That(first.Marker.Position, Is.EqualTo(_here));
            Assert.That(first.Marker.ExpiresAt, Is.EqualTo(15f));
            Assert.That(second.Team, Is.EqualTo(Team.Bravo));
            Assert.That(relay.Count, Is.EqualTo(2));
            Assert.That(_changes.Count, Is.EqualTo(2));
            Assert.That(_changes[0].Kind, Is.EqualTo(SharedPingChangeKind.Placed));
            Assert.That(_changes[0].Refreshed, Is.False);
            Assert.That(_changes[1].Ping.Id, Is.EqualTo(second.Id));
        }

        [Test]
        public void TheSamePlayerMustWaitTheIntervalEvenAfterMarkingNothing()
        {
            var relay = new TeamPingRelay(new PingSpec(minIntervalSeconds: 1f));

            Assert.That(relay.CheckInterval(_alice, 0f), Is.EqualTo(PingRefusal.None));
            Assert.That(relay.TryPlace(_alice, Team.Alpha, PingHit.None, null, 0f, _changes, out _), Is.EqualTo(PingRefusal.NoTarget));
            Assert.That(_changes, Is.Empty);

            Assert.That(relay.CheckInterval(_alice, 0.5f), Is.EqualTo(PingRefusal.TooSoon));
            Assert.That(relay.TryPlace(_alice, Team.Alpha, PingHit.Location(_here), null, 0.5f, _changes, out _), Is.EqualTo(PingRefusal.TooSoon));
            Assert.That(relay.Count, Is.EqualTo(0));

            // 断った試みは数え直さないので、最初の試みから 1 秒で付けられる
            Assert.That(relay.TryPlace(_alice, Team.Alpha, PingHit.Location(_here), null, 1f, _changes, out _), Is.EqualTo(PingRefusal.None));

            // 間隔は 1 人ごと
            Assert.That(relay.TryPlace(_bob, Team.Alpha, PingHit.Location(_here), null, 1f, _changes, out _), Is.EqualTo(PingRefusal.None));
            Assert.That(relay.Count, Is.EqualTo(2));
        }

        [Test]
        public void GoingOverTheLimitRemovesTheOldestBeforePlacing()
        {
            var relay = new TeamPingRelay(new PingSpec(maxMarkers: 1, minIntervalSeconds: 0f));
            relay.TryPlace(_alice, Team.Alpha, PingHit.Location(_here), null, 0f, _changes, out var oldest);
            _changes.Clear();

            relay.TryPlace(_alice, Team.Alpha, PingHit.Location(_there), null, 1f, _changes, out var newest);

            Assert.That(_changes.Count, Is.EqualTo(2));
            Assert.That(_changes[0].Kind, Is.EqualTo(SharedPingChangeKind.Removed));
            Assert.That(_changes[0].Removal, Is.EqualTo(PingRemoval.Replaced));
            Assert.That(_changes[0].Ping.Id, Is.EqualTo(oldest.Id));
            Assert.That(_changes[1].Kind, Is.EqualTo(SharedPingChangeKind.Placed));
            Assert.That(_changes[1].Ping.Id, Is.EqualTo(newest.Id));
            Assert.That(relay.TryGet(oldest.Id, out _), Is.False);
            Assert.That(relay.Count, Is.EqualTo(1));
        }

        [Test]
        public void MarkingTheSameEnemyAgainRefreshesThePing()
        {
            var relay = new TeamPingRelay(new PingSpec(lifetimeSeconds: 5f, minIntervalSeconds: 0f));
            relay.TryPlace(_alice, Team.Alpha, PingHit.Enemy(0, _here), _enemy, 0f, _changes, out var first);
            _changes.Clear();

            relay.TryPlace(_alice, Team.Alpha, PingHit.Enemy(0, _there), _enemy, 2f, _changes, out var again);

            Assert.That(again.Id, Is.EqualTo(first.Id));
            Assert.That(again.Marker.TargetKey, Is.EqualTo(_enemy));
            Assert.That(again.Marker.ExpiresAt, Is.EqualTo(7f));
            Assert.That(_changes.Count, Is.EqualTo(1));
            Assert.That(_changes[0].Refreshed, Is.True);
            Assert.That(relay.Count, Is.EqualTo(1));
        }

        [Test]
        public void EnemyPingsMoveWhileSeenStayWhenLostAndGoWhenTheEnemyFalls()
        {
            var relay = new TeamPingRelay(new PingSpec(minIntervalSeconds: 0f));
            relay.TryPlace(_alice, Team.Alpha, PingHit.Enemy(0, _here), _enemy, 0f, _changes, out var ping);
            relay.TryPlace(_bob, Team.Alpha, PingHit.Location(_there), null, 0f, _changes, out _);
            var tracking = new List<SharedPing>();
            relay.CollectTracking(tracking);
            Assert.That(tracking.Count, Is.EqualTo(1));
            Assert.That(tracking[0].Id, Is.EqualTo(ping.Id));
            _changes.Clear();

            relay.Track(ping.Id, true, true, _there, _changes);
            Assert.That(_changes.Count, Is.EqualTo(1));
            Assert.That(_changes[0].Kind, Is.EqualTo(SharedPingChangeKind.Moved));
            Assert.That(_changes[0].Ping.Marker.Position, Is.EqualTo(_there));

            _changes.Clear();
            relay.Track(ping.Id, true, false, Vector3.Zero, _changes);
            Assert.That(_changes.Count, Is.EqualTo(1));
            Assert.That(_changes[0].Kind, Is.EqualTo(SharedPingChangeKind.Lost));
            Assert.That(_changes[0].Ping.Marker.Position, Is.EqualTo(_there));
            relay.CollectTracking(tracking);
            Assert.That(tracking.Count, Is.EqualTo(1));

            _changes.Clear();
            relay.Track(ping.Id, false, false, Vector3.Zero, _changes);
            Assert.That(_changes.Count, Is.EqualTo(1));
            Assert.That(_changes[0].Kind, Is.EqualTo(SharedPingChangeKind.Removed));
            Assert.That(_changes[0].Removal, Is.EqualTo(PingRemoval.TargetGone));
            Assert.That(relay.TryGet(ping.Id, out _), Is.False);

            _changes.Clear();
            relay.Track(ping.Id, true, true, _here, _changes);
            Assert.That(_changes, Is.Empty);
        }

        [Test]
        public void ExpiredPingsAreRemoved()
        {
            var relay = new TeamPingRelay(new PingSpec(lifetimeSeconds: 2f, minIntervalSeconds: 0f));
            relay.TryPlace(_alice, Team.Alpha, PingHit.Location(_here), null, 0f, _changes, out var early);
            relay.TryPlace(_bob, Team.Bravo, PingHit.Location(_there), null, 1f, _changes, out var late);
            _changes.Clear();

            Assert.That(relay.Expire(2f, _changes), Is.EqualTo(1));
            Assert.That(_changes.Count, Is.EqualTo(1));
            Assert.That(_changes[0].Ping.Id, Is.EqualTo(early.Id));
            Assert.That(_changes[0].Removal, Is.EqualTo(PingRemoval.Expired));
            Assert.That(relay.TryGet(late.Id, out _), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => relay.Expire(float.NaN, _changes));
        }

        [Test]
        public void ALeavingPlayerTakesTheirPingsAndIntervalAlong()
        {
            var relay = new TeamPingRelay(new PingSpec(minIntervalSeconds: 10f));
            relay.TryPlace(_alice, Team.Alpha, PingHit.Location(_here), null, 0f, _changes, out var mine);
            relay.TryPlace(_bob, Team.Alpha, PingHit.Location(_there), null, 0f, _changes, out var theirs);
            _changes.Clear();

            Assert.That(relay.RemovePlayer(_alice, _changes), Is.EqualTo(1));
            Assert.That(_changes.Count, Is.EqualTo(1));
            Assert.That(_changes[0].Ping.Id, Is.EqualTo(mine.Id));
            Assert.That(_changes[0].Removal, Is.EqualTo(PingRemoval.PingerLeft));
            Assert.That(relay.TryGet(theirs.Id, out _), Is.True);
            Assert.That(relay.CheckInterval(_alice, 1f), Is.EqualTo(PingRefusal.None));
            Assert.That(relay.RemovePlayer(_alice, _changes), Is.EqualTo(0));
        }

        [Test]
        public void ChangingTeamsRemovesThePingsShownToTheOldTeam()
        {
            var relay = new TeamPingRelay(new PingSpec(minIntervalSeconds: 0f));
            relay.TryPlace(_alice, Team.Alpha, PingHit.Location(_here), null, 0f, _changes, out var before);
            _changes.Clear();

            relay.TryPlace(_alice, Team.Bravo, PingHit.Location(_there), null, 1f, _changes, out var after);

            Assert.That(_changes.Count, Is.EqualTo(2));
            Assert.That(_changes[0].Ping.Id, Is.EqualTo(before.Id));
            Assert.That(_changes[0].Ping.Team, Is.EqualTo(Team.Alpha));
            Assert.That(_changes[0].Removal, Is.EqualTo(PingRemoval.PingerLeft));
            Assert.That(after.Team, Is.EqualTo(Team.Bravo));
        }

        [Test]
        public void CollectTeamListsOnlyThatTeamsPings()
        {
            var relay = new TeamPingRelay(new PingSpec(minIntervalSeconds: 0f));
            relay.TryPlace(_alice, Team.Alpha, PingHit.Location(_here), null, 0f, _changes, out var alpha);
            relay.TryPlace(_bob, Team.Bravo, PingHit.Location(_there), null, 0f, _changes, out _);
            var pings = new List<SharedPing>();

            relay.CollectTeam(Team.Alpha, pings);

            Assert.That(pings.Count, Is.EqualTo(1));
            Assert.That(pings[0].Id, Is.EqualTo(alpha.Id));
        }

        [Test]
        public void ClearingKeepsCountingIdsSoLateNewsCannotHitANewPing()
        {
            var relay = new TeamPingRelay(new PingSpec(minIntervalSeconds: 10f));
            relay.TryPlace(_alice, Team.Alpha, PingHit.Location(_here), null, 0f, _changes, out var old);
            _changes.Clear();

            Assert.That(relay.Clear(_changes), Is.EqualTo(1));
            Assert.That(_changes[0].Removal, Is.EqualTo(PingRemoval.Cleared));
            Assert.That(relay.Count, Is.EqualTo(0));

            Assert.That(relay.TryPlace(_alice, Team.Alpha, PingHit.Location(_here), null, 1f, _changes, out var next), Is.EqualTo(PingRefusal.None));
            Assert.That(next.Id, Is.GreaterThan(old.Id));
            Assert.That(relay.Clear(null), Is.EqualTo(1));
        }

        [Test]
        public void MismatchedHitsAreRejected()
        {
            var relay = new TeamPingRelay(new PingSpec(minIntervalSeconds: 0f));

            Assert.Throws<ArgumentException>(() => relay.TryPlace(_alice, Team.Alpha, PingHit.Enemy(0, _here), null, 0f, _changes, out _));
            Assert.Throws<ArgumentNullException>(() => relay.TryPlace(_alice, Team.Alpha, PingHit.Location(_here), null, 0f, null, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => relay.CheckInterval(_alice, float.NaN));
        }
    }
}
