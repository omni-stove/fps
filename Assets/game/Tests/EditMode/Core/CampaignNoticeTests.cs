using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class CampaignNoticeTests
    {
        private static HudNotice Saved => HudNotice.CheckpointSaved;

        private static List<HudNotice> Record(CampaignNotice notice)
        {
            var texts = new List<HudNotice>();
            notice.Changed += texts.Add;
            return texts;
        }

        private static HudNotice Restart(int seconds)
        {
            return HudNotice.CheckpointRestart(seconds);
        }

        [Test]
        public void StartsEmpty()
        {
            var notice = new CampaignNotice();

            Assert.That(notice.Notice, Is.EqualTo(HudNotice.None));
            Assert.That(notice.IsCountingDown, Is.False);
        }

        [Test]
        public void SavedShowsForTheGivenSecondsThenClears()
        {
            var notice = new CampaignNotice();
            var texts = Record(notice);

            notice.ShowSaved(2f);
            Assert.That(notice.Notice, Is.EqualTo(Saved));

            notice.Tick(1.5f);
            Assert.That(notice.Notice, Is.EqualTo(Saved));

            notice.Tick(0.5f);
            Assert.That(notice.Notice, Is.EqualTo(HudNotice.None));
            Assert.That(texts, Is.EqualTo(new[] { Saved, HudNotice.None }));
        }

        [Test]
        public void SavingAgainRestartsTheTimerWithoutNotifying()
        {
            var notice = new CampaignNotice();
            notice.ShowSaved(2f);
            notice.Tick(1.5f);
            var texts = Record(notice);

            notice.ShowSaved(2f);
            notice.Tick(1.5f);

            Assert.That(notice.Notice, Is.EqualTo(Saved));
            Assert.That(texts, Is.Empty);
        }

        [Test]
        public void CountdownShowsRoundedUpSecondsAndOnlyNotifiesWhenTheSecondChanges()
        {
            var notice = new CampaignNotice();
            var texts = Record(notice);

            notice.ShowRestartCountdown(3f);
            notice.ShowRestartCountdown(2.5f);
            notice.ShowRestartCountdown(2f);
            notice.ShowRestartCountdown(0.1f);

            Assert.That(notice.IsCountingDown, Is.True);
            Assert.That(notice.Notice, Is.EqualTo(Restart(1)));
            Assert.That(
                texts,
                Is.EqualTo(new[]
                {
                    Restart(3),
                    Restart(2),
                    Restart(1),
                }));
        }

        [Test]
        public void CountdownReplacesTheSavedNoticeAndIsNotOverwrittenOrTimedOut()
        {
            var notice = new CampaignNotice();
            notice.ShowSaved(2f);

            notice.ShowRestartCountdown(3f);
            notice.ShowSaved(2f);
            notice.Tick(10f);

            Assert.That(notice.Notice, Is.EqualTo(Restart(3)));
        }

        [Test]
        public void ClearHidesTheNoticeAndEndsTheCountdown()
        {
            var notice = new CampaignNotice();
            notice.ShowRestartCountdown(3f);

            notice.Clear();

            Assert.That(notice.Notice, Is.EqualTo(HudNotice.None));
            Assert.That(notice.IsCountingDown, Is.False);

            notice.ShowSaved(1f);
            Assert.That(notice.Notice, Is.EqualTo(Saved), "saving shows again after the countdown is cleared");
        }

        [Test]
        public void RejectsInvalidSeconds()
        {
            var notice = new CampaignNotice();

            Assert.Throws<ArgumentOutOfRangeException>(() => notice.ShowSaved(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => notice.ShowSaved(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => notice.ShowSaved(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => notice.ShowRestartCountdown(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => notice.Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => notice.Tick(float.PositiveInfinity));
            Assert.That(notice.Notice, Is.EqualTo(HudNotice.None));
        }
    }
}
