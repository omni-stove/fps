using NUnit.Framework;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientGunGameLogTests
    {
        [Test]
        public void ALadderThatCannotBeShownNamesTheLadderAndTheReason()
        {
            Assert.That(ClientGunGameLog.LadderNotShown("GunGameLadder", "tier 2 has no weapon"),
                Is.EqualTo("[Net] gun game ladder 'GunGameLadder' cannot be shown: tier 2 has no weapon"));
        }

        [Test]
        public void AMissingLadderSaysTheHudDoesNotShowTheTiers()
        {
            Assert.That(ClientGunGameLog.LadderMissing(),
                Is.EqualTo("[Net] the server plays Gun Game but GameContent has no gun game ladder; the HUD does not show the tiers"));
        }

        [Test]
        public void ALadderMismatchSaysTheHudShowsTheLastLocalTier()
        {
            Assert.That(ClientGunGameLog.LadderMismatch(),
                Is.EqualTo("[Net] the server's gun game has more tiers than the local ladder; the HUD shows the last local tier"));
        }
    }
}
