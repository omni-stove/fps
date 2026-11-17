using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class MenuReturnReasonTests
    {
        [Test]
        public void ADisconnectKeepsTheNoticeAndTheExitCode()
        {
            var notice = DisconnectNotice.Classify(DisconnectNotice.ServerStoppedReason, true, "0.1.0");

            var reason = MenuReturnReason.Disconnected(notice, 0);

            Assert.That(reason.Kind, Is.EqualTo(MenuReturnKind.Disconnected));
            Assert.That(reason.Disconnect, Is.SameAs(notice));
            Assert.That(reason.ExitCode, Is.EqualTo(0));
            Assert.That(reason.Detail, Is.Empty);
            Assert.That(reason.Describe(), Is.EqualTo(
                "return=Disconnected exit=0 kind=ServerStopped connected=True reason=\"shooter:server-stopped\""));
            Assert.Throws<ArgumentNullException>(() => MenuReturnReason.Disconnected(null, 0));
        }

        [Test]
        public void AFailedStartKeepsItsDetailAndTheExitCode()
        {
            var reason = MenuReturnReason.OnlineFailed(7, "[QuickMatch] no lobby was found");

            Assert.That(reason.Kind, Is.EqualTo(MenuReturnKind.OnlineFailed));
            Assert.That(reason.Disconnect, Is.Null);
            Assert.That(reason.ExitCode, Is.EqualTo(7));
            Assert.That(reason.Detail, Is.EqualTo("[QuickMatch] no lobby was found"));
            Assert.That(reason.ToString(), Is.EqualTo("return=OnlineFailed exit=7 detail=\"[QuickMatch] no lobby was found\""));
            Assert.That(MenuReturnReason.OnlineFailed(3, null).Detail, Is.Empty);
            Assert.That(MenuReturnReason.OnlineFailed(3, null).Describe(), Is.EqualTo("return=OnlineFailed exit=3"));
        }
    }
}
