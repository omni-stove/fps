using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ServerEventLogPingTests
    {
        private const string _matchId = "6c1f3b2a9d0e4f5a8b7c6d5e4f3a2b1c";
        private static readonly DateTime _time = new DateTime(2026, 11, 13, 9, 0, 41, 870, DateTimeKind.Utc);

        [Test]
        public void PingPlacedNamesThePlayerTeamAndTarget()
        {
            Assert.That(
                ServerEventLog.PingPlaced(_time, _matchId, 1, null, Team.Alpha, 4, PingKind.Enemy, 2, false),
                Is.EqualTo("{\"type\":\"ping_placed\",\"time\":\"2026-11-13T09:00:41.870Z\",\"matchId\":\"" + _matchId
                    + "\",\"playerId\":\"1\",\"team\":\"Alpha\",\"pingId\":\"4\",\"kind\":\"Enemy\",\"targetId\":\"2\",\"refreshed\":\"false\"}"));
            Assert.That(
                ServerEventLog.PingPlaced(_time, _matchId, 3, null, Team.Bravo, 5, PingKind.Location, null, true),
                Is.EqualTo("{\"type\":\"ping_placed\",\"time\":\"2026-11-13T09:00:41.870Z\",\"matchId\":\"" + _matchId
                    + "\",\"playerId\":\"3\",\"team\":\"Bravo\",\"pingId\":\"5\",\"kind\":\"Location\",\"refreshed\":\"true\"}"));
        }

        [Test]
        public void PingPlacedRejectsAKindThatDoesNotMatchTheTarget()
        {
            Assert.Throws<ArgumentException>(() => ServerEventLog.PingPlaced(_time, _matchId, 1, null, Team.Alpha, 4, PingKind.Enemy, null, false));
            Assert.Throws<ArgumentException>(() => ServerEventLog.PingPlaced(_time, _matchId, 1, null, Team.Alpha, 4, PingKind.Location, 2, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => ServerEventLog.PingPlaced(_time, _matchId, 1, null, Team.Alpha, 4, (PingKind)9, null, false));
            Assert.Throws<ArgumentException>(() => ServerEventLog.PingPlaced(_time, null, 1, null, Team.Alpha, 4, PingKind.Location, null, false));
        }

        [Test]
        public void PingRefusedNamesTheReason()
        {
            Assert.That(
                ServerEventLog.PingRefused(_time, _matchId, 1, null, PingRefusal.TooSoon),
                Is.EqualTo("{\"type\":\"ping_refused\",\"time\":\"2026-11-13T09:00:41.870Z\",\"matchId\":\"" + _matchId
                    + "\",\"playerId\":\"1\",\"reason\":\"TooSoon\"}"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ServerEventLog.PingRefused(_time, _matchId, 1, null, PingRefusal.None));
        }

        [Test]
        public void NetLinesNameTheSharedPing()
        {
            var ping = new SharedPing(1, Team.Alpha, new PingMarker(4, PingKind.Enemy, 2, new Vector3(1f, 1.1f, 10f), true, 0f, 6f));

            Assert.That(
                PingLog.NetPlaced(ping, false, 2),
                Is.EqualTo("[Ping] net placed id=4 player=1 team=Alpha kind=Enemy target=2 at=(1.00, 1.10, 10.00) life=6.00 refreshed=false recipients=2"));
            Assert.That(PingLog.NetRefused(1, PingRefusal.TooSoon), Is.EqualTo("[Ping] net refused player=1 reason=TooSoon"));
            Assert.That(PingLog.NetLost(ping), Is.EqualTo("[Ping] net lost id=4 player=1 at=(1.00, 1.10, 10.00)"));
            Assert.That(PingLog.NetRemoved(ping, PingRemoval.Expired), Is.EqualTo("[Ping] net removed id=4 player=1 reason=Expired"));
            Assert.That(
                PingLog.ReceivedPlaced(4, PingKind.Enemy, new Vector3(1f, 1.1f, 10f), 5.95f, true),
                Is.EqualTo("[Ping] received placed id=4 kind=Enemy at=(1.00, 1.10, 10.00) life=5.95 tracking=true"));
            Assert.That(PingLog.ReceivedLost(4, new Vector3(1f, 1.1f, 10f)), Is.EqualTo("[Ping] received lost id=4 at=(1.00, 1.10, 10.00)"));
            Assert.That(PingLog.ReceivedRemoved(4, PingRemoval.TargetGone), Is.EqualTo("[Ping] received removed id=4 reason=TargetGone"));
            Assert.That(PingLog.Shared(), Is.EqualTo("[Ping] shared"));
        }
    }
}
