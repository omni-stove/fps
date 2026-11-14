using UnityEngine;

namespace Fps.Runtime
{
    // 枠は任意 (PresentationCatalog の規約)。無いか空なら SuppressionVignette はコードで作る仮の模様で画面の端を暗くする。
    [CreateAssetMenu(fileName = "SuppressionCatalog", menuName = "Fps/Suppression Catalog")]
    public sealed class SuppressionCatalog : ScriptableObject
    {
        public const string CatalogName = "Suppression";

        // PresentationCatalog.CatalogResourcePath(CatalogName) と同じ値。RagdollCatalog と同じく const にしてログにも使えるようにする。
        public const string ResourcePath = PresentationCatalog.ResourceFolder + "/" + CatalogName + PresentationCatalog.CatalogSuffix;

        [SerializeField, Tooltip("制圧されたときに画面全体に重ねる模様。中央を透明、端を不透明にする。空ならコードで作る仮のもの")]
        private Texture2D _vignette;

        [SerializeField, Tooltip("模様に掛ける色。アルファは制圧の度合いで決めるので使わない")]
        private Color _tint = Color.black;

        public Texture2D Vignette
        {
            get => _vignette;
            set => _vignette = value;
        }

        public Color Tint
        {
            get => _tint;
            set => _tint = value;
        }

        public static SuppressionCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.Suppression);
            return fromContent != null ? fromContent : Resources.Load<SuppressionCatalog>(ResourcePath);
        }
    }
}
