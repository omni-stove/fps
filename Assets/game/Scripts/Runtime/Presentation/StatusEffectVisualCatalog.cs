using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "StatusEffectVisualsCatalog", menuName = "Shooter/Status Effect Visuals Catalog")]
    public sealed class StatusEffectVisualCatalog : ScriptableObject
    {
        public const string CatalogName = "StatusEffectVisuals";

        public static readonly string ResourcePath = PresentationCatalog.CatalogResourcePath(CatalogName);

        [SerializeField, Tooltip("種類ごとの見た目。種類の Id は重複させない")]
        private List<StatusEffectVisualEntry> _kinds = new List<StatusEffectVisualEntry>();

        public IReadOnlyList<StatusEffectVisualEntry> Kinds => _kinds ??= new List<StatusEffectVisualEntry>();

        public static StatusEffectVisualCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.StatusEffectVisuals);
            return fromContent != null ? fromContent : Resources.Load<StatusEffectVisualCatalog>(ResourcePath);
        }

        public void SetKinds(IEnumerable<StatusEffectVisualEntry> kinds)
        {
            _kinds = kinds == null ? new List<StatusEffectVisualEntry>() : new List<StatusEffectVisualEntry>(kinds);
        }

        public GameObject VisualFor(string kindId)
        {
            foreach (var entry in Kinds)
            {
                if (entry != null && string.Equals(entry.KindId, kindId, StringComparison.Ordinal) && entry.Visual != null)
                {
                    return entry.Visual;
                }
            }

            return null;
        }
    }

    [Serializable]
    public sealed class StatusEffectVisualEntry
    {
        [SerializeField, Tooltip("状態異常の種類の Id (burn など)")]
        private string _kindId = string.Empty;

        [SerializeField, Tooltip("かかっている間、体の中心に付ける見た目。空ならコードで形を作る")]
        private GameObject _visual;

        public StatusEffectVisualEntry()
        {
        }

        public StatusEffectVisualEntry(string kindId, GameObject visual)
        {
            _kindId = kindId ?? string.Empty;
            _visual = visual;
        }

        public string KindId => _kindId ?? string.Empty;

        public GameObject Visual => _visual;
    }
}
