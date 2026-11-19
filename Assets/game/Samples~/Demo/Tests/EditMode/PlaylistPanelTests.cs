using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class PlaylistPanelTests
    {
        private static readonly GameRulesMutator _lowHealth = new GameRulesMutator("low-health", "{ \"playerMaxHealth\": 50 }");
        private static readonly GameRulesMutator _noRegen = new GameRulesMutator("no-regen", "{ \"regenPerSecond\": 0 }");
        private static readonly GameRulesMutator[] _available = { _lowHealth, _noRegen };

        private static readonly GameRules _teamRules = GameRules.Default.WithRegenPerSecond(11f);

        private static readonly string[] _bothMutators = { "low-health", "no-regen" };
        private static readonly string[] _noRegenOnly = { "no-regen" };
        private static readonly string[] _unnamedMutator = { "moon-gravity" };
        private static readonly string[] _rotationLinesJa = { "Arena (体力半分、自然回復なし)", "Yard" };
        private static readonly string[] _rotationLinesEn = { "Arena (Half health, No regeneration)", "Yard" };
        private static readonly string[] _mixedLinesJa = { "Range (自然回復なし)" };
        private static readonly string[] _unnamedMutatorLines = { "Arena (moon-gravity)" };
        private static readonly string[] _arenaOnly = { "Arena" };
        private static readonly string[] _rangeOnly = { "Range" };
        private static readonly GameLanguage[] _languages = { GameLanguage.Japanese, GameLanguage.English };

        private readonly List<string> _loaded = new List<string>();
        private readonly HashSet<string> _missingScenes = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<Playlist> _started = new List<Playlist>();

        private GameObject _host;
        private PlaylistRunner _runner;
        private VisualElement _container;
        private PlaylistPanel _panel;
        private int _cancelled;
        private GameLanguage _languageBefore;

        private static Playlist Rotation() => new Playlist(
            "rotation",
            PlaylistOrder.Sequential,
            new[]
            {
                new PlaylistEntry("Arena", "TeamDeathmatch", _bothMutators),
                new PlaylistEntry("Assets/Scenes/Yard.unity"),
            });

        private static Playlist Mixed() => new Playlist(
            "mixed",
            PlaylistOrder.Shuffle,
            new[] { new PlaylistEntry("Range", null, _noRegenOnly) });

        private static Dictionary<string, GameRules> RulesTable() =>
            new Dictionary<string, GameRules>(StringComparer.Ordinal) { { "TeamDeathmatch", _teamRules } };

        [SetUp]
        public void SetUp()
        {
            _languageBefore = Localizer.Language;
            Localizer.Apply(GameLanguage.Japanese);
            GameRulesOverride.Clear();
            GameRulesOverride.ClearMutators();
            _loaded.Clear();
            _missingScenes.Clear();
            _started.Clear();
            _cancelled = 0;
            _panel = null;
            _container = new VisualElement();

            _host = new GameObject("PlaylistRunner");
            _runner = _host.AddComponent<PlaylistRunner>();
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
            _panel?.Dispose();
            if (_runner != null)
            {
                _runner.Stop();
            }

            Object.DestroyImmediate(_host);
            GameRulesOverride.Clear();
            GameRulesOverride.ClearMutators();
            Localizer.Apply(_languageBefore);
        }

        private void Open(IReadOnlyList<Playlist> playlists, IReadOnlyDictionary<string, GameRules> rules = null)
        {
            _panel = new PlaylistPanel(_container, _runner, playlists, rules);
            _panel.Started += playlist => _started.Add(playlist);
            _panel.Cancelled += () => _cancelled++;
        }

        [Test]
        public void ListsEveryPlaylistWithTheModeAndMutatorsOfEachEntry()
        {
            Open(new[] { Rotation(), Mixed() }, RulesTable());

            Assert.That(_panel.Count, Is.EqualTo(2));
            Assert.That(_panel.EmptyNotice, Is.Null);

            Assert.That(_panel.PlaylistName(0), Is.EqualTo("rotation"), "a playlist without a name in the table shows its id");
            Assert.That(_panel.OrderText(0), Is.EqualTo("順番に回す"));
            Assert.That(
                _panel.EntryTexts(0),
                Is.EqualTo(_rotationLinesJa),
                "mutator names come from the table in the entry's order, and a scene path shows only the scene name");

            Assert.That(_panel.PlaylistName(1), Is.EqualTo("mixed"));
            Assert.That(_panel.OrderText(1), Is.EqualTo("一巡ごとに混ぜる"));
            Assert.That(_panel.EntryTexts(1), Is.EqualTo(_mixedLinesJa));

            Assert.That(_container.Query<Button>(className: PlaylistPanel.PlaylistClass).ToList(), Has.Count.EqualTo(2));
            Assert.That(_container.Query<Label>(className: PlaylistPanel.EntryClass).ToList(), Has.Count.EqualTo(3));
        }

        [Test]
        public void AMutatorWithoutANameInTheTableShowsItsId()
        {
            var playlist = new Playlist("odd", PlaylistOrder.Sequential, new[] { new PlaylistEntry("Arena", null, _unnamedMutator) });

            Open(new[] { playlist });

            Assert.That(_panel.EntryTexts(0), Is.EqualTo(_unnamedMutatorLines));
        }

        [Test]
        public void NamesAreLookedUpByKeysBuiltFromTheData()
        {
            Assert.That(PlaylistPanel.NameKeyOf("rotation"), Is.EqualTo("playlist.name.rotation"));
            Assert.That(PlaylistPanel.ModeKeyOf("Assets/Scenes/Yard.unity"), Is.EqualTo("playlist.mode.Yard"));
            Assert.That(PlaylistPanel.ModeKeyOf("Yard"), Is.EqualTo("playlist.mode.Yard"));
            Assert.That(PlaylistPanel.MutatorKeyOf("low-health"), Is.EqualTo("playlist.mutator.low-health"));
            Assert.That(PlaylistPanel.MutatorOf("low-health"), Is.EqualTo(new LocalizedText("playlist.mutator.low-health")));
            Assert.That(PlaylistPanel.MutatorOf("moon-gravity"), Is.EqualTo(LocalizedText.Literal("moon-gravity")));
            Assert.That(PlaylistPanel.NameOf(Rotation()), Is.EqualTo(LocalizedText.Literal("rotation")));
        }

        [Test]
        public void TheStaticTextsComeFromTheTable()
        {
            Open(new[] { Rotation() });

            Assert.That(_panel.Title, Is.EqualTo("プレイリスト"));
            Assert.That(_panel.BackButton.text, Is.EqualTo("戻る"));
            Assert.That(_panel.Status, Is.Null);
        }

        [Test]
        public void WithNoPlaylistsItSaysSoAndOnlyBackIsOffered()
        {
            Open(Array.Empty<Playlist>());

            Assert.That(_panel.Count, Is.Zero);
            Assert.That(_panel.EmptyNotice, Is.EqualTo("選べるプレイリストがありません"));
            Assert.That(_panel.BackButton.enabledSelf, Is.True);
        }

        [Test]
        public void ChoosingAPlaylistStartsItWithTheRunner()
        {
            var rotation = Rotation();
            Open(new[] { Mixed(), rotation }, RulesTable());

            LogAssert.Expect(LogType.Log, new Regex(@"^\[PlaylistPanel\] starting playlist 'rotation'"));
            Assert.That(_panel.Start(1), Is.True);

            Assert.That(_runner.IsRunning, Is.True);
            Assert.That(_runner.Playlist, Is.SameAs(rotation));
            Assert.That(_loaded, Is.EqualTo(_arenaOnly));
            Assert.That(GameRulesOverride.Current, Is.SameAs(_teamRules), "the rules table given to the panel reaches the runner");
            Assert.That(GameRulesOverride.Mutators, Is.EqualTo(new[] { _lowHealth, _noRegen }));
            Assert.That(_started, Is.EqualTo(new[] { rotation }));
            Assert.That(_panel.HasStarted, Is.True);
            Assert.That(_panel.Status, Is.EqualTo("rotation を始めています…"));
            Assert.That(_panel.IsStatusError, Is.False);
        }

        [Test]
        public void AfterStartingNothingElseCanBeChosen()
        {
            Open(new[] { Mixed(), Rotation() }, RulesTable());
            _panel.Start(0);

            Assert.That(_panel.PlaylistButton(0).enabledSelf, Is.False);
            Assert.That(_panel.PlaylistButton(1).enabledSelf, Is.False);
            Assert.That(_panel.BackButton.enabledSelf, Is.False);
            Assert.That(_panel.Start(1), Is.False);
            _panel.Cancel();

            Assert.That(_loaded, Is.EqualTo(_rangeOnly));
            Assert.That(_runner.Playlist.Id, Is.EqualTo("mixed"));
            Assert.That(_started, Has.Count.EqualTo(1));
            Assert.That(_cancelled, Is.Zero);
        }

        [Test]
        public void APlaylistWhoseRulesAreMissingIsNotStartedAndSaysWhy()
        {
            Open(new[] { Rotation() });

            LogAssert.Expect(LogType.Error, new Regex(@"^\[PlaylistPanel\] could not start playlist 'rotation': .*TeamDeathmatch"));
            Assert.That(_panel.Start(0), Is.False);

            Assert.That(_runner.IsRunning, Is.False);
            Assert.That(_loaded, Is.Empty);
            Assert.That(_started, Is.Empty);
            Assert.That(_panel.HasStarted, Is.False);
            Assert.That(_panel.Status, Is.EqualTo("rotation を始められません: プレイリストのデータを確かめてください"));
            Assert.That(_panel.IsStatusError, Is.True);
            Assert.That(_panel.PlaylistButton(0).enabledSelf, Is.True);
        }

        [Test]
        public void APlaylistWithNoLoadableSceneIsNotStartedAndSaysWhy()
        {
            _missingScenes.Add("Range");
            Open(new[] { Mixed() });

            // どのシーンも読めないときは Runner が項目ごとの理由をエラーで出すので、パネルの扱いだけを見る
            LogAssert.ignoreFailingMessages = true;
            Assert.That(_panel.Start(0), Is.False);

            Assert.That(_runner.IsRunning, Is.False);
            Assert.That(_started, Is.Empty);
            Assert.That(_panel.HasStarted, Is.False);
            Assert.That(_panel.Status, Is.EqualTo("mixed を始められません: どのモードのシーンもこのビルドにありません"));
            Assert.That(_panel.IsStatusError, Is.True);
            Assert.That(_panel.PlaylistButton(0).enabledSelf, Is.True);
        }

        [Test]
        public void AnIndexOutsideTheListIsRejected()
        {
            Open(new[] { Rotation() });

            Assert.That(() => _panel.Start(1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => _panel.Start(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ANullPlaylistOrRunnerIsRejected()
        {
            Assert.That(() => new PlaylistPanel(_container, null, new[] { Rotation() }), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => new PlaylistPanel(_container, _runner, new Playlist[] { null }), Throws.ArgumentException);
        }

        [Test]
        public void TheTextsFollowTheLanguage()
        {
            Open(new[] { Rotation(), Mixed() }, RulesTable());
            _panel.Start(0);

            Localizer.Apply(GameLanguage.English);

            Assert.That(_panel.Title, Is.EqualTo("PLAYLISTS"));
            Assert.That(_panel.BackButton.text, Is.EqualTo("BACK"));
            Assert.That(_panel.OrderText(0), Is.EqualTo("In order"));
            Assert.That(_panel.OrderText(1), Is.EqualTo("Shuffled each round"));
            Assert.That(_panel.EntryTexts(0), Is.EqualTo(_rotationLinesEn));
            Assert.That(_panel.Status, Is.EqualTo("Starting rotation…"));
        }

        [Test]
        public void BackCancelsThePanel()
        {
            Open(new[] { Rotation() });

            _panel.Cancel();

            Assert.That(_cancelled, Is.EqualTo(1));
            Assert.That(_runner.IsRunning, Is.False);
        }

        [Test]
        public void NothingStartsAfterDisposing()
        {
            Open(new[] { Rotation() }, RulesTable());
            _panel.Dispose();

            Assert.That(_container.childCount, Is.Zero);
            Assert.That(_panel.Start(0), Is.False);
            _panel.Cancel();
            Assert.That(_runner.IsRunning, Is.False);
            Assert.That(_cancelled, Is.Zero);
        }

        [Test]
        public void EveryPlaylistKeyIsInBothLanguages()
        {
            var keys = new List<string>
            {
                PlaylistPanel.TitleKey, PlaylistPanel.EmptyKey, PlaylistPanel.BackKey,
                PlaylistPanel.EntryKey, PlaylistPanel.EntryWithMutatorsKey, PlaylistPanel.MutatorSeparatorKey,
                PlaylistPanel.StartingKey, PlaylistPanel.CannotStartKey, PlaylistPanel.NoSceneKey,
            };
            foreach (PlaylistOrder order in Enum.GetValues(typeof(PlaylistOrder)))
            {
                keys.Add(PlaylistPanel.OrderKey(order));
            }

            foreach (var mutator in KitTuning.Mutators)
            {
                keys.Add(PlaylistPanel.MutatorKeyOf(mutator.Id));
            }

            Assert.That(KitTuning.Playlists, Is.Not.Empty, "the bundled example names its playlists and modes");
            foreach (var playlist in KitTuning.Playlists)
            {
                keys.Add(PlaylistPanel.NameKeyOf(playlist.Id));
                foreach (var entry in playlist.Entries)
                {
                    keys.Add(PlaylistPanel.ModeKeyOf(entry.Scene));
                }
            }

            foreach (var language in _languages)
            {
                var table = Localizer.TableOf(language);
                foreach (var key in keys)
                {
                    Assert.That(table.Contains(key), Is.True, language + " " + key);
                }
            }
        }
    }
}
