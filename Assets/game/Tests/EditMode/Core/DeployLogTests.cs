using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class DeployLogTests
    {
        [Test]
        public void EveryLineStartsWithTheTagOwnerAndKind()
        {
            Assert.That(DeployLog.Prefix("Player", "deploy-shield"), Is.EqualTo("[Deploy] owner=Player kind=deploy-shield"));
            Assert.That(DeployLog.Prefix(null, string.Empty), Is.EqualTo("[Deploy] owner=- kind=-"));
        }

        [Test]
        public void FormatsEachEvent()
        {
            Assert.That(
                DeployLog.Preview("Player", "shield", DeployVerdict.TooSteep),
                Is.EqualTo("[Deploy] owner=Player kind=shield preview verdict=TooSteep"));
            Assert.That(
                DeployLog.Placed("Player", "shield", 3, new System.Numerics.Vector3(-9.5f, 0f, -16f), 2, 2),
                Is.EqualTo("[Deploy] owner=Player kind=shield placed serial=3 pos=(-9.50, 0.00, -16.00) active=2/2"));
            Assert.That(
                DeployLog.Refused("Player", "shield", DeployVerdict.Blocked, 2, 2),
                Is.EqualTo("[Deploy] owner=Player kind=shield refused reason=Blocked charges=2/2"));
            Assert.That(
                DeployLog.LimitReached("Player", "shield", 2, 2),
                Is.EqualTo("[Deploy] owner=Player kind=shield refused reason=Limit active=2/2"));
            Assert.That(
                DeployLog.Cancelled("Player", "shield", 1, 2),
                Is.EqualTo("[Deploy] owner=Player kind=shield cancelled charges=1/2"));
            Assert.That(
                DeployLog.Blocked("Player", "shield", 1, 20, 280, "Enemy_1"),
                Is.EqualTo("[Deploy] owner=Player kind=shield serial=1 blocked dmg=20 hp=280 shooter=Enemy_1"));
            Assert.That(
                DeployLog.Blocked("Player", "shield", 1, 20, 280, null),
                Is.EqualTo("[Deploy] owner=Player kind=shield serial=1 blocked dmg=20 hp=280 shooter=-"));
            Assert.That(
                DeployLog.Removed("Player", "shield", 1, DeployRemoval.Replaced),
                Is.EqualTo("[Deploy] owner=Player kind=shield serial=1 removed reason=Replaced"));
        }
    }
}
