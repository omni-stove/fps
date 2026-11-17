using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class CampaignMenuMission
    {
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

    public enum CampaignMenuItemKind
    {
        Mission,

        Difficulty,

        Continue,

        Restart,

        Start,

        Back,

        ConfirmRestart,

        CancelRestart,
    }

    public readonly struct CampaignMenuItem : IEquatable<CampaignMenuItem>
    {
        public CampaignMenuItem(CampaignMenuItemKind kind, int missionIndex = -1)
        {
            Kind = kind;
            MissionIndex = kind == CampaignMenuItemKind.Mission ? missionIndex : -1;
        }

        public CampaignMenuItemKind Kind { get; }

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

        public static bool operator ==(CampaignMenuItem left, CampaignMenuItem right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CampaignMenuItem left, CampaignMenuItem right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return Kind == CampaignMenuItemKind.Mission ? Kind + "#" + MissionIndex : Kind.ToString();
        }
    }

    public enum CampaignStartMode
    {
        Continue,

        FromBeginning,
    }

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

        public CampaignLaunch Launch { get; }

        public CampaignStartMode Mode { get; }

        public bool DeletesSave { get; }

        public override string ToString()
        {
            return Launch + " " + (Mode == CampaignStartMode.Continue ? "continue" : "from-beginning") + (DeletesSave ? " (delete save)" : "");
        }
    }

    public enum CampaignMenuResultKind
    {
        None,

        Start,

        Back,
    }

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

        public static IReadOnlyList<CampaignDifficulty> Difficulties => _difficulties;

        public IReadOnlyList<CampaignMenuMission> Missions => _missions;

        public int SelectedMissionIndex => _selectedMission;

        public CampaignMenuMission SelectedMission => _missions[_selectedMission];

        public CampaignDifficulty Difficulty { get; private set; }

        public bool IsConfirmingRestart { get; private set; }

        public IReadOnlyList<CampaignMenuItem> Items => _items;

        public int FocusIndex => _focusIndex;

        public CampaignMenuItem FocusedItem => _items[_focusIndex];

        public CampaignMenuStart LastStart { get; private set; }

        public int IndexOf(CampaignMenuItemKind kind, int missionIndex = -1)
        {
            return _items.IndexOf(new CampaignMenuItem(kind, missionIndex));
        }

        public void MoveFocus(int step)
        {
            if (step == 0)
            {
                return;
            }

            FocusOn(MenuRoute.MoveFocus(_focusIndex, step, _items.Count));
        }

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

        // 「最初から」の確認では、間違えてセーブを消さないよう「やめる」にフォーカスを置く。
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

        public CampaignMenuResultKind Activate(CampaignMenuItemKind kind, int missionIndex = -1)
        {
            var index = IndexOf(kind, missionIndex);
            return FocusOn(index) ? Activate() : CampaignMenuResultKind.None;
        }

        public CampaignMenuResultKind Cancel()
        {
            if (IsConfirmingRestart)
            {
                LeaveConfirm();
                return CampaignMenuResultKind.None;
            }

            return CampaignMenuResultKind.Back;
        }

        // 「最初から」でセーブを消したあと、シーンを読めずにメニューに残ったときに、消えたセーブを出し続けないよう使う。
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
