using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class VehicleSeatingTests
    {
        private readonly object _alice = new object();
        private readonly object _bob = new object();

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleSeating(0));

            var seating = new VehicleSeating(2);
            Assert.Throws<ArgumentOutOfRangeException>(() => seating.IsOccupied(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => seating.Board(_alice, -1));
            Assert.Throws<ArgumentNullException>(() => seating.Board(null, 0));
            Assert.Throws<ArgumentNullException>(() => seating.SeatOf(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => seating.NextFree(-2, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => seating.NextFree(0, 0));
            Assert.Throws<ArgumentNullException>(() => seating.NearestFree(null, 1f));
            Assert.Throws<ArgumentException>(() => seating.NearestFree(new[] { 1f }, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => seating.NearestFree(new[] { 1f, 1f }, -1f));
        }

        [Test]
        public void BoardingTakesTheSeatOnlyOnce()
        {
            var seating = new VehicleSeating(2);

            Assert.That(seating.Board(_alice, 0), Is.True);
            Assert.That(seating.Board(_bob, 0), Is.False, "the seat is taken");
            Assert.That(seating.Board(_alice, 1), Is.False, "one rider sits in one seat");
            Assert.That(seating.SeatOf(_alice), Is.EqualTo(0));
            Assert.That(seating.OccupantOf(0), Is.SameAs(_alice));
            Assert.That(seating.SeatOf(_bob), Is.EqualTo(-1));
            Assert.That(seating.OccupiedCount, Is.EqualTo(1));
        }

        [Test]
        public void LeavingFreesTheSeat()
        {
            var seating = new VehicleSeating(2);
            seating.Board(_alice, 1);

            Assert.That(seating.Leave(_alice), Is.EqualTo(1));
            Assert.That(seating.Leave(_alice), Is.EqualTo(-1));
            Assert.That(seating.IsOccupied(1), Is.False);
            Assert.That(seating.OccupiedCount, Is.Zero);
        }

        [Test]
        public void MovingNeedsAFreeOtherSeat()
        {
            var seating = new VehicleSeating(3);
            seating.Board(_alice, 0);
            seating.Board(_bob, 1);

            Assert.That(seating.MoveTo(_alice, 1), Is.False, "taken");
            Assert.That(seating.MoveTo(_alice, 0), Is.False, "already there");
            Assert.That(seating.MoveTo(new object(), 2), Is.False, "not aboard");
            Assert.That(seating.MoveTo(_alice, 2), Is.True);
            Assert.That(seating.SeatOf(_alice), Is.EqualTo(2));
            Assert.That(seating.IsOccupied(0), Is.False);
        }

        [Test]
        public void NextFreeCyclesAndSkipsTakenSeats()
        {
            var seating = new VehicleSeating(4);
            seating.Board(_alice, 1);
            seating.Board(_bob, 2);

            Assert.That(seating.NextFree(1, 1), Is.EqualTo(3));
            Assert.That(seating.NextFree(3, 1), Is.EqualTo(0), "wraps around");
            Assert.That(seating.NextFree(1, -1), Is.EqualTo(0));
            Assert.That(seating.NextFree(0, -1), Is.EqualTo(3), "wraps around backwards");
            Assert.That(seating.NextFree(-1, 1), Is.EqualTo(0));
            Assert.That(seating.NextFree(-1, -1), Is.EqualTo(3));
        }

        [Test]
        public void NextFreeIsMinusOneWhenEveryOtherSeatIsTaken()
        {
            var seating = new VehicleSeating(2);
            seating.Board(_alice, 0);
            seating.Board(_bob, 1);

            Assert.That(seating.NextFree(0, 1), Is.EqualTo(-1));
            Assert.That(new VehicleSeating(1).NextFree(0, 1), Is.EqualTo(-1), "the current seat is never chosen");
        }

        [Test]
        public void NearestFreePicksTheClosestReachableEmptySeat()
        {
            var seating = new VehicleSeating(4);
            seating.Board(_alice, 1);

            Assert.That(seating.NearestFree(new[] { 2f, 0.5f, 1.5f, float.NaN }, 2.5f), Is.EqualTo(2), "seat 1 is taken and seat 3 is unreachable");
            Assert.That(seating.NearestFree(new[] { 2f, 0.5f, 2f, 2f }, 2.5f), Is.EqualTo(0), "ties go to the lower seat");
            Assert.That(seating.NearestFree(new[] { 3f, 0.5f, 3f, 3f }, 2.5f), Is.EqualTo(-1), "too far");
        }

        [Test]
        public void ClearReturnsEveryoneInSeatOrder()
        {
            var seating = new VehicleSeating(3);
            seating.Board(_bob, 2);
            seating.Board(_alice, 0);

            Assert.That(seating.Clear(), Is.EqualTo(new[] { _alice, _bob }));
            Assert.That(seating.OccupiedCount, Is.Zero);
        }

        [Test]
        public void LogLinesStartWithTheTag()
        {
            Assert.That(VehicleLog.Board("Jeep", "Player", 0, VehicleSeatRole.Driver), Is.EqualTo("[Vehicle] 'Jeep' board rider=Player seat=0 role=Driver"));
            Assert.That(
                VehicleLog.BoardRefused("Jeep", "Player", VehicleLog.ReasonNoFreeSeat),
                Is.EqualTo("[Vehicle] 'Jeep' board refused rider=Player reason=no_free_seat"));
            Assert.That(
                VehicleLog.Switch("Jeep", "Player", 0, 2, VehicleSeatRole.Gunner),
                Is.EqualTo("[Vehicle] 'Jeep' switch rider=Player from=0 to=2 role=Gunner"));
            Assert.That(
                VehicleLog.Leave("Jeep", "Player", 0, VehicleLeaveReason.Request, 1),
                Is.EqualTo("[Vehicle] 'Jeep' leave rider=Player seat=0 reason=Request exit=1"));
            Assert.That(
                VehicleLog.Leave("Jeep", "Player", 0, VehicleLeaveReason.Wrecked, -1),
                Is.EqualTo("[Vehicle] 'Jeep' leave rider=Player seat=0 reason=Wrecked exit=-"));
            Assert.That(
                VehicleLog.LeaveRefused("Jeep", "Player", VehicleLog.ReasonExitBlocked),
                Is.EqualTo("[Vehicle] 'Jeep' leave refused rider=Player reason=exit_blocked"));
            Assert.That(VehicleLog.Wrecked("Jeep", 2), Is.EqualTo("[Vehicle] 'Jeep' wrecked riders=2"));
            Assert.Throws<ArgumentOutOfRangeException>(() => VehicleLog.Wrecked("Jeep", -1));
        }
    }
}
