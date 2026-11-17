using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class VoiceSettingsTests
    {
        private static readonly string[] _unknownKeyProblems = { "ignored unknown key 'echo'" };

        [Test]
        public void TheDefaultUsesTheMicrophoneAtFullVolume()
        {
            Assert.That(VoiceSettings.Default.MicrophoneEnabled, Is.True);
            Assert.That(VoiceSettings.Default.VolumePercent, Is.EqualTo(100));
            Assert.That(VoiceSettings.Default.Volume, Is.EqualTo(1f));
        }

        [Test]
        public void TheVolumeIsAPercentOfUpToTwoHundred()
        {
            Assert.That(new VoiceSettings(true, 0).Volume, Is.EqualTo(0f));
            Assert.That(new VoiceSettings(true, 50).Volume, Is.EqualTo(0.5f));
            Assert.That(new VoiceSettings(true, 200).Volume, Is.EqualTo(2f));
            Assert.That(VoiceSettings.MaxVolume, Is.EqualTo(2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VoiceSettings(true, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VoiceSettings(true, 201));
        }

        [Test]
        public void WithVolumePercentClampsToTheRange()
        {
            Assert.That(VoiceSettings.Default.WithVolumePercent(500).VolumePercent, Is.EqualTo(200));
            Assert.That(VoiceSettings.Default.WithVolumePercent(-5).VolumePercent, Is.EqualTo(0));
            Assert.That(VoiceSettings.Default.WithVolumePercent(30).VolumePercent, Is.EqualTo(30));
        }

        [Test]
        public void JsonRoundTrips()
        {
            var settings = new VoiceSettings(false, 35);

            var json = settings.ToJson();
            var read = VoiceSettings.FromJson(json, out var problems);

            Assert.That(json, Is.EqualTo("{\"version\":1,\"microphoneEnabled\":false,\"volumePercent\":35}"));
            Assert.That(read, Is.EqualTo(settings));
            Assert.That(problems, Is.Empty);
        }

        [Test]
        public void MissingValuesUseTheDefaultSilently()
        {
            var read = VoiceSettings.FromJson("{\"version\":1,\"volumePercent\":40}", out var problems);

            Assert.That(read, Is.EqualTo(new VoiceSettings(true, 40)));
            Assert.That(problems, Is.Empty);
        }

        [Test]
        public void AVolumeOutOfRangeIsClampedWithAProblem()
        {
            var read = VoiceSettings.FromJson("{\"version\":1,\"microphoneEnabled\":false,\"volumePercent\":500}", out var problems);

            Assert.That(read, Is.EqualTo(new VoiceSettings(false, 200)));
            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("'volumePercent' 500 is out of range"));
        }

        [Test]
        public void AWrongTypeKeepsTheDefaultForThatValueOnly()
        {
            var read = VoiceSettings.FromJson("{\"version\":1,\"microphoneEnabled\":\"no\",\"volumePercent\":60}", out var problems);

            Assert.That(read, Is.EqualTo(new VoiceSettings(true, 60)));
            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("'microphoneEnabled' must be true or false"));
        }

        [TestCase("not json")]
        [TestCase("[]")]
        [TestCase("{\"microphoneEnabled\":false}")]
        [TestCase("{\"version\":2,\"microphoneEnabled\":false}")]
        public void UnreadableTextFallsBackToTheDefault(string json)
        {
            var read = VoiceSettings.FromJson(json, out var problems);

            Assert.That(read, Is.EqualTo(VoiceSettings.Default));
            Assert.That(problems, Has.Count.EqualTo(1));
        }

        [Test]
        public void UnknownKeysAreReportedAndIgnored()
        {
            var read = VoiceSettings.FromJson("{\"version\":1,\"microphoneEnabled\":false,\"echo\":true}", out var problems);

            Assert.That(read, Is.EqualTo(new VoiceSettings(false, 100)));
            Assert.That(problems, Is.EqualTo(_unknownKeyProblems));
        }
    }
}
