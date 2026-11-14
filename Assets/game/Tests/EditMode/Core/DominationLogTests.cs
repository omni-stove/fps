using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class DominationLogTests
    {
        // 拠点 1 つ、1 秒で占領、1 秒に 10 点、30 点で勝ち、制限時間 60 秒。
        private static Domination OnePointMatch()
        {
            var domination = new Domination(new DominationSpec(1, 1f, 10f, 30, 60f));
            domination.AddPlayer(0, Team.Alpha);
            domination.AddPlayer(1, Team.Bravo);
            return domination;
        }

        [Test]
        public void StateWritesTheFlooredScoresAndRemainingTime()
        {
            var domination = OnePointMatch();
            Assert.That(DominationLog.State(domination), Is.EqualTo("[Domination] state=Waiting A=0 B=0 remaining=60.0"));

            domination.Start();
            domination.SetPlayerPoint(0, 0);
            domination.Tick(1f);
            domination.Tick(0.59f);

            Assert.That(DominationLog.State(domination), Is.EqualTo("[Domination] state=Playing A=5 B=0 remaining=58.4"));
        }

        [Test]
        public void EndedWritesTheWinnerOrDraw()
        {
            var domination = OnePointMatch();
            domination.Start();
            domination.SetPlayerPoint(0, 0);
            domination.Tick(1f);
            domination.Tick(3f);

            Assert.That(domination.Winner, Is.EqualTo(Team.Alpha));
            Assert.That(DominationLog.Ended(domination), Is.EqualTo("[Domination] ended winner=Alpha A=30 B=0"));

            var draw = OnePointMatch();
            draw.Start();
            draw.Tick(60f);
            Assert.That(DominationLog.Ended(draw), Is.EqualTo("[Domination] ended winner=draw A=0 B=0"));
        }

        [Test]
        public void PointEventsUseThePointLetter()
        {
            Assert.That(DominationLog.Captured(0, Team.Alpha), Is.EqualTo("[Domination] captured point=A team=Alpha"));
            Assert.That(DominationLog.Captured(2, Team.Bravo), Is.EqualTo("[Domination] captured point=C team=Bravo"));
            Assert.That(DominationLog.Neutralized(1, Team.Bravo), Is.EqualTo("[Domination] neutralized point=B from=Bravo"));
        }

        [Test]
        public void ParticipantLinesKeepValuesWithoutSpaces()
        {
            Assert.That(DominationLog.Joined(1, Team.Bravo, "Enemy 1"), Is.EqualTo("[Domination] joined player=1 team=Bravo name=Enemy_1"));
            Assert.That(DominationLog.Joined(0, Team.Alpha, " "), Is.EqualTo("[Domination] joined player=0 team=Alpha name=-"));
            Assert.That(DominationLog.Kill(0, 1), Is.EqualTo("[Domination] kill killer=0 victim=1"));
            Assert.That(DominationLog.Death(1), Is.EqualTo("[Domination] death player=1"));
            Assert.That(
                DominationLog.Respawn(1, Team.Bravo, new Vector3(0f, 0f, 20f)),
                Is.EqualTo("[Domination] respawn player=1 team=Bravo at=(0.00, 0.00, 20.00)"));
            Assert.That(DominationLog.Restart(), Is.EqualTo("[Domination] restart"));
        }

        [Test]
        public void RejectsNullAndUnknownTeams()
        {
            Assert.Throws<ArgumentNullException>(() => DominationLog.State(null));
            Assert.Throws<ArgumentNullException>(() => DominationLog.Ended(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => DominationLog.Captured(0, (Team)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => DominationLog.Captured(-1, Team.Alpha));
        }

        [Test]
        public void HudFormatNamesPointsAndOwners()
        {
            Assert.That(DominationHudFormat.PointName(0), Is.EqualTo("A"));
            Assert.That(DominationHudFormat.PointName(25), Is.EqualTo("Z"));
            Assert.That(DominationHudFormat.PointName(26), Is.EqualTo("P27"));
            Assert.Throws<ArgumentOutOfRangeException>(() => DominationHudFormat.PointName(-1));

            Assert.That(GameText.Resolve(DominationHudFormat.PointOwner(0, Team.Bravo)), Is.EqualTo("A BRAVO"));
            Assert.That(GameText.Resolve(DominationHudFormat.PointOwner(1, null)), Is.EqualTo("B --"));
            Assert.That(GameText.Resolve(DominationHudFormat.PointOwner(26, null)), Is.EqualTo("P27 --"));
        }

        [Test]
        public void HudFormatFloorsScores()
        {
            Assert.That(DominationHudFormat.DisplayedScore(12.99f), Is.EqualTo(12));
            Assert.That(DominationHudFormat.DisplayedScore(0f), Is.EqualTo(0));
            Assert.That(DominationHudFormat.DisplayedScore(-3f), Is.EqualTo(0));
            Assert.That(DominationHudFormat.DisplayedScore(float.NaN), Is.EqualTo(0));
            Assert.That(DominationHudFormat.DisplayedScore(float.PositiveInfinity), Is.EqualTo(int.MaxValue));
            Assert.That(GameText.Resolve(DominationHudFormat.TeamScore(Team.Alpha, 12.9f)), Is.EqualTo("ALPHA 12"));
        }
    }
}
