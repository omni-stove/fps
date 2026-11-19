using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    public sealed class ServerPlaylistTests
    {
        private const string _rotation = @"{
  ""playlists"": [
    {
      ""id"": ""rotation"",
      ""entries"": [
        { ""scene"": ""NetTest"", ""rules"": ""TeamDeathmatch"" },
        { ""scene"": ""NetArena01"", ""rules"": ""Domination"", ""mutators"": [""low-health""] },
        { ""scene"": ""NetTest"", ""rules"": ""TeamDeathmatch"" }
      ]
    }
  ]
}";

        private const string _mutators = @"{
  ""mutators"": [
    { ""id"": ""low-health"", ""rules"": { ""playerMaxHealth"": 50 } },
    { ""id"": ""blind-fire"", ""rules"": { ""actions"": { ""blindFire"": ""On"" } } }
  ]
}";

        private static readonly string[] _rotationMaps = { "NetTest", "NetArena01" };
        private static readonly string[] _lowHealthIds = { "low-health" };

        private TempDirectory _first;
        private TempDirectory _second;

        [SetUp]
        public void SetUp()
        {
            _first = new TempDirectory();
            _second = new TempDirectory();
            TestRules.Write(_first.Path, "TeamDeathmatch");
            TestRules.Write(_first.Path, "Domination", TestRules.File(GameRules.Default.WithObjective(MatchObjective.Domination), name: "Domination"));
        }

        [TearDown]
        public void TearDown()
        {
            _first.Dispose();
            _second.Dispose();
        }

        [Test]
        public void TheEntriesNameTheirMapsAndRulesAndTheMutatorsAreOnTheRules()
        {
            var path = WritePlaylists(_first.Path, "rotation.json", _rotation);
            var mutatorsPath = WriteMutators(_first.Path, _mutators);

            var playlist = Load("rotation");

            Assert.That(playlist.Playlist.Id, Is.EqualTo("rotation"));
            Assert.That(playlist.Playlist.Order, Is.EqualTo(PlaylistOrder.Sequential));
            Assert.That(playlist.Path, Is.EqualTo(path));
            Assert.That(playlist.MutatorsPath, Is.EqualTo(mutatorsPath));
            Assert.That(playlist.MapNames, Is.EqualTo(_rotationMaps));
            Assert.That(playlist.Entries, Has.Count.EqualTo(3));

            var first = playlist.Entries[0];
            Assert.That(first.Position, Is.Zero);
            Assert.That(first.Map, Is.EqualTo("NetTest"));
            Assert.That(first.RulesName, Is.EqualTo("TeamDeathmatch"));
            Assert.That(first.Name, Is.EqualTo("TeamDeathmatch"));
            Assert.That(first.Rules.Objective, Is.EqualTo(MatchObjective.None));
            Assert.That(first.Rules.PlayerMaxHealth, Is.EqualTo(GameRules.Default.PlayerMaxHealth));
            Assert.That(first.MutatorIds, Is.Empty);

            var second = playlist.Entries[1];
            Assert.That(second.Map, Is.EqualTo("NetArena01"));
            Assert.That(second.RulesName, Is.EqualTo("Domination"));
            Assert.That(second.Name, Is.EqualTo("Domination"));
            Assert.That(second.Rules.Objective, Is.EqualTo(MatchObjective.Domination));
            Assert.That(second.Rules.PlayerMaxHealth, Is.EqualTo(50));
            Assert.That(second.MutatorIds, Is.EqualTo(_lowHealthIds));
            Assert.That(second.Data.File.Name, Is.EqualTo("Domination"));

            // 同じルールの項目は同じ GameRules を指し、戻ってきたときに目的を作り直さない
            Assert.That(playlist.Entries[2].Rules, Is.SameAs(first.Rules));
            Assert.That(playlist.Entries[2].Data, Is.SameAs(first.Data));
        }

        [Test]
        public void TheSameRulesAndMutatorsShareOneRulesObject()
        {
            WritePlaylists(_first.Path, "rotation.json", @"{ ""playlists"": [ { ""id"": ""rotation"", ""entries"": [
                { ""scene"": ""NetTest"", ""rules"": ""Domination"", ""mutators"": [""low-health""] },
                { ""scene"": ""NetArena01"", ""rules"": ""Domination"", ""mutators"": [""low-health""] },
                { ""scene"": ""NetTest"", ""rules"": ""Domination"" } ] } ] }");
            WriteMutators(_first.Path, _mutators);

            var playlist = Load("rotation");

            Assert.That(playlist.Entries[1].Rules, Is.SameAs(playlist.Entries[0].Rules));
            Assert.That(playlist.Entries[2].Rules, Is.Not.SameAs(playlist.Entries[0].Rules));
            Assert.That(playlist.Entries[2].Rules.PlayerMaxHealth, Is.EqualTo(GameRules.Default.PlayerMaxHealth));
        }

        [Test]
        public void TheNextEntryIsKnownBeforeTheMatchMovesOnAndRoundsAreCounted()
        {
            WritePlaylists(_first.Path, "rotation.json", _rotation);
            WriteMutators(_first.Path, _mutators);
            var playlist = Load("rotation");

            Assert.That(playlist.Current.Position, Is.Zero);
            Assert.That(playlist.Round, Is.Zero);
            Assert.That(playlist.Upcoming.Position, Is.EqualTo(1));

            Assert.That(playlist.Advance().Position, Is.EqualTo(1));
            Assert.That(playlist.Current.Position, Is.EqualTo(1));
            Assert.That(playlist.Upcoming.Position, Is.EqualTo(2));
            Assert.That(playlist.Round, Is.Zero);

            Assert.That(playlist.Advance().Position, Is.EqualTo(2));
            Assert.That(playlist.Upcoming.Position, Is.Zero);
            Assert.That(playlist.Round, Is.Zero);

            Assert.That(playlist.Advance().Position, Is.Zero);
            Assert.That(playlist.Round, Is.EqualTo(1));
            Assert.That(playlist.Upcoming.Position, Is.EqualTo(1));
        }

        [Test]
        public void AShuffledPlaylistGoesInTheOrderOfTheCursorWithTheSameSeed()
        {
            WritePlaylists(_first.Path, "rotation.json", @"{ ""playlists"": [ { ""id"": ""mixed"", ""order"": ""Shuffle"", ""entries"": [
                { ""scene"": ""NetTest"" }, { ""scene"": ""NetArena01"" }, { ""scene"": ""NetArena02"" }, { ""scene"": ""NetArena03"" } ] } ] }");
            Assert.That(ServerPlaylist.TryLoad("mixed", new[] { _first.Path }, 7, out var playlist, out var error), Is.True, error);

            var cursor = new PlaylistCursor(playlist.Playlist, 7);
            var expected = new List<int> { cursor.Position };
            var actual = new List<int> { playlist.Current.Position };
            for (var i = 0; i < 11; i++)
            {
                expected.Add(Position(cursor));
                actual.Add(playlist.Advance().Position);
            }

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void AnEntryWithoutRulesPlaysTheDefaultRules()
        {
            WritePlaylists(_first.Path, "rotation.json", @"{ ""playlists"": [ { ""id"": ""plain"", ""entries"": [ { ""scene"": ""NetTest"" } ] } ] }");

            var playlist = Load("plain");

            Assert.That(playlist.Current.RulesName, Is.EqualTo(ServerLaunch.DefaultRules));
            Assert.That(playlist.Current.Name, Is.EqualTo(ServerLaunch.DefaultRules));
            Assert.That(playlist.MutatorsPath, Is.Null);

            // 1 項目だけなら次もその項目で、2 周目に入る
            Assert.That(playlist.Upcoming, Is.SameAs(playlist.Current));
            Assert.That(playlist.Advance(), Is.SameAs(playlist.Entries[0]));
            Assert.That(playlist.Round, Is.EqualTo(1));
        }

        [Test]
        public void ThePlaylistIsLookedForInTheNextRootWhenTheFirstHasNoSuchId()
        {
            WritePlaylists(_first.Path, "other.json", @"{ ""playlists"": [ { ""id"": ""other"", ""entries"": [ { ""scene"": ""NetTest"" } ] } ] }");
            var path = WritePlaylists(_second.Path, "rotation.json", @"{ ""playlists"": [ { ""id"": ""rotation"", ""entries"": [ { ""scene"": ""NetTest"" } ] } ] }");

            Assert.That(ServerPlaylist.TryLoad("rotation", new[] { _first.Path, _second.Path }, 0, out var playlist, out var error), Is.True, error);

            Assert.That(playlist.Path, Is.EqualTo(path));
        }

        [Test]
        public void AnUnknownIdListsTheKnownIds()
        {
            WritePlaylists(_first.Path, "rotation.json", _rotation);
            WritePlaylists(_first.Path, "other.json", @"{ ""playlists"": [ { ""id"": ""b-side"", ""entries"": [ { ""scene"": ""NetTest"" } ] } ] }");

            var error = Fail("missing", _first.Path, _second.Path);

            Assert.That(error, Is.EqualTo(
                "playlist missing not found in " + PlaylistsDirectory(_first.Path) + " or " + PlaylistsDirectory(_second.Path)
                + "; known ids are b-side, rotation"));
        }

        [Test]
        public void WithoutAnyPlaylistsNoIdIsKnown()
        {
            Assert.That(Fail("rotation", _first.Path), Is.EqualTo(
                "playlist rotation not found in " + PlaylistsDirectory(_first.Path) + "; known ids are (none)"));
        }

        [Test]
        public void TheSameIdInTwoFilesIsRefused()
        {
            var a = WritePlaylists(_first.Path, "a.json", @"{ ""playlists"": [ { ""id"": ""rotation"", ""entries"": [ { ""scene"": ""NetTest"" } ] } ] }");
            var b = WritePlaylists(_first.Path, "b.json", @"{ ""playlists"": [ { ""id"": ""rotation"", ""entries"": [ { ""scene"": ""NetArena01"" } ] } ] }");

            Assert.That(Fail("rotation", _first.Path), Is.EqualTo("playlist id 'rotation' is used in both " + a + " and " + b));
        }

        [Test]
        public void ABrokenPlaylistFileIsRefusedEvenWhenTheIdIsInAnother()
        {
            WritePlaylists(_first.Path, "a.json", _rotation);
            var broken = WritePlaylists(_first.Path, "b.json", "{");

            Assert.That(Fail("rotation", _first.Path), Does.StartWith("cannot read playlists " + broken + ": "));
        }

        [Test]
        public void ABrokenMutatorsFileIsRefused()
        {
            WritePlaylists(_first.Path, "rotation.json", _rotation);
            var path = WriteMutators(_first.Path, "{ \"mutators\": 3 }");

            Assert.That(Fail("rotation", _first.Path), Does.StartWith("cannot read mutators " + path + ": "));
        }

        [Test]
        public void AnUnknownMutatorIsRefusedWithTheKnownIds()
        {
            WritePlaylists(_first.Path, "rotation.json", _rotation.Replace("low-health", "missing", System.StringComparison.Ordinal));
            var path = WriteMutators(_first.Path, _mutators);

            Assert.That(Fail("rotation", _first.Path), Is.EqualTo(
                "playlist rotation entry 2/3: no mutator has the id 'missing' in " + path + "; known ids are low-health, blind-fire"));
        }

        [Test]
        public void AMutatorWithoutTheMutatorsFileIsRefused()
        {
            WritePlaylists(_first.Path, "rotation.json", _rotation);

            Assert.That(Fail("rotation", _first.Path), Is.EqualTo(
                "playlist rotation entry 2/3: no mutator has the id 'low-health' in server-data/tuning/mutators.json (not found); known ids are (none)"));
        }

        [Test]
        public void AMutatorTheRulesCannotTakeIsRefused()
        {
            WritePlaylists(_first.Path, "rotation.json", @"{ ""playlists"": [ { ""id"": ""rotation"", ""entries"": [
                { ""scene"": ""NetTest"", ""rules"": ""TeamDeathmatch"", ""mutators"": [""blind-fire""] } ] } ] }");
            WriteMutators(_first.Path, _mutators);

            Assert.That(Fail("rotation", _first.Path), Does.StartWith("playlist rotation entry 1/1: cannot put the mutators on rules TeamDeathmatch: "));
        }

        [Test]
        public void MissingRulesAreRefused()
        {
            WritePlaylists(_first.Path, "rotation.json", @"{ ""playlists"": [ { ""id"": ""rotation"", ""entries"": [
                { ""scene"": ""NetTest"", ""rules"": ""Hardpoint"" } ] } ] }");

            Assert.That(Fail("rotation", _first.Path), Does.StartWith("playlist rotation entry 1/1: rules Hardpoint not found: Hardpoint.json is not in "));
        }

        [Test]
        public void ASceneThatIsNotAMapNameIsRefused()
        {
            WritePlaylists(_first.Path, "rotation.json", @"{ ""playlists"": [ { ""id"": ""rotation"", ""entries"": [
                { ""scene"": ""NetTest"" }, { ""scene"": ""Scenes/NetArena01"" } ] } ] }");

            Assert.That(Fail("rotation", _first.Path), Is.EqualTo(
                "playlist rotation entry 2/2: scene 'Scenes/NetArena01' is not a map name of letters, digits, '_' or '-'"));
        }

        [Test]
        public void AnEmptyIdIsRefused()
        {
            Assert.That(ServerPlaylist.TryLoad(" ", new[] { _first.Path }, 0, out var playlist, out var error), Is.False);
            Assert.That(playlist, Is.Null);
            Assert.That(error, Is.EqualTo("a playlist id is needed"));
        }

        [Test]
        public void TheEntriesMustBeThoseOfThePlaylist()
        {
            var source = new Playlist("rotation", PlaylistOrder.Sequential, new[] { new PlaylistEntry("NetTest"), new PlaylistEntry("NetArena01") });
            var data = TestRules.Data();
            var first = new ServerPlaylistEntry(0, source.Entries[0], "TeamDeathmatch", data);

            Assert.Throws<System.ArgumentException>(() => new ServerPlaylist(source, new[] { first }, 0));
            Assert.Throws<System.ArgumentException>(() => new ServerPlaylist(
                source, new[] { first, new ServerPlaylistEntry(1, source.Entries[0], "TeamDeathmatch", data) }, 0));
            Assert.Throws<System.ArgumentException>(() => new ServerPlaylist(
                source, new[] { first, new ServerPlaylistEntry(2, source.Entries[1], "TeamDeathmatch", data) }, 0));
            Assert.DoesNotThrow(() => new ServerPlaylist(
                source, new[] { first, new ServerPlaylistEntry(1, source.Entries[1], "TeamDeathmatch", data) }, 0));
        }

        [Test]
        public void TheLogLinesNameThePlaylistTheEntryAndTheRound()
        {
            var path = WritePlaylists(_first.Path, "rotation.json", _rotation);
            var mutatorsPath = WriteMutators(_first.Path, _mutators);
            var playlist = Load("rotation");

            Assert.That(ServerPlaylistLog.Loaded(playlist), Is.EqualTo(
                "[Server] playlist rotation order=Sequential entries=3 maps=NetTest, NetArena01 from=" + path + " mutators=" + mutatorsPath));
            Assert.That(ServerPlaylistLog.Entry(playlist, playlist.Entries[1], 0), Is.EqualTo(
                "[Server] playlist rotation entry 2/3 round=1: map=NetArena01 rules=Domination objective=Domination mutators=low-health"));
            Assert.That(ServerPlaylistLog.Entry(playlist, playlist.Entries[0], 1), Is.EqualTo(
                "[Server] playlist rotation entry 1/3 round=2: map=NetTest rules=TeamDeathmatch objective=None mutators=none"));
            Assert.That(ServerPlaylistLog.MapVotedOver(playlist, playlist.Entries[1], "NetArena02"), Is.EqualTo(
                "[Server] playlist rotation entry 2/3: the map vote chose NetArena02 over NetArena01"));
            Assert.That(ServerPlaylistLog.RulesRefused(playlist, playlist.Entries[1], "refused"), Is.EqualTo(
                "[Server] playlist rotation entry 2/3: the next match keeps the rules of the last one: refused"));
        }

        // make server-dotnet が読むのと同じ口で、コミットした例のプレイリストが読めて、例のマップと例のルールを指すことを確かめる
        [Test]
        public void TheExamplePlaylistLoadsFromServerData()
        {
            var roots = RepositoryRoots();
            Assert.That(ServerPlaylist.TryLoad("rotation", roots, 0, out var playlist, out var error), Is.True, error);

            Assert.That(playlist.Entries, Has.Count.EqualTo(3));
            Assert.That(playlist.Entries[0].Rules.Objective, Is.EqualTo(MatchObjective.None));
            Assert.That(playlist.Entries[1].Rules.Objective, Is.EqualTo(MatchObjective.Domination));
            Assert.That(playlist.Entries[2].Rules.Objective, Is.EqualTo(MatchObjective.KillConfirmed));
            foreach (var map in playlist.MapNames)
            {
                Assert.That(ServerMapData.TryLoad(map, roots, out var data, out error), Is.True, error);
                Assert.That(data.Points.ControlPoints, Is.Not.Empty, "the Domination entry needs control points on every map of the playlist");
            }
        }

        private static int Position(PlaylistCursor cursor)
        {
            cursor.Next();
            return cursor.Position;
        }

        private ServerPlaylist Load(string id)
        {
            Assert.That(ServerPlaylist.TryLoad(id, new[] { _first.Path }, 0, out var playlist, out var error), Is.True, error);
            Assert.That(error, Is.Null);
            return playlist;
        }

        private static string Fail(string id, params string[] roots)
        {
            Assert.That(ServerPlaylist.TryLoad(id, roots, 0, out var playlist, out var error), Is.False);
            Assert.That(playlist, Is.Null);
            return error;
        }

        private static string PlaylistsDirectory(string root)
        {
            return Path.GetFullPath(Path.Combine(root, ServerMapData.DataDirectoryName, ServerRulesData.PlaylistsDirectoryName));
        }

        private static string WritePlaylists(string root, string fileName, string json)
        {
            var directory = PlaylistsDirectory(root);
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, fileName);
            File.WriteAllText(path, json);
            return path;
        }

        private static string WriteMutators(string root, string json)
        {
            var directory = Path.GetFullPath(Path.Combine(root, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, ServerPlaylist.MutatorsTuningFileName);
            File.WriteAllText(path, json);
            return path;
        }

        private static string[] RepositoryRoots()
        {
            for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ServerMapData.DataDirectoryName, ServerRulesData.PlaylistsDirectoryName)))
                {
                    return new[] { directory.FullName };
                }
            }

            Assert.Fail(ServerMapData.DataDirectoryName + "/" + ServerRulesData.PlaylistsDirectoryName + " is not above " + TestContext.CurrentContext.TestDirectory);
            return null;
        }
    }
}
