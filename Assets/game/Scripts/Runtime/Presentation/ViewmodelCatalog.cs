using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    [CreateAssetMenu(fileName = "ViewmodelCatalog", menuName = "Fps/Viewmodel Catalog")]
    public sealed class ViewmodelCatalog : ScriptableObject
    {
        public const string CatalogName = "Viewmodel";

        public const string ResourcePath = PresentationCatalog.ResourceFolder + "/" + CatalogName + PresentationCatalog.CatalogSuffix;

        [SerializeField, Tooltip("武器ごとのビューモデル。Id は重複させない")]
        private List<ViewmodelEntry> _weapons = new List<ViewmodelEntry>();

        public IReadOnlyList<ViewmodelEntry> Weapons => _weapons ??= new List<ViewmodelEntry>();

        public static ViewmodelCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.Viewmodels);
            return fromContent != null ? fromContent : Resources.Load<ViewmodelCatalog>(ResourcePath);
        }

        public void SetWeapons(IEnumerable<ViewmodelEntry> weapons)
        {
            _weapons = weapons == null ? new List<ViewmodelEntry>() : new List<ViewmodelEntry>(weapons);
        }

        public bool TryGetWeapon(string weaponId, out ViewmodelEntry entry)
        {
            entry = null;
            if (weaponId == null)
            {
                return false;
            }

            foreach (var candidate in Weapons)
            {
                if (candidate != null && string.Equals(candidate.WeaponId, weaponId, StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }

        public void Validate()
        {
            var source = string.IsNullOrEmpty(name) ? "(unnamed viewmodel catalog)" : name;
            var problems = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < Weapons.Count; i++)
            {
                var entry = Weapons[i];
                if (entry == null)
                {
                    problems.Add($"weapon #{i} is empty");
                    continue;
                }

                var label = $"weapon #{i} ('{entry.WeaponId}')";
                if (string.IsNullOrEmpty(entry.WeaponId))
                {
                    problems.Add($"weapon #{i} has no weapon id");
                }
                else if (!seen.Add(entry.WeaponId))
                {
                    problems.Add($"{label} uses a weapon id already used by another entry");
                }

                if (entry.Prefab == null)
                {
                    problems.Add($"{label} prefab is missing");
                }

                if (!entry.TryGetSwaySettings(out _, out var error))
                {
                    problems.Add($"{label} sway: {error}");
                }
            }

            if (problems.Count > 0)
            {
                throw new PresentationCatalogException($"viewmodel catalog '{source}': {string.Join("; ", problems)}");
            }
        }
    }

    [Serializable]
    public sealed class ViewmodelEntry
    {
        [SerializeField, Tooltip("武器の Id (WeaponDefinition の Id と同じ値)")]
        private string _weaponId = string.Empty;

        [SerializeField, Tooltip("手と銃のモデル。+Z を銃口の向きにし、銃口に Socket_Muzzle を置く")]
        private GameObject _prefab;

        [SerializeField, Tooltip("カメラから見たモデルの根の位置 (m)。右・上・前")]
        private Vector3 _offset = ViewmodelEntry.DefaultOffset;

        [SerializeField, Tooltip("歩行の揺れ・視点への追従遅れ・発砲時の跳ね上がりの強さ")]
        private ViewmodelSwayParameters _sway = new ViewmodelSwayParameters();

        public static readonly Vector3 DefaultOffset = new Vector3(0.14f, -0.14f, 0.2f);

        public ViewmodelEntry()
        {
        }

        public ViewmodelEntry(string weaponId, GameObject prefab, Vector3 offset, ViewmodelSwayParameters sway)
        {
            _weaponId = weaponId?.Trim() ?? string.Empty;
            _prefab = prefab;
            _offset = offset;
            _sway = sway ?? new ViewmodelSwayParameters();
        }

        public string WeaponId => _weaponId ?? string.Empty;

        public GameObject Prefab => _prefab;

        public Vector3 Offset => _offset;

        public ViewmodelSwayParameters Sway => _sway ??= new ViewmodelSwayParameters();

        public bool TryGetSwaySettings(out ViewmodelSwaySettings settings, out string error)
        {
            try
            {
                settings = Sway.ToSettings();
                error = null;
                return true;
            }
            catch (ArgumentOutOfRangeException e)
            {
                settings = null;
                error = e.Message;
                return false;
            }
        }
    }

    // Core の ViewmodelSwaySettings は読み取り専用で Unity が書き出せないので、同じ値をここで持って変換する。
    [Serializable]
    public sealed class ViewmodelSwayParameters
    {
        [SerializeField, Min(0f), Tooltip("歩行の揺れの左右の幅 (m)")]
        private float _bobHorizontalMeters = ViewmodelSwaySettings.DefaultBobHorizontalMeters;

        [SerializeField, Min(0f), Tooltip("歩行の揺れの上下の幅 (m)")]
        private float _bobVerticalMeters = ViewmodelSwaySettings.DefaultBobVerticalMeters;

        [SerializeField, Min(0f), Tooltip("1 m 進むあいだに左右に往復する回数")]
        private float _bobCyclesPerMeter = ViewmodelSwaySettings.DefaultBobCyclesPerMeter;

        [SerializeField, Min(0.01f), Tooltip("揺れの幅が最大になる水平の速さ (m/s)")]
        private float _bobReferenceSpeed = ViewmodelSwaySettings.DefaultBobReferenceSpeed;

        [SerializeField, Min(0.01f), Tooltip("動き出し・止まり際に揺れの幅が追いつく鋭さ (1/秒)")]
        private float _bobSharpness = ViewmodelSwaySettings.DefaultBobSharpness;

        [SerializeField, Min(0f), Tooltip("視点の回転の速さ (度/秒) に掛けて遅れの角度にする秒数")]
        private float _lookLagSeconds = ViewmodelSwaySettings.DefaultLookLagSeconds;

        [SerializeField, Min(0f), Tooltip("視点への追従遅れの角度の上限 (度)")]
        private float _maxLookLagDegrees = ViewmodelSwaySettings.DefaultMaxLookLagDegrees;

        [SerializeField, Min(0.01f), Tooltip("遅れが目標に追いつく鋭さ (1/秒)")]
        private float _lookLagSharpness = ViewmodelSwaySettings.DefaultLookLagSharpness;

        [SerializeField, Min(0f), Tooltip("1 発ごとに手前へ下がる距離 (m)")]
        private float _kickBackMeters = ViewmodelSwaySettings.DefaultKickBackMeters;

        [SerializeField, Min(0f), Tooltip("1 発ごとに銃口が上がる角度 (度)")]
        private float _kickPitchDegrees = ViewmodelSwaySettings.DefaultKickPitchDegrees;

        [SerializeField, Min(0.01f), Tooltip("跳ね上がりが戻る速さ (1/秒)")]
        private float _kickReturnPerSecond = ViewmodelSwaySettings.DefaultKickReturnPerSecond;

        [SerializeField, Min(0f), Tooltip("跳ね上がりが重なる上限 (発数)")]
        private float _maxKicks = ViewmodelSwaySettings.DefaultMaxKicks;

        [SerializeField, Range(0f, 1f), Tooltip("覗き込み切ったときに歩行の揺れと追従遅れに掛ける倍率")]
        private float _adsSwayScale = ViewmodelSwaySettings.DefaultAdsSwayScale;

        [SerializeField, Range(0f, 1f), Tooltip("覗き込み切ったときに跳ね上がりに掛ける倍率")]
        private float _adsKickScale = ViewmodelSwaySettings.DefaultAdsKickScale;

        public ViewmodelSwayParameters()
        {
        }

        public ViewmodelSwayParameters(ViewmodelSwaySettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _bobHorizontalMeters = settings.BobHorizontalMeters;
            _bobVerticalMeters = settings.BobVerticalMeters;
            _bobCyclesPerMeter = settings.BobCyclesPerMeter;
            _bobReferenceSpeed = settings.BobReferenceSpeed;
            _bobSharpness = settings.BobSharpness;
            _lookLagSeconds = settings.LookLagSeconds;
            _maxLookLagDegrees = settings.MaxLookLagDegrees;
            _lookLagSharpness = settings.LookLagSharpness;
            _kickBackMeters = settings.KickBackMeters;
            _kickPitchDegrees = settings.KickPitchDegrees;
            _kickReturnPerSecond = settings.KickReturnPerSecond;
            _maxKicks = settings.MaxKicks;
            _adsSwayScale = settings.AdsSwayScale;
            _adsKickScale = settings.AdsKickScale;
        }

        public ViewmodelSwaySettings ToSettings()
        {
            return new ViewmodelSwaySettings(
                _bobHorizontalMeters,
                _bobVerticalMeters,
                _bobCyclesPerMeter,
                _bobReferenceSpeed,
                _bobSharpness,
                _lookLagSeconds,
                _maxLookLagDegrees,
                _lookLagSharpness,
                _kickBackMeters,
                _kickPitchDegrees,
                _kickReturnPerSecond,
                _maxKicks,
                _adsSwayScale,
                _adsKickScale);
        }
    }
}
