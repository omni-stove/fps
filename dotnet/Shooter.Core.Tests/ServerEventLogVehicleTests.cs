using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class ServerEventLogVehicleTests
    {
        private const string _matchId = "6c1f3b2a8d4e4f7a9b0c2e5d7a1f4c83";
        private static readonly DateTime _time = new DateTime(2026, 11, 17, 9, 0, 0, 123, DateTimeKind.Utc);

        [Test]
        public void EnteringAVehicleNamesTheSeatAndRole()
        {
            var line = ServerEventLog.VehicleEntered(_time, _matchId, 3, null, 0, "jeep", 2, VehicleSeatRole.Gunner);

            Assert.That(line, Is.EqualTo(
                "{\"type\":\"vehicle_entered\",\"time\":\"2026-11-17T09:00:00.123Z\",\"matchId\":\"" + _matchId
                + "\",\"playerId\":\"3\",\"vehicle\":\"0\",\"vehicleId\":\"jeep\",\"seat\":\"2\",\"role\":\"Gunner\"}"));
        }

        [Test]
        public void LeavingAVehicleNamesTheReason()
        {
            var line = ServerEventLog.VehicleLeft(_time, _matchId, 3, null, 1, "jeep", 0, VehicleLeaveReason.Wrecked);

            Assert.That(line, Is.EqualTo(
                "{\"type\":\"vehicle_left\",\"time\":\"2026-11-17T09:00:00.123Z\",\"matchId\":\"" + _matchId
                + "\",\"playerId\":\"3\",\"vehicle\":\"1\",\"vehicleId\":\"jeep\",\"seat\":\"0\",\"reason\":\"Wrecked\"}"));
        }

        [Test]
        public void AnExplosionNamesTheAttackerWhenThereIsOne()
        {
            var withAttacker = ServerEventLog.VehicleExploded(_time, _matchId, 0, "jeep", 2, null, 1, 2, 1);
            var withoutAttacker = ServerEventLog.VehicleExploded(_time, _matchId, 0, "jeep", null, null, 0, 0, 0);

            Assert.That(withAttacker, Is.EqualTo(
                "{\"type\":\"vehicle_exploded\",\"time\":\"2026-11-17T09:00:00.123Z\",\"matchId\":\"" + _matchId
                + "\",\"vehicle\":\"0\",\"vehicleId\":\"jeep\",\"attackerId\":\"2\",\"riders\":\"1\",\"damaged\":\"2\",\"killed\":\"1\"}"));
            Assert.That(withoutAttacker, Does.Not.Contain("attackerId"));
        }

        [Test]
        public void InvalidValuesAreRefused()
        {
            Assert.That(
                () => ServerEventLog.VehicleEntered(_time, _matchId, 3, null, -1, "jeep", 0, VehicleSeatRole.Driver),
                Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => ServerEventLog.VehicleLeft(_time, _matchId, 3, null, 0, "", 0, VehicleLeaveReason.Request),
                Throws.ArgumentException);
            Assert.That(
                () => ServerEventLog.VehicleExploded(_time, _matchId, 0, "jeep", null, null, -1, 0, 0),
                Throws.InstanceOf<ArgumentOutOfRangeException>());
        }
    }
}
