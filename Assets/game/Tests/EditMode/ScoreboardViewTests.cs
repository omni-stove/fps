using System.Linq;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="ScoreboardView"/> が開いている間だけ一覧を出し、渡された一覧で行を作り直すこと。
    /// Edit Mode では UIDocument が木を作らないことがあるので、Hud.uxml を複製した木を直接 Bind する。
    /// </summary>
    public sealed class ScoreboardViewTests
    {
        private GameObject _host;
        private ScoreboardView _view;
        private VisualElement _root;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("TestScoreboard");
            _view = _host.AddComponent<ScoreboardView>();
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            _root = layout.CloneTree();
            _view.Bind(_root);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
        }

        private static ScoreboardEntry Entry(
            int id, Team team, int kills, int deaths,
            ScoreboardConnection connection = ScoreboardConnection.Connected, bool isLocal = false)
        {
            return new ScoreboardEntry(id, ScoreboardRows.PlayerName(id), team, kills, deaths, connection, isLocal);
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
            _view.SetEntries(
                new[]
                {
                    Entry(0, Team.Alpha, 1, 2, isLocal: true),
                    Entry(1, Team.Bravo, 4, 0),
                    Entry(2, Team.Alpha, 3, 1, ScoreboardConnection.Disconnected),
                },
                Team.Alpha);

            _view.SetOpen(true);

            Assert.That(IsShown, Is.True);
            var teams = _view.Element.Query<VisualElement>(className: ScoreboardView.TeamRowClass).ToList();
            Assert.That(teams.Select(t => Texts(t)[0]), Is.EqualTo(new[] { HudFormat.TeamName(Team.Alpha), HudFormat.TeamName(Team.Bravo) }));
            Assert.That(Texts(teams[0]).Skip(1).Take(2), Is.EqualTo(new[] { "4", "3" }), "team totals");

            var rows = PlayerRows();
            Assert.That(rows.Select(r => Texts(r)[0]), Is.EqualTo(new[] { "Player 2", "Player 0", "Player 1" }));
            Assert.That(Texts(rows[0]), Is.EqualTo(new[] { "Player 2", "3", "1", ScoreboardRows.DisconnectedText }));
            Assert.That(Texts(rows[1]), Is.EqualTo(new[] { "Player 0", "1", "2", ScoreboardRows.ConnectedText }));
            Assert.That(rows[0].ClassListContains(ScoreboardView.DisconnectedRowClass), Is.True);
            Assert.That(rows[1].ClassListContains(ScoreboardView.LocalRowClass), Is.True);
            Assert.That(rows[2].ClassListContains(ScoreboardView.LocalRowClass), Is.False);
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
            _view.SetEntries(new[] { Entry(0, Team.Alpha, 0, 0), Entry(1, Team.Bravo, 0, 0) }, Team.Alpha);
            _view.SetOpen(true);

            _view.SetEntries(new[] { Entry(0, Team.Alpha, 1, 0), Entry(1, Team.Bravo, 0, 1) }, Team.Alpha);

            var rows = PlayerRows();
            Assert.That(Texts(rows[0]).Take(3), Is.EqualTo(new[] { "Player 0", "1", "0" }));
            Assert.That(Texts(rows[1]).Take(3), Is.EqualTo(new[] { "Player 1", "0", "1" }));
        }

        [Test]
        public void EntriesReceivedWhileClosedAreShownOnOpen()
        {
            _view.SetEntries(new[] { Entry(5, Team.Bravo, 2, 0) }, Team.Bravo);

            Assert.That(PlayerRows(), Is.Empty, "nothing is built while closed");

            _view.SetOpen(true);

            var rows = PlayerRows();
            Assert.That(rows, Has.Length.EqualTo(1));
            Assert.That(Texts(rows[0])[0], Is.EqualTo("Player 5"));
        }

        [Test]
        public void InvalidEntriesKeepThePreviousList()
        {
            _view.SetEntries(new[] { Entry(0, Team.Alpha, 1, 0) }, Team.Alpha);
            var before = _view.Sections;

            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(ScoreboardView.LogTag) + " could not list the players"));
            _view.SetEntries(new[] { Entry(1, Team.Alpha, 0, 0), Entry(1, Team.Bravo, 0, 0) }, Team.Alpha);

            Assert.That(_view.Sections, Is.SameAs(before));
        }

        [Test]
        public void ClearEntriesEmptiesBothTeams()
        {
            _view.SetEntries(new[] { Entry(0, Team.Alpha, 1, 0) }, Team.Alpha);
            _view.SetOpen(true);

            _view.ClearEntries();

            Assert.That(_view.Sections.All(s => s.Rows.Count == 0), Is.True);
            Assert.That(PlayerRows(), Is.Empty);
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
        public void OpensWithTheScoreboardAction()
        {
            Assert.That(ScoreboardView.ToggleActionPath, Is.EqualTo(InputActionPaths.Scoreboard));
        }
    }
}
