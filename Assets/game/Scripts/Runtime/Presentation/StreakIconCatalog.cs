using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "StreakIconCatalog", menuName = "Shooter/Streak Icon Catalog")]
    public sealed class StreakIconCatalog : ScriptableObject
    {
        public const string CatalogName = "StreakIcon";

        public static readonly string ResourcePath = PresentationCatalog.CatalogResourcePath(CatalogName);

        [SerializeField, Tooltip("ストリークの報酬の Id と画像の組")]
        private List<Entry> _icons = new List<Entry>();

        public IReadOnlyList<Entry> Icons => _icons ??= new List<Entry>();

        public static StreakIconCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.StreakIcons);
            return fromContent != null ? fromContent : Resources.Load<StreakIconCatalog>(ResourcePath);
        }

        public Texture2D Find(string rewardId)
        {
            if (string.IsNullOrEmpty(rewardId))
            {
                return null;
            }

            foreach (var entry in Icons)
            {
                if (entry != null && string.Equals(entry.RewardId, rewardId, StringComparison.Ordinal))
                {
                    return entry.Icon;
                }
            }

            return null;
        }

        public void Add(string rewardId, Texture2D icon)
        {
            _icons ??= new List<Entry>();
            _icons.Add(new Entry { RewardId = rewardId, Icon = icon });
        }

        [Serializable]
        public sealed class Entry
        {
            [SerializeField, Tooltip("streaks.json の報酬の id")]
            private string _rewardId;

            [SerializeField]
            private Texture2D _icon;

            public string RewardId
            {
                get => _rewardId;
                set => _rewardId = value;
            }

            public Texture2D Icon
            {
                get => _icon;
                set => _icon = value;
            }
        }
    }
}
