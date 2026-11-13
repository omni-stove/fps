using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class NetDownedLogTests
    {
        [Test]
        public void FormatsDowned()
        {
            Assert.That(NetDownedLog.Downed(2, 1), Is.EqualTo("[Net] downed attacker=2 victim=1"));
        }

        [Test]
        public void FormatsRevived()
        {
            Assert.That(NetDownedLog.Revived(0, 1), Is.EqualTo("[Net] revived reviver=0 target=1"));
        }

        [Test]
        public void FormatsBleedoutWithAndWithoutAKiller()
        {
            Assert.That(NetDownedLog.Bleedout(1, 2), Is.EqualTo("[Net] bleedout player=1 killer=2"));

            // ホスト (0) に倒されたことと、倒した相手がいないことを取り違えない
            Assert.That(NetDownedLog.Bleedout(1, 0), Is.EqualTo("[Net] bleedout player=1 killer=0"));
            Assert.That(NetDownedLog.Bleedout(1, null), Is.EqualTo("[Net] bleedout player=1 killer=none"));
        }

        [Test]
        public void FormatsRejectedRevive()
        {
            Assert.That(
                NetDownedLog.Rejected(0, 3, ReviveRejection.TooFar),
                Is.EqualTo("[Net] rejected revive reviver=0 target=3 reason=TooFar"));
            Assert.That(NetDownedLog.Rejected(0, 1, ReviveRejection.Enemy), Does.EndWith(" reason=Enemy"));
        }

        [Test]
        public void FormatsTheRules()
        {
            var actions = new ActionRules(Downed.Revive, 15f, 0.3f, 3f, Cover.Off, BlindFire.Off, ActiveReload.Off);

            Assert.That(
                NetDownedLog.Settings(actions, 1.5f),
                Is.EqualTo("[Net] downed rules downed=Revive bleedout=15.00s health=0.30 revive=3.00s range=1.50m"));
            Assert.That(NetDownedLog.Settings(ActionRules.Default, 1.5f), Does.StartWith("[Net] downed rules downed=Off "));
            Assert.Throws<ArgumentNullException>(() => NetDownedLog.Settings(null, 1.5f));
        }
    }
}
