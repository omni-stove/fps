using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class ScoreboardView : HudElementView
    {
        public const string UxmlName = "scoreboard";

        public const string ToggleActionPath = InputActionPaths.Scoreboard;

        public const string LogTag = "[Scoreboard]";

        public const string BoardClass = "scoreboard";

        public const string CaptionRowClass = "scoreboard__caption";

        public const string SectionClass = "scoreboard__section";

        public const string TeamRowClass = "scoreboard__team";

        public const string PlayerRowClass = "scoreboard__row";

        public const string LocalRowClass = "scoreboard__row--local";

        public const string DisconnectedRowClass = "scoreboard__row--disconnected";

        public const string NameCellClass = "scoreboard__name";

        public const string CountCellClass = "scoreboard__count";

        public const string ConnectionCellClass = "scoreboard__connection";

        public const string PinnedClass = "scoreboard--pinned";

        public const string MutedRowClass = "scoreboard__row--muted";

        public const string BlockedRowClass = "scoreboard__row--blocked";

        public const string ActionsCellClass = "scoreboard__actions";

        public const string ActionButtonClass = "scoreboard__action";

        public const string ActionOnClass = "scoreboard__action--on";

        public const string MuteButtonClass = "scoreboard__mute";

        public const string BlockButtonClass = "scoreboard__block";

        public const string ReportButtonClass = "scoreboard__report";

        public const string VoteKickButtonClass = "scoreboard__vote-kick";

        public const string ReasonsRowClass = "scoreboard__reasons";

        public const string ReasonButtonClass = "scoreboard__reason";

        public const string CancelReportButtonClass = "scoreboard__cancel-report";

        public const string FooterClass = "scoreboard__footer";

        public const string BackButtonName = "scoreboard-back";

        public const string BackButtonClass = "scoreboard__back";

        public const string KillsCaption = "K";

        public const string DeathsCaption = "D";

        public static string AssistsCaption => Localizer.Resolve(ScoreboardText.AssistsCaption);

        public static string ConnectionCaption => Localizer.Resolve(ScoreboardText.ConnectionCaption);

        private static readonly IReadOnlyList<ScoreboardSection> _empty =
            ScoreboardRows.Build(Array.Empty<ScoreboardEntry>(), Team.Alpha);

        private static readonly ReportReason[] _reasons = (ReportReason[])Enum.GetValues(typeof(ReportReason));

        private readonly HashSet<int> _reported = new HashSet<int>();
        private IReadOnlyList<ScoreboardSection> _sections = _empty;
        private bool _open;
        private bool _pinned;
        private bool _dirty = true;
        private bool _subscribed;
        private IBlockControl _blockControl;
        private PauseMenu _pause;
        private VisualElement _pausePanel;
        private int _homeIndex = -1;
        private int? _reportingId;
        private bool _voteKickCallable;
        private string _focusKey;
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _toggleAction;
        private bool _resolvedToggleAction;
        private bool _enabledToggleAction;

        public override string ElementName => UxmlName;

        public bool IsOpen => _open;

        public bool IsPinned => _pinned;

        public int? ReportingId => _reportingId;

        public IReadOnlyList<ScoreboardSection> Sections => _sections;

        public static bool CanModerate =>
            GameSignals.BlockControl.HasValue || GameSignals.ReportSender.HasValue || GameSignals.VoteKickSender.HasValue;

        // 投票は進行中の 1 件しか持てない (IVoteKickSender) ので、その間は押しても始まらないボタンにしない。
        public static bool CanCallVoteKick => GameSignals.VoteKickSender.HasValue && !GameSignals.VoteKick.HasValue;

        // ボットや射撃場の標的は覚える相手ではなく、自分は自分を止められないので、ほかの人の行にだけ操作を出す。
        public static bool IsModeratable(ScoreboardEntry entry)
        {
            return !entry.IsLocal && entry.PlayerId >= 0
                && entry.Name.Kind != ScoreboardNameKind.Bot && entry.Name.Kind != ScoreboardNameKind.Target
                && entry.Name.Kind != ScoreboardNameKind.LocalPlayer;
        }

        public InputAction ToggleAction => _toggleAction;

        // entries が並べられない (同じ Id が 2 つ・知らないチーム) ときは、エラーを出して前の一覧を残す。届いた値の誤りで HUD ごと止めないため。
        private void ApplyEntries(IEnumerable<ScoreboardEntry> entries, Team firstTeam)
        {
            IReadOnlyList<ScoreboardSection> sections;
            try
            {
                sections = ScoreboardRows.Build(entries, firstTeam);
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{LogTag} could not list the players: {e.Message}", this);
                return;
            }

            _sections = sections;
            MarkDirty();
        }

        // 試合が終わって一覧が消えたら、次の試合の同じ参加者 Id は別の人なので、通報したことも忘れる。
        private void ClearEntries()
        {
            _sections = _empty;
            _reported.Clear();
            _reportingId = null;
            MarkDirty();
        }

        public static Team FirstTeamOf(IReadOnlyList<ScoreboardEntry> entries)
        {
            if (entries != null)
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    if (entries[i].IsLocal)
                    {
                        return entries[i].Team;
                    }
                }
            }

            return Team.Alpha;
        }

        public void SetOpen(bool open)
        {
            if (_open == open)
            {
                return;
            }

            _open = open;
            Refresh();
        }

        public void RefreshTeamColors()
        {
            MarkDirty();
        }

        // ポーズ画面の後ろでは押せないので、ポーズ画面の前に出して枠を隠し、閉じたときに元の並びへ戻す。
        public void Pin()
        {
            var element = Element;
            if (element == null || _pinned || !CanModerate)
            {
                return;
            }

            _pinned = true;
            var parent = element.parent;
            if (parent != null)
            {
                _homeIndex = parent.IndexOf(element);
                element.BringToFront();
                _pausePanel = parent.Q<VisualElement>(PauseSettingsView.PanelName);
                if (_pausePanel != null)
                {
                    _pausePanel.style.display = DisplayStyle.None;
                }
            }

            element.pickingMode = PickingMode.Position;
            element.AddToClassList(PinnedClass);
            MarkDirty();
            element.Q<Button>(BackButtonName)?.Focus();
            Debug.Log($"{LogTag} opened the player list from the pause menu", this);
        }

        public void Unpin()
        {
            if (!_pinned)
            {
                return;
            }

            _pinned = false;
            _reportingId = null;
            _focusKey = null;
            var element = Element;
            if (element != null)
            {
                var parent = element.parent;
                if (parent != null && _homeIndex >= 0)
                {
                    element.RemoveFromHierarchy();
                    parent.Insert(Math.Min(_homeIndex, parent.childCount), element);
                }

                element.pickingMode = PickingMode.Ignore;
                element.RemoveFromClassList(PinnedClass);
            }

            if (_pausePanel != null)
            {
                _pausePanel.style.display = DisplayStyle.Flex;
                _pausePanel.Q<Button>(PauseMenu.PlayersButtonName)?.Focus();
            }

            _pausePanel = null;
            _homeIndex = -1;
            MarkDirty();
        }

        public void ToggleMute(int participantId)
        {
            var blocks = GameSignals.BlockControl.Value;
            if (blocks == null)
            {
                return;
            }

            var muted = !blocks.IsMuted(participantId);
            blocks.SetMuted(participantId, muted);
            Debug.Log($"{LogTag} {(muted ? "muted" : "unmuted")} participant={participantId}", this);
        }

        public void ToggleBlock(int participantId)
        {
            var blocks = GameSignals.BlockControl.Value;
            if (blocks == null)
            {
                return;
            }

            var blocked = !blocks.IsBlocked(participantId);
            blocks.SetBlocked(participantId, blocked);
            Debug.Log($"{LogTag} {(blocked ? "blocked" : "unblocked")} participant={participantId}", this);
        }

        public void BeginReport(int participantId)
        {
            if (!_pinned || !GameSignals.ReportSender.HasValue || _reported.Contains(participantId))
            {
                return;
            }

            _reportingId = participantId;
            MarkDirty();
        }

        public void CancelReport()
        {
            if (_reportingId == null)
            {
                return;
            }

            _reportingId = null;
            MarkDirty();
        }

        public void SendReport(int participantId, ReportReason reason)
        {
            var sender = GameSignals.ReportSender.Value;
            if (sender == null || _reported.Contains(participantId))
            {
                return;
            }

            sender.Report(participantId, reason);
            _reported.Add(participantId);
            _reportingId = null;
            Debug.Log($"{LogTag} reported participant={participantId} reason={reason}", this);
            MarkDirty();
        }

        // 投票の状態はサーバーから GameSignals.VoteKick で届くので、ここでは言い出すだけにして、ボタンは届いた状態で無効にする。
        public bool CallVoteKick(int participantId)
        {
            var sender = GameSignals.VoteKickSender.Value;
            if (sender == null || GameSignals.VoteKick.HasValue)
            {
                return false;
            }

            sender.Call(participantId);
            Debug.Log($"{LogTag} called a vote to kick participant={participantId}", this);
            return true;
        }

        protected override void OnBound(VisualElement element)
        {
            element.AddToClassList(BoardClass);
            Subscribe();
            AttachPause();

            // 要素を持っていない間の通知は受けていないので、今の値に合わせる
            if (GameSignals.Scoreboard.HasValue)
            {
                ShowSignal(GameSignals.Scoreboard.Value);
            }
            else if (_sections != _empty)
            {
                ClearEntries();
            }

            // 新しい木の要素は空なので、前の木で作った行があっても作り直す
            _dirty = true;
            Refresh();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            DetachPause();
        }

        private void Subscribe()
        {
            if (!_subscribed)
            {
                GameSignals.Scoreboard.Changed += HandleScoreboardChanged;
                GameSignals.BlockControl.Changed += HandleBlockControlChanged;
                GameSignals.ReportSender.Changed += HandleReportSenderChanged;
                GameSignals.VoteKickSender.Changed += HandleVoteKickSenderChanged;
                GameSignals.VoteKick.Changed += HandleVoteKickChanged;
                _subscribed = true;
            }

            _voteKickCallable = CanCallVoteKick;
            WatchBlockControl(GameSignals.BlockControl.Value);
        }

        private void Unsubscribe()
        {
            if (_subscribed)
            {
                GameSignals.Scoreboard.Changed -= HandleScoreboardChanged;
                GameSignals.BlockControl.Changed -= HandleBlockControlChanged;
                GameSignals.ReportSender.Changed -= HandleReportSenderChanged;
                GameSignals.VoteKickSender.Changed -= HandleVoteKickSenderChanged;
                GameSignals.VoteKick.Changed -= HandleVoteKickChanged;
                _subscribed = false;
            }

            WatchBlockControl(null);
        }

        private void WatchBlockControl(IBlockControl control)
        {
            if (ReferenceEquals(_blockControl, control))
            {
                return;
            }

            if (_blockControl != null)
            {
                _blockControl.Changed -= HandleBlocksChanged;
            }

            _blockControl = control;
            if (_blockControl != null)
            {
                _blockControl.Changed += HandleBlocksChanged;
            }
        }

        private void HandleBlockControlChanged(IBlockControl control)
        {
            if (this == null)
            {
                GameSignals.BlockControl.Changed -= HandleBlockControlChanged;
                return;
            }

            WatchBlockControl(control);
            RefreshPauseHook();
            MarkDirty();
        }

        private void HandleReportSenderChanged(IReportSender sender)
        {
            if (this == null)
            {
                GameSignals.ReportSender.Changed -= HandleReportSenderChanged;
                return;
            }

            if (sender == null)
            {
                _reportingId = null;
            }

            RefreshPauseHook();
            MarkDirty();
        }

        private void HandleVoteKickSenderChanged(IVoteKickSender sender)
        {
            if (this == null)
            {
                GameSignals.VoteKickSender.Changed -= HandleVoteKickSenderChanged;
                return;
            }

            _voteKickCallable = CanCallVoteKick;
            RefreshPauseHook();
            MarkDirty();
        }

        private void HandleVoteKickChanged(VoteKickStatus? status)
        {
            if (this == null)
            {
                GameSignals.VoteKick.Changed -= HandleVoteKickChanged;
                return;
            }

            // 残り秒が変わるたびに届くので、ボタンの押せる・押せないが変わるときだけ作り直す。
            var callable = CanCallVoteKick;
            if (callable == _voteKickCallable)
            {
                return;
            }

            _voteKickCallable = callable;
            MarkDirty();
        }

        private void HandleBlocksChanged()
        {
            if (this == null)
            {
                WatchBlockControl(null);
                return;
            }

            MarkDirty();
        }

        // ポーズ画面のボタンは、ほかの部品が先に渡した口を上書きせず、自分が渡したものだけを外す。
        private void AttachPause()
        {
            var pause = GetComponent<PauseMenu>();
            if (pause != _pause)
            {
                DetachPause();
                _pause = pause;
            }

            if (_pause == null)
            {
                return;
            }

            _pause.Closed -= Unpin;
            _pause.Closed += Unpin;
            RefreshPauseHook();
        }

        private void DetachPause()
        {
            Unpin();
            if (_pause == null)
            {
                return;
            }

            _pause.Closed -= Unpin;
            if (_pause.OpenPlayers == (Action)Pin)
            {
                _pause.OpenPlayers = null;
            }

            _pause = null;
        }

        private void RefreshPauseHook()
        {
            if (_pause == null)
            {
                return;
            }

            var mine = _pause.OpenPlayers == (Action)Pin;
            if (CanModerate && Element != null)
            {
                if (_pause.OpenPlayers == null)
                {
                    _pause.OpenPlayers = Pin;
                }

                return;
            }

            Unpin();
            if (mine)
            {
                _pause.OpenPlayers = null;
            }
        }

        private void HandleScoreboardChanged(IReadOnlyList<ScoreboardEntry> entries)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.Scoreboard.Changed -= HandleScoreboardChanged;
                return;
            }

            ShowSignal(entries);
        }

        private void ShowSignal(IReadOnlyList<ScoreboardEntry> entries)
        {
            if (entries == null)
            {
                ClearEntries();
                return;
            }

            ApplyEntries(entries, FirstTeamOf(entries));
        }

        protected override void OnLanguageChanged()
        {
            MarkDirty();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            DetachPause();
            Unsubscribe();
            element.Clear();
            element.style.display = DisplayStyle.None;
        }

        private void OnEnable()
        {
            AcquireToggleAction();
        }

        private void OnDisable()
        {
            ReleaseToggleAction();
            SetOpen(false);
        }

        private void Update()
        {
            if (_toggleAction != null && !_pinned)
            {
                SetOpen(_toggleAction.IsPressed());
            }
        }

        private bool IsShown => _open || _pinned;

        private void MarkDirty()
        {
            _dirty = true;
            Refresh();
        }

        // 閉じている間に届いた一覧は覚えるだけにして開いたときに作り直すのは、キルのたびに見えない要素を作り直さないため。
        private void Refresh()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            if (IsShown && _dirty)
            {
                Rebuild(element);
                _dirty = false;
            }

            element.style.display = IsShown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Rebuild(VisualElement element)
        {
            // 押した直後以外 (スコア更新・名簿の変化) の作り直しでも、乗っていたボタンへフォーカスを戻すため、消す前に控える。
            string focusedKey = null;
            if (_pinned && _focusKey == null && element.panel?.focusController?.focusedElement is Button focused
                && element.Contains(focused))
            {
                focusedKey = focused.userData as string;
            }

            element.Clear();

            var caption = Row(CaptionRowClass);
            caption.Add(Cell(string.Empty, NameCellClass));
            caption.Add(Cell(KillsCaption, CountCellClass));
            caption.Add(Cell(AssistsCaption, CountCellClass));
            caption.Add(Cell(DeathsCaption, CountCellClass));
            caption.Add(Cell(ConnectionCaption, ConnectionCellClass));
            if (_pinned)
            {
                caption.Add(EmptyActions());
            }

            element.Add(caption);

            var hud = GetComponent<Hud>();
            var blocks = GameSignals.BlockControl.Value;
            var reports = GameSignals.ReportSender.Value;
            var voteKicks = GameSignals.VoteKickSender.Value;
            foreach (var section in _sections)
            {
                var box = new VisualElement { pickingMode = PickingMode.Ignore };
                box.AddToClassList(SectionClass);

                var header = Row(TeamRowClass);
                var teamName = Cell(Localizer.Resolve(HudText.TeamName(section.Team)), NameCellClass);
                if (hud != null)
                {
                    teamName.style.color = hud.TeamColor(section.Team);
                }

                header.Add(teamName);
                header.Add(Cell(ScoreboardText.Count(section.Kills), CountCellClass));
                header.Add(Cell(ScoreboardText.Count(section.Assists), CountCellClass));
                header.Add(Cell(ScoreboardText.Count(section.Deaths), CountCellClass));
                header.Add(Cell(string.Empty, ConnectionCellClass));
                if (_pinned)
                {
                    header.Add(EmptyActions());
                }

                box.Add(header);

                foreach (var entry in section.Rows)
                {
                    var row = Row(PlayerRowClass);
                    if (entry.IsLocal)
                    {
                        row.AddToClassList(LocalRowClass);
                    }

                    if (entry.Connection == ScoreboardConnection.Disconnected)
                    {
                        row.AddToClassList(DisconnectedRowClass);
                    }

                    var moderatable = IsModeratable(entry);
                    var muted = moderatable && blocks != null && blocks.IsMuted(entry.PlayerId);
                    var blocked = moderatable && blocks != null && blocks.IsBlocked(entry.PlayerId);
                    if (muted)
                    {
                        row.AddToClassList(MutedRowClass);
                    }

                    if (blocked)
                    {
                        row.AddToClassList(BlockedRowClass);
                    }

                    var shownName = NameOrEmpty(entry.Name);
                    row.Add(Cell(shownName, NameCellClass));
                    row.Add(Cell(ScoreboardText.Count(entry.Kills), CountCellClass));
                    row.Add(Cell(ScoreboardText.Count(entry.Assists), CountCellClass));
                    row.Add(Cell(ScoreboardText.Count(entry.Deaths), CountCellClass));
                    row.Add(Cell(ConnectionTextOrEmpty(entry.Connection), ConnectionCellClass));
                    if (_pinned)
                    {
                        row.Add(Actions(entry, moderatable, blocks, reports, voteKicks, muted, blocked));
                    }

                    box.Add(row);
                    if (_pinned && moderatable && reports != null && _reportingId == entry.PlayerId)
                    {
                        box.Add(Reasons(entry.PlayerId, shownName));
                    }
                }

                element.Add(box);
            }

            if (_pinned)
            {
                var footer = new VisualElement { pickingMode = PickingMode.Ignore };
                footer.AddToClassList(FooterClass);
                var back = ActionButton(Localizer.Resolve(ModerationText.Back), BackButtonClass, false, Unpin);
                back.name = BackButtonName;
                footer.Add(back);
                element.Add(footer);
                RestoreFocus(element, focusedKey);
            }
        }

        // 作り直しで押したボタンごと消えるので、押したボタンを覚えておき、同じボタンへフォーカスを戻す。
        private static string FocusKey(int participantId, string className)
        {
            return participantId + ":" + className;
        }

        // 理由ごとにキーを分けるのは、作り直しで乗っていた理由とは別の理由へフォーカスが移らないようにするため。
        private static string ReasonFocusKey(int participantId, ReportReason reason)
        {
            return FocusKey(participantId, ReasonButtonClass + ":" + reason);
        }

        private void RestoreFocus(VisualElement element, string fallbackKey)
        {
            var key = _focusKey ?? fallbackKey;
            _focusKey = null;
            Button target = null;
            if (key != null)
            {
                target = element.Query<Button>().Where(b => (b.userData as string) == key).First();
            }

            // 押したボタンが無効になった・消えたときは、戻るボタンへ移す。
            if (target == null || !target.enabledSelf)
            {
                target = element.Q<Button>(BackButtonName);
            }

            target?.Focus();
        }

        // 操作の無い行にも同じ幅の空の列を置くのは、数の列が行ごとにずれないようにするため。
        private VisualElement Actions(
            ScoreboardEntry entry, bool moderatable, IBlockControl blocks, IReportSender reports, IVoteKickSender voteKicks,
            bool muted, bool blocked)
        {
            var actions = EmptyActions();
            if (!moderatable)
            {
                return actions;
            }

            var id = entry.PlayerId;
            if (blocks != null)
            {
                var mute = ActionButton(Localizer.Resolve(ModerationText.Mute(muted)), MuteButtonClass, muted, () =>
                {
                    _focusKey = FocusKey(id, MuteButtonClass);
                    ToggleMute(id);
                });
                mute.userData = FocusKey(id, MuteButtonClass);
                actions.Add(mute);
                var block = ActionButton(Localizer.Resolve(ModerationText.Block(blocked)), BlockButtonClass, blocked, () =>
                {
                    _focusKey = FocusKey(id, BlockButtonClass);
                    ToggleBlock(id);
                });
                block.userData = FocusKey(id, BlockButtonClass);
                actions.Add(block);
            }

            if (reports != null)
            {
                var reported = _reported.Contains(id);
                var report = ActionButton(Localizer.Resolve(ModerationText.Report(reported)), ReportButtonClass, false, () =>
                {
                    // 理由の行が開いたら、最初の理由へ移す。
                    _focusKey = ReasonFocusKey(id, _reasons[0]);
                    BeginReport(id);
                });
                report.userData = FocusKey(id, ReportButtonClass);
                report.SetEnabled(!reported);
                actions.Add(report);
            }

            if (voteKicks != null)
            {
                var voteKick = ActionButton(Localizer.Resolve(ModerationText.VoteKickStart), VoteKickButtonClass, false, () =>
                {
                    _focusKey = FocusKey(id, VoteKickButtonClass);
                    CallVoteKick(id);
                });
                voteKick.userData = FocusKey(id, VoteKickButtonClass);
                voteKick.SetEnabled(CanCallVoteKick);
                actions.Add(voteKick);
            }

            return actions;
        }

        private static VisualElement EmptyActions()
        {
            var actions = new VisualElement { pickingMode = PickingMode.Ignore };
            actions.AddToClassList(ActionsCellClass);
            return actions;
        }

        private VisualElement Reasons(int participantId, string shownName)
        {
            var reasons = new VisualElement { pickingMode = PickingMode.Ignore };
            reasons.AddToClassList(ReasonsRowClass);
            reasons.Add(new Label(Localizer.Resolve(ModerationText.ReportPrompt(shownName))) { pickingMode = PickingMode.Ignore });
            foreach (var reason in _reasons)
            {
                var chosen = reason;
                var reasonButton = ActionButton(
                    Localizer.Resolve(ModerationText.Reason(chosen)), ReasonButtonClass, false, () =>
                    {
                        _focusKey = FocusKey(participantId, ReportButtonClass);
                        SendReport(participantId, chosen);
                    });
                reasonButton.userData = ReasonFocusKey(participantId, chosen);
                reasons.Add(reasonButton);
            }

            var cancel = ActionButton(Localizer.Resolve(ModerationText.Cancel), CancelReportButtonClass, false, () =>
            {
                _focusKey = FocusKey(participantId, ReportButtonClass);
                CancelReport();
            });
            cancel.userData = FocusKey(participantId, CancelReportButtonClass);
            reasons.Add(cancel);
            return reasons;
        }

        private static Button ActionButton(string text, string className, bool on, Action clicked)
        {
            var button = new Button(clicked) { text = text };
            button.AddToClassList(ActionButtonClass);
            button.AddToClassList(className);
            if (on)
            {
                button.AddToClassList(ActionOnClass);
            }

            return button;
        }

        // 知らない名前の種類が来ても一覧ごと出せなくならないよう、空欄にする
        private string NameOrEmpty(ScoreboardName name)
        {
            try
            {
                return Localizer.Resolve(ScoreboardText.Name(name));
            }
            catch (ArgumentOutOfRangeException)
            {
                Debug.LogWarning($"{LogTag} unknown name {name}", this);
                return string.Empty;
            }
        }

        // 新しいサーバーが知らない値を送ってきても一覧ごと出せなくならないよう、空欄にする
        private string ConnectionTextOrEmpty(ScoreboardConnection connection)
        {
            try
            {
                return Localizer.Resolve(ScoreboardText.ConnectionText(connection));
            }
            catch (ArgumentOutOfRangeException)
            {
                Debug.LogWarning($"{LogTag} unknown connection state {connection}", this);
                return string.Empty;
            }
        }

        private static VisualElement Row(string className)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList(className);
            return row;
        }

        private static Label Cell(string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。
        private void AcquireToggleAction()
        {
            // Edit Mode では OnDisable が呼ばれず無効に戻せないので、Play 中だけ解決して有効にする。
            if (!Application.isPlaying)
            {
                return;
            }

            if (!_resolvedToggleAction)
            {
                _toggleAction = ResolveToggleAction();
                _resolvedToggleAction = true;
            }

            if (_toggleAction == null || _toggleAction.enabled)
            {
                return;
            }

            _toggleAction.Enable();
            _enabledToggleAction = true;
        }

        private void ReleaseToggleAction()
        {
            if (_enabledToggleAction && _toggleAction != null)
            {
                _toggleAction.Disable();
            }

            _enabledToggleAction = false;
        }

        private InputAction ResolveToggleAction()
        {
            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError(
                    $"{LogTag} project-wide Input Actions are not set (Project Settings > Input System Package); the scoreboard cannot be opened",
                    this);
                return null;
            }

            var action = actions.FindAction(ToggleActionPath);
            if (action == null)
            {
                Debug.LogError($"{LogTag} Input Actions '{actions.name}' has no '{ToggleActionPath}'; the scoreboard cannot be opened", this);
            }

            return action;
        }
    }
}
