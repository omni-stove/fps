using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "WeaponExtrasCatalog", menuName = "Shooter/Weapon Extras Catalog")]
    public sealed class WeaponExtrasCatalog : ScriptableObject
    {
        public const string CatalogName = "WeaponExtras";

        public static readonly string ResourcePath = PresentationCatalog.CatalogResourcePath(CatalogName);

        private static bool _warnedMissing;

        [SerializeField, Tooltip("武器ごとの薬莢と空撃ちの素材。Id は重複させない")]
        private List<WeaponExtrasAssets> _weapons = new List<WeaponExtrasAssets>();

        public IReadOnlyList<WeaponExtrasAssets> Weapons => _weapons ??= new List<WeaponExtrasAssets>();

        public static WeaponExtrasCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.WeaponExtras);
            return fromContent != null ? fromContent : Resources.Load<WeaponExtrasCatalog>(ResourcePath);
        }

        // ShellEjector と DryFireSound のどちらが先にカタログが無いことに気づいても警告を 1 回だけ出すよう、ここで共有する。
        public static void WarnMissingOnce(UnityEngine.Object context)
        {
            if (_warnedMissing)
            {
                return;
            }

            _warnedMissing = true;
            Debug.LogWarning(
                $"{WeaponPresentation.LogTag} no WeaponExtrasCatalog at Resources/{ResourcePath}; shell casings and dry-fire sounds are off. "
                + "Run Shooter/Build All Presentation Assets (Shooter.Editor.PresentationAssetSteps.BuildAll)",
                context);
        }

        public void SetWeapons(IEnumerable<WeaponExtrasAssets> weapons)
        {
            _weapons = weapons == null ? new List<WeaponExtrasAssets>() : new List<WeaponExtrasAssets>(weapons);
        }

        public bool TryGetWeapon(string weaponId, out WeaponExtrasAssets assets)
        {
            assets = null;
            if (weaponId == null)
            {
                return false;
            }

            foreach (var entry in Weapons)
            {
                if (entry != null && string.Equals(entry.WeaponId, weaponId, StringComparison.Ordinal))
                {
                    assets = entry;
                    return true;
                }
            }

            return false;
        }

        // 光線や噴射のように薬莢を出さない武器があるので、薬莢のプレハブは空でも通す。
        public void Validate()
        {
            var source = string.IsNullOrEmpty(name) ? "(unnamed weapon extras catalog)" : name;
            var problems = new List<string>();

            if (Weapons.Count == 0)
            {
                problems.Add("has no weapons");
            }

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

                // 破棄されたアセットや参照切れも拾うため、Unity の == null で比べる。
                if (entry.ShellPrefab != null && entry.ShellLandSound == null)
                {
                    problems.Add($"{label} shell landing sound is missing");
                }

                if (entry.DryFireSound == null)
                {
                    problems.Add($"{label} dry-fire sound is missing");
                }
            }

            if (problems.Count > 0)
            {
                throw new PresentationCatalogException($"weapon extras catalog '{source}': {string.Join("; ", problems)}");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _warnedMissing = false;
        }
    }

    [Serializable]
    public sealed class WeaponExtrasAssets
    {
        [SerializeField, Tooltip("武器の Id (WeaponDefinition の Id と同じ値)")]
        private string _weaponId = string.Empty;

        [SerializeField, Tooltip("排莢口から飛ばす薬莢。Rigidbody と Collider が無ければ実行時に足す。空なら薬莢を出さない")]
        private GameObject _shellPrefab;

        [SerializeField, Tooltip("薬莢が最初に何かに当たったときの音。空なら鳴らさない")]
        private AudioClip _shellLandSound;

        [SerializeField, Tooltip("弾切れでトリガーを引いたときの音。空なら鳴らさない")]
        private AudioClip _dryFireSound;

        [SerializeField, Tooltip("地面に置いた (拾える) ときの銃の見た目。原点が見た目の中心で、前が +Z (銃口)。当たり判定は外す。空なら仮の銃の形を組む")]
        private GameObject _pickupModel;

        public WeaponExtrasAssets()
        {
        }

        // weaponId の前後の空白を除くのは、Shooter.Core.WeaponSpec.Id と同じ形にそろえるため。
        public WeaponExtrasAssets(string weaponId, GameObject shellPrefab, AudioClip shellLandSound, AudioClip dryFireSound)
        {
            _weaponId = weaponId?.Trim() ?? string.Empty;
            _shellPrefab = shellPrefab;
            _shellLandSound = shellLandSound;
            _dryFireSound = dryFireSound;
        }

        public string WeaponId => _weaponId ?? string.Empty;

        public GameObject ShellPrefab => _shellPrefab;

        public AudioClip ShellLandSound => _shellLandSound;

        public AudioClip DryFireSound => _dryFireSound;

        public GameObject PickupModel
        {
            get => _pickupModel;
            set => _pickupModel = value;
        }
    }
}
