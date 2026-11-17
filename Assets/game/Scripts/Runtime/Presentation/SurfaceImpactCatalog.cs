using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "SurfaceImpactCatalog", menuName = "Shooter/Surface Impact Catalog")]
    public sealed class SurfaceImpactCatalog : ScriptableObject
    {
        public const string CatalogName = "SurfaceImpact";

        [SerializeField, Tooltip("材質ごとの銃痕・エフェクト・音。材質の Id は重複させず、default を入れておく")]
        private List<SurfaceImpactAssets> _surfaces = new List<SurfaceImpactAssets>();

        public static string ResourcePath => PresentationCatalog.CatalogResourcePath(CatalogName);

        public IReadOnlyList<SurfaceImpactAssets> Surfaces => _surfaces ??= new List<SurfaceImpactAssets>();

        public static SurfaceImpactCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.SurfaceImpacts);
            return fromContent != null ? fromContent : Resources.Load<SurfaceImpactCatalog>(ResourcePath);
        }

        public void SetSurfaces(IEnumerable<SurfaceImpactAssets> surfaces)
        {
            _surfaces = surfaces == null ? new List<SurfaceImpactAssets>() : new List<SurfaceImpactAssets>(surfaces);
        }

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

        public SurfaceImpactAssets Resolve(string materialId)
        {
            if (TryGetSurface(materialId, out var assets))
            {
                return assets;
            }

            return TryGetSurface(SurfaceMaterial.DefaultId, out var fallback) ? fallback : null;
        }

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

    [Serializable]
    public sealed class SurfaceImpactAssets
    {
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

        public SurfaceImpactAssets(string materialId, Material decal, float decalSize, GameObject effect, AudioClip sound)
        {
            _materialId = SurfaceMaterial.Normalize(materialId);
            _decal = decal;
            _decalSize = decalSize;
            _effect = effect;
            _sound = sound;
        }

        public string MaterialId => SurfaceMaterial.Normalize(_materialId);

        public Material Decal => _decal;

        public float DecalSize => _decalSize;

        public GameObject Effect => _effect;

        public AudioClip Sound => _sound;
    }
}
