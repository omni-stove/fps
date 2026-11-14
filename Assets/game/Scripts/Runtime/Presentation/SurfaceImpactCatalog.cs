using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 弾が当たった面の材質 (<see cref="SurfaceMaterial"/> の Id) ごとの銃痕・着弾エフェクト・着弾音。
    /// Resources の <see cref="ResourcePath"/> に置いたものを <see cref="Load"/> で読み、<see cref="WeaponPresentation"/> が着弾ごとに
    /// <see cref="Resolve"/> で引く。置いていなければ <see cref="WeaponPresentation"/> は <see cref="PresentationCatalog"/> の火花と音を使う。
    /// 中身は Fps.Editor.SurfaceImpactAssetStep が生成した仮の素材で、本物の素材に替えるときはこのアセットの参照を差し替える。
    /// </summary>
    [CreateAssetMenu(fileName = "SurfaceImpactCatalog", menuName = "Fps/Surface Impact Catalog")]
    public sealed class SurfaceImpactCatalog : ScriptableObject
    {
        /// <summary>演出のカタログの名前 (<see cref="PresentationCatalog.CatalogResourcePath"/> に渡す)。</summary>
        public const string CatalogName = "SurfaceImpact";

        [SerializeField, Tooltip("材質ごとの銃痕・エフェクト・音。材質の Id は重複させず、default を入れておく")]
        private List<SurfaceImpactAssets> _surfaces = new List<SurfaceImpactAssets>();

        /// <summary>Resources からのパス (どこかの Resources フォルダの Presentation/SurfaceImpactCatalog.asset、拡張子なし)。</summary>
        public static string ResourcePath => PresentationCatalog.CatalogResourcePath(CatalogName);

        /// <summary>定義順の材質ごとの素材。中身を書き換えるときは <see cref="SetSurfaces"/> を使う。</summary>
        public IReadOnlyList<SurfaceImpactAssets> Surfaces => _surfaces ??= new List<SurfaceImpactAssets>();

        /// <summary>入口 (<see cref="GameContent"/>) のもの、無ければ Resources の <see cref="ResourcePath"/> から読む。どちらも無ければ null。</summary>
        public static SurfaceImpactCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.SurfaceImpacts);
            return fromContent != null ? fromContent : Resources.Load<SurfaceImpactCatalog>(ResourcePath);
        }

        /// <summary>材質ごとの素材をまとめて差し替える。null は空にする。渡した列は複製して持つ。</summary>
        public void SetSurfaces(IEnumerable<SurfaceImpactAssets> surfaces)
        {
            _surfaces = surfaces == null ? new List<SurfaceImpactAssets>() : new List<SurfaceImpactAssets>(surfaces);
        }

        /// <summary>
        /// 材質の Id が materialId (<see cref="SurfaceMaterial.Normalize"/> で揃えたもの) の素材を探す。Id は大文字小文字を区別して比べる。
        /// 無ければ false と null を返す。
        /// </summary>
        public bool TryGetSurface(string materialId, out SurfaceImpactAssets assets)
        {
            var id = SurfaceMaterial.Normalize(materialId);
            foreach (var entry in Surfaces)
            {
                if (entry != null && string.Equals(entry.MaterialId, id, StringComparison.Ordinal))
                {
                    assets = entry;
                    return true;
                }
            }

            assets = null;
            return false;
        }

        /// <summary>
        /// materialId の着弾に使う素材。その材質の素材があればそれ、無ければ <see cref="SurfaceMaterial.DefaultId"/> の素材、
        /// それも無ければ null (着弾の演出を出さない)。見つけた素材の空の枠は、その枠の演出だけを出さない (default の枠で埋めない)。
        /// </summary>
        public SurfaceImpactAssets Resolve(string materialId)
        {
            if (TryGetSurface(materialId, out var assets))
            {
                return assets;
            }

            return TryGetSurface(SurfaceMaterial.DefaultId, out var fallback) ? fallback : null;
        }

        /// <summary>
        /// 材質が 1 つ以上あり、<see cref="SurfaceMaterial.DefaultId"/> を含み、どの材質も Id が重複せず、銃痕・エフェクト・音と
        /// 銃痕の大きさがすべて埋まっていることを確かめる。足りないものはすべて並べて 1 つの例外にする。
        /// </summary>
        /// <exception cref="PresentationCatalogException">どれかが欠けている・重複しているとき。</exception>
        public void Validate()
        {
            var source = string.IsNullOrEmpty(name) ? "(unnamed surface impact catalog)" : name;
            var problems = new List<string>();

            if (Surfaces.Count == 0)
            {
                problems.Add("has no surfaces");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < Surfaces.Count; i++)
            {
                var entry = Surfaces[i];
                if (entry == null)
                {
                    problems.Add($"surface #{i} is empty");
                    continue;
                }

                var label = $"surface #{i} ('{entry.MaterialId}')";
                if (!seen.Add(entry.MaterialId))
                {
                    problems.Add($"{label} uses a material id already used by another entry");
                }

                RequireReference(problems, entry.Decal, $"{label} decal");
                RequireReference(problems, entry.Effect, $"{label} effect");
                RequireReference(problems, entry.Sound, $"{label} sound");
                if (!(entry.DecalSize > 0f) || float.IsInfinity(entry.DecalSize))
                {
                    problems.Add($"{label} decal size {entry.DecalSize} is not a finite number > 0");
                }
            }

            if (Surfaces.Count > 0 && !seen.Contains(SurfaceMaterial.DefaultId))
            {
                problems.Add($"has no '{SurfaceMaterial.DefaultId}' surface for materials without their own entry");
            }

            if (problems.Count > 0)
            {
                throw new PresentationCatalogException($"surface impact catalog '{source}': {string.Join("; ", problems)}");
            }
        }

        // 破棄されたアセットや参照切れも拾うため、Unity の == null で比べる。
        private static void RequireReference(List<string> problems, UnityEngine.Object reference, string what)
        {
            if (reference == null)
            {
                problems.Add($"{what} is missing");
            }
        }
    }

    /// <summary>材質 1 つの銃痕・着弾エフェクト・着弾音。<see cref="SurfaceImpactCatalog"/> の要素。</summary>
    [Serializable]
    public sealed class SurfaceImpactAssets
    {
        /// <summary>銃痕の一辺の既定の長さ (m)。</summary>
        public const float DefaultDecalSize = 0.12f;

        [SerializeField, Tooltip("材質の Id (SurfaceMaterial の Id と同じ値)")]
        private string _materialId = SurfaceMaterial.DefaultId;

        [SerializeField, Tooltip("銃痕の材質。四角い板に貼り、面の法線の側から見える")]
        private Material _decal;

        [SerializeField, Min(0.001f), Tooltip("銃痕の一辺の長さ (m)")]
        private float _decalSize = DefaultDecalSize;

        [SerializeField, Tooltip("当たったところに出すエフェクト。+Z を面の法線に向けて置く")]
        private GameObject _effect;

        [SerializeField, Tooltip("当たったときの音")]
        private AudioClip _sound;

        public SurfaceImpactAssets()
        {
        }

        /// <summary>materialId は <see cref="SurfaceMaterial.Normalize"/> で揃えて持つ (null・空白なら default)。</summary>
        public SurfaceImpactAssets(string materialId, Material decal, float decalSize, GameObject effect, AudioClip sound)
        {
            _materialId = SurfaceMaterial.Normalize(materialId);
            _decal = decal;
            _decalSize = decalSize;
            _effect = effect;
            _sound = sound;
        }

        public string MaterialId => SurfaceMaterial.Normalize(_materialId);

        /// <summary>銃痕の材質。無ければ銃痕を出さない。</summary>
        public Material Decal => _decal;

        /// <summary>銃痕の一辺の長さ (m)。</summary>
        public float DecalSize => _decalSize;

        /// <summary>着弾のエフェクト。無ければ出さない。</summary>
        public GameObject Effect => _effect;

        /// <summary>着弾の音。無ければ鳴らさない。</summary>
        public AudioClip Sound => _sound;
    }
}
