using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "HitFeedbackCatalog", menuName = "Shooter/Hit Feedback Catalog")]
    public sealed class HitFeedbackCatalog : ScriptableObject
    {
        public const string CatalogName = "HitFeedback";

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

        public float WallDecalMaxDistance
        {
            get => Mathf.Max(0.01f, _wallDecalMaxDistance);
            set => _wallDecalMaxDistance = Mathf.Max(0.01f, value);
        }

        public float WallDecalLifetime
        {
            get => Mathf.Max(0.01f, _wallDecalLifetime);
            set => _wallDecalLifetime = Mathf.Max(0.01f, value);
        }

        public float WallDecalSize
        {
            get => Mathf.Max(0.01f, _wallDecalSize);
            set => _wallDecalSize = Mathf.Max(0.01f, value);
        }

        public static HitFeedbackCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.HitFeedback);
            return fromContent != null ? fromContent : Resources.Load<HitFeedbackCatalog>(ResourcePath);
        }

        public void SetSprays(IEnumerable<ZoneBloodSpray> sprays)
        {
            _sprays = sprays == null ? new List<ZoneBloodSpray>() : new List<ZoneBloodSpray>(sprays);
        }

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
