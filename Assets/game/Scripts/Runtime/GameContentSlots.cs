using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace Shooter.Runtime
{
    // 本体が型を知らない GameContent の欄 (キャンペーンのミッション、隠密の印など) を、それを持つ側が登録する。
    // GameContentAssets はここに出た枠を組み込みの枠と同じに埋める。
    public static class GameContentSlots
    {
        private static readonly List<GameContentSlot> _slots = new List<GameContentSlot>();

        public static IReadOnlyList<GameContentSlot> All
        {
            get
            {
                lock (_slots)
                {
                    return _slots.ToArray();
                }
            }
        }

        // Play の開始時と Editor の読み込み時の両方で呼ばれるので、同じ名前は並びの位置を保ったまま置き換える。
        public static void Register(string name, Type assetType, Func<GameContent, Object> get, Action<GameContent, Object> set)
        {
            var slot = new GameContentSlot(name, assetType, get, set);
            lock (_slots)
            {
                var index = _slots.FindIndex(s => string.Equals(s.Name, slot.Name, StringComparison.Ordinal));
                if (index >= 0)
                {
                    _slots[index] = slot;
                }
                else
                {
                    _slots.Add(slot);
                }
            }
        }

        public static bool Unregister(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            lock (_slots)
            {
                return _slots.RemoveAll(s => string.Equals(s.Name, name, StringComparison.Ordinal)) > 0;
            }
        }
    }

    public sealed class GameContentSlot
    {
        public GameContentSlot(string name, Type assetType, Func<GameContent, Object> get, Action<GameContent, Object> set)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("a slot needs a name", nameof(name));
            }

            if (assetType == null)
            {
                throw new ArgumentNullException(nameof(assetType));
            }

            if (!typeof(Object).IsAssignableFrom(assetType))
            {
                throw new ArgumentException($"slot '{name}': {assetType.Name} is not a UnityEngine.Object", nameof(assetType));
            }

            Name = name;
            AssetType = assetType;
            Get = get ?? throw new ArgumentNullException(nameof(get));
            Set = set ?? throw new ArgumentNullException(nameof(set));
        }

        public string Name { get; }

        public Type AssetType { get; }

        public Func<GameContent, Object> Get { get; }

        public Action<GameContent, Object> Set { get; }
    }
}
