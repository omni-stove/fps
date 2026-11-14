using UnityEngine;

namespace Fps.Runtime
{
    // 枠は任意 (PresentationCatalog の規約)。無いか空なら EnemyStealth は仮の印 (AwarenessMarker) を出す。
    [CreateAssetMenu(fileName = "AwarenessIndicatorCatalog", menuName = "Fps/Awareness Indicator Catalog")]
    public sealed class AwarenessIndicatorCatalog : ScriptableObject
    {
        public const string CatalogName = "AwarenessIndicator";

        // PresentationCatalog.CatalogResourcePath(CatalogName) と同じ値。メソッドを呼ばず const にして、ログの文字列にも使えるようにする。
        public const string ResourcePath = PresentationCatalog.ResourceFolder + "/" + CatalogName + PresentationCatalog.CatalogSuffix;

        [SerializeField, Tooltip("敵の頭上に出す印。根に IAwarenessIndicator を実装したコンポーネントを持つプレハブ")]
        private GameObject _indicatorPrefab;

        public GameObject IndicatorPrefab
        {
            get => _indicatorPrefab;
            set => _indicatorPrefab = value;
        }

        public static AwarenessIndicatorCatalog Load()
        {
            return Resources.Load<AwarenessIndicatorCatalog>(ResourcePath);
        }
    }
}
