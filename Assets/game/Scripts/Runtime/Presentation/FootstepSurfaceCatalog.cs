using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 床の材質 (<see cref="SurfaceMaterial.Id"/>) ごとの足音と着地音のクリップの組。<see cref="CharacterAudio"/> が足元の材質で引く。
    /// Resources の <see cref="ResourcePath"/> に置いたものを <see cref="Load"/> で読む (<see cref="PresentationCatalog"/> の規約)。
    /// 引き方は <see cref="CollectClips"/>: 材質の組 → <see cref="SurfaceMaterial.DefaultId"/> の組 → <see cref="PresentationCatalog"/> の
    /// 足音・着地音の順に、クリップが 1 つでもある最初のものを使う。カタログが無くても従来の足音で鳴る。
    /// 中身は Fps.Editor.FootstepSurfaceAssetStep が生成した仮の素材で、本物の素材に替えるときはこのアセットの参照を差し替える。
    /// </summary>
    [CreateAssetMenu(fileName = "FootstepSurfaceCatalog", menuName = "Fps/Footstep Surface Catalog")]
    public sealed class FootstepSurfaceCatalog : ScriptableObject
    {
        /// <summary><see cref="PresentationCatalog.CatalogResourcePath"/> に渡す名前。</summary>
        public const string CatalogName = "FootstepSurface";

        /// <summary>Resources からのパス (Presentation/FootstepSurfaceCatalog、拡張子なし)。</summary>
        public static string ResourcePath => PresentationCatalog.CatalogResourcePath(CatalogName);

        [SerializeField, Tooltip("材質ごとの足音と着地音。材質の Id は重複させず、default を必ず入れる")]
        private List<FootstepSurfaceSounds> _surfaces = new List<FootstepSurfaceSounds>();

        /// <summary>定義順の材質ごとの音。中身を書き換えるときは <see cref="SetSurfaces"/> を使う。</summary>
        public IReadOnlyList<FootstepSurfaceSounds> Surfaces => _surfaces ??= new List<FootstepSurfaceSounds>();

        /// <summary>入口 (<see cref="GameContent"/>) のもの、無ければ Resources の <see cref="ResourcePath"/> から読む。どちらも無ければ null。</summary>
        public static FootstepSurfaceCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.Footsteps);
            return fromContent != null ? fromContent : Resources.Load<FootstepSurfaceCatalog>(ResourcePath);
        }

        /// <summary>材質ごとの音をまとめて差し替える。null は空にする。渡した列は複製して持つ。</summary>
        public void SetSurfaces(IEnumerable<FootstepSurfaceSounds> surfaces)
        {
            _surfaces = surfaces == null ? new List<FootstepSurfaceSounds>() : new List<FootstepSurfaceSounds>(surfaces);
        }

        /// <summary>
        /// 材質の Id が materialId (<see cref="SurfaceMaterial.Normalize"/> で揃えて、大文字小文字を区別して比べる) の組を探す。
        /// 無ければ false と null。
        /// </summary>
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

        /// <summary>
        /// materialId の床で kind の音を鳴らすときの候補を into に入れ (先に空にする)、どこから引いたかを返す。
        /// 材質の組 → <see cref="SurfaceMaterial.DefaultId"/> の組 → fallback の <see cref="PresentationCatalog.Footstep"/> /
        /// <see cref="PresentationCatalog.Landing"/> の順に、空でない (破棄済みでない) クリップが 1 つでもある最初のものを使う。
        /// どれも無ければ into は空で <see cref="FootstepClipSource.None"/>。catalog・fallback は null でよい。
        /// </summary>
        /// <exception cref="ArgumentNullException">into が null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">kind が足音でも着地でもない。</exception>
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

        /// <summary>
        /// clips から 1 つ選ぶ。previous が clips にあり候補が 2 つ以上なら previous 以外から選ぶ (同じ音を続けない)。
        /// randomBelow(n) は [0, n) の整数を返すもの。clips が空なら null。
        /// </summary>
        /// <exception cref="ArgumentNullException">clips か randomBelow が null。</exception>
        /// <exception cref="InvalidOperationException">randomBelow が範囲の外を返した。</exception>
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

        /// <summary>
        /// <see cref="SurfaceMaterial.DefaultId"/> の組が足音と着地音をそれぞれ 1 つ以上持ち、どの組も材質の Id が重複せず、
        /// 足音か着地音を 1 つ以上持ち、どのクリップの参照も切れていないことを確かめる。足りないものはすべて並べて 1 つの例外にする。
        /// </summary>
        /// <exception cref="PresentationCatalogException">どれかが欠けている・重複しているとき。</exception>
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

    /// <summary><see cref="FootstepSurfaceCatalog.CollectClips"/> が候補をどこから引いたか。</summary>
    public enum FootstepClipSource
    {
        /// <summary>どこにも音が無い (鳴らさない)。</summary>
        None = 0,

        /// <summary>足元の材質の組。</summary>
        Surface = 1,

        /// <summary><see cref="SurfaceMaterial.DefaultId"/> の組。</summary>
        Default = 2,

        /// <summary><see cref="PresentationCatalog"/> の足音・着地音。</summary>
        PresentationCatalog = 3,
    }

    /// <summary>材質 1 つの足音と着地音。<see cref="FootstepSurfaceCatalog"/> の要素。</summary>
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

        /// <summary>materialId は <see cref="SurfaceMaterial.Normalize"/> で揃えて持つ。null の列は空にし、渡した列は複製して持つ。</summary>
        public FootstepSurfaceSounds(string materialId, IEnumerable<AudioClip> footsteps, IEnumerable<AudioClip> landings)
        {
            _materialId = SurfaceMaterial.Normalize(materialId);
            _footsteps = footsteps == null ? new List<AudioClip>() : new List<AudioClip>(footsteps);
            _landings = landings == null ? new List<AudioClip>() : new List<AudioClip>(landings);
        }

        /// <summary>材質の Id。Inspector で空にしても <see cref="SurfaceMaterial.DefaultId"/> になる。</summary>
        public string MaterialId => SurfaceMaterial.Normalize(_materialId);

        public IReadOnlyList<AudioClip> Footsteps => _footsteps ??= new List<AudioClip>();

        public IReadOnlyList<AudioClip> Landings => _landings ??= new List<AudioClip>();

        /// <summary>kind の音の列。着地なら <see cref="Landings"/>、それ以外は <see cref="Footsteps"/>。</summary>
        public IReadOnlyList<AudioClip> ClipsFor(FootstepCueKind kind)
        {
            return kind == FootstepCueKind.Landing ? Landings : Footsteps;
        }
    }
}
