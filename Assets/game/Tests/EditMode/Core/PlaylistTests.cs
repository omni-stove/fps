using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class PlaylistTests
    {
        private static Playlist Make(PlaylistOrder order, int count, float delay = Playlist.DefaultNextDelaySeconds)
        {
            var entries = Enumerable.Range(0, count).Select(i => new PlaylistEntry("Scene" + i)).ToList();
            return new Playlist("rotation", order, entries, delay);
        }

        [Test]
        public void AnEntryKeepsItsSceneRulesAndMutatorsInOrder()
        {
            var entry = new PlaylistEntry("Arena", "TeamDeathmatch", new[] { "low-health", "no-regen" });

            Assert.That(entry.Scene, Is.EqualTo("Arena"));
            Assert.That(entry.Rules, Is.EqualTo("TeamDeathmatch"));
            Assert.That(entry.HasRules, Is.True);
            Assert.That(entry.MutatorIds, Is.EqualTo(new[] { "low-health", "no-regen" }));
        }

        [Test]
        public void AnEntryWithoutRulesOrMutatorsKeepsTheSceneRules()
        {
            var entry = new PlaylistEntry("Arena");
            var blank = new PlaylistEntry("Arena", " ");

            Assert.That(entry.Rules, Is.Empty);
            Assert.That(entry.HasRules, Is.False);
            Assert.That(entry.MutatorIds, Is.Empty);
            Assert.That(blank.HasRules, Is.False);
        }

        [Test]
        public void AnEntryCopiesTheMutatorList()
        {
            var ids = new List<string> { "low-health" };
            var entry = new PlaylistEntry("Arena", null, ids);
            ids.Add("no-regen");

            Assert.That(entry.MutatorIds, Is.EqualTo(new[] { "low-health" }));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)entry.MutatorIds).Add("x"));
        }

        [Test]
        public void AnEntryRejectsAMissingSceneOrABlankMutatorId()
        {
            Assert.That(Assert.Throws<ArgumentException>(() => new PlaylistEntry(null)).ParamName, Is.EqualTo("scene"));
            Assert.That(Assert.Throws<ArgumentException>(() => new PlaylistEntry(" ")).ParamName, Is.EqualTo("scene"));
            Assert.That(Assert.Throws<ArgumentException>(() => new PlaylistEntry("Arena", null, new[] { "a", "" })).ParamName, Is.EqualTo("mutators"));
            Assert.That(Assert.Throws<ArgumentException>(() => new PlaylistEntry("Arena", null, new string[] { null })).ParamName, Is.EqualTo("mutators"));
        }

        [Test]
        public void APlaylistKeepsItsValues()
        {
            var playlist = Make(PlaylistOrder.Shuffle, 2, 3.5f);

            Assert.That(playlist.Id, Is.EqualTo("rotation"));
            Assert.That(playlist.Order, Is.EqualTo(PlaylistOrder.Shuffle));
            Assert.That(playlist.Entries.Select(e => e.Scene), Is.EqualTo(new[] { "Scene0", "Scene1" }));
            Assert.That(playlist.NextDelaySeconds, Is.EqualTo(3.5f));
            Assert.That(new Playlist("p", PlaylistOrder.Sequential, new[] { new PlaylistEntry("A") }).NextDelaySeconds, Is.EqualTo(10f));
        }

        [Test]
        public void APlaylistRejectsBrokenValuesWithTheItemName()
        {
            var one = new[] { new PlaylistEntry("A") };

            Assert.That(Assert.Throws<ArgumentException>(() => new Playlist(" ", PlaylistOrder.Sequential, one)).ParamName, Is.EqualTo("id"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new Playlist("p", (PlaylistOrder)9, one)).ParamName, Is.EqualTo("order"));
            Assert.That(Assert.Throws<ArgumentNullException>(() => new Playlist("p", PlaylistOrder.Sequential, null)).ParamName, Is.EqualTo("entries"));
            Assert.That(
                Assert.Throws<ArgumentException>(() => new Playlist("p", PlaylistOrder.Sequential, Array.Empty<PlaylistEntry>())).ParamName,
                Is.EqualTo("entries"));
            Assert.That(
                Assert.Throws<ArgumentException>(() => new Playlist("p", PlaylistOrder.Sequential, new PlaylistEntry[] { null })).ParamName,
                Is.EqualTo("entries"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => new Playlist("p", PlaylistOrder.Sequential, one, -1f)).ParamName,
                Is.EqualTo("nextDelaySeconds"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => new Playlist("p", PlaylistOrder.Sequential, one, float.NaN)).ParamName,
                Is.EqualTo("nextDelaySeconds"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => new Playlist("p", PlaylistOrder.Sequential, one, float.PositiveInfinity)).ParamName,
                Is.EqualTo("nextDelaySeconds"));
            Assert.That(new Playlist("p", PlaylistOrder.Sequential, one, 0f).NextDelaySeconds, Is.EqualTo(0f));
        }

        [Test]
        public void FindLooksUpByIdOnly()
        {
            var first = Make(PlaylistOrder.Sequential, 1);
            var second = new Playlist("other", PlaylistOrder.Sequential, new[] { new PlaylistEntry("A") });

            Assert.That(Playlist.Find(new[] { first, second }, "other"), Is.SameAs(second));
            Assert.That(Playlist.Find(new[] { first, second }, "Other"), Is.Null);
            Assert.That(Playlist.Find(new[] { first, second }, "missing"), Is.Null);
            Assert.Throws<ArgumentNullException>(() => Playlist.Find(null, "x"));
        }

        [Test]
        public void ASequentialCursorGoesInOrderAndBackToTheFirstAfterTheLast()
        {
            var cursor = new PlaylistCursor(Make(PlaylistOrder.Sequential, 3), 42);

            var positions = new List<int> { cursor.Position };
            var rounds = new List<int> { cursor.Round };
            for (var i = 0; i < 6; i++)
            {
                cursor.Next();
                positions.Add(cursor.Position);
                rounds.Add(cursor.Round);
            }

            Assert.That(positions, Is.EqualTo(new[] { 0, 1, 2, 0, 1, 2, 0 }));
            Assert.That(rounds, Is.EqualTo(new[] { 0, 0, 0, 1, 1, 1, 2 }));
        }

        [Test]
        public void NextReturnsTheNewCurrentEntry()
        {
            var playlist = Make(PlaylistOrder.Sequential, 2);
            var cursor = new PlaylistCursor(playlist, 1);

            Assert.That(cursor.Current, Is.SameAs(playlist.Entries[0]));
            Assert.That(cursor.Next(), Is.SameAs(playlist.Entries[1]));
            Assert.That(cursor.Current, Is.SameAs(playlist.Entries[1]));
            Assert.That(cursor.Next(), Is.SameAs(playlist.Entries[0]));
        }

        [Test]
        public void AOneEntryPlaylistRepeatsItsEntry()
        {
            foreach (var order in new[] { PlaylistOrder.Sequential, PlaylistOrder.Shuffle })
            {
                var cursor = new PlaylistCursor(Make(order, 1), 7);

                cursor.Next();
                cursor.Next();

                Assert.That(cursor.Position, Is.EqualTo(0), order.ToString());
                Assert.That(cursor.Round, Is.EqualTo(2), order.ToString());
            }
        }

        [Test]
        public void AShuffledCursorPlaysEveryEntryOncePerRound()
        {
            var cursor = new PlaylistCursor(Make(PlaylistOrder.Shuffle, 5), 12345);

            var played = new List<int> { cursor.Position };
            for (var i = 1; i < 5 * 4; i++)
            {
                cursor.Next();
                played.Add(cursor.Position);
            }

            for (var round = 0; round < 4; round++)
            {
                Assert.That(played.Skip(round * 5).Take(5).OrderBy(p => p), Is.EqualTo(new[] { 0, 1, 2, 3, 4 }), "round " + round);
            }

            Assert.That(cursor.Round, Is.EqualTo(3));
        }

        [Test]
        public void AShuffledCursorNeverPlaysTheSameEntryTwiceInARowAcrossRounds()
        {
            for (var seed = 0; seed < 50; seed++)
            {
                var cursor = new PlaylistCursor(Make(PlaylistOrder.Shuffle, 3), seed);
                var previous = cursor.Position;
                for (var i = 0; i < 30; i++)
                {
                    cursor.Next();
                    Assert.That(cursor.Position, Is.Not.EqualTo(previous), "seed " + seed + " step " + i);
                    previous = cursor.Position;
                }
            }
        }

        [Test]
        public void TheSameSeedGivesTheSameShuffleAndOtherSeedsDiffer()
        {
            var playlist = Make(PlaylistOrder.Shuffle, 6);

            var first = Positions(new PlaylistCursor(playlist, 99), 18);
            var again = Positions(new PlaylistCursor(playlist, 99), 18);

            Assert.That(again, Is.EqualTo(first));

            var distinct = new HashSet<string>();
            for (var seed = 0; seed < 20; seed++)
            {
                distinct.Add(string.Join(",", Positions(new PlaylistCursor(playlist, seed), 6)));
            }

            Assert.That(distinct.Count, Is.GreaterThan(1), "the seed changes the order");
        }

        [Test]
        public void TheCursorRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new PlaylistCursor(null, 1));
        }

        [Test]
        public void TheLogsNameThePlaylistTheEntryAndTheReason()
        {
            var playlist = new Playlist(
                "rotation",
                PlaylistOrder.Sequential,
                new[] { new PlaylistEntry("Arena", "TeamDeathmatch", new[] { "low-health", "no-regen" }), new PlaylistEntry("Yard") },
                2.5f);
            var cursor = new PlaylistCursor(playlist, 1);
            var available = new[] { new GameRulesMutator("low-health", "{}"), new GameRulesMutator("no-regen", "{}") };

            Assert.That(PlaylistLog.Started(playlist), Is.EqualTo("[Playlist] started id=rotation order=Sequential entries=2"));
            Assert.That(
                PlaylistLog.Loading(cursor),
                Is.EqualTo("[Playlist] id=rotation entry 1/2 round=1: scene=Arena rules=TeamDeathmatch mutators=low-health, no-regen"));
            cursor.Next();
            cursor.Next();
            cursor.Next();
            Assert.That(PlaylistLog.Loading(cursor), Is.EqualTo("[Playlist] id=rotation entry 2/2 round=2: scene=Yard rules=(scene) mutators=none"));
            Assert.That(PlaylistLog.MatchFinished(playlist, 2.5f), Is.EqualTo("[Playlist] id=rotation match finished; loading the next entry in 2.5s"));
            Assert.That(
                PlaylistLog.UnknownMutator(playlist, 0, "missing", available),
                Is.EqualTo("[Playlist] id=rotation entry 1/2 skipped mutator id=missing: no mutator has this id; known ids are low-health, no-regen"));
            Assert.That(
                PlaylistLog.UnknownMutator(playlist, 1, "missing", Array.Empty<GameRulesMutator>()),
                Is.EqualTo("[Playlist] id=rotation entry 2/2 skipped mutator id=missing: no mutator has this id; known ids are (none)"));
            Assert.That(
                PlaylistLog.SceneNotLoaded(playlist, 1, "Yard"),
                Is.EqualTo("[Playlist] id=rotation entry 2/2 skipped: scene 'Yard' cannot be loaded; add it to the Build Settings"));
            Assert.That(PlaylistLog.NoSceneLoaded(playlist), Is.EqualTo("[Playlist] id=rotation stopped: no entry has a scene that can be loaded"));
            Assert.That(PlaylistLog.Stopped(playlist), Is.EqualTo("[Playlist] id=rotation stopped"));
            Assert.That(
                PlaylistLog.UnknownRules(playlist, new[] { "Missing" }, new[] { "TeamDeathmatch", "Domination" }),
                Is.EqualTo("playlist 'rotation' names rules that are not in the rules table: Missing; known rules are Domination, TeamDeathmatch"));
            Assert.That(
                PlaylistLog.UnknownRules(playlist, new[] { "Missing" }, Array.Empty<string>()),
                Is.EqualTo("playlist 'rotation' names rules that are not in the rules table: Missing; known rules are (none)"));
        }

        private static List<int> Positions(PlaylistCursor cursor, int count)
        {
            var positions = new List<int> { cursor.Position };
            for (var i = 1; i < count; i++)
            {
                cursor.Next();
                positions.Add(cursor.Position);
            }

            return positions;
        }
    }
}
