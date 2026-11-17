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

        public const string KillsCaption = "K";

        public const string DeathsCaption = "D";

        public static string ConnectionCaption => Localizer.Resolve(ScoreboardText.ConnectionCaption);

        private static readonly IReadOnlyList<ScoreboardSection> _empty =
            ScoreboardRows.Build(Array.Empty<ScoreboardEntry>(), Team.Alpha);

        private IReadOnlyList<ScoreboardSection> _sections = _empty;
        private bool _open;
        private bool _dirty = true;
        private bool _subscribed;
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _toggleAction;
        private bool _resolvedToggleAction;
        private bool _enabledToggleAction;

        public override string ElementName => UxmlName;

        public bool IsOpen => _open;

        public IReadOnlyList<ScoreboardSection> Sections => _sections;

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

        private void ClearEntries()
        {
            _sections = _empty;
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

        protected override void OnBound(VisualElement element)
        {
            element.AddToClassList(BoardClass);
            Subscribe();

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
        }

        private void Subscribe()
        {
            if (!_subscribed)
            {
                GameSignals.Scoreboard.Changed += HandleScoreboardChanged;
                _subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed)
            {
                GameSignals.Scoreboard.Changed -= HandleScoreboardChanged;
                _subscribed = false;
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
            if (_toggleAction != null)
            {
                SetOpen(_toggleAction.IsPressed());
            }
        }

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

            if (_open && _dirty)
            {
                Rebuild(element);
                _dirty = false;
            }

            element.style.display = _open ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Rebuild(VisualElement element)
        {
            element.Clear();

            var caption = Row(CaptionRowClass);
            caption.Add(Cell(string.Empty, NameCellClass));
            caption.Add(Cell(KillsCaption, CountCellClass));
            caption.Add(Cell(DeathsCaption, CountCellClass));
            caption.Add(Cell(ConnectionCaption, ConnectionCellClass));
            element.Add(caption);

            var hud = GetComponent<Hud>();
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
                header.Add(Cell(ScoreboardText.Count(section.Deaths), CountCellClass));
                header.Add(Cell(string.Empty, ConnectionCellClass));
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

                    row.Add(Cell(NameOrEmpty(entry.Name), NameCellClass));
                    row.Add(Cell(ScoreboardText.Count(entry.Kills), CountCellClass));
                    row.Add(Cell(ScoreboardText.Count(entry.Deaths), CountCellClass));
                    row.Add(Cell(ConnectionTextOrEmpty(entry.Connection), ConnectionCellClass));
                    box.Add(row);
                }

                element.Add(box);
            }
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
