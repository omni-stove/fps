using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class EmoteJsonTests
    {
        private const string _source = "emotes.json";

        private static readonly string[] _expectedItemNames = { "emotes" };
        private static readonly string[] _expectedEmoteItemNames = { "id", "displayNameKey", "durationSeconds", "loops", "stopsOnMove" };

        [Test]
        public void ReadsEveryItem()
        {
            var emotes = EmoteJson.Parse(
                "{\"emotes\": [{\"id\": \"wave\", \"displayNameKey\": \"emote.wave\", \"durationSeconds\": 2.5, \"loops\": false, \"stopsOnMove\": true},"
                + " {\"id\": \"dance\", \"displayNameKey\": \"emote.dance\", \"durationSeconds\": 4, \"loops\": true, \"stopsOnMove\": false}]}",
                _source);

            Assert.That(emotes.Count, Is.EqualTo(2));
            var wave = emotes[0];
            Assert.That(wave.Id, Is.EqualTo("wave"));
            Assert.That(wave.DisplayNameKey, Is.EqualTo("emote.wave"));
            Assert.That(wave.DurationSeconds, Is.EqualTo(2.5f));
            Assert.That(wave.Loops, Is.False);
            Assert.That(wave.StopsOnMove, Is.True);

            var dance = emotes[1];
            Assert.That(dance.Id, Is.EqualTo("dance"));
            Assert.That(dance.DisplayNameKey, Is.EqualTo("emote.dance"));
            Assert.That(dance.DurationSeconds, Is.EqualTo(4f));
            Assert.That(dance.Loops, Is.True);
            Assert.That(dance.StopsOnMove, Is.False);

            Assert.That(EmoteSpec.Find(emotes, "dance"), Is.SameAs(dance));
            Assert.That(EmoteSpec.Find(emotes, "missing"), Is.Null);
            Assert.That(EmoteSpec.Find(emotes, null), Is.Null);
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            Assert.That(EmoteJson.Parse("{}", _source), Is.Empty);
            Assert.That(EmoteJson.Parse("{\"emotes\": []}", _source), Is.Empty);
            Assert.That(EmoteSpec.DefaultEmotes, Is.Empty);

            var bare = EmoteJson.Parse("{\"emotes\": [{\"id\": \" salute \"}]}", _source)[0];
            Assert.That(bare.Id, Is.EqualTo("salute"));
            Assert.That(bare.DisplayNameKey, Is.EqualTo(string.Empty));
            Assert.That(bare.DurationSeconds, Is.EqualTo(3f));
            Assert.That(bare.Loops, Is.False);
            Assert.That(bare.StopsOnMove, Is.True);
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(EmoteJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(EmoteJson.EmoteItemNames, Is.EqualTo(_expectedEmoteItemNames));
            Assert.That(EmoteJson.ResourcePath, Is.EqualTo("Tuning/emotes"));
        }

        [Test]
        public void ABadValueNamesItsItem()
        {
            var noId = Reject("{\"emotes\": [{\"durationSeconds\": 2}]}");
            var blankId = Reject("{\"emotes\": [{\"id\": \" \"}]}");
            var zero = Reject("{\"emotes\": [{\"id\": \"wave\", \"durationSeconds\": 0}]}");
            var loops = Reject("{\"emotes\": [{\"id\": \"wave\", \"loops\": \"yes\"}]}");
            var twice = Reject("{\"emotes\": [{\"id\": \"wave\"}, {\"id\": \"wave\", \"loops\": true}]}");
            var unknown = Reject("{\"emotes\": [{\"id\": \"wave\", \"speed\": 2}]}");

            Assert.That(noId.Item, Is.EqualTo("emotes[0].id"));
            Assert.That(blankId.Item, Is.EqualTo("emotes[0].id"));
            Assert.That(zero.Item, Is.EqualTo("emotes[0].durationSeconds"));
            Assert.That(zero.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(loops.Item, Is.EqualTo("emotes[0].loops"));
            Assert.That(twice.Item, Is.EqualTo("emotes"));
            Assert.That(unknown.Item, Is.EqualTo("emotes[0].speed"));
        }

        [Test]
        public void TheSpecRefusesBadValues()
        {
            Assert.Throws<ArgumentException>(() => new EmoteSpec(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EmoteSpec("wave", durationSeconds: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EmoteSpec("wave", durationSeconds: float.NaN));
            Assert.Throws<ArgumentException>(() => EmoteSpec.UniqueById(new[] { new EmoteSpec("wave"), new EmoteSpec("wave") }));
            Assert.Throws<ArgumentNullException>(() => EmoteSpec.UniqueById(null));
        }

        [Test]
        public void OnlyAOneShotEmoteFinishesByItself()
        {
            var once = new EmoteSpec("wave", durationSeconds: 2f);
            var looping = new EmoteSpec("dance", durationSeconds: 2f, loops: true);

            Assert.That(once.IsFinishedAt(1.99f), Is.False);
            Assert.That(once.IsFinishedAt(2f), Is.True);
            Assert.That(looping.IsFinishedAt(100f), Is.False);
        }

        private static TuningJsonException Reject(string json)
        {
            return Assert.Throws<TuningJsonException>(() => EmoteJson.Parse(json, _source));
        }
    }
}
