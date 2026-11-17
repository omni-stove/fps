using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class CaptureTheFlagLogTests
    {
        [Test]
        public void FlagEventsNameTheFlagAndThePlayer()
        {
            Assert.That(CaptureTheFlagLog.Taken(Side.Bravo, 0), Is.EqualTo("[Mode] flag-taken flag=1 player=0"));
            Assert.That(CaptureTheFlagLog.Dropped(Side.Bravo, 0), Is.EqualTo("[Mode] flag-dropped flag=1 player=0"));
            Assert.That(CaptureTheFlagLog.Returned(Side.Alpha, 2), Is.EqualTo("[Mode] flag-returned flag=0 by=2"));
            Assert.That(CaptureTheFlagLog.Returned(Side.Alpha, null), Is.EqualTo("[Mode] flag-returned flag=0 by=-"));
            Assert.That(CaptureTheFlagLog.Captured(Side.Bravo, 0, Side.Alpha, 2d), Is.EqualTo("[Mode] flag-captured flag=1 player=0 side=0 score=2"));
        }

        [Test]
        public void TheFlagsLineShowsTheCarrierOrTheSecondsUntilReturn()
        {
            var game = new CaptureTheFlag(new CaptureTheFlagSpec(returnSeconds: 12.5f));
            game.Session.AddPlayer(1, Side.Alpha);
            game.Session.AddPlayer(3, Side.Bravo);
            game.Session.Start();

            Assert.That(CaptureTheFlagLog.Flags(game), Is.EqualTo("[Mode] flags 0=AtBase 1=AtBase"));

            game.Touch(Side.Bravo, 1);
            Assert.That(CaptureTheFlagLog.Flags(game), Is.EqualTo("[Mode] flags 0=AtBase 1=Carried(1)"));

            game.ReportKill(3, 1);
            Assert.That(CaptureTheFlagLog.Flags(game), Is.EqualTo("[Mode] flags 0=AtBase 1=Dropped(12.5)"));
            Assert.Throws<ArgumentNullException>(() => CaptureTheFlagLog.Flags(null));
        }

        [Test]
        public void TheErrandShowsTheDestinationOnlyWhenThereIsOne()
        {
            Assert.That(
                CaptureTheFlagLog.Errand(3, BotErrandKind.TakeFlag, new Vector3(4f, 0f, 12f)),
                Is.EqualTo("[Mode] errand player=3 kind=TakeFlag at=(4.00, 0.00, 12.00)"));
            Assert.That(CaptureTheFlagLog.Errand(3, BotErrandKind.None, new Vector3(4f, 0f, 12f)), Is.EqualTo("[Mode] errand player=3 kind=None"));
        }
    }
}
