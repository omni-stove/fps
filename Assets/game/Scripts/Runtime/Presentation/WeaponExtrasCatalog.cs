using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 武器ごとの薬莢と空撃ちの素材 (薬莢のプレハブ・薬莢の落下音・空撃ちの音) を武器の Id (<see cref="Fps.Core.WeaponSpec.Id"/>) で引く。
    /// <see cref="ShellEjector"/> と <see cref="DryFireSound"/> が <see cref="Load"/> で読む。
    /// 中身は Fps.Editor.WeaponExtrasAssetStep が生成した仮の素材で、本物の素材に替えるときはこのアセットの参照を差し替える。
    /// 置き場所と枠の扱いは <see cref="PresentationCatalog"/> の規約に従う (どの枠も空でよく、空の枠の演出だけを出さない)。
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponExtrasCatalog", menuName = "Fps/Weapon Extras Catalog")]
    public sealed class WeaponExtrasCatalog : ScriptableObject
    {
        /// <summary><see cref="PresentationCatalog.CatalogResourcePath"/> に渡す名前。</summary>
        public const string CatalogName = "WeaponExtras";

        /// <summary>Resources からのパス (どこかの Resources フォルダの Presentation/WeaponExtrasCatalog.asset)。</summary>
        public static readonly string ResourcePath = PresentationCatalog.CatalogResourcePath(CatalogName);

        private static bool _warnedMissing;

        [SerializeField, Tooltip("武器ごとの薬莢と空撃ちの素材。Id は重複させない")]
        private List<WeaponExtrasAssets> _weapons = new List<WeaponExtrasAssets>();

        /// <summary>定義順の武器ごとの素材。中身を書き換えるときは <see cref="SetWeapons"/> を使う。</summary>
        public IReadOnlyList<WeaponExtrasAssets> Weapons => _weapons ??= new List<WeaponExtrasAssets>();

        /// <summary>入口 (<see cref="GameContent"/>) のもの、無ければ Resources の <see cref="ResourcePath"/> から読む。どちらも無ければ null。</summary>
        public static WeaponExtrasCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.WeaponExtras);
            return fromContent != null ? fromContent : Resources.Load<WeaponExtrasCatalog>(ResourcePath);
        }

        /// <summary>
        /// カタログが無いことを 1 度だけ警告する。<see cref="ShellEjector"/> と <see cref="DryFireSound"/> で共有し、
        /// どちらが先に気づいても 1 回だけ出す。
        /// </summary>
        public static void WarnMissingOnce(UnityEngine.Object context)
        {
            if (_warnedMissing)
            {
                return;
            }

            _warnedMissing = true;
            Debug.LogWarning(
                $"{WeaponPresentation.LogTag} no WeaponExtrasCatalog at Resources/{ResourcePath}; shell casings and dry-fire sounds are off. "
                + "Run Fps/Build All Presentation Assets (Fps.Editor.PresentationAssetSteps.BuildAll)",
                context);
        }

        /// <summary>武器ごとの素材をまとめて差し替える。null は空にする。渡した列は複製して持つ。</summary>
        public void SetWeapons(IEnumerable<WeaponExtrasAssets> weapons)
        {
            _weapons = weapons == null ? new List<WeaponExtrasAssets>() : new List<WeaponExtrasAssets>(weapons);
        }

        /// <summary>
        /// Id が weaponId の武器の素材を探す。Id は大文字小文字を区別して比べる。
        /// 無い・weaponId が null なら false と null を返す。
        /// </summary>
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

        /// <summary>
        /// 武器が 1 つ以上あり、どの武器も Id が空でなく重複せず、仮素材が必ず埋める枠 (薬莢のプレハブ・落下音・空撃ちの音) が埋まっていることを確かめる。
        /// 足りないものはすべて並べて 1 つの例外にする。
        /// </summary>
        /// <exception cref="PresentationCatalogException">どれかが欠けている・重複しているとき。</exception>
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
                if (entry.ShellPrefab == null)
                {
                    problems.Add($"{label} shell prefab is missing");
                }

                if (entry.ShellLandSound == null)
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

    /// <summary>武器 1 種類の薬莢と空撃ちの素材。<see cref="WeaponExtrasCatalog"/> の要素。</summary>
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

        public WeaponExtrasAssets()
        {
        }

        /// <summary>weaponId は前後の空白を除いて持つ (<see cref="Fps.Core.WeaponSpec.Id"/> と同じ形にそろえる)。</summary>
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
    }
}
