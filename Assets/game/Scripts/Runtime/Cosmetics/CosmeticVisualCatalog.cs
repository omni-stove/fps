using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Runtime
{
    // スキンは体のプレハブを丸ごと替え、迷彩は武器のモデルの材質だけを替える。武器ごとのモデルを迷彩の数だけ用意しなくて済むようにするため。
    [CreateAssetMenu(fileName = "CosmeticVisualCatalog", menuName = "Shooter/Cosmetic Visual Catalog")]
    public sealed class CosmeticVisualCatalog : ScriptableObject
    {
        public const string CatalogName = "CosmeticVisual";

        public static readonly string ResourcePath = PresentationCatalog.CatalogResourcePath(CatalogName);

        [SerializeField, Tooltip("見た目の JSON (Tuning/cosmetics.json) の visualId と絵の組")]
        private List<Entry> _visuals = new List<Entry>();

        public IReadOnlyList<Entry> Visuals => _visuals ??= new List<Entry>();

        public static CosmeticVisualCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.CosmeticVisuals);
            return fromContent != null ? fromContent : Resources.Load<CosmeticVisualCatalog>(ResourcePath);
        }

        public Entry Find(string visualId)
        {
            if (string.IsNullOrEmpty(visualId))
            {
                return null;
            }

            foreach (var entry in Visuals)
            {
                if (entry != null && string.Equals(entry.VisualId, visualId, StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        public GameObject FindBody(string visualId)
        {
            var entry = Find(visualId);
            return entry != null && entry.Body != null ? entry.Body : null;
        }

        public Material FindPaint(string visualId)
        {
            var entry = Find(visualId);
            return entry != null && entry.Paint != null ? entry.Paint : null;
        }

        public void Add(string visualId, GameObject body, Material paint)
        {
            _visuals ??= new List<Entry>();
            _visuals.Add(new Entry { VisualId = visualId, Body = body, Paint = paint });
        }

        [Serializable]
        public sealed class Entry
        {
            [SerializeField, Tooltip("見た目の JSON の visualId")]
            private string _visualId;

            [SerializeField, Tooltip("スキンの体。ネット対戦の相手の体 (NetV2/RemotePlayerBody) と同じく、根の原点を体の中心に合わせ、立った高さで作る")]
            private GameObject _body;

            [SerializeField, Tooltip("迷彩で武器のモデルに塗る材質")]
            private Material _paint;

            public string VisualId
            {
                get => _visualId;
                set => _visualId = value;
            }

            public GameObject Body
            {
                get => _body;
                set => _body = value;
            }

            public Material Paint
            {
                get => _paint;
                set => _paint = value;
            }
        }
    }
}
