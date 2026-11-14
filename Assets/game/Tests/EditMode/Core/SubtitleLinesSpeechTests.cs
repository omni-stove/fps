using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SubtitleLinesSpeechTests
    {
        [Test]
        public void EachSpeakerHasOneLineThatIsRewritten()
        {
            var lines = new SubtitleLines(3f);

            Assert.That(lines.Say("Bot_1", "Bot_1: 了解、そこを守る", 0f), Is.True);
            Assert.That(lines.Say("Bot_2", "Bot_2: 了解、そこを守る", 0f), Is.True);
            Assert.That(lines.Say("Bot_1", "Bot_1: 了解、付いていく", 1f), Is.True);
            Assert.That(lines.Say("Bot_1", "Bot_1: 了解、付いていく", 2f), Is.False, "the same words only extend the line");

            Assert.That(lines.Lines.Count, Is.EqualTo(2));
            Assert.That(lines.Lines[0].IsSpeech, Is.True);
            Assert.That(lines.Lines[0].Speaker, Is.EqualTo("Bot_1"));
            Assert.That(lines.Lines[0].Text, Is.EqualTo("Bot_1: 了解、付いていく"));
            Assert.That(lines.Lines[0].ExpiresAt, Is.EqualTo(5f));

            Assert.That(lines.Expire(3f), Is.True, "Bot_2 said its line at 0");
            Assert.That(lines.Lines.Count, Is.EqualTo(1));
        }

        [Test]
        public void SpeechAndSoundCuesDoNotReplaceEachOther()
        {
            var lines = new SubtitleLines(3f);
            lines.Show(new SoundCue(SoundCueKind.Gunshot, SoundCueDirection.None, float.NaN), 0f);
            lines.Say("Bot_1", "Bot_1: 了解、あいつを狙う", 0f);
            lines.Show(new SoundCue(SoundCueKind.Gunshot, SoundCueDirection.Front, float.NaN), 0f);

            Assert.That(lines.Lines.Count, Is.EqualTo(2));
            Assert.That(lines.Lines[0].IsSpeech, Is.False);
            Assert.That(lines.Lines[0].Text, Does.Contain(SubtitleText.DirectionText(SoundCueDirection.Front)), "the gunshot line was rewritten");
            Assert.That(lines.Lines[1].Text, Is.EqualTo("Bot_1: 了解、あいつを狙う"));
            Assert.That(lines.Lines[0].Speaker, Is.Null);
            Assert.That(lines.Lines[1].IsSpeech, Is.True);
        }

        [Test]
        public void SpeechNeedsASpeakerAndWords()
        {
            var lines = new SubtitleLines();
            Assert.Throws<ArgumentNullException>(() => lines.Say(null, "words", 0f));
            Assert.Throws<ArgumentNullException>(() => lines.Say("Bot_1", null, 0f));
        }
    }
}
