using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "ReticleCatalog", menuName = "Shooter/Reticle Catalog")]
    public sealed class ReticleCatalog : ScriptableObject
    {
        public const string CatalogName = "Reticle";

        public const float DefaultLensDiameter = 0.92f;

        public static readonly string ResourcePath = PresentationCatalog.CatalogResourcePath(CatalogName);

        public static readonly Color DefaultMaskColor = Color.black;

        public static readonly ReticleEntry DefaultScope = new ReticleEntry(string.Empty, null, ReticleShape.Crosshair, Color.black, 1f);

        public static readonly ReticleEntry DefaultDot = new ReticleEntry(string.Empty, null, ReticleShape.Dot, new Color(1f, 0.15f, 0.1f, 1f), 0.008f);

        [SerializeField, Tooltip("照準ごとの見た目。Id は重複させない")]
        private List<ReticleEntry> _reticles = new List<ReticleEntry>();

        [SerializeField, Range(0.1f, 2f), Tooltip("スコープの縁の内側の円の直径 (画面の高さに対する割合)")]
        private float _lensDiameter = DefaultLensDiameter;

        [SerializeField, Tooltip("スコープの縁の外側を塗る色")]
        private Color _maskColor = DefaultMaskColor;

        public IReadOnlyList<ReticleEntry> Reticles => _reticles ??= new List<ReticleEntry>();

        public float LensDiameter
        {
            get => _lensDiameter;
            set => _lensDiameter = Mathf.Clamp(value, 0.1f, 2f);
        }

        public Color MaskColor
        {
            get => _maskColor;
            set => _maskColor = value;
        }

        public static ReticleCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.Reticles);
            return fromContent != null ? fromContent : Resources.Load<ReticleCatalog>(ResourcePath);
        }

        public static ReticleEntry Resolve(ReticleCatalog catalog, string reticleId, bool scopeOverlay)
        {
            if (catalog != null && catalog.TryGet(reticleId, out var entry))
            {
                return entry;
            }

            return scopeOverlay ? DefaultScope : DefaultDot;
        }

        public void SetReticles(IEnumerable<ReticleEntry> reticles)
        {
            _reticles = reticles == null ? new List<ReticleEntry>() : new List<ReticleEntry>(reticles);
        }

        public bool TryGet(string reticleId, out ReticleEntry entry)
        {
            if (!string.IsNullOrEmpty(reticleId))
            {
                foreach (var candidate in Reticles)
                {
                    if (candidate != null && string.Equals(candidate.Id, reticleId, StringComparison.Ordinal))
                    {
                        entry = candidate;
                        return true;
                    }
                }
            }

            entry = null;
            return false;
        }
    }

    public enum ReticleShape
    {
        Dot,

        Crosshair,

        Circle,

        MilDot,
    }

    [Serializable]
    public sealed class ReticleEntry
    {
        [SerializeField, Tooltip("照準の Id (武器・アタッチメントの JSON の sight.reticleId)")]
        private string _id;

        [SerializeField, Tooltip("照準の画像。中央を狙う点にする。空なら形をコードで描く")]
        private Texture2D _image;

        [SerializeField, Tooltip("画像が空のときにコードで描く形")]
        private ReticleShape _shape;

        [SerializeField, Tooltip("照準の色。画像があれば画像に掛ける")]
        private Color _color = Color.white;

        [SerializeField, Range(0.001f, 2f), Tooltip("照準の大きさ。スコープの縁を出すときは縁の内側の円の直径、出さないときは画面の高さに対する割合")]
        private float _size = 0.01f;

        public ReticleEntry(string id, Texture2D image, ReticleShape shape, Color color, float size)
        {
            _id = id;
            _image = image;
            _shape = shape;
            _color = color;
            _size = size;
        }

        public string Id => _id;

        public Texture2D Image => _image;

        public ReticleShape Shape => _shape;

        public Color Color => _color;

        public float Size => _size;
    }
}
