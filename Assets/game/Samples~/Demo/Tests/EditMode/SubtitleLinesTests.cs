using System;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class SubtitleLinesTests
    {
        [TestCase(SoundCueKind.Gunshot, SoundCueDirection.Front, 5f, "[銃声 正面]")]
        [TestCase(SoundCueKind.Gunshot, SoundCueDirection.BackLeft, 5f, "[銃声 左後ろ]")]
        [TestCase(SoundCueKind.Footstep, SoundCueDirection.Right, 50f, "[足音 右 遠く]")]
        [TestCase(SoundCueKind.Explosion, SoundCueDirection.None, 3f, "[爆発]")]
        [TestCase(SoundCueKind.Reload, SoundCueDirection.None, float.NaN, "[リロード]")]
        [TestCase(SoundCueKind.Reload, SoundCueDirection.FrontLeft, 10f, "[リロード 左前]")]
        [TestCase(SoundCueKind.Clatter, SoundCueDirection.Back, 8f, "[物音 後ろ]")]
        public void FormatsKindDirectionAndDistance(SoundCueKind kind, SoundCueDirection direction, float distance, string expected)
        {
            Assert.That(SubtitleText.Format(new SoundCue(kind, direction, distance)), Is.EqualTo(expected));
        }

        [Test]
        public void EveryDirectionHasAWord()
        {
            foreach (SoundCueDirection direction in Enum.GetValues(typeof(SoundCueDirection)))
            {
                var text = SubtitleText.DirectionText(direction);
                Assert.That(text.Length == 0, Is.EqualTo(direction == SoundCueDirection.None), direction.ToString());
            }
        }

        [Test]
        public void UnknownValuesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SubtitleText.KindText((SoundCueKind)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => SubtitleText.DirectionText((SoundCueDirection)9));
        }

        [Test]
        public void ACueShowsALineForThreeSeconds()
        {
            var lines = new SubtitleLines();

            Assert.That(lines.Show(Cue(SoundCueKind.Gunshot, SoundCueDirection.Front), 10f), Is.True);

            Assert.That(lines.Lines, Has.Count.EqualTo(1));
            Assert.That(lines.Lines[0].Text, Is.EqualTo("[銃声 正面]"));
            Assert.That(lines.Expire(12.9f), Is.False);
            Assert.That(lines.Lines, Has.Count.EqualTo(1));
            Assert.That(lines.Expire(13f), Is.True);
            Assert.That(lines.Lines, Is.Empty);
        }

        [Test]
        public void TheSameKindIsMergedIntoOneLineAndExtended()
        {
            var lines = new SubtitleLines();
            lines.Show(Cue(SoundCueKind.Gunshot, SoundCueDirection.Front), 0f);

            Assert.That(lines.Show(Cue(SoundCueKind.Gunshot, SoundCueDirection.Front), 2f), Is.False, "same text");
            lines.Expire(4f);

            Assert.That(lines.Lines, Has.Count.EqualTo(1));
            Assert.That(lines.Lines[0].ExpiresAt, Is.EqualTo(5f));
        }

        [Test]
        public void TheSameKindFromAnotherDirectionRewritesTheLine()
        {
            var lines = new SubtitleLines();
            lines.Show(Cue(SoundCueKind.Gunshot, SoundCueDirection.Front), 0f);

            Assert.That(lines.Show(Cue(SoundCueKind.Gunshot, SoundCueDirection.Back), 1f), Is.True);

            Assert.That(lines.Lines, Has.Count.EqualTo(1));
            Assert.That(lines.Lines[0].Text, Is.EqualTo("[銃声 後ろ]"));
        }

        [Test]
        public void DifferentKindsKeepTheOrderTheyFirstAppeared()
        {
            var lines = new SubtitleLines();
            lines.Show(Cue(SoundCueKind.Footstep, SoundCueDirection.Left), 0f);
            lines.Show(Cue(SoundCueKind.Gunshot, SoundCueDirection.Front), 0.5f);
            lines.Show(Cue(SoundCueKind.Footstep, SoundCueDirection.Right), 1f);

            Assert.That(lines.Lines, Has.Count.EqualTo(2));
            Assert.That(lines.Lines[0].Text, Is.EqualTo("[足音 右]"));
            Assert.That(lines.Lines[1].Text, Is.EqualTo("[銃声 正面]"));

            lines.Expire(3.5f);

            Assert.That(lines.Lines, Has.Count.EqualTo(1));
            Assert.That(lines.Lines[0].Kind, Is.EqualTo(SoundCueKind.Footstep));
        }

        [Test]
        public void ClearRemovesEveryLine()
        {
            var lines = new SubtitleLines();
            Assert.That(lines.Clear(), Is.False);
            lines.Show(Cue(SoundCueKind.Explosion, SoundCueDirection.None), 0f);

            Assert.That(lines.Clear(), Is.True);
            Assert.That(lines.Lines, Is.Empty);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsANonPositiveDuration(float seconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SubtitleLines(seconds));
        }

        private static SoundCue Cue(SoundCueKind kind, SoundCueDirection direction)
        {
            return new SoundCue(kind, direction, 5f);
        }
    }
}
