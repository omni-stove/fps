using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// 参加者のキル・デス・接続状態をチームごとに一覧にするビュー。Hud.uxml の <see cref="UxmlName"/> を使う。
    /// <see cref="ToggleActionPath"/> (Tab / ゲームパッドの View) を押している間だけ出す。
    /// </summary>
    /// <remarks>
    /// 一覧の中身は持ち主が <see cref="SetEntries"/> で渡す。オンラインは Fps.Net の NetHud が
    /// NetTeamDeathmatch.Players の変化のたびに、オフラインは <see cref="MatchRunner"/> がキル・デスのたびに渡す。
    /// 並べ方は Core の <see cref="ScoreboardRows"/> が決める。閉じている間に届いた一覧は覚えておくだけにし、
    /// 次に開いたときに要素を作り直す (キルのたびに見えない要素を作り直さないため)。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ScoreboardView : HudElementView
    {
        /// <summary>Hud.uxml のスコアボードの要素。</summary>
        public const string UxmlName = "scoreboard";

        /// <summary>押している間だけ一覧を出すアクション。</summary>
        public const string ToggleActionPath = InputActionPaths.Scoreboard;

        public const string LogTag = "[Scoreboard]";

        /// <summary>一覧全体に付ける USS クラス。</summary>
        public const string BoardClass = "scoreboard";

        /// <summary>列の見出しの行。</summary>
        public const string CaptionRowClass = "scoreboard__caption";

        /// <summary>1 チーム分の欄。</summary>
        public const string SectionClass = "scoreboard__section";

        /// <summary>チームの欄の見出しの行 (チーム名と合計)。</summary>
        public const string TeamRowClass = "scoreboard__team";

        /// <summary>参加者 1 人分の行。</summary>
        public const string PlayerRowClass = "scoreboard__row";

        /// <summary>自分の行に足す。</summary>
        public const string LocalRowClass = "scoreboard__row--local";

        /// <summary>切断した人の行に足す。</summary>
        public const string DisconnectedRowClass = "scoreboard__row--disconnected";

        /// <summary>名前の列。</summary>
        public const string NameCellClass = "scoreboard__name";

        /// <summary>キル数・デス数の列。</summary>
        public const string CountCellClass = "scoreboard__count";

        /// <summary>接続状態の列。</summary>
        public const string ConnectionCellClass = "scoreboard__connection";

        /// <summary>列の見出しの文字列。</summary>
        public const string KillsCaption = "K";

        public const string DeathsCaption = "D";

        public static string ConnectionCaption => Localizer.Get("scoreboard.connectionCaption");

        private static readonly IReadOnlyList<ScoreboardSection> _empty =
            ScoreboardRows.Build(Array.Empty<ScoreboardEntry>(), Team.Alpha);

        private IReadOnlyList<ScoreboardSection> _sections = _empty;
        private bool _open;
        private bool _dirty = true;
        private InputAction _toggleAction;
        private bool _resolvedToggleAction;
        private bool _enabledToggleAction;

        public override string ElementName => UxmlName;

        /// <summary>一覧を出しているか。</summary>
        public bool IsOpen => _open;

        /// <summary>
        /// 最後に <see cref="SetEntries"/> で受けた一覧を並べたもの。先に出すチームの欄が先で、いつも 2 つ。
        /// 何も受けていなければ両チームとも空。
        /// </summary>
        public IReadOnlyList<ScoreboardSection> Sections => _sections;

        /// <summary>押している間だけ一覧を出すアクション。解決できなければ null (一覧は開かない)。</summary>
        public InputAction ToggleAction => _toggleAction;

        /// <summary>
        /// 一覧の中身を差し替える。開いていればすぐ書き直す。
        /// entries が並べられない (null・同じ Id が 2 つ・知らないチーム) ときはエラーを出して前の一覧を残す
        /// (届いた値の誤りで HUD ごと止めないため)。
        /// </summary>
        /// <param name="entries">参加者。順番は問わない。</param>
        /// <param name="firstTeam">先に出すチーム。</param>
        public void SetEntries(IEnumerable<ScoreboardEntry> entries, Team firstTeam)
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

        /// <summary>一覧を空にする (試合が無くなったときなど)。</summary>
        public void ClearEntries()
        {
            _sections = _empty;
            MarkDirty();
        }

        /// <summary>
        /// 一覧を出すか隠す。Play 中は <see cref="ToggleAction"/> を押しているかで毎フレーム呼ぶ。
        /// 要素をまだ受けていなくても状態は覚え、受けたときに合わせる。
        /// </summary>
        public void SetOpen(bool open)
        {
            if (_open == open)
            {
                return;
            }

            _open = open;
            Refresh();
        }

        /// <summary>見出しの色 (<see cref="Hud.TeamColor"/>) が変わったときに呼ぶ。開いていればすぐ塗り直す。</summary>
        public void RefreshTeamColors()
        {
            MarkDirty();
        }

        protected override void OnBound(VisualElement element)
        {
            element.AddToClassList(BoardClass);

            // 新しい木の要素は空なので、前の木で作った行があっても作り直す
            _dirty = true;
            Refresh();
        }

        protected override void OnLanguageChanged()
        {
            MarkDirty();
        }

        protected override void OnUnbinding(VisualElement element)
        {
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
                var teamName = Cell(HudFormat.TeamName(section.Team), NameCellClass);
                if (hud != null)
                {
                    teamName.style.color = hud.TeamColor(section.Team);
                }

                header.Add(teamName);
                header.Add(Cell(ScoreboardRows.Count(section.Kills), CountCellClass));
                header.Add(Cell(ScoreboardRows.Count(section.Deaths), CountCellClass));
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

                    row.Add(Cell(entry.Name, NameCellClass));
                    row.Add(Cell(ScoreboardRows.Count(entry.Kills), CountCellClass));
                    row.Add(Cell(ScoreboardRows.Count(entry.Deaths), CountCellClass));
                    row.Add(Cell(ConnectionTextOrEmpty(entry.Connection), ConnectionCellClass));
                    box.Add(row);
                }

                element.Add(box);
            }
        }

        // 新しいサーバーが知らない値を送ってきても一覧ごと出せなくならないよう、空欄にする
        private string ConnectionTextOrEmpty(ScoreboardConnection connection)
        {
            try
            {
                return ScoreboardRows.ConnectionText(connection);
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

        /// <summary>プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。</summary>
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
