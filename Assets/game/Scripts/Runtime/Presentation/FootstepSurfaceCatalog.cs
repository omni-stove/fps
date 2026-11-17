using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "FootstepSurfaceCatalog", menuName = "Shooter/Footstep Surface Catalog")]
    public sealed class FootstepSurfaceCatalog : ScriptableObject
    {
        public const string CatalogName = "FootstepSurface";

        public static string ResourcePath => PresentationCatalog.CatalogResourcePath(CatalogName);

        [SerializeField, Tooltip("材質ごとの足音と着地音。材質の Id は重複させず、default を必ず入れる")]
        private List<FootstepSurfaceSounds> _surfaces = new List<FootstepSurfaceSounds>();

        public IReadOnlyList<FootstepSurfaceSounds> Surfaces => _surfaces ??= new List<FootstepSurfaceSounds>();

        public static FootstepSurfaceCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.Footsteps);
            return fromContent != null ? fromContent : Resources.Load<FootstepSurfaceCatalog>(ResourcePath);
        }

        public void SetSurfaces(IEnumerable<FootstepSurfaceSounds> surfaces)
        {
            _surfaces = surfaces == null ? new List<FootstepSurfaceSounds>() : new List<FootstepSurfaceSounds>(surfaces);
        }

        public bool TryGetSurface(string materialId, out FootstepSurfaceSounds sounds)
        {
            var id = SurfaceMaterial.Normalize(materialId);
            foreach (var entry in Surfaces)
            {
                if (entry != null && string.Equals(entry.MaterialId, id, StringComparison.Ordinal))
                {
                    sounds = entry;
                    return true;
                }
            }

            sounds = null;
            return false;
        }

        public static FootstepClipSource CollectClips(
            FootstepSurfaceCatalog catalog, PresentationCatalog fallback, string materialId, FootstepCueKind kind, List<AudioClip> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            if (kind != FootstepCueKind.Footstep && kind != FootstepCueKind.Landing)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind must be Footstep or Landing");
            }

            into.Clear();
            var id = SurfaceMaterial.Normalize(materialId);
            if (catalog != null)
            {
                if (catalog.TryGetSurface(id, out var surface) && AddClips(surface.ClipsFor(kind), into))
                {
                    return id == SurfaceMaterial.DefaultId ? FootstepClipSource.Default : FootstepClipSource.Surface;
                }

                if (id != SurfaceMaterial.DefaultId && catalog.TryGetSurface(SurfaceMaterial.DefaultId, out var fallbackSurface)
                    && AddClips(fallbackSurface.ClipsFor(kind), into))
                {
                    return FootstepClipSource.Default;
                }
            }

            var legacy = fallback == null ? null : kind == FootstepCueKind.Landing ? fallback.Landing : fallback.Footstep;
            if (legacy != null)
            {
                into.Add(legacy);
                return FootstepClipSource.PresentationCatalog;
            }

            return FootstepClipSource.None;
        }

        public static AudioClip PickAvoidingRepeat(IReadOnlyList<AudioClip> clips, AudioClip previous, Func<int, int> randomBelow)
        {
            if (clips == null)
            {
                throw new ArgumentNullException(nameof(clips));
            }

            if (randomBelow == null)
            {
                throw new ArgumentNullException(nameof(randomBelow));
            }

            if (clips.Count == 0)
            {
                return null;
            }

            if (clips.Count == 1)
            {
                return clips[0];
            }

            var previousIndex = -1;
            if (previous != null)
            {
                for (var i = 0; i < clips.Count; i++)
                {
                    if (clips[i] == previous)
                    {
                        previousIndex = i;
                        break;
                    }
                }
            }

            var count = previousIndex < 0 ? clips.Count : clips.Count - 1;
            var roll = randomBelow(count);
            if (roll < 0 || roll >= count)
            {
                throw new InvalidOperationException($"random index {roll} is outside [0, {count})");
            }

            return clips[previousIndex >= 0 && roll >= previousIndex ? roll + 1 : roll];
        }

        public void Validate()
        {
            var source = string.IsNullOrEmpty(name) ? "(unnamed footstep surface catalog)" : name;
            var problems = new List<string>();
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

                if (entry.Footsteps.Count == 0 && entry.Landings.Count == 0)
                {
                    problems.Add($"{label} has no footstep or landing clip");
                }

                RequireClips(problems, entry.Footsteps, $"{label} footstep");
                RequireClips(problems, entry.Landings, $"{label} landing");
            }

            if (!TryGetSurface(SurfaceMaterial.DefaultId, out var fallback))
            {
                problems.Add($"has no '{SurfaceMaterial.DefaultId}' surface");
            }
            else
            {
                if (fallback.Footsteps.Count == 0)
                {
                    problems.Add($"'{SurfaceMaterial.DefaultId}' surface has no footstep clip");
                }

                if (fallback.Landings.Count == 0)
                {
                    problems.Add($"'{SurfaceMaterial.DefaultId}' surface has no landing clip");
                }
            }

            if (problems.Count > 0)
            {
                throw new PresentationCatalogException($"footstep surface catalog '{source}': {string.Join("; ", problems)}");
            }
        }

        private static bool AddClips(IReadOnlyList<AudioClip> clips, List<AudioClip> into)
        {
            foreach (var clip in clips)
            {
                // 破棄されたアセットや参照切れも候補から外すため、Unity の == null で比べる。
                if (clip != null)
                {
                    into.Add(clip);
                }
            }

            return into.Count > 0;
        }

        private static void RequireClips(List<string> problems, IReadOnlyList<AudioClip> clips, string what)
        {
            for (var i = 0; i < clips.Count; i++)
            {
                if (clips[i] == null)
                {
                    problems.Add($"{what} #{i} is missing");
                }
            }
        }
    }

    public enum FootstepClipSource
    {
        None = 0,

        Surface = 1,

        Default = 2,

        PresentationCatalog = 3,
    }

    [Serializable]
    public sealed class FootstepSurfaceSounds
    {
        [SerializeField, Tooltip("材質の Id (SurfaceMaterial の Id と同じ値)。空なら default")]
        private string _materialId = SurfaceMaterial.DefaultId;

        [SerializeField, Tooltip("足音。鳴らすたびにこの中からランダムに選ぶ (直前と同じものは避ける)")]
        private List<AudioClip> _footsteps = new List<AudioClip>();

        [SerializeField, Tooltip("着地の音。鳴らすたびにこの中からランダムに選ぶ (直前と同じものは避ける)")]
        private List<AudioClip> _landings = new List<AudioClip>();

        public FootstepSurfaceSounds()
        {
        }

        public FootstepSurfaceSounds(string materialId, IEnumerable<AudioClip> footsteps, IEnumerable<AudioClip> landings)
        {
            _materialId = SurfaceMaterial.Normalize(materialId);
            _footsteps = footsteps == null ? new List<AudioClip>() : new List<AudioClip>(footsteps);
            _landings = landings == null ? new List<AudioClip>() : new List<AudioClip>(landings);
        }

        // Inspector で空にされても SurfaceMaterial.DefaultId になるよう、読むたびに揃える。
        public string MaterialId => SurfaceMaterial.Normalize(_materialId);

        public IReadOnlyList<AudioClip> Footsteps => _footsteps ??= new List<AudioClip>();

        public IReadOnlyList<AudioClip> Landings => _landings ??= new List<AudioClip>();

        public IReadOnlyList<AudioClip> ClipsFor(FootstepCueKind kind)
        {
            return kind == FootstepCueKind.Landing ? Landings : Footsteps;
        }
    }
}
