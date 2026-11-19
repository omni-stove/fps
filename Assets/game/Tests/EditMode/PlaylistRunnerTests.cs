using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class PlaylistRunnerTests
    {
        private static readonly GameRulesMutator _lowHealth = new GameRulesMutator("low-health", "{ \"playerMaxHealth\": 50 }");
        private static readonly GameRulesMutator _noRegen = new GameRulesMutator("no-regen", "{ \"regenPerSecond\": 0 }");
        private static readonly GameRulesMutator[] _available = { _lowHealth, _noRegen };

        private static readonly GameRules _teamRules = GameRules.Default.WithRegenPerSecond(11f);
        private static readonly GameRules _dominationRules = GameRules.Default.WithRegenPerSecond(22f);

        private readonly List<Object> _created = new List<Object>();
        private readonly List<string> _loaded = new List<string>();
        private readonly HashSet<string> _missingScenes = new HashSet<string>(StringComparer.Ordinal);

        private PlaylistRunner _runner;

        private static Dictionary<string, GameRules> RulesTable() =>
            new Dictionary<string, GameRules>(StringComparer.Ordinal) { { "TeamDeathmatch", _teamRules }, { "Domination", _dominationRules } };

        private static Playlist ThreeEntries(float delay = 5f) => new Playlist(
            "rotation",
            PlaylistOrder.Sequential,
            new[]
            {
                new PlaylistEntry("Arena", "TeamDeathmatch", new[] { "low-health" }),
                new PlaylistEntry("Yard", "Domination", new[] { "no-regen", "low-health" }),
                new PlaylistEntry("Range"),
            },
            delay);

        [SetUp]
        public void SetUp()
        {
            GameRulesOverride.Clear();
            GameRulesOverride.ClearMutators();
            _loaded.Clear();
            _missingScenes.Clear();

            var host = Track(new GameObject("PlaylistRunner"));
            _runner = host.AddComponent<PlaylistRunner>();
            _runner.Mutators = _available;
            _runner.Seed = 1;
            _runner.SceneLoader = scene =>
            {
                if (_missingScenes.Contains(scene))
                {
                    return false;
                }

                _loaded.Add(scene);
                return true;
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (_runner != null)
            {
                _runner.Stop();
            }

            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            GameRulesOverride.Clear();
            GameRulesOverride.ClearMutators();
        }

        [Test]
        public void BeginStacksTheFirstEntryAndLoadsItsScene()
        {
            _runner.Begin(ThreeEntries(), RulesTable());

            Assert.That(_runner.IsRunning, Is.True);
            Assert.That(_loaded, Is.EqualTo(new[] { "Arena" }));
            Assert.That(GameRulesOverride.Current, Is.SameAs(_teamRules));
            Assert.That(GameRulesOverride.Mutators, Is.EqualTo(new[] { _lowHealth }));
            Assert.That(_runner.CurrentEntry.Scene, Is.EqualTo("Arena"));
            Assert.That(_runner.IsAdvancePending, Is.False);
        }

        [Test]
        public void AFinishedMatchLoadsTheNextEntryAfterTheDelay()
        {
            _runner.Begin(ThreeEntries(5f), RulesTable());

            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 3, 1, 30));

            Assert.That(_runner.IsAdvancePending, Is.True);
            Assert.That(_runner.RemainingDelaySeconds, Is.EqualTo(5f));
            _runner.Tick(4.9f);
            Assert.That(_loaded, Is.EqualTo(new[] { "Arena" }), "the result is shown until the delay runs out");
            Assert.That(GameRulesOverride.Current, Is.SameAs(_teamRules));

            _runner.Tick(0.2f);

            Assert.That(_loaded, Is.EqualTo(new[] { "Arena", "Yard" }));
            Assert.That(GameRulesOverride.Current, Is.SameAs(_dominationRules));
            Assert.That(GameRulesOverride.Mutators, Is.EqualTo(new[] { _noRegen, _lowHealth }), "the entry's mutators replace the last entry's, in order");
            Assert.That(_runner.IsAdvancePending, Is.False);
            Assert.That(_runner.RemainingDelaySeconds, Is.EqualTo(0f));
        }

        [Test]
        public void AnEntryWithoutRulesDropsTheLastEntryRulesAndMutators()
        {
            _runner.Begin(ThreeEntries(), RulesTable());

            _runner.Advance();
            _runner.Advance();

            Assert.That(_loaded.Last(), Is.EqualTo("Range"));
            Assert.That(GameRulesOverride.Current, Is.Null, "the scene's own rules are used");
            Assert.That(GameRulesOverride.Mutators, Is.Empty);
        }

        [Test]
        public void AfterTheLastEntryTheFirstComesAgain()
        {
            _runner.Begin(ThreeEntries(0f), RulesTable());

            for (var i = 0; i < 4; i++)
            {
                CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Lost, 0, 1, 0));
                _runner.Tick(0f);
            }

            Assert.That(_loaded, Is.EqualTo(new[] { "Arena", "Yard", "Range", "Arena", "Yard" }));
            Assert.That(_runner.Cursor.Round, Is.EqualTo(1));
            Assert.That(GameRulesOverride.Current, Is.SameAs(_dominationRules));
        }

        [Test]
        public void ASecondFinishForTheSameMatchDoesNotSkipAnEntry()
        {
            _runner.Begin(ThreeEntries(1f), RulesTable());

            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 1, 0, 10));
            _runner.Tick(0.5f);
            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 1, 0, 10));
            _runner.Tick(0.5f);
            _runner.Tick(5f);

            Assert.That(_loaded, Is.EqualTo(new[] { "Arena", "Yard" }));
        }

        [Test]
        public void NothingAdvancesWithoutAFinishedMatch()
        {
            _runner.Begin(ThreeEntries(0f), RulesTable());

            _runner.Tick(100f);

            Assert.That(_loaded, Is.EqualTo(new[] { "Arena" }));
        }

        [Test]
        public void AnUnknownMutatorIsSkippedWithTheReasonAndTheRestAreStacked()
        {
            var playlist = new Playlist(
                "rotation",
                PlaylistOrder.Sequential,
                new[] { new PlaylistEntry("Arena", "TeamDeathmatch", new[] { "missing", "no-regen" }) });

            LogAssert.Expect(LogType.Error, PlaylistLog.UnknownMutator(playlist, 0, "missing", _available));
            _runner.Begin(playlist, RulesTable());

            Assert.That(
                PlaylistLog.UnknownMutator(playlist, 0, "missing", _available),
                Does.Contain("id=missing").And.Contain("known ids are low-health, no-regen"));
            Assert.That(GameRulesOverride.Mutators, Is.EqualTo(new[] { _noRegen }));
            Assert.That(GameRulesOverride.Current, Is.SameAs(_teamRules));
            Assert.That(_loaded, Is.EqualTo(new[] { "Arena" }), "an unknown mutator does not stop the match");
        }

        [Test]
        public void AnUnknownRulesNameStopsBeginBeforeAnythingIsLoaded()
        {
            var playlist = new Playlist(
                "rotation",
                PlaylistOrder.Sequential,
                new[] { new PlaylistEntry("Arena", "TeamDeathmatch"), new PlaylistEntry("Yard", "Hardpoint") });

            var e = Assert.Throws<ArgumentException>(() => _runner.Begin(playlist, RulesTable()));

            Assert.That(e.ParamName, Is.EqualTo("rules"));
            Assert.That(e.Message, Does.Contain("Hardpoint").And.Contain("Domination, TeamDeathmatch"));
            Assert.That(_runner.IsRunning, Is.False);
            Assert.That(_loaded, Is.Empty);
            Assert.That(GameRulesOverride.Current, Is.Null);

            Assert.Throws<ArgumentException>(() => _runner.Begin(playlist, null), "no table means no rules name is known");
        }

        [Test]
        public void ASceneThatCannotBeLoadedIsSkippedWithTheReason()
        {
            _missingScenes.Add("Yard");
            var playlist = ThreeEntries(0f);
            _runner.Begin(playlist, RulesTable());

            LogAssert.Expect(LogType.Error, PlaylistLog.SceneNotLoaded(playlist, 1, "Yard"));
            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 1, 0, 10));
            _runner.Tick(0f);

            Assert.That(_loaded, Is.EqualTo(new[] { "Arena", "Range" }));
            Assert.That(_runner.CurrentEntry.Scene, Is.EqualTo("Range"));
            Assert.That(GameRulesOverride.Current, Is.Null, "the skipped entry's rules are not left behind");
            Assert.That(GameRulesOverride.Mutators, Is.Empty);
        }

        [Test]
        public void WhenNoSceneCanBeLoadedThePlaylistStops()
        {
            _missingScenes.UnionWith(new[] { "Arena", "Yard", "Range" });
            var playlist = ThreeEntries();

            LogAssert.Expect(LogType.Error, PlaylistLog.SceneNotLoaded(playlist, 0, "Arena"));
            LogAssert.Expect(LogType.Error, PlaylistLog.SceneNotLoaded(playlist, 1, "Yard"));
            LogAssert.Expect(LogType.Error, PlaylistLog.SceneNotLoaded(playlist, 2, "Range"));
            LogAssert.Expect(LogType.Error, PlaylistLog.NoSceneLoaded(playlist));
            _runner.Begin(playlist, RulesTable());

            Assert.That(_runner.IsRunning, Is.False);
            Assert.That(_loaded, Is.Empty);
            Assert.That(GameRulesOverride.Current, Is.Null);
            Assert.That(GameRulesOverride.Mutators, Is.Empty);
        }

        [Test]
        public void StopDropsTheStackedRulesAndIgnoresLaterMatches()
        {
            _runner.Begin(ThreeEntries(0f), RulesTable());

            _runner.Stop();
            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 1, 0, 10));
            _runner.Tick(1f);

            Assert.That(_runner.IsRunning, Is.False);
            Assert.That(_loaded, Is.EqualTo(new[] { "Arena" }));
            Assert.That(GameRulesOverride.Current, Is.Null);
            Assert.That(GameRulesOverride.Mutators, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => _runner.Advance());
        }

        [Test]
        public void BeginAgainRestartsFromTheFirstEntry()
        {
            _runner.Begin(ThreeEntries(), RulesTable());
            _runner.Advance();

            _runner.Begin(ThreeEntries(), RulesTable());

            Assert.That(_loaded, Is.EqualTo(new[] { "Arena", "Yard", "Arena" }));
            Assert.That(GameRulesOverride.Mutators, Is.EqualTo(new[] { _lowHealth }));
        }

        [Test]
        public void AShuffledPlaylistLoadsEverySceneOncePerRound()
        {
            var playlist = new Playlist("mixed", PlaylistOrder.Shuffle, ThreeEntries().Entries);
            _runner.Seed = 2024;

            _runner.Begin(playlist, RulesTable());
            _runner.Advance();
            _runner.Advance();

            Assert.That(_loaded.OrderBy(s => s, StringComparer.Ordinal), Is.EqualTo(new[] { "Arena", "Range", "Yard" }));
        }

        [Test]
        public void BeginByIdFindsThePlaylistOrListsTheKnownIds()
        {
            var playlists = new[] { ThreeEntries(), new Playlist("other", PlaylistOrder.Sequential, new[] { new PlaylistEntry("Range") }) };

            _runner.Begin("other", playlists, RulesTable());
            Assert.That(_loaded, Is.EqualTo(new[] { "Range" }));

            var e = Assert.Throws<ArgumentException>(() => _runner.Begin("missing", playlists, RulesTable()));
            Assert.That(e.ParamName, Is.EqualTo("playlistId"));
            Assert.That(e.Message, Does.Contain("'missing'").And.Contain("rotation, other"));
        }

        [Test]
        public void BeginWithADefinitionUsesItsRulesTable()
        {
            var team = Track(ScriptableObject.CreateInstance<GameRulesDefinition>());
            team.RegenPerSecond = 13f;
            var definition = Track(ScriptableObject.CreateInstance<PlaylistDefinition>());
            definition.name = "Rotation";
            definition.Id = "rotation";
            definition.NextDelaySeconds = 2f;
            definition.SetEntries(new[]
            {
                new PlaylistDefinition.Entry("Arena", "TeamDeathmatch", new[] { "low-health" }),
                new PlaylistDefinition.Entry("Range"),
            });
            definition.SetRules(new[] { new PlaylistDefinition.NamedRules("TeamDeathmatch", team) });

            _runner.Begin(definition);

            Assert.That(_runner.Playlist.Id, Is.EqualTo("rotation"));
            Assert.That(_runner.Playlist.NextDelaySeconds, Is.EqualTo(2f));
            Assert.That(_loaded, Is.EqualTo(new[] { "Arena" }));
            Assert.That(GameRulesOverride.Current.RegenPerSecond, Is.EqualTo(13f));
            Assert.That(GameRulesOverride.Mutators, Is.EqualTo(new[] { _lowHealth }));
        }

        [Test]
        public void ABrokenDefinitionIsRejectedWithItsName()
        {
            var team = Track(ScriptableObject.CreateInstance<GameRulesDefinition>());
            var definition = Track(ScriptableObject.CreateInstance<PlaylistDefinition>());
            definition.name = "Broken";
            definition.Id = "broken";

            Assert.That(Assert.Throws<InvalidOperationException>(() => definition.ToPlaylist()).Message, Does.Contain("'Broken'"), "no entries");

            definition.SetEntries(new[] { new PlaylistDefinition.Entry(string.Empty) });
            Assert.That(Assert.Throws<InvalidOperationException>(() => definition.ToPlaylist()).Message, Does.Contain("entry 0"));

            definition.SetRules(new[] { new PlaylistDefinition.NamedRules("TeamDeathmatch", null) });
            Assert.That(Assert.Throws<InvalidOperationException>(() => definition.RulesByName()).Message, Does.Contain("no GameRulesDefinition"));

            definition.SetRules(new[] { new PlaylistDefinition.NamedRules(" ", team) });
            Assert.That(Assert.Throws<InvalidOperationException>(() => definition.RulesByName()).Message, Does.Contain("has no name"));

            definition.SetRules(new[] { new PlaylistDefinition.NamedRules("A", team), new PlaylistDefinition.NamedRules("A", team) });
            Assert.That(Assert.Throws<InvalidOperationException>(() => definition.RulesByName()).Message, Does.Contain("more than one rules named 'A'"));

            Assert.That(_runner.IsRunning, Is.False);
            Assert.Throws<ArgumentNullException>(() => _runner.Begin((PlaylistDefinition)null));
        }

        [Test]
        public void TickRejectsABrokenDelta()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _runner.Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _runner.Tick(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => _runner.Tick(float.PositiveInfinity));
        }

        [Test]
        public void TheBundledPlaylistsFileReads()
        {
            var asset = Resources.Load<TextAsset>(KitTuning.PlaylistsResourcePath);
            Assert.That(asset, Is.Not.Null, "Resources/" + KitTuning.PlaylistsResourcePath + ".json is bundled");

            var playlists = PlaylistJson.Parse(asset.text, "Resources/" + KitTuning.PlaylistsResourcePath + ".json");
            Resources.UnloadAsset(asset);

            Assert.That(playlists, Is.Empty, "the kit ships no playlist; a game adds its own scenes");
            Assert.That(PlaylistJson.ResourcePath, Is.EqualTo(KitTuning.PlaylistsResourcePath));
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
