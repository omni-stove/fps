using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "ItemIconCatalog", menuName = "Shooter/Item Icon Catalog")]
    public sealed class ItemIconCatalog : ScriptableObject
    {
        public const string CatalogName = "ItemIcon";

        public static readonly string ResourcePath = PresentationCatalog.CatalogResourcePath(CatalogName);

        [SerializeField, Tooltip("アイコンの Id と画像の組")]
        private List<Entry> _icons = new List<Entry>();

        public IReadOnlyList<Entry> Icons => _icons ??= new List<Entry>();

        public static ItemIconCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.ItemIcons);
            return fromContent != null ? fromContent : Resources.Load<ItemIconCatalog>(ResourcePath);
        }

        public Texture2D Find(string iconId)
        {
            if (string.IsNullOrEmpty(iconId))
            {
                return null;
            }

            foreach (var entry in Icons)
            {
                if (entry != null && string.Equals(entry.IconId, iconId, StringComparison.Ordinal))
                {
                    return entry.Icon;
                }
            }

            return null;
        }

        public void Add(string iconId, Texture2D icon)
        {
            _icons ??= new List<Entry>();
            _icons.Add(new Entry { IconId = iconId, Icon = icon });
        }

        [Serializable]
        public sealed class Entry
        {
            [SerializeField, Tooltip("アイテムの JSON の iconId")]
            private string _iconId;

            [SerializeField]
            private Texture2D _icon;

            public string IconId
            {
                get => _iconId;
                set => _iconId = value;
            }

            public Texture2D Icon
            {
                get => _icon;
                set => _icon = value;
            }
        }
    }
}
