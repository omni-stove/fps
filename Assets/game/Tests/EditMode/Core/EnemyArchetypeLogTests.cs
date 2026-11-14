using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class EnemyArchetypeLogTests
    {
        [Test]
        public void ShieldLinesAreTaggedAsAi()
        {
            Assert.That(ShieldLog.Blocked("ShieldEnemy", 25, "Player"), Is.EqualTo("[AI] id=ShieldEnemy shield blocked dmg=25 by=Player"));
            Assert.That(ShieldLog.Blocked("ShieldEnemy", 25, null), Is.EqualTo("[AI] id=ShieldEnemy shield blocked dmg=25 by=unknown"));
            Assert.That(ShieldLog.Lowered("ShieldEnemy", "melee", 1.5f), Is.EqualTo("[AI] id=ShieldEnemy shield lowered cause=melee seconds=1.50"));
            Assert.That(ShieldLog.Raised("ShieldEnemy"), Is.EqualTo("[AI] id=ShieldEnemy shield raised"));
            Assert.That(ShieldLog.Bash("ShieldEnemy"), Is.EqualTo("[AI] id=ShieldEnemy shield bash"));
        }

        [Test]
        public void SniperLinesAreTaggedAsAi()
        {
            Assert.That(SniperLog.Phase("Sniper", SniperPhase.Holding, SniperPhase.Aiming), Is.EqualTo("[AI] id=Sniper sniper phase=Aiming prev=Holding"));
            Assert.That(SniperLog.Aim("Sniper", "Player", 2.5f), Is.EqualTo("[AI] id=Sniper sniper aim target=Player seconds=2.50"));
            Assert.That(SniperLog.AimCancelled("Sniper", 1.25f), Is.EqualTo("[AI] id=Sniper sniper aim cancelled after=1.25s"));
            Assert.That(SniperLog.Fire("Sniper", 3f), Is.EqualTo("[AI] id=Sniper sniper fire multiplier=3.00"));
            Assert.That(
                SniperLog.Relocate("Sniper", 2, new Vector3(22f, 4f, 23.5f)),
                Is.EqualTo("[AI] id=Sniper sniper relocate perch=2 to=(22.0, 4.0, 23.5)"));
            Assert.That(SniperLog.Arrived("Sniper", 2, gaveUp: false), Is.EqualTo("[AI] id=Sniper sniper arrived perch=2"));
            Assert.That(SniperLog.Arrived("Sniper", 2, gaveUp: true), Is.EqualTo("[AI] id=Sniper sniper relocate gave-up perch=2"));
            Assert.That(SniperLog.Stay("Sniper"), Is.EqualTo("[AI] id=Sniper sniper relocate none"));
        }
    }
}
