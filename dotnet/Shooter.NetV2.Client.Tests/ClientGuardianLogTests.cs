using NUnit.Framework;
using Shooter.Core;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientGuardianLogTests
    {
        [Test]
        public void AShownMarkerNamesThePlayerAndTheSide()
        {
            Assert.That(ClientGuardianLog.MarkerShown(7, Side.Bravo),
                Is.EqualTo("[Net] guardian leader marker shown player=7 side=1"));
        }

        [Test]
        public void ARemovedMarkerNamesThePlayer()
        {
            Assert.That(ClientGuardianLog.MarkerRemoved(7), Is.EqualTo("[Net] guardian leader marker removed player=7"));
        }
    }
}
