using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Stealth.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AwarenessStageLogTests
    {
        [Test]
        public void TheWitnessLineCarriesTheStageNameAsTheEnumPrintsIt()
        {
            Assert.That(
                ContextMeleeLog.Witnessed("p1", "e1", "takedown", "e2", AwarenessStage.Engaged.ToString()),
                Is.EqualTo("[ContextMelee] witnessed id=p1 target=e1 move=takedown by=e2 stage=Engaged"));
            Assert.That(
                ContextMeleeLog.Witnessed("p1", "e1", "takedown", "e2", AwarenessStage.Noticed.ToString()),
                Does.EndWith(" stage=Noticed"));
        }
    }
}
