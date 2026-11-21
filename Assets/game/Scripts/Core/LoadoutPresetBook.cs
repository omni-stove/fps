using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // 試合で使うのは選んでいる組 (Active) だけ。組を足す・選ぶ・名前を変える・中身を替えるのはメニューから。
    public sealed class LoadoutPresetBook
    {
        public const int NoPreset = -1;

        private readonly List<LoadoutPreset> _presets = new List<LoadoutPreset>();

        public LoadoutPresetBook(LoadoutPresetSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public LoadoutPresetSpec Spec { get; }

        public int MaxPresets => Spec.MaxPresets;

        public int Count => _presets.Count;

        public bool IsFull => _presets.Count >= Spec.MaxPresets;

        public IReadOnlyList<LoadoutPreset> Presets => _presets;

        public int ActiveIndex { get; private set; } = NoPreset;

        public LoadoutPreset Active => ActiveIndex == NoPreset ? null : _presets[ActiveIndex];

        public LoadoutPreset this[int index]
        {
            get
            {
                RequireIndex(index);
                return _presets[index];
            }
        }

        // 最初の組は足したときに選ぶ。2 つ目からは選び直すかを呼ぶ側が決める。
        public int Add(string name, LoadoutChoice choice)
        {
            var preset = new LoadoutPreset(NormalizeName(name), RequireSavable(choice));
            if (IsFull)
            {
                throw new InvalidOperationException("at most " + Spec.MaxPresets + " loadout presets can be kept");
            }

            _presets.Add(preset);
            if (ActiveIndex == NoPreset)
            {
                ActiveIndex = 0;
            }

            return _presets.Count - 1;
        }

        public void Select(int index)
        {
            RequireIndex(index);
            ActiveIndex = index;
        }

        public void Rename(int index, string name)
        {
            RequireIndex(index);
            _presets[index] = new LoadoutPreset(NormalizeName(name), _presets[index].Choice);
        }

        public void Replace(int index, LoadoutChoice choice)
        {
            RequireIndex(index);
            _presets[index] = new LoadoutPreset(_presets[index].Name, RequireSavable(choice));
        }

        // 選んでいる組より前を消したら番号を詰め、選んでいる組を消したら同じ番号 (末尾なら 1 つ前) の組を選ぶ
        public void RemoveAt(int index)
        {
            RequireIndex(index);
            _presets.RemoveAt(index);
            if (_presets.Count == 0)
            {
                ActiveIndex = NoPreset;
            }
            else if (ActiveIndex > index || ActiveIndex >= _presets.Count)
            {
                ActiveIndex--;
            }
        }

        // 前後の空白は打ち間違いとみなして落とす。長すぎる名前は切らずに断り、どこまで残るかを UI の入力欄の上限に任せる。
        public string NormalizeName(string name)
        {
            var trimmed = (name ?? string.Empty).Trim();
            if (trimmed.Length > Spec.MaxNameLength)
            {
                throw new ArgumentException(
                    "a loadout preset name must be at most " + Spec.MaxNameLength + " characters but has " + trimmed.Length,
                    nameof(name));
            }

            foreach (var c in trimmed)
            {
                if (char.IsControl(c))
                {
                    throw new ArgumentException("a loadout preset name must not contain control characters", nameof(name));
                }
            }

            return trimmed;
        }

        public static LoadoutChoice RequireSavable(LoadoutChoice choice)
        {
            if (choice == null)
            {
                throw new ArgumentNullException(nameof(choice));
            }

            var weaponIds = choice.WeaponIds;
            if (weaponIds.Count == 0)
            {
                throw new ArgumentException("at least one weapon id is required", nameof(choice));
            }

            for (var i = 0; i < weaponIds.Count; i++)
            {
                if (string.IsNullOrEmpty(weaponIds[i]))
                {
                    throw new ArgumentException("slot " + i + " has no weapon id", nameof(choice));
                }
            }

            return choice;
        }

        private void RequireIndex(int index)
        {
            if (index < 0 || index >= _presets.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must be in [0, " + _presets.Count + ")");
            }
        }
    }
}
