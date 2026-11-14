using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// キャラが撃たれたときの血の演出 (<see cref="BloodPresenter"/>) の素材。部位 (<see cref="HitZone"/>) ごとの血しぶきと、
    /// 撃たれたキャラの背後の壁に付ける血痕を持つ。
    /// Resources の <see cref="ResourcePath"/> に置いたものを <see cref="Load"/> で読む (<see cref="PresentationCatalog"/> の規約)。
    /// 中身は Fps.Editor.HitFeedbackAssetStep が生成した仮の素材で、本物の素材に替えるときはこのアセットの参照を差し替える。
    /// </summary>
    [CreateAssetMenu(fileName = "HitFeedbackCatalog", menuName = "Fps/Hit Feedback Catalog")]
    public sealed class HitFeedbackCatalog : ScriptableObject
    {
        /// <summary>カタログの名前 (<see cref="PresentationCatalog.CatalogResourcePath"/> に渡すもの)。</summary>
        public const string CatalogName = "HitFeedback";

        /// <summary>Resources からのパス (<c>Presentation/HitFeedbackCatalog</c>)。</summary>
        public static readonly string ResourcePath = PresentationCatalog.CatalogResourcePath(CatalogName);

        public const float DefaultWallDecalMaxDistance = 2f;
        public const float DefaultWallDecalLifetime = 20f;
        public const float DefaultWallDecalSize = 0.45f;

        [SerializeField, Tooltip("部位ごとの血しぶき。部位は重複させない。+Z を弾の進む向きに向けて当たった点に置く")]
        private List<ZoneBloodSpray> _sprays = new List<ZoneBloodSpray>();

        [SerializeField, Tooltip("部位が分からないか、部位の血しぶきが無いときの血しぶき。空なら出さない")]
        private GameObject _defaultSpray;

        [SerializeField, Tooltip("背後の壁に付ける血痕。+Z を壁の中へ向けて壁の面に置く (Quad の表が壁の外を向く)。空なら付けない")]
        private GameObject _wallDecal;

        [SerializeField, Min(0.01f), Tooltip("当たった点から弾の進む向きにこの距離 (m) までに壁があれば血痕を付ける")]
        private float _wallDecalMaxDistance = DefaultWallDecalMaxDistance;

        [SerializeField, Min(0.01f), Tooltip("血痕が消えるまでの秒数")]
        private float _wallDecalLifetime = DefaultWallDecalLifetime;

        [SerializeField, Min(0.01f), Tooltip("血痕の直径 (m)。プレハブの大きさに掛ける")]
        private float _wallDecalSize = DefaultWallDecalSize;

        /// <summary>定義順の部位ごとの血しぶき。中身を書き換えるときは <see cref="SetSprays"/> を使う。</summary>
        public IReadOnlyList<ZoneBloodSpray> Sprays => _sprays ??= new List<ZoneBloodSpray>();

        public GameObject DefaultSpray
        {
            get => _defaultSpray;
            set => _defaultSpray = value;
        }

        public GameObject WallDecal
        {
            get => _wallDecal;
            set => _wallDecal = value;
        }

        /// <summary>背後の壁を探す距離 (m)。0.01 より小さくしない。</summary>
        public float WallDecalMaxDistance
        {
            get => Mathf.Max(0.01f, _wallDecalMaxDistance);
            set => _wallDecalMaxDistance = Mathf.Max(0.01f, value);
        }

        /// <summary>血痕を残す秒数。0.01 より小さくしない。</summary>
        public float WallDecalLifetime
        {
            get => Mathf.Max(0.01f, _wallDecalLifetime);
            set => _wallDecalLifetime = Mathf.Max(0.01f, value);
        }

        /// <summary>血痕の直径 (m)。0.01 より小さくしない。</summary>
        public float WallDecalSize
        {
            get => Mathf.Max(0.01f, _wallDecalSize);
            set => _wallDecalSize = Mathf.Max(0.01f, value);
        }

        /// <summary>Resources の <see cref="ResourcePath"/> から読む。置いていなければ null。</summary>
        public static HitFeedbackCatalog Load()
        {
            return Resources.Load<HitFeedbackCatalog>(ResourcePath);
        }

        /// <summary>部位ごとの血しぶきをまとめて差し替える。null は空にする。渡した列は複製して持つ。</summary>
        public void SetSprays(IEnumerable<ZoneBloodSpray> sprays)
        {
            _sprays = sprays == null ? new List<ZoneBloodSpray>() : new List<ZoneBloodSpray>(sprays);
        }

        /// <summary>
        /// zone の部位に出す血しぶき。部位の血しぶきが無い (枠が空を含む) か zone が null なら <see cref="DefaultSpray"/>。どちらも無ければ null。
        /// </summary>
        public GameObject SprayFor(HitZone? zone)
        {
            if (zone.HasValue)
            {
                foreach (var entry in Sprays)
                {
                    if (entry != null && entry.Zone == zone.Value && entry.Effect != null)
                    {
                        return entry.Effect;
                    }
                }
            }

            return _defaultSpray != null ? _defaultSpray : null;
        }

        /// <summary>
        /// すべての部位 (<see cref="HitZone"/>) に血しぶきがあり、部位が重複せず、既定の血しぶきと血痕が埋まっていることを確かめる。
        /// 仮素材はすべてを埋めるので、生成した直後に通す。足りないものはすべて並べて 1 つの例外にする。
        /// </summary>
        /// <exception cref="PresentationCatalogException">どれかが欠けている・重複しているとき。</exception>
        public void Validate()
        {
            var source = string.IsNullOrEmpty(name) ? "(unnamed hit feedback catalog)" : name;
            var problems = new List<string>();
            var seen = new HashSet<HitZone>();

            for (var i = 0; i < Sprays.Count; i++)
            {
                var entry = Sprays[i];
                if (entry == null)
                {
                    problems.Add($"spray #{i} is empty");
                    continue;
                }

                if (!Enum.IsDefined(typeof(HitZone), entry.Zone))
                {
                    problems.Add($"spray #{i} has an unknown zone {(int)entry.Zone}");
                    continue;
                }

                if (!seen.Add(entry.Zone))
                {
                    problems.Add($"spray #{i} uses the zone {entry.Zone} already used by another entry");
                }

                if (entry.Effect == null)
                {
                    problems.Add($"spray #{i} ({entry.Zone}) effect is missing");
                }
            }

            foreach (HitZone zone in Enum.GetValues(typeof(HitZone)))
            {
                if (!seen.Contains(zone))
                {
                    problems.Add($"zone {zone} has no spray");
                }
            }

            if (_defaultSpray == null)
            {
                problems.Add("default spray is missing");
            }

            if (_wallDecal == null)
            {
                problems.Add("wall decal is missing");
            }

            if (problems.Count > 0)
            {
                throw new PresentationCatalogException($"hit feedback catalog '{source}': {string.Join("; ", problems)}");
            }
        }
    }

    /// <summary>部位 1 つの血しぶき。<see cref="HitFeedbackCatalog"/> の要素。</summary>
    [Serializable]
    public sealed class ZoneBloodSpray
    {
        [SerializeField, Tooltip("当たった部位")]
        private HitZone _zone;

        [SerializeField, Tooltip("血しぶき。+Z を弾の進む向きに向けて当たった点に置く")]
        private GameObject _effect;

        public ZoneBloodSpray()
        {
        }

        public ZoneBloodSpray(HitZone zone, GameObject effect)
        {
            _zone = zone;
            _effect = effect;
        }

        public HitZone Zone => _zone;

        public GameObject Effect => _effect;
    }
}
