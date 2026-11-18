using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では UIDocument が木を作らないことがあるので、Hud.uxml を複製した木を直接 Bind する。
    // 木はパネルに付いていないのでボタンを押す知らせが届かず、ボタンが呼ぶビューの操作を直接呼んで確かめる。
    public sealed class ScoreboardModerationTests
    {
        private const int _localId = 0;
        private const int _otherId = 1;
        private const int _botId = 2;

        private GameObject _host;
        private ScoreboardView _view;
        private PauseMenu _pause;
        private VisualElement _root;
        private FakeBlockControl _blocks;
        private FakeReportSender _reports;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _host = new GameObject("TestScoreboardModeration");
            _view = _host.AddComponent<ScoreboardView>();
            _pause = _host.AddComponent<PauseMenu>();
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            _root = layout.CloneTree();
            _pause.Bind(_root);
            _view.Bind(_root);
            _blocks = new FakeBlockControl();
            _reports = new FakeReportSender();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            GameSignals.ResetAll();
        }

        private Button PlayersButton => _root.Q<Button>(PauseMenu.PlayersButtonName);

        private VisualElement PausePanel => _root.Q<VisualElement>(PauseSettingsView.PanelName);

        private void PublishPlayers()
        {
            GameSignals.Scoreboard.Publish(new[]
            {
                new ScoreboardEntry(_localId, ScoreboardName.Player(_localId), Team.Alpha, 0, 0, 0, ScoreboardConnection.Connected, true),
                new ScoreboardEntry(_otherId, ScoreboardName.Player(_otherId), Team.Bravo, 0, 0, 0, ScoreboardConnection.Connected, false),
                new ScoreboardEntry(_botId, ScoreboardName.Bot(1), Team.Bravo, 0, 0, 0, ScoreboardConnection.Connected, false),
            });
        }

        private void PublishControls()
        {
            GameSignals.BlockControl.Publish(_blocks);
            GameSignals.ReportSender.Publish(_reports);
        }

        private VisualElement RowOf(string name)
        {
            return _view.Element.Query<VisualElement>(className: ScoreboardView.PlayerRowClass).ToList()
                .Single(row => row.Q<Label>(className: ScoreboardView.NameCellClass).text == name);
        }

        private static string[] ButtonTexts(VisualElement row)
        {
            return row.Query<Button>(className: ScoreboardView.ActionButtonClass).ToList().Select(b => b.text).ToArray();
        }

        [Test]
        public void ThePlayersButtonIsShownOnlyWhileSomethingCanBeModerated()
        {
            Assert.That(PlayersButton.style.display.value, Is.EqualTo(DisplayStyle.None), "offline there is nothing to block or report");
            Assert.That(_pause.OpenPlayers, Is.Null);

            GameSignals.BlockControl.Publish(_blocks);

            Assert.That(PlayersButton.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_pause.OpenPlayers, Is.Not.Null);

            GameSignals.BlockControl.Clear();

            Assert.That(PlayersButton.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_pause.OpenPlayers, Is.Null);
        }

        [Test]
        public void ThePauseButtonPinsTheBoardInFrontOfThePauseMenu()
        {
            PublishPlayers();
            PublishControls();

            _pause.OpenPlayers();

            Assert.That(_view.IsPinned, Is.True);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.Element.pickingMode, Is.EqualTo(PickingMode.Position));
            Assert.That(_view.Element.ClassListContains(ScoreboardView.PinnedClass), Is.True);
            Assert.That(_view.Element.parent.IndexOf(_view.Element), Is.EqualTo(_view.Element.parent.childCount - 1), "drawn after the pause menu");
            Assert.That(PausePanel.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.Element.Q<Button>(ScoreboardView.BackButtonName), Is.Not.Null);
        }

        [Test]
        public void OnlyOtherPlayersRowsGetTheActions()
        {
            PublishPlayers();
            PublishControls();

            _view.Pin();

            var other = Localizer.Resolve(ScoreboardText.PlayerName(_otherId));
            var you = Localizer.Resolve(ScoreboardText.PlayerName(_localId));
            Assert.That(ButtonTexts(RowOf(other)), Is.EqualTo(new[]
            {
                Localizer.Resolve(ModerationText.Mute(false)),
                Localizer.Resolve(ModerationText.Block(false)),
                Localizer.Resolve(ModerationText.Report(false)),
            }));
            Assert.That(ButtonTexts(RowOf(you)), Is.Empty, "the local player cannot block themselves");
            Assert.That(ButtonTexts(RowOf("[BOT] Bot 1")), Is.Empty, "bots are not remembered");
        }

        [Test]
        public void TheHeldBoardHasNoActions()
        {
            PublishPlayers();
            PublishControls();

            _view.SetOpen(true);

            Assert.That(_view.Element.Query<Button>(className: ScoreboardView.ActionButtonClass).ToList(), Is.Empty);
        }

        [Test]
        public void MutingAndBlockingGoThroughTheControlAndRelabelTheRow()
        {
            PublishPlayers();
            PublishControls();
            _view.Pin();
            var other = Localizer.Resolve(ScoreboardText.PlayerName(_otherId));

            _view.ToggleMute(_otherId);

            Assert.That(_blocks.IsMuted(_otherId), Is.True);
            Assert.That(_blocks.IsBlocked(_otherId), Is.False);
            var row = RowOf(other);
            Assert.That(row.ClassListContains(ScoreboardView.MutedRowClass), Is.True);
            Assert.That(row.Q<Button>(className: ScoreboardView.MuteButtonClass).text, Is.EqualTo(Localizer.Resolve(ModerationText.Mute(true))));
            Assert.That(row.Q<Button>(className: ScoreboardView.MuteButtonClass).ClassListContains(ScoreboardView.ActionOnClass), Is.True);

            _view.ToggleBlock(_otherId);

            row = RowOf(other);
            Assert.That(_blocks.IsBlocked(_otherId), Is.True);
            Assert.That(row.ClassListContains(ScoreboardView.BlockedRowClass), Is.True);
            Assert.That(row.Q<Button>(className: ScoreboardView.BlockButtonClass).text, Is.EqualTo(Localizer.Resolve(ModerationText.Block(true))));

            _view.ToggleMute(_otherId);

            Assert.That(_blocks.IsMuted(_otherId), Is.False);
            Assert.That(RowOf(other).ClassListContains(ScoreboardView.MutedRowClass), Is.False);
        }

        [Test]
        public void ReportingAsksForAReasonThenSendsItOnce()
        {
            PublishPlayers();
            PublishControls();
            _view.Pin();

            _view.BeginReport(_otherId);

            Assert.That(_view.ReportingId, Is.EqualTo(_otherId));
            var reasons = _view.Element.Q<VisualElement>(className: ScoreboardView.ReasonsRowClass);
            Assert.That(reasons, Is.Not.Null);
            Assert.That(
                reasons.Query<Button>(className: ScoreboardView.ReasonButtonClass).ToList().Select(b => b.text),
                Is.EqualTo(new[]
                {
                    Localizer.Resolve(ModerationText.Reason(ReportReason.Cheating)),
                    Localizer.Resolve(ModerationText.Reason(ReportReason.Abuse)),
                    Localizer.Resolve(ModerationText.Reason(ReportReason.Spam)),
                    Localizer.Resolve(ModerationText.Reason(ReportReason.OffensiveName)),
                    Localizer.Resolve(ModerationText.Reason(ReportReason.Other)),
                }));

            _view.SendReport(_otherId, ReportReason.Spam);
            _view.SendReport(_otherId, ReportReason.Cheating);

            Assert.That(_reports.Sent, Is.EqualTo(new[] { (_otherId, ReportReason.Spam) }), "a second report of the same player is not sent");
            Assert.That(_view.ReportingId, Is.Null);
            Assert.That(_view.Element.Q<VisualElement>(className: ScoreboardView.ReasonsRowClass), Is.Null);
            var report = RowOf(Localizer.Resolve(ScoreboardText.PlayerName(_otherId))).Q<Button>(className: ScoreboardView.ReportButtonClass);
            Assert.That(report.text, Is.EqualTo(Localizer.Resolve(ModerationText.Report(true))));
            Assert.That(report.enabledSelf, Is.False);
        }

        [Test]
        public void EachReasonAndTheCancelButtonHaveTheirOwnFocusKey()
        {
            PublishPlayers();
            PublishControls();
            _view.Pin();

            _view.BeginReport(_otherId);

            var reasons = _view.Element.Q<VisualElement>(className: ScoreboardView.ReasonsRowClass);
            var keys = reasons.Query<Button>(className: ScoreboardView.ActionButtonClass).ToList()
                .Select(b => b.userData as string).ToArray();
            Assert.That(keys, Is.EqualTo(new[]
            {
                _otherId + ":" + ScoreboardView.ReasonButtonClass + ":" + ReportReason.Cheating,
                _otherId + ":" + ScoreboardView.ReasonButtonClass + ":" + ReportReason.Abuse,
                _otherId + ":" + ScoreboardView.ReasonButtonClass + ":" + ReportReason.Spam,
                _otherId + ":" + ScoreboardView.ReasonButtonClass + ":" + ReportReason.OffensiveName,
                _otherId + ":" + ScoreboardView.ReasonButtonClass + ":" + ReportReason.Other,
                _otherId + ":" + ScoreboardView.CancelReportButtonClass,
            }), "a rebuild must put focus back on the reason it was on, not the first one");
        }

        [Test]
        public void CancellingAReportSendsNothing()
        {
            PublishPlayers();
            PublishControls();
            _view.Pin();
            _view.BeginReport(_otherId);

            _view.CancelReport();

            Assert.That(_view.ReportingId, Is.Null);
            Assert.That(_reports.Sent, Is.Empty);
        }

        [Test]
        public void WithoutAReportSenderThereIsNoReportButton()
        {
            PublishPlayers();
            GameSignals.BlockControl.Publish(_blocks);
            _view.Pin();

            _view.BeginReport(_otherId);

            Assert.That(_view.ReportingId, Is.Null);
            Assert.That(ButtonTexts(RowOf(Localizer.Resolve(ScoreboardText.PlayerName(_otherId)))), Is.EqualTo(new[]
            {
                Localizer.Resolve(ModerationText.Mute(false)),
                Localizer.Resolve(ModerationText.Block(false)),
            }));
        }

        [Test]
        public void UnpinningPutsTheBoardBackAndShowsThePausePanel()
        {
            PublishPlayers();
            PublishControls();
            var home = _view.Element.parent.IndexOf(_view.Element);
            _view.Pin();

            _view.Unpin();

            Assert.That(_view.IsPinned, Is.False);
            Assert.That(_view.Element.parent.IndexOf(_view.Element), Is.EqualTo(home));
            Assert.That(_view.Element.pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(_view.Element.ClassListContains(ScoreboardView.PinnedClass), Is.False);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(PausePanel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void ClosingThePauseMenuUnpinsTheBoard()
        {
            PublishPlayers();
            PublishControls();
            _pause.Open();
            _view.Pin();

            _pause.Close();

            Assert.That(_view.IsPinned, Is.False);
        }

        [Test]
        public void LosingTheControlsUnpinsTheBoard()
        {
            PublishPlayers();
            PublishControls();
            _view.Pin();

            GameSignals.BlockControl.Clear();
            Assert.That(_view.IsPinned, Is.True, "reporting is still possible");

            GameSignals.ReportSender.Clear();
            Assert.That(_view.IsPinned, Is.False);
        }

        [Test]
        public void PinningDoesNothingOffline()
        {
            PublishPlayers();

            _view.Pin();

            Assert.That(_view.IsPinned, Is.False);
        }

        [Test]
        public void AnotherOwnersPlayersActionIsNotReplacedOrCleared()
        {
            Action other = () => { };
            _pause.OpenPlayers = other;

            GameSignals.BlockControl.Publish(_blocks);
            GameSignals.BlockControl.Clear();

            Assert.That(_pause.OpenPlayers, Is.SameAs(other));
        }

        [Test]
        public void BotsTargetsAndTheLocalPlayerAreNotModeratable()
        {
            Assert.That(ScoreboardView.IsModeratable(Entry(ScoreboardName.Player(3), false)), Is.True);
            Assert.That(ScoreboardView.IsModeratable(Entry(ScoreboardName.Literal("someone"), false)), Is.True);
            Assert.That(ScoreboardView.IsModeratable(Entry(ScoreboardName.Player(3), true)), Is.False);
            Assert.That(ScoreboardView.IsModeratable(Entry(ScoreboardName.LocalPlayer, false)), Is.False);
            Assert.That(ScoreboardView.IsModeratable(Entry(ScoreboardName.Bot(1), false)), Is.False);
            Assert.That(ScoreboardView.IsModeratable(Entry(ScoreboardName.Target(1), false)), Is.False);
        }

        private static ScoreboardEntry Entry(ScoreboardName name, bool isLocal)
        {
            return new ScoreboardEntry(3, name, Team.Alpha, 0, 0, 0, ScoreboardConnection.Connected, isLocal);
        }

        private sealed class FakeReportSender : IReportSender
        {
            public List<(int, ReportReason)> Sent { get; } = new List<(int, ReportReason)>();

            public void Report(int participantId, ReportReason reason)
            {
                Sent.Add((participantId, reason));
            }
        }

        private sealed class FakeBlockControl : IBlockControl
        {
            private readonly HashSet<int> _blocked = new HashSet<int>();
            private readonly HashSet<int> _muted = new HashSet<int>();

            public event Action Changed;

            public bool IsBlocked(int participantId)
            {
                return _blocked.Contains(participantId);
            }

            public bool IsMuted(int participantId)
            {
                return _muted.Contains(participantId);
            }

            public void SetBlocked(int participantId, bool blocked)
            {
                Set(_blocked, participantId, blocked);
            }

            public void SetMuted(int participantId, bool muted)
            {
                Set(_muted, participantId, muted);
            }

            private void Set(HashSet<int> set, int participantId, bool on)
            {
                if (on ? set.Add(participantId) : set.Remove(participantId))
                {
                    Changed?.Invoke();
                }
            }
        }
    }
}
