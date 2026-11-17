using System.Linq;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では UIDocument が木を作らないことがあるので、Hud.uxml を複製した木を直接 Bind する。
    public sealed class ScoreboardViewTests
    {
        private GameObject _host;
        private ScoreboardView _view;
        private VisualElement _root;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _host = new GameObject("TestScoreboard");
            _view = _host.AddComponent<ScoreboardView>();
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            _root = layout.CloneTree();
            _view.Bind(_root);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            GameSignals.ResetAll();
        }

        private static ScoreboardEntry Entry(
            int id, Team team, int kills, int deaths,
            ScoreboardConnection connection = ScoreboardConnection.Connected, bool isLocal = false)
        {
            return new ScoreboardEntry(id, ScoreboardName.Player(id), team, kills, deaths, connection, isLocal);
        }

        private VisualElement[] PlayerRows()
        {
            return _view.Element.Query<VisualElement>(className: ScoreboardView.PlayerRowClass).ToList().ToArray();
        }

        private static string[] Texts(VisualElement row)
        {
            return row.Query<Label>().ToList().Select(label => label.text).ToArray();
        }

        private bool IsShown => _view.Element.style.display.value != DisplayStyle.None;

        [Test]
        public void StartsClosedAndHidden()
        {
            Assert.That(_view.IsOpen, Is.False);
            Assert.That(IsShown, Is.False);
            Assert.That(_view.Element.ClassListContains(ScoreboardView.BoardClass), Is.True);
            Assert.That(_view.Sections.Select(s => s.Team), Is.EqualTo(new[] { Team.Alpha, Team.Bravo }));
            Assert.That(_view.Sections.All(s => s.Rows.Count == 0), Is.True);
        }

        [Test]
        public void OpeningListsEachTeamByKills()
        {
            GameSignals.Scoreboard.Publish(
                new[]
                {
                    Entry(0, Team.Alpha, 1, 2, isLocal: true),
                    Entry(1, Team.Bravo, 4, 0),
                    Entry(2, Team.Alpha, 3, 1, ScoreboardConnection.Disconnected),
                });

            _view.SetOpen(true);

            Assert.That(IsShown, Is.True);
            var teams = _view.Element.Query<VisualElement>(className: ScoreboardView.TeamRowClass).ToList();
            Assert.That(teams.Select(t => Texts(t)[0]), Is.EqualTo(new[] { "ALPHA", "BRAVO" }));
            Assert.That(Texts(teams[0]).Skip(1).Take(2), Is.EqualTo(new[] { "4", "3" }), "team totals");

            var rows = PlayerRows();
            Assert.That(rows.Select(r => Texts(r)[0]), Is.EqualTo(new[] { "Player 2", "Player 0", "Player 1" }));
            Assert.That(Texts(rows[0]), Is.EqualTo(new[] { "Player 2", "3", "1", Localizer.Resolve(ScoreboardText.DisconnectedText) }));
            Assert.That(Texts(rows[1]), Is.EqualTo(new[] { "Player 0", "1", "2", Localizer.Resolve(ScoreboardText.ConnectedText) }));
            Assert.That(rows[0].ClassListContains(ScoreboardView.DisconnectedRowClass), Is.True);
            Assert.That(rows[1].ClassListContains(ScoreboardView.LocalRowClass), Is.True);
            Assert.That(rows[2].ClassListContains(ScoreboardView.LocalRowClass), Is.False);
        }

        [Test]
        public void KeyedAndLiteralNamesAreShownInTheCurrentLanguage()
        {
            GameSignals.Scoreboard.Publish(
                new[]
                {
                    new ScoreboardEntry(0, ScoreboardName.LocalPlayer, Team.Alpha, 3, 0, ScoreboardConnection.None, true),
                    new ScoreboardEntry(1, ScoreboardName.Bot(1), Team.Alpha, 2, 0, ScoreboardConnection.None, false),
                    new ScoreboardEntry(2, ScoreboardName.Literal("enemy-1"), Team.Alpha, 1, 0, ScoreboardConnection.None, false),
                    new ScoreboardEntry(3, ScoreboardName.Target(2), Team.Alpha, 0, 0, ScoreboardConnection.None, false),
                });

            _view.SetOpen(true);

            var you = Localizer.Resolve(ScoreboardText.LocalPlayerName);
            var target = Localizer.Resolve(ScoreboardText.TargetName(2));
            Assert.That(you, Does.Not.StartWith("hud."), "the key exists in the current language's table");
            Assert.That(target, Does.Not.StartWith("hud."), "the key exists in the current language's table");
            Assert.That(PlayerRows().Select(r => Texts(r)[0]), Is.EqualTo(new[] { you, "[BOT] Bot 1", "enemy-1", target }));
            Assert.That(Texts(PlayerRows()[0])[3], Is.Empty, "no connection state offline");
        }

        [Test]
        public void ClosingHidesTheList()
        {
            _view.SetOpen(true);
            _view.SetOpen(false);

            Assert.That(_view.IsOpen, Is.False);
            Assert.That(IsShown, Is.False);
        }

        [Test]
        public void NewEntriesRedrawRightAwayWhileOpen()
        {
            GameSignals.Scoreboard.Publish(new[] { Entry(0, Team.Alpha, 0, 0), Entry(1, Team.Bravo, 0, 0) });
            _view.SetOpen(true);

            GameSignals.Scoreboard.Publish(new[] { Entry(0, Team.Alpha, 1, 0), Entry(1, Team.Bravo, 0, 1) });

            var rows = PlayerRows();
            Assert.That(Texts(rows[0]).Take(3), Is.EqualTo(new[] { "Player 0", "1", "0" }));
            Assert.That(Texts(rows[1]).Take(3), Is.EqualTo(new[] { "Player 1", "0", "1" }));
        }

        [Test]
        public void EntriesReceivedWhileClosedAreShownOnOpen()
        {
            GameSignals.Scoreboard.Publish(new[] { Entry(5, Team.Bravo, 2, 0) });

            Assert.That(PlayerRows(), Is.Empty, "nothing is built while closed");

            _view.SetOpen(true);

            var rows = PlayerRows();
            Assert.That(rows, Has.Length.EqualTo(1));
            Assert.That(Texts(rows[0])[0], Is.EqualTo("Player 5"));
        }

        [Test]
        public void TeamNamesUseTheHudTeamColors()
        {
            var hud = _host.GetComponent<Hud>();
            _view.SetOpen(true);

            hud.SetTeamColors(Color.red, Color.blue);

            var teams = _view.Element.Query<VisualElement>(className: ScoreboardView.TeamRowClass).ToList();
            Assert.That(teams[0].Q<Label>().style.color.value, Is.EqualTo(Color.red));
            Assert.That(teams[1].Q<Label>().style.color.value, Is.EqualTo(Color.blue));
        }

        [Test]
        public void UnbindClearsAndHidesTheElement()
        {
            var element = _view.Element;
            _view.SetOpen(true);

            _view.Unbind();

            Assert.That(element.childCount, Is.Zero);
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.IsOpen, Is.True, "the open state survives until the next tree is bound");
        }

        [Test]
        public void ShowsTheEntriesPublishedToGameSignals()
        {
            _view.SetOpen(true);

            GameSignals.Scoreboard.Publish(new[]
            {
                new ScoreboardEntry(0, ScoreboardName.LocalPlayer, Team.Alpha, 1, 0, ScoreboardConnection.None, true),
                new ScoreboardEntry(1, ScoreboardName.Target(1), Team.Bravo, 0, 1, ScoreboardConnection.None, false),
            });

            var rows = PlayerRows();
            Assert.That(rows, Has.Length.EqualTo(2));
            Assert.That(Texts(rows[0]), Is.EqualTo(new[] { Localizer.Resolve(ScoreboardText.LocalPlayerName), "1", "0", string.Empty }));
            Assert.That(Texts(rows[1]), Is.EqualTo(new[] { Localizer.Resolve(ScoreboardText.TargetName(1)), "0", "1", string.Empty }));
            Assert.That(rows[0].ClassListContains(ScoreboardView.LocalRowClass), Is.True);
        }

        [Test]
        public void PublishedEntriesPutTheLocalPlayersTeamFirst()
        {
            GameSignals.Scoreboard.Publish(new[]
            {
                Entry(0, Team.Alpha, 0, 0),
                Entry(1, Team.Bravo, 0, 0, isLocal: true),
            });

            Assert.That(_view.Sections.Select(s => s.Team), Is.EqualTo(new[] { Team.Bravo, Team.Alpha }));
        }

        [Test]
        public void ClearingTheSignalEmptiesTheList()
        {
            GameSignals.Scoreboard.Publish(new[] { Entry(0, Team.Alpha, 1, 0, isLocal: true) });
            _view.SetOpen(true);

            GameSignals.Scoreboard.Clear();

            Assert.That(_view.Sections.All(s => s.Rows.Count == 0), Is.True);
            Assert.That(PlayerRows(), Is.Empty);
        }

        [Test]
        public void BindingCatchesUpWithTheCurrentSignal()
        {
            _view.Unbind();
            GameSignals.Scoreboard.Publish(new[] { Entry(4, Team.Bravo, 2, 0) });

            Assert.That(_view.Sections.All(s => s.Rows.Count == 0), Is.True, "an unbound view does not listen");

            _view.Bind(_root);

            Assert.That(_view.Sections[1].Rows.Select(r => r.PlayerId), Is.EqualTo(new[] { 4 }));
        }

        [Test]
        public void ASignalClearedWhileUnboundIsClearedOnBind()
        {
            GameSignals.Scoreboard.Publish(new[] { Entry(0, Team.Alpha, 1, 0) });
            _view.Unbind();
            GameSignals.Scoreboard.Clear();

            _view.Bind(_root);

            Assert.That(_view.Sections.All(s => s.Rows.Count == 0), Is.True);
        }

        [Test]
        public void InvalidPublishedEntriesKeepThePreviousList()
        {
            GameSignals.Scoreboard.Publish(new[] { Entry(0, Team.Alpha, 1, 0) });
            var before = _view.Sections;

            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(ScoreboardView.LogTag) + " could not list the players"));
            GameSignals.Scoreboard.Publish(new[] { Entry(1, Team.Alpha, 0, 0), Entry(1, Team.Bravo, 0, 0) });

            Assert.That(_view.Sections, Is.SameAs(before));
        }

        [Test]
        public void FirstTeamOfIsTheLocalPlayersTeamOrAlpha()
        {
            Assert.That(ScoreboardView.FirstTeamOf(new[] { Entry(0, Team.Bravo, 0, 0, isLocal: true) }), Is.EqualTo(Team.Bravo));
            Assert.That(ScoreboardView.FirstTeamOf(new[] { Entry(0, Team.Bravo, 0, 0) }), Is.EqualTo(Team.Alpha));
            Assert.That(ScoreboardView.FirstTeamOf(null), Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void OpensWithTheScoreboardAction()
        {
            Assert.That(ScoreboardView.ToggleActionPath, Is.EqualTo(InputActionPaths.Scoreboard));
        }
    }
}
