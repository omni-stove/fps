using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class CampaignNoticeTests
    {
        private static List<string> Record(CampaignNotice notice)
        {
            var texts = new List<string>();
            notice.TextChanged += texts.Add;
            return texts;
        }

        [Test]
        public void StartsEmpty()
        {
            var notice = new CampaignNotice();

            Assert.That(notice.Text, Is.EqualTo(HudFormat.NoNotice));
            Assert.That(notice.IsCountingDown, Is.False);
        }

        [Test]
        public void SavedShowsForTheGivenSecondsThenClears()
        {
            var notice = new CampaignNotice();
            var texts = Record(notice);

            notice.ShowSaved(2f);
            Assert.That(notice.Text, Is.EqualTo(HudFormat.CheckpointSaved));

            notice.Tick(1.5f);
            Assert.That(notice.Text, Is.EqualTo(HudFormat.CheckpointSaved));

            notice.Tick(0.5f);
            Assert.That(notice.Text, Is.EqualTo(HudFormat.NoNotice));
            Assert.That(texts, Is.EqualTo(new[] { HudFormat.CheckpointSaved, HudFormat.NoNotice }));
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

            Assert.That(notice.Text, Is.EqualTo(HudFormat.CheckpointSaved));
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
            Assert.That(notice.Text, Is.EqualTo(HudFormat.CheckpointRestart(1)));
            Assert.That(
                texts,
                Is.EqualTo(new[]
                {
                    HudFormat.CheckpointRestart(3),
                    HudFormat.CheckpointRestart(2),
                    HudFormat.CheckpointRestart(1),
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

            Assert.That(notice.Text, Is.EqualTo(HudFormat.CheckpointRestart(3)));
        }

        [Test]
        public void ClearHidesTheNoticeAndEndsTheCountdown()
        {
            var notice = new CampaignNotice();
            notice.ShowRestartCountdown(3f);

            notice.Clear();

            Assert.That(notice.Text, Is.EqualTo(HudFormat.NoNotice));
            Assert.That(notice.IsCountingDown, Is.False);

            notice.ShowSaved(1f);
            Assert.That(notice.Text, Is.EqualTo(HudFormat.CheckpointSaved), "saving shows again after the countdown is cleared");
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
            Assert.That(notice.Text, Is.EqualTo(HudFormat.NoNotice));
        }
    }
}
