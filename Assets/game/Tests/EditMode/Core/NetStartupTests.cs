using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class NetStartupTests
    {
        [Test]
        public void HeadlessPlayerIsBatchModeOrDedicatedServerOutsideEditor()
        {
            Assert.That(NetStartup.IsHeadlessPlayer(false, true, false), Is.True);
            Assert.That(NetStartup.IsHeadlessPlayer(false, false, true), Is.True);
            Assert.That(NetStartup.IsHeadlessPlayer(false, false, false), Is.False);
            Assert.That(NetStartup.IsHeadlessPlayer(true, true, true), Is.False);
        }

        [Test]
        public void DisablesPresentationOnlyOnDedicatedServer()
        {
            Assert.That(NetStartup.ShouldDisablePresentation(true), Is.True);
            Assert.That(NetStartup.ShouldDisablePresentation(false), Is.False);
        }

        [Test]
        public void ThisBuildIsNotADedicatedServer()
        {
            Assert.That(NetStartup.IsDedicatedServerBuild, Is.False);
        }
    }
}
