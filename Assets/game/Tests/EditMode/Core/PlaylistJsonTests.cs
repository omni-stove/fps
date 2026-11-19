using System.Linq;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class PlaylistJsonTests
    {
        private const string _source = "playlists.json";
        private static readonly string[] _unknownMutator = { "not-in-mutators-json" };
        private static readonly string[] _playlistItems = { "id", "order", "nextDelaySeconds", "entries" };
        private static readonly string[] _entryItems = { "scene", "rules", "mutators" };

        [Test]
        public void ParseReadsEveryPlaylistAndEntryInOrder()
        {
            const string text = @"{
  ""playlists"": [
    {
      ""id"": ""team-rotation"",
      ""order"": ""Shuffle"",
      ""nextDelaySeconds"": 4.5,
      ""entries"": [
        { ""scene"": ""Arena"", ""rules"": ""TeamDeathmatch"", ""mutators"": [""low-health"", ""no-regen""] },
        { ""scene"": ""Assets/Scenes/Yard.unity"", ""rules"": ""Domination"" },
        { ""scene"": ""Range"" }
      ]
    },
    {
      ""id"": ""solo"",
      ""entries"": [ { ""scene"": ""Arena"", ""mutators"": [] } ]
    }
  ]
}";

            var playlists = PlaylistJson.Parse(text, _source);

            Assert.That(playlists.Select(p => p.Id), Is.EqualTo(new[] { "team-rotation", "solo" }));

            var team = playlists[0];
            Assert.That(team.Order, Is.EqualTo(PlaylistOrder.Shuffle));
            Assert.That(team.NextDelaySeconds, Is.EqualTo(4.5f));
            Assert.That(team.Entries.Select(e => e.Scene), Is.EqualTo(new[] { "Arena", "Assets/Scenes/Yard.unity", "Range" }));
            Assert.That(team.Entries.Select(e => e.Rules), Is.EqualTo(new[] { "TeamDeathmatch", "Domination", string.Empty }));
            Assert.That(team.Entries[0].MutatorIds, Is.EqualTo(new[] { "low-health", "no-regen" }));
            Assert.That(team.Entries[1].MutatorIds, Is.Empty);
            Assert.That(team.Entries[2].HasRules, Is.False);

            var solo = playlists[1];
            Assert.That(solo.Order, Is.EqualTo(PlaylistOrder.Sequential), "order defaults to Sequential");
            Assert.That(solo.NextDelaySeconds, Is.EqualTo(Playlist.DefaultNextDelaySeconds), "the delay defaults to the kit value");
            Assert.That(solo.Entries.Single().MutatorIds, Is.Empty);
        }

        [Test]
        public void AFileWithoutTheListReadsAsNoPlaylists()
        {
            Assert.That(PlaylistJson.Parse("{}", _source), Is.Empty);
            Assert.That(PlaylistJson.Parse("{ \"playlists\": [] }", _source), Is.Empty);
        }

        [Test]
        public void AMutatorIdIsKeptEvenIfNoMutatorHasItBecauseTheRunnerSkipsItWithAReason()
        {
            var playlists = PlaylistJson.Parse(
                "{ \"playlists\": [ { \"id\": \"p\", \"entries\": [ { \"scene\": \"Arena\", \"mutators\": [\"not-in-mutators-json\"] } ] } ] }",
                _source);

            Assert.That(playlists[0].Entries[0].MutatorIds, Is.EqualTo(_unknownMutator));
        }

        [TestCase("{ \"playlist\": [] }", "playlist")]
        [TestCase("{ \"playlists\": {} }", "playlists")]
        [TestCase("{ \"playlists\": [ { \"entries\": [ { \"scene\": \"A\" } ] } ] }", "playlists[0].id")]
        [TestCase("{ \"playlists\": [ { \"id\": \" \", \"entries\": [ { \"scene\": \"A\" } ] } ] }", "playlists[0].id")]
        [TestCase("{ \"playlists\": [ { \"id\": 3, \"entries\": [ { \"scene\": \"A\" } ] } ] }", "playlists[0].id")]
        [TestCase("{ \"playlists\": [ { \"id\": \"p\" } ] }", "playlists[0].entries")]
        [TestCase("{ \"playlists\": [ { \"id\": \"p\", \"entries\": [] } ] }", "playlists[0].entries")]
        [TestCase("{ \"playlists\": [ { \"id\": \"p\", \"order\": \"Random\", \"entries\": [ { \"scene\": \"A\" } ] } ] }", "playlists[0].order")]
        [TestCase("{ \"playlists\": [ { \"id\": \"p\", \"order\": \"1\", \"entries\": [ { \"scene\": \"A\" } ] } ] }", "playlists[0].order")]
        [TestCase("{ \"playlists\": [ { \"id\": \"p\", \"nextDelaySeconds\": -1, \"entries\": [ { \"scene\": \"A\" } ] } ] }", "playlists[0].nextDelaySeconds")]
        [TestCase("{ \"playlists\": [ { \"id\": \"p\", \"name\": \"P\", \"entries\": [ { \"scene\": \"A\" } ] } ] }", "playlists[0].name")]
        [TestCase("{ \"playlists\": [ { \"id\": \"p\", \"entries\": [ {} ] } ] }", "playlists[0].entries[0].scene")]
        [TestCase("{ \"playlists\": [ { \"id\": \"p\", \"entries\": [ { \"scene\": \" \" } ] } ] }", "playlists[0].entries[0].scene")]
        [TestCase("{ \"playlists\": [ { \"id\": \"p\", \"entries\": [ { \"scene\": \"A\", \"rules\": 1 } ] } ] }", "playlists[0].entries[0].rules")]
        [TestCase("{ \"playlists\": [ { \"id\": \"p\", \"entries\": [ { \"scene\": \"A\", \"mutators\": \"low-health\" } ] } ] }", "playlists[0].entries[0].mutators")]
        [TestCase("{ \"playlists\": [ { \"id\": \"p\", \"entries\": [ { \"scene\": \"A\", \"mutators\": [1] } ] } ] }", "playlists[0].entries[0].mutators[0]")]
        [TestCase("{ \"playlists\": [ { \"id\": \"p\", \"entries\": [ { \"scene\": \"A\", \"mutators\": [\"\"] } ] } ] }", "playlists[0].entries[0].mutators")]
        [TestCase("{ \"playlists\": [ { \"id\": \"p\", \"entries\": [ { \"scene\": \"A\", \"map\": \"B\" } ] } ] }", "playlists[0].entries[0].map")]
        [TestCase("{ \"playlists\": [ { \"id\": \"p\", \"entries\": [ { \"scene\": \"A\" } ] }, { \"id\": \"p\", \"entries\": [ { \"scene\": \"B\" } ] } ] }", "playlists[1].id")]
        public void ABrokenItemIsRejectedWithItsPath(string text, string item)
        {
            var e = Assert.Throws<TuningJsonException>(() => PlaylistJson.Parse(text, _source));

            Assert.That(e.Item, Is.EqualTo(item));
            Assert.That(e.SourceName, Is.EqualTo(_source));
            Assert.That(e.Message, Does.Contain(_source));
        }

        [Test]
        public void TextThatIsNotJsonIsRejected()
        {
            var e = Assert.Throws<TuningJsonException>(() => PlaylistJson.Parse("{ \"playlists\": [", _source));

            Assert.That(e.Message, Does.Contain("not valid JSON"));
            Assert.That(e.Item, Is.Null);
        }

        [Test]
        public void TheItemNamesMatchTheConstructorArgumentsSoErrorsPointAtTheItem()
        {
            Assert.That(PlaylistJson.PlaylistItemNames, Is.EqualTo(_playlistItems));
            Assert.That(PlaylistJson.EntryItemNames, Is.EqualTo(_entryItems));
            Assert.That(PlaylistJson.ResourcePath, Is.EqualTo("Tuning/playlists"));
        }
    }
}
