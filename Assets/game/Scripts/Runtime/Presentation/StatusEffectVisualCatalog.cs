using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>
    /// 状態異常の種類 (<see cref="Shooter.Core.StatusEffectSpec.KindId"/>) ごとの、かかっている体に付ける見た目。
    /// <see cref="StatusEffectVisuals"/> が <see cref="Load"/> で読む。置き場所と枠の扱いは <see cref="PresentationCatalog"/> の規約に従い、
    /// カタログが無い・種類の枠が空なら <see cref="StatusEffectVisuals"/> がコードで形を作る。
    /// </summary>
    [CreateAssetMenu(fileName = "StatusEffectVisualsCatalog", menuName = "Shooter/Status Effect Visuals Catalog")]
    public sealed class StatusEffectVisualCatalog : ScriptableObject
    {
        /// <summary><see cref="PresentationCatalog.CatalogResourcePath"/> に渡す名前。</summary>
        public const string CatalogName = "StatusEffectVisuals";

        /// <summary>Resources からのパス (どこかの Resources フォルダの Presentation/StatusEffectVisualsCatalog.asset)。</summary>
        public static readonly string ResourcePath = PresentationCatalog.CatalogResourcePath(CatalogName);

        [SerializeField, Tooltip("種類ごとの見た目。種類の Id は重複させない")]
        private List<StatusEffectVisualEntry> _kinds = new List<StatusEffectVisualEntry>();

        public IReadOnlyList<StatusEffectVisualEntry> Kinds => _kinds ??= new List<StatusEffectVisualEntry>();

        /// <summary>Resources の <see cref="ResourcePath"/> から読む。無ければ null。</summary>
        public static StatusEffectVisualCatalog Load()
        {
            return Resources.Load<StatusEffectVisualCatalog>(ResourcePath);
        }

        /// <summary>種類の見た目をまとめて差し替える。null は空にする。渡した列は複製して持つ。</summary>
        public void SetKinds(IEnumerable<StatusEffectVisualEntry> kinds)
        {
            _kinds = kinds == null ? new List<StatusEffectVisualEntry>() : new List<StatusEffectVisualEntry>(kinds);
        }

        /// <summary>kindId の見た目。無い・枠が空なら null。</summary>
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
