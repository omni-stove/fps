using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>キャンペーンのメニューに並べる 1 ミッション。</summary>
    public sealed class CampaignMenuMission
    {
        /// <param name="id">ミッション Id (<see cref="CampaignLaunch.IsValidMissionId"/> を満たすこと)。</param>
        /// <param name="displayName">メニューに出す名前。空にはできない。</param>
        /// <param name="hasSave">再開できるセーブがあるか。</param>
        public CampaignMenuMission(string id, string displayName, bool hasSave)
        {
            CampaignLaunch.RequireValidMissionId(id, nameof(id));
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("displayName must not be empty", nameof(displayName));
            }

            Id = id;
            DisplayName = displayName;
            HasSave = hasSave;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public bool HasSave { get; }

        internal CampaignMenuMission WithoutSave()
        {
            return HasSave ? new CampaignMenuMission(Id, DisplayName, false) : this;
        }
    }

    /// <summary>キャンペーンのメニューで選べる項目の種類。</summary>
    public enum CampaignMenuItemKind
    {
        /// <summary>ミッション (<see cref="CampaignMenuItem.MissionIndex"/> 番目)。</summary>
        Mission,

        /// <summary>難易度の行。左右で替える。</summary>
        Difficulty,

        /// <summary>セーブから再開する。セーブがあるミッションだけに出る。</summary>
        Continue,

        /// <summary>セーブを消して最初から始める。セーブがあるミッションだけに出て、押すと確認を出す。</summary>
        Restart,

        /// <summary>最初から始める。セーブが無いミッションに出る。</summary>
        Start,

        /// <summary>メニューに戻る。</summary>
        Back,

        /// <summary>確認でセーブを消して始める。</summary>
        ConfirmRestart,

        /// <summary>確認をやめる。</summary>
        CancelRestart,
    }

    /// <summary>キャンペーンのメニューの 1 項目。</summary>
    public readonly struct CampaignMenuItem : IEquatable<CampaignMenuItem>
    {
        public CampaignMenuItem(CampaignMenuItemKind kind, int missionIndex = -1)
        {
            Kind = kind;
            MissionIndex = kind == CampaignMenuItemKind.Mission ? missionIndex : -1;
        }

        public CampaignMenuItemKind Kind { get; }

        /// <summary><see cref="CampaignMenuItemKind.Mission"/> ならミッションの位置、それ以外は -1。</summary>
        public int MissionIndex { get; }

        public bool Equals(CampaignMenuItem other)
        {
            return Kind == other.Kind && MissionIndex == other.MissionIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is CampaignMenuItem other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)Kind * 397) ^ MissionIndex;
        }

        public override string ToString()
        {
            return Kind == CampaignMenuItemKind.Mission ? Kind + "#" + MissionIndex : Kind.ToString();
        }
    }

    /// <summary>キャンペーンをどう始めるか。</summary>
    public enum CampaignStartMode
    {
        /// <summary>セーブから再開する。</summary>
        Continue,

        /// <summary>開始地点から始める。</summary>
        FromBeginning,
    }

    /// <summary>メニューで決めた始め方。</summary>
    public sealed class CampaignMenuStart
    {
        public CampaignMenuStart(CampaignLaunch launch, CampaignStartMode mode, bool deletesSave)
        {
            Launch = launch ?? throw new ArgumentNullException(nameof(launch));
            if (deletesSave && mode != CampaignStartMode.FromBeginning)
            {
                throw new ArgumentException("only a start from the beginning deletes the save", nameof(deletesSave));
            }

            Mode = mode;
            DeletesSave = deletesSave;
        }

        /// <summary>CampaignLaunchSelection に入れる選択。</summary>
        public CampaignLaunch Launch { get; }

        public CampaignStartMode Mode { get; }

        /// <summary>シーンを読む前にセーブを消すか (確認を経た「最初から」)。</summary>
        public bool DeletesSave { get; }

        public override string ToString()
        {
            return Launch + " " + (Mode == CampaignStartMode.Continue ? "continue" : "from-beginning") + (DeletesSave ? " (delete save)" : "");
        }
    }

    /// <summary><see cref="CampaignMenu.Activate"/> と <see cref="CampaignMenu.Cancel"/> の結果。</summary>
    public enum CampaignMenuResultKind
    {
        /// <summary>メニューの中で状態が変わっただけ。</summary>
        None,

        /// <summary>始める。<see cref="CampaignMenu.LastStart"/> に始め方がある。</summary>
        Start,

        /// <summary>メインメニューに戻る。</summary>
        Back,
    }

    /// <summary>
    /// キャンペーンのメニューの状態。ミッションと難易度を選び、セーブがあるミッションには「続きから」と「最初から」を出す。
    /// 「最初から」は確認 (<see cref="IsConfirmingRestart"/>) を挟む。
    /// 選べる項目 (<see cref="Items"/>) を上から並べ、ゲームパッドの上下でフォーカス (<see cref="FocusIndex"/>) を動かす。
    /// ミッションの行にフォーカスを移すとそのミッションを選び、難易度の行では左右で難易度を替える。
    /// 画面に出すのは Fps.Runtime の CampaignSelectPanel。
    /// </summary>
    public sealed class CampaignMenu
    {
        private static readonly CampaignDifficulty[] _difficulties =
        {
            CampaignDifficulty.Easy, CampaignDifficulty.Normal, CampaignDifficulty.Hard,
        };

        private readonly CampaignMenuMission[] _missions;
        private readonly List<CampaignMenuItem> _items = new List<CampaignMenuItem>();
        private int _selectedMission;
        private int _focusIndex;

        /// <param name="missions">並べるミッション。1 つ以上で、null と Id の重複を含まないこと。</param>
        /// <param name="difficulty">最初に選んでおく難易度。</param>
        /// <param name="preferredMissionId">最初に選んでおくミッションの Id。null か一覧に無ければ先頭。</param>
        public CampaignMenu(IReadOnlyList<CampaignMenuMission> missions, CampaignDifficulty difficulty, string preferredMissionId = null)
        {
            if (missions == null)
            {
                throw new ArgumentNullException(nameof(missions));
            }

            if (missions.Count == 0)
            {
                throw new ArgumentException("the campaign menu needs at least one mission", nameof(missions));
            }

            RequireDifficulty(difficulty, nameof(difficulty));
            _missions = new CampaignMenuMission[missions.Count];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < missions.Count; i++)
            {
                var mission = missions[i] ?? throw new ArgumentException($"mission #{i} is null", nameof(missions));
                if (!seen.Add(mission.Id))
                {
                    throw new ArgumentException($"mission id '{mission.Id}' appears more than once", nameof(missions));
                }

                _missions[i] = mission;
                if (string.Equals(mission.Id, preferredMissionId, StringComparison.Ordinal))
                {
                    _selectedMission = i;
                }
            }

            Difficulty = difficulty;
            RebuildItems();
            _focusIndex = _selectedMission;
        }

        /// <summary>選べる難易度。左右で替える順。</summary>
        public static IReadOnlyList<CampaignDifficulty> Difficulties => _difficulties;

        public IReadOnlyList<CampaignMenuMission> Missions => _missions;

        public int SelectedMissionIndex => _selectedMission;

        public CampaignMenuMission SelectedMission => _missions[_selectedMission];

        public CampaignDifficulty Difficulty { get; private set; }

        /// <summary>「最初から」の確認を出しているか。出している間の項目は確認の 2 つだけ。</summary>
        public bool IsConfirmingRestart { get; private set; }

        /// <summary>今選べる項目。上から並べる順。</summary>
        public IReadOnlyList<CampaignMenuItem> Items => _items;

        /// <summary>フォーカスしている項目の位置 (<see cref="Items"/> の添字)。</summary>
        public int FocusIndex => _focusIndex;

        public CampaignMenuItem FocusedItem => _items[_focusIndex];

        /// <summary>最後に <see cref="CampaignMenuResultKind.Start"/> を返したときの始め方。まだなら null。</summary>
        public CampaignMenuStart LastStart { get; private set; }

        /// <summary>kind の項目の位置。今出ていなければ -1。</summary>
        public int IndexOf(CampaignMenuItemKind kind, int missionIndex = -1)
        {
            return _items.IndexOf(new CampaignMenuItem(kind, missionIndex));
        }

        /// <summary>フォーカスを step だけ動かし、端では反対側へ回り込む。ミッションの行に移ればそのミッションを選ぶ。</summary>
        public void MoveFocus(int step)
        {
            if (step == 0)
            {
                return;
            }

            FocusOn(MenuRoute.MoveFocus(_focusIndex, step, _items.Count));
        }

        /// <summary>index の項目にフォーカスを移す (マウスで指したとき)。ミッションの行ならそのミッションを選ぶ。範囲外なら false。</summary>
        public bool FocusOn(int index)
        {
            if (index < 0 || index >= _items.Count)
            {
                return false;
            }

            var item = _items[index];
            if (item.Kind == CampaignMenuItemKind.Mission)
            {
                SelectMission(item.MissionIndex);
            }

            _focusIndex = index;
            return true;
        }

        /// <summary>index 番目のミッションを選ぶ。項目の並びが変わってもフォーカスは同じ項目に残す。確認中・範囲外なら false。</summary>
        public bool SelectMission(int index)
        {
            if (IsConfirmingRestart || index < 0 || index >= _missions.Length)
            {
                return false;
            }

            if (index == _selectedMission)
            {
                return true;
            }

            var focused = FocusedItem;
            _selectedMission = index;
            RebuildItems();
            RefocusOrFallBack(focused);
            return true;
        }

        /// <summary>難易度を step だけ替え、端では回り込む。確認中は替えずに false。</summary>
        public bool CycleDifficulty(int step)
        {
            if (IsConfirmingRestart || step == 0)
            {
                return false;
            }

            var current = Array.IndexOf(_difficulties, Difficulty);
            var next = ((current + step) % _difficulties.Length + _difficulties.Length) % _difficulties.Length;
            Difficulty = _difficulties[next];
            return true;
        }

        /// <summary>難易度を difficulty にする。確認中は替えずに false。</summary>
        public bool SetDifficulty(CampaignDifficulty difficulty)
        {
            RequireDifficulty(difficulty, nameof(difficulty));
            if (IsConfirmingRestart)
            {
                return false;
            }

            Difficulty = difficulty;
            return true;
        }

        /// <summary>
        /// フォーカスしている項目を決定する。ミッションなら難易度の行へ、難易度なら最初の始め方へフォーカスを進める。
        /// 「最初から」は確認を出し、間違えて消さないよう「やめる」にフォーカスを置く。
        /// </summary>
        public CampaignMenuResultKind Activate()
        {
            var item = FocusedItem;
            switch (item.Kind)
            {
                case CampaignMenuItemKind.Mission:
                    SelectMission(item.MissionIndex);
                    _focusIndex = IndexOf(CampaignMenuItemKind.Difficulty);
                    return CampaignMenuResultKind.None;
                case CampaignMenuItemKind.Difficulty:
                    _focusIndex = IndexOf(CampaignMenuItemKind.Difficulty) + 1;
                    return CampaignMenuResultKind.None;
                case CampaignMenuItemKind.Continue:
                    return Begin(CampaignStartMode.Continue, false);
                case CampaignMenuItemKind.Start:
                    return Begin(CampaignStartMode.FromBeginning, false);
                case CampaignMenuItemKind.Restart:
                    IsConfirmingRestart = true;
                    RebuildItems();
                    _focusIndex = IndexOf(CampaignMenuItemKind.CancelRestart);
                    return CampaignMenuResultKind.None;
                case CampaignMenuItemKind.ConfirmRestart:
                    LeaveConfirm();
                    return Begin(CampaignStartMode.FromBeginning, true);
                case CampaignMenuItemKind.CancelRestart:
                    LeaveConfirm();
                    return CampaignMenuResultKind.None;
                case CampaignMenuItemKind.Back:
                    return CampaignMenuResultKind.Back;
                default:
                    throw new InvalidOperationException("unknown campaign menu item " + item);
            }
        }

        /// <summary>kind の項目にフォーカスを移して決定する (マウスで押したとき)。今出ていない項目なら何もせず <see cref="CampaignMenuResultKind.None"/>。</summary>
        public CampaignMenuResultKind Activate(CampaignMenuItemKind kind, int missionIndex = -1)
        {
            var index = IndexOf(kind, missionIndex);
            return FocusOn(index) ? Activate() : CampaignMenuResultKind.None;
        }

        /// <summary>取り消し (ゲームパッドの B・Esc)。確認中なら確認を閉じ、そうでなければメインメニューへ戻る。</summary>
        public CampaignMenuResultKind Cancel()
        {
            if (IsConfirmingRestart)
            {
                LeaveConfirm();
                return CampaignMenuResultKind.None;
            }

            return CampaignMenuResultKind.Back;
        }

        /// <summary>
        /// missionId のミッションのセーブを消したことにする (「最初から」でセーブを消したあと、シーンを読めずにメニューに残ったとき)。
        /// 一覧に無ければ false。
        /// </summary>
        public bool ForgetSave(string missionId)
        {
            for (var i = 0; i < _missions.Length; i++)
            {
                if (!string.Equals(_missions[i].Id, missionId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!_missions[i].HasSave)
                {
                    return true;
                }

                var focused = FocusedItem;
                _missions[i] = _missions[i].WithoutSave();
                if (i == _selectedMission)
                {
                    IsConfirmingRestart = false;
                    RebuildItems();
                    RefocusOrFallBack(focused);
                }

                return true;
            }

            return false;
        }

        private CampaignMenuResultKind Begin(CampaignStartMode mode, bool deletesSave)
        {
            LastStart = new CampaignMenuStart(new CampaignLaunch(SelectedMission.Id, Difficulty), mode, deletesSave);
            return CampaignMenuResultKind.Start;
        }

        private void LeaveConfirm()
        {
            IsConfirmingRestart = false;
            RebuildItems();
            var restart = IndexOf(CampaignMenuItemKind.Restart);
            _focusIndex = restart >= 0 ? restart : IndexOf(CampaignMenuItemKind.Difficulty) + 1;
        }

        // 同じ項目がまだあればそこに、無くなった (始め方が替わった) なら最初の始め方に置く
        private void RefocusOrFallBack(CampaignMenuItem focused)
        {
            var index = _items.IndexOf(focused);
            _focusIndex = index >= 0 ? index : IndexOf(CampaignMenuItemKind.Difficulty) + 1;
        }

        private void RebuildItems()
        {
            _items.Clear();
            if (IsConfirmingRestart)
            {
                _items.Add(new CampaignMenuItem(CampaignMenuItemKind.ConfirmRestart));
                _items.Add(new CampaignMenuItem(CampaignMenuItemKind.CancelRestart));
                return;
            }

            for (var i = 0; i < _missions.Length; i++)
            {
                _items.Add(new CampaignMenuItem(CampaignMenuItemKind.Mission, i));
            }

            _items.Add(new CampaignMenuItem(CampaignMenuItemKind.Difficulty));
            if (SelectedMission.HasSave)
            {
                _items.Add(new CampaignMenuItem(CampaignMenuItemKind.Continue));
                _items.Add(new CampaignMenuItem(CampaignMenuItemKind.Restart));
            }
            else
            {
                _items.Add(new CampaignMenuItem(CampaignMenuItemKind.Start));
            }

            _items.Add(new CampaignMenuItem(CampaignMenuItemKind.Back));
        }

        private static void RequireDifficulty(CampaignDifficulty difficulty, string parameterName)
        {
            if (!Enum.IsDefined(typeof(CampaignDifficulty), difficulty))
            {
                throw new ArgumentOutOfRangeException(parameterName, difficulty, "unknown difficulty");
            }
        }
    }
}
