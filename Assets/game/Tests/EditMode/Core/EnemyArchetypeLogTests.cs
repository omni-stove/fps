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

        [Test]
        public void ChargerLinesAreTaggedAsAi()
        {
            Assert.That(
                ChargerLog.Phase("Charger", ChargerPhase.Ready, ChargerPhase.WindingUp),
                Is.EqualTo("[AI] id=Charger charger phase=WindingUp prev=Ready"));
            Assert.That(ChargerLog.Windup("Charger", "Player", 0.9f), Is.EqualTo("[AI] id=Charger charger windup target=Player seconds=0.90"));
            Assert.That(
                ChargerLog.Charge("Charger", new Vector3(0.6f, 0f, 0.8f), 11f),
                Is.EqualTo("[AI] id=Charger charger charge dir=(0.60, 0.80) distance=11.0"));
            Assert.That(ChargerLog.Hit("Charger", "Player", 35, 4.3f), Is.EqualTo("[AI] id=Charger charger hit target=Player dmg=35 travelled=4.3"));
            Assert.That(
                ChargerLog.Missed("Charger", ChargeEnd.Obstacle, 6f, 1.8f),
                Is.EqualTo("[AI] id=Charger charger missed cause=obstacle travelled=6.0 stun=1.80"));
            Assert.That(ChargerLog.Missed("Charger", ChargeEnd.Spent, 11f, 1.8f), Is.EqualTo("[AI] id=Charger charger missed cause=spent travelled=11.0 stun=1.80"));
            Assert.That(ChargerLog.Blocked("Charger", "Wall_North"), Is.EqualTo("[AI] id=Charger charger blocked by=Wall_North"));
            Assert.That(ChargerLog.Blocked("Charger", null), Is.EqualTo("[AI] id=Charger charger blocked by=unknown"));
            Assert.That(ChargerLog.Cancelled("Charger", "lost-sight"), Is.EqualTo("[AI] id=Charger charger cancelled reason=lost-sight"));
            Assert.That(ChargerLog.Ready("Charger"), Is.EqualTo("[AI] id=Charger charger ready"));
        }

        [Test]
        public void BomberLinesAreTaggedAsAi()
        {
            Assert.That(
                BomberLog.Phase("Bomber", BomberPhase.Approaching, BomberPhase.Fusing),
                Is.EqualTo("[AI] id=Bomber bomber phase=Fusing prev=Approaching"));
            Assert.That(BomberLog.Fuse("Bomber", "Player", 1.5f), Is.EqualTo("[AI] id=Bomber bomber fuse target=Player seconds=1.50"));
            Assert.That(
                BomberLog.Exploded("Bomber", BomberLog.CauseFuse, 4.5f, 1, 0),
                Is.EqualTo("[AI] id=Bomber bomber exploded cause=fuse radius=4.5 damaged=1 killed=0"));
            Assert.That(
                BomberLog.Exploded("Bomber", BomberLog.CauseKilled, 2.5f, 0, 0),
                Is.EqualTo("[AI] id=Bomber bomber exploded cause=killed radius=2.5 damaged=0 killed=0"));
            Assert.That(BomberLog.Hit("Bomber", "Player", 80, 1.3f), Is.EqualTo("[AI] id=Bomber bomber hit target=Player dmg=80 distance=1.3"));
        }
    }
}
