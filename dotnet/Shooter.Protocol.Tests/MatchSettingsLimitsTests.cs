using System.Text;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Protocol.Tests
{
    public sealed class MatchSettingsLimitsTests
    {
        [Test]
        public void TheLongestChatLineFitsTheChatMessage()
        {
            var longest = new string('あ', NetMatchSettings.MaxMaxChatLength);

            Assert.That(Encoding.UTF8.GetByteCount(longest), Is.LessThanOrEqualTo(MatchProtocol.MaxChatBytes));
        }

        [Test]
        public void EveryMapInThePoolFitsTheMapVote()
        {
            Assert.That(NetMatchSettings.MaxMapPoolCount, Is.EqualTo(MapVoteStateMessage.MaxCandidates));
        }
    }
}
