using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class BattleRoyaleLogTests
    {
        [Test]
        public void FormatsTheLogLines()
        {
            Assert.That(BattleRoyaleLog.Downed(2, 0), Is.EqualTo("[Mode] downed player=2 by=0"));
            Assert.That(BattleRoyaleLog.Downed(2, null), Is.EqualTo("[Mode] downed player=2 by=-"));
            Assert.That(BattleRoyaleLog.Revived(3, 2), Is.EqualTo("[Mode] revived player=2 by=3"));
            Assert.That(BattleRoyaleLog.BledOut(2, null), Is.EqualTo("[Mode] bledout player=2 credit=-"));
            Assert.That(BattleRoyaleLog.SquadWiped(3, 0), Is.EqualTo("[Mode] squad-wiped player=3 credit=0"));
            Assert.That(BattleRoyaleLog.Eliminated(new Side(2), 3, 2), Is.EqualTo("[Mode] eliminated squad=2 place=3 remaining=2"));
            Assert.That(
                BattleRoyaleLog.Errand(3, BotErrandKind.Collect, new Vector3(4f, 0f, 12f)),
                Is.EqualTo("[Mode] errand player=3 kind=Collect at=(4.00, 0.00, 12.00)"));
            Assert.That(BattleRoyaleLog.Errand(3, BotErrandKind.None, Vector3.Zero), Is.EqualTo("[Mode] errand player=3 kind=None"));
            Assert.That(
                BattleRoyaleLog.Drop(0, 60f, ParachuteOpenVerdict.Open),
                Is.EqualTo("[Mode] drop player=0 height=60.00 parachute=Open"));
            Assert.That(
                BattleRoyaleLog.Drop(0, 60f, ParachuteOpenVerdict.RuleOff),
                Is.EqualTo("[Mode] drop player=0 height=60.00 parachute=RuleOff"));
        }
    }
}
