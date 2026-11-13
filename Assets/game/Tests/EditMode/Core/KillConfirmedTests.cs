using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class KillConfirmedTests
    {
        private const int _alpha1 = 1;
        private const int _alpha2 = 2;
        private const int _bravo1 = 3;
        private const int _bravo2 = 4;
        private const float _tagLifetime = 10f;

        private static readonly string[] _expectedHudLines = { "確定 ALPHA 1 / BRAVO 0", "阻止 ALPHA 0 / BRAVO 1", "落ちているタグ 1" };

        [Test]
        public void SpecRejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new KillConfirmedSpec(0, 300f, 3f, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KillConfirmedSpec(10, 0f, 3f, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KillConfirmedSpec(10, float.PositiveInfinity, 3f, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KillConfirmedSpec(10, 300f, -0.1f, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KillConfirmedSpec(10, 300f, 3f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KillConfirmedSpec(10, 300f, 3f, float.NaN));
            Assert.Throws<ArgumentNullException>(() => new KillConfirmed(null));
        }

        [Test]
        public void AKillAloneScoresNothing()
        {
            var mode = Start();

            mode.Session.ReportKill(_alpha1, _bravo1);
            mode.Session.Tick(1f);

            Assert.That(mode.Session.GetKills(_alpha1), Is.EqualTo(1));
            Assert.That(mode.Session.GetScore(Side.Alpha), Is.Zero);
            Assert.That(mode.Session.GetScore(Side.Bravo), Is.Zero);
        }

        [Test]
        public void AnEnemyCollectingTheTagConfirmsTheKillForTheirTeam()
        {
            var mode = Start();
            mode.Session.ReportKill(_alpha1, _bravo1);
            var tag = mode.DropTag(_bravo1);

            Assert.That(tag.OwnerId, Is.EqualTo(_bravo1));
            Assert.That(tag.OwnerSide, Is.EqualTo(Side.Bravo));
            Assert.That(tag.SecondsLeft, Is.EqualTo(_tagLifetime));
            Assert.That(mode.Tags, Has.Count.EqualTo(1));

            // 倒した本人でなくても、敵のチームの誰が拾っても確定する。
            Assert.That(mode.Collect(tag.Id, _alpha2), Is.EqualTo(KillConfirmedResult.Confirmed));

            Assert.That(mode.Session.GetScore(Side.Alpha), Is.EqualTo(KillConfirmed.PointsPerConfirm));
            Assert.That(mode.Session.GetScore(Side.Bravo), Is.Zero);
            Assert.That(mode.GetConfirmed(Side.Alpha), Is.EqualTo(1));
            Assert.That(mode.GetDenied(Side.Alpha), Is.Zero);
            Assert.That(mode.Tags, Is.Empty);
            Assert.That(tag.SecondsLeft, Is.Zero);
        }

        [Test]
        public void AnAllyCollectingTheTagDeniesItWithoutScoring()
        {
            var mode = Start();
            mode.Session.ReportKill(_alpha1, _bravo1);
            var tag = mode.DropTag(_bravo1);

            Assert.That(mode.Collect(tag.Id, _bravo2), Is.EqualTo(KillConfirmedResult.Denied));

            Assert.That(mode.Session.GetScore(Side.Alpha), Is.Zero);
            Assert.That(mode.Session.GetScore(Side.Bravo), Is.Zero);
            Assert.That(mode.GetDenied(Side.Bravo), Is.EqualTo(1));
            Assert.That(mode.GetConfirmed(Side.Alpha), Is.Zero);
            Assert.That(mode.Tags, Is.Empty);
            Assert.That(mode.Collect(tag.Id, _alpha1), Is.EqualTo(KillConfirmedResult.Gone), "a denied tag cannot be confirmed afterwards");
        }

        [Test]
        public void TheOwnerCanDenyTheirOwnTagAfterRespawning()
        {
            var mode = Start(respawnSeconds: 1f);
            mode.Session.ReportKill(_alpha1, _bravo1);
            var tag = mode.DropTag(_bravo1);

            Assert.That(mode.Collect(tag.Id, _bravo1), Is.EqualTo(KillConfirmedResult.CollectorDown), "the owner is still down");
            Assert.That(mode.Tags, Has.Count.EqualTo(1));

            mode.Session.Tick(1f);
            Assert.That(mode.Collect(tag.Id, _bravo1), Is.EqualTo(KillConfirmedResult.Denied));
            Assert.That(mode.GetDenied(Side.Bravo), Is.EqualTo(1));
        }

        [Test]
        public void ATagThatNobodyCollectsExpires()
        {
            var mode = Start();
            var expired = new List<int>();
            mode.TagExpired += tag => expired.Add(tag.Id);
            var first = mode.DropTag(_bravo1);
            mode.Session.Tick(4f);
            var second = mode.DropTag(_alpha1);

            mode.Session.Tick(_tagLifetime - 4f - 0.5f);
            Assert.That(expired, Is.Empty);
            Assert.That(first.SecondsLeft, Is.EqualTo(0.5f).Within(1e-4f));

            mode.Session.Tick(0.5f);
            Assert.That(expired, Is.EqualTo(new[] { first.Id }));
            Assert.That(mode.Tags, Is.EqualTo(new[] { second }));
            Assert.That(mode.Collect(first.Id, _alpha1), Is.EqualTo(KillConfirmedResult.Gone));

            mode.Session.Tick(4f);
            Assert.That(expired, Is.EqualTo(new[] { first.Id, second.Id }));
            Assert.That(mode.Tags, Is.Empty);
            Assert.That(mode.Session.GetScore(Side.Alpha), Is.Zero, "an expired tag scores for nobody");
            Assert.That(mode.Session.GetScore(Side.Bravo), Is.Zero);
        }

        [Test]
        public void SmallTicksStillExpireATagOnTime()
        {
            var mode = Start();
            var expiredCount = 0;
            mode.TagExpired += tag => expiredCount++;
            mode.DropTag(_bravo1);

            for (var i = 0; i < 100; i++)
            {
                mode.Session.Tick(_tagLifetime / 100f);
            }

            Assert.That(expiredCount, Is.EqualTo(1));
        }

        [Test]
        public void ReachingTheScoreLimitByConfirmingEndsTheMatch()
        {
            var mode = Start(scoreLimit: 2);
            var first = mode.DropTag(_bravo1);
            var second = mode.DropTag(_bravo2);
            var leftover = mode.DropTag(_alpha1);
            var expired = 0;
            mode.TagExpired += tag => expired++;

            mode.Collect(first.Id, _alpha1);
            Assert.That(mode.Session.State, Is.EqualTo(GameModeState.Playing));

            Assert.That(mode.Collect(second.Id, _alpha2), Is.EqualTo(KillConfirmedResult.Confirmed));
            Assert.That(mode.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(mode.Session.Winner, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(mode.Tags, Is.Empty, "tags left on the field go away with the match");
            Assert.That(leftover.SecondsLeft, Is.Zero);
            Assert.That(expired, Is.Zero, "clearing at the end is not an expiry");
        }

        [Test]
        public void NothingHappensOutsideOfPlay()
        {
            var mode = new KillConfirmed(new KillConfirmedSpec(10, 300f, 3f, _tagLifetime));
            mode.Session.AddPlayer(_alpha1, Side.Alpha);
            mode.Session.AddPlayer(_bravo1, Side.Bravo);

            Assert.That(mode.CanDropTag(_bravo1), Is.False);
            Assert.That(mode.DropTag(_bravo1), Is.Null, "no tag before the start");

            mode.Session.Start();
            var tag = mode.DropTag(_bravo1);
            mode.Session.Tick(300f);

            Assert.That(mode.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(mode.DropTag(_alpha1), Is.Null);
            Assert.That(mode.Collect(tag.Id, _alpha1), Is.EqualTo(KillConfirmedResult.NotPlaying));
            Assert.That(mode.GetConfirmed(Side.Alpha), Is.Zero);
        }

        [Test]
        public void TheTimeLimitGoesToTheTeamWithMoreConfirms()
        {
            var mode = Start();
            var tag = mode.DropTag(_alpha1);
            mode.Collect(tag.Id, _bravo1);

            mode.Session.Tick(300f);

            Assert.That(mode.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(mode.Session.Winner, Is.EqualTo((Side?)Side.Bravo));
        }

        [Test]
        public void UnknownPlayersAreRejected()
        {
            var mode = Start();
            var tag = mode.DropTag(_bravo1);

            Assert.Throws<ArgumentOutOfRangeException>(() => mode.DropTag(99));
            Assert.Throws<ArgumentOutOfRangeException>(() => mode.CanDropTag(99));
            Assert.Throws<ArgumentOutOfRangeException>(() => mode.Collect(tag.Id, 99));
            Assert.That(mode.Tags, Has.Count.EqualTo(1), "a rejected collector leaves the tag on the field");
        }

        [Test]
        public void ADiscardedTagCountsForNobody()
        {
            var mode = Start();
            var tag = mode.DropTag(_bravo1);

            Assert.That(mode.Discard(tag.Id), Is.True);
            Assert.That(mode.Discard(tag.Id), Is.False);
            Assert.That(mode.Tags, Is.Empty);
            Assert.That(mode.GetConfirmed(Side.Alpha), Is.Zero);
            Assert.That(mode.GetDenied(Side.Bravo), Is.Zero);
            Assert.That(mode.FindTag(tag.Id), Is.Null);
        }

        [Test]
        public void EveryDeathDropsItsOwnTag()
        {
            var mode = Start();

            var first = mode.DropTag(_bravo1);
            var second = mode.DropTag(_bravo1);

            Assert.That(second.Id, Is.Not.EqualTo(first.Id));
            Assert.That(mode.FindTag(first.Id), Is.SameAs(first));
            Assert.That(mode.FindTag(second.Id), Is.SameAs(second));
        }

        [Test]
        public void ResetClearsTagsAndCounts()
        {
            var mode = Start();
            var confirmed = mode.DropTag(_bravo1);
            mode.Collect(confirmed.Id, _alpha1);
            var left = mode.DropTag(_alpha1);

            mode.Reset();

            Assert.That(mode.Session.State, Is.EqualTo(GameModeState.Waiting));
            Assert.That(mode.Tags, Is.Empty);
            Assert.That(left.SecondsLeft, Is.Zero);
            Assert.That(mode.GetConfirmed(Side.Alpha), Is.Zero);
            Assert.That(mode.Session.GetScore(Side.Alpha), Is.Zero);

            mode.Session.Start();
            Assert.That(mode.DropTag(_bravo1).Id, Is.EqualTo(1), "tag numbers start over");
        }

        [Test]
        public void TheHudShowsConfirmsDenialsAndTagsOnTheField()
        {
            var mode = Start();
            var confirmed = mode.DropTag(_bravo1);
            mode.Collect(confirmed.Id, _alpha1);
            var denied = mode.DropTag(_bravo2);
            mode.Collect(denied.Id, _bravo1);
            mode.DropTag(_alpha1);

            Assert.That(KillConfirmedText.HudLines(mode), Is.EqualTo(_expectedHudLines));
            Assert.Throws<ArgumentNullException>(() => KillConfirmedText.HudLines(null));
        }

        [Test]
        public void TheLogsNameTheTagAndTheCollector()
        {
            var mode = Start();
            var tag = mode.DropTag(_bravo1);

            Assert.That(KillConfirmedText.DropLog(tag), Is.EqualTo("[Mode] killconfirmed drop tag=1 owner=3 side=1"));
            Assert.That(
                KillConfirmedText.CollectLog(tag, _alpha1, Side.Alpha, KillConfirmedResult.Confirmed),
                Is.EqualTo("[Mode] killconfirmed confirm tag=1 owner=3 side=1 by=1 by-side=0"));
            Assert.That(
                KillConfirmedText.CollectLog(tag, _bravo2, Side.Bravo, KillConfirmedResult.Denied),
                Is.EqualTo("[Mode] killconfirmed deny tag=1 owner=3 side=1 by=4 by-side=1"));
            Assert.That(
                KillConfirmedText.CollectLog(tag, _alpha1, Side.Alpha, KillConfirmedResult.Gone),
                Is.EqualTo("[Mode] killconfirmed ignore tag=1 owner=3 side=1 by=1 by-side=0 reason=Gone"));
            Assert.That(KillConfirmedText.ExpireLog(tag), Is.EqualTo("[Mode] killconfirmed expire tag=1 owner=3 side=1"));
            Assert.That(KillConfirmedText.DiscardLog(tag, null), Is.EqualTo("[Mode] killconfirmed discard tag=1 owner=3 side=1 by=-"));
        }

        private static KillConfirmed Start(int scoreLimit = 10, float respawnSeconds = 3f)
        {
            var mode = new KillConfirmed(new KillConfirmedSpec(scoreLimit, 300f, respawnSeconds, _tagLifetime));
            mode.Session.AddPlayer(_alpha1, Side.Alpha);
            mode.Session.AddPlayer(_alpha2, Side.Alpha);
            mode.Session.AddPlayer(_bravo1, Side.Bravo);
            mode.Session.AddPlayer(_bravo2, Side.Bravo);
            mode.Session.Start();
            return mode;
        }
    }
}
