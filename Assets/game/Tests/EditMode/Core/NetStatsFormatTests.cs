using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class NetStatsFormatTests
    {
        [TestCase(0, NetPingLevel.Good)]
        [TestCase(149, NetPingLevel.Good)]
        [TestCase(150, NetPingLevel.Warning)]
        [TestCase(200, NetPingLevel.Warning)]
        [TestCase(299, NetPingLevel.Warning)]
        [TestCase(300, NetPingLevel.Bad)]
        [TestCase(int.MaxValue, NetPingLevel.Bad)]
        public void PingLevelUsesTheThresholds(int ping, NetPingLevel expected)
        {
            Assert.That(NetStatsFormat.PingLevel(ping), Is.EqualTo(expected));
        }

        [Test]
        public void UnknownPingIsNotColored()
        {
            Assert.That(NetStatsFormat.PingLevel(null), Is.EqualTo(NetPingLevel.Good));
        }

        [Test]
        public void TextShowsEveryValue()
        {
            Assert.That(NetStatsFormat.Text(123, 4, 5678), Is.EqualTo("Ping 123 ms  ロス 4%  tick 5678"));
        }

        [Test]
        public void TextShowsUnknownValuesAsDashes()
        {
            Assert.That(NetStatsFormat.Text(null, null, null), Is.EqualTo("Ping -- ms  ロス --%  tick --"));
        }

        [Test]
        public void TextClampsOutOfRangeValues()
        {
            Assert.That(NetStatsFormat.Text(-5, 150, 0), Is.EqualTo("Ping 0 ms  ロス 100%  tick 0"));
            Assert.That(NetStatsFormat.Text(10, -3, 1), Is.EqualTo("Ping 10 ms  ロス 0%  tick 1"));
        }

        [Test]
        public void PingFromRttClampsToInt()
        {
            Assert.That(NetStatsFormat.PingFromRtt(0UL), Is.EqualTo(0));
            Assert.That(NetStatsFormat.PingFromRtt(200UL), Is.EqualTo(200));
            Assert.That(NetStatsFormat.PingFromRtt(ulong.MaxValue), Is.EqualTo(int.MaxValue));
        }
    }
}
