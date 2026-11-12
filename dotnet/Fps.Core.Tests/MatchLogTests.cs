using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class MatchLogTests
    {
        [Test]
        public void FormatsScore()
        {
            Assert.That(MatchLog.Score(3, 1), Is.EqualTo("[Match] score A=3 B=1"));
        }

        [Test]
        public void FormatsEndedWithWinner()
        {
            Assert.That(MatchLog.Ended(Team.Alpha), Is.EqualTo("[Match] ended winner=Alpha"));
            Assert.That(MatchLog.Ended(Team.Bravo), Is.EqualTo("[Match] ended winner=Bravo"));
        }

        [Test]
        public void FormatsEndedInDraw()
        {
            Assert.That(MatchLog.Ended(null), Is.EqualTo("[Match] ended winner=draw"));
        }

        [Test]
        public void FormatsJoinKillAndSpawn()
        {
            Assert.That(MatchLog.Joined(2, Team.Bravo), Is.EqualTo("[Match] joined player=2 team=Bravo"));
            Assert.That(MatchLog.Kill(1, 2), Is.EqualTo("[Match] kill killer=1 victim=2"));
            Assert.That(MatchLog.Spawn(2, new SpawnPoint(new Vector3(1f, 0f, -20.5f))),
                Is.EqualTo("[Match] spawn player=2 at=(1.00, 0.00, -20.50)"));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MatchLog.Ended((Team)99));
            Assert.Throws<ArgumentNullException>(() => MatchLog.Spawn(1, null));
        }
    }
}
