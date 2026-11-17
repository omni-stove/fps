using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Runtime
{
    // 素材の枠を空でも続けるのは、空の枠の演出だけを出さず、戦闘や他の演出を止めないため。
    [CreateAssetMenu(fileName = "PresentationCatalog", menuName = "Shooter/Presentation Catalog")]
    public sealed class PresentationCatalog : ScriptableObject
    {
        public const string ResourceFolder = "Presentation";

        public const string CatalogSuffix = "Catalog";

        public const string ResourcePath = ResourceFolder + "/PresentationCatalog";

        public static string CatalogResourcePath(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("catalog name must not be empty", nameof(name));
            }

            foreach (var c in name)
            {
                if (char.IsWhiteSpace(c) || c == '/' || c == '\\' || c == '.')
                {
                    throw new ArgumentException($"catalog name '{name}' must not contain whitespace, '/', '\\' or '.'", nameof(name));
                }
            }

            if (name.EndsWith(CatalogSuffix, StringComparison.Ordinal))
            {
                throw new ArgumentException($"catalog name '{name}' must not end with '{CatalogSuffix}'; it is appended", nameof(name));
            }

            return ResourceFolder + "/" + name + CatalogSuffix;
        }

        [SerializeField, Tooltip("武器ごとの音とエフェクト。Id は重複させない")]
        private List<WeaponPresentationAssets> _weapons = new List<WeaponPresentationAssets>();

        [SerializeField, Tooltip("弾が当たったときの音")]
        private AudioClip _impactSound;

        [SerializeField, Tooltip("弾が当たったところに出す火花。+Z を面の法線に向けて置く")]
        private GameObject _impactSpark;

        [SerializeField, Tooltip("曳光弾。+Z を弾の向きに向けて銃口に置く")]
        private GameObject _tracer;

        [SerializeField, Tooltip("足音")]
        private AudioClip _footstep;

        [SerializeField, Tooltip("着地の音")]
        private AudioClip _landing;

        [SerializeField, Tooltip("被弾したときの声")]
        private AudioClip _hurtVoice;

        [SerializeField, Tooltip("爆発の音")]
        private AudioClip _explosionSound;

        [SerializeField, Tooltip("爆発のエフェクト")]
        private GameObject _explosion;

        [SerializeField, Tooltip("生きている相手に当てたときの音。空なら鳴らさない")]
        private AudioClip _hitMarkerSound;

        [SerializeField, Tooltip("倒したときの音。空なら当てたときの音を使う")]
        private AudioClip _killMarkerSound;

        [SerializeField, Range(0f, 1f), Tooltip("ヒットマーカーの音量")]
        private float _hitMarkerVolume = 0.4f;

        [SerializeField, Min(1), Tooltip("1 人が同じフレームに鳴らす着弾の音の数の上限。散弾のペレットが一度に当たっても重ならない")]
        private int _maxImpactSoundsPerFrame = DefaultMaxImpactSoundsPerFrame;

        public const int DefaultMaxImpactSoundsPerFrame = 2;

        public IReadOnlyList<WeaponPresentationAssets> Weapons => _weapons ??= new List<WeaponPresentationAssets>();

        public int MaxImpactSoundsPerFrame
        {
            get => _maxImpactSoundsPerFrame;
            set => _maxImpactSoundsPerFrame = value;
        }

        public AudioClip ImpactSound
        {
            get => _impactSound;
            set => _impactSound = value;
        }

        public GameObject ImpactSpark
        {
            get => _impactSpark;
            set => _impactSpark = value;
        }

        public GameObject Tracer
        {
            get => _tracer;
            set => _tracer = value;
        }

        public AudioClip Footstep
        {
            get => _footstep;
            set => _footstep = value;
        }

        public AudioClip Landing
        {
            get => _landing;
            set => _landing = value;
        }

        public AudioClip HurtVoice
        {
            get => _hurtVoice;
            set => _hurtVoice = value;
        }

        public AudioClip ExplosionSound
        {
            get => _explosionSound;
            set => _explosionSound = value;
        }

        public GameObject Explosion
        {
            get => _explosion;
            set => _explosion = value;
        }

        public AudioClip HitMarkerSound
        {
            get => _hitMarkerSound;
            set => _hitMarkerSound = value;
        }

        public AudioClip KillMarkerSound
        {
            get => _killMarkerSound;
            set => _killMarkerSound = value;
        }

        public float HitMarkerVolume
        {
            get => _hitMarkerVolume;
            set => _hitMarkerVolume = Mathf.Clamp01(value);
        }

        public AudioClip HitMarkerSoundFor(bool killed)
        {
            return killed && _killMarkerSound != null ? _killMarkerSound : _hitMarkerSound;
        }

        public static PresentationCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.Presentation);
            return fromContent != null ? fromContent : Resources.Load<PresentationCatalog>(ResourcePath);
        }

        public void SetWeapons(IEnumerable<WeaponPresentationAssets> weapons)
        {
            _weapons = weapons == null ? new List<WeaponPresentationAssets>() : new List<WeaponPresentationAssets>(weapons);
        }

        public bool TryGetWeapon(string weaponId, out WeaponPresentationAssets assets)
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

        public void Validate()
        {
            var source = string.IsNullOrEmpty(name) ? "(unnamed presentation catalog)" : name;
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

                RequireReference(problems, entry.Fire, $"{label} fire sound");
                RequireReference(problems, entry.Reload, $"{label} reload sound");
                RequireReference(problems, entry.MuzzleFlash, $"{label} muzzle flash");
            }

            RequireReference(problems, _impactSound, "impact sound");
            RequireReference(problems, _impactSpark, "impact spark");
            RequireReference(problems, _tracer, "tracer");
            RequireReference(problems, _footstep, "footstep");
            RequireReference(problems, _landing, "landing");
            RequireReference(problems, _hurtVoice, "hurt voice");
            RequireReference(problems, _explosionSound, "explosion sound");
            RequireReference(problems, _explosion, "explosion");

            if (problems.Count > 0)
            {
                throw new PresentationCatalogException($"presentation catalog '{source}': {string.Join("; ", problems)}");
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
    public sealed class WeaponPresentationAssets
    {
        [SerializeField, Tooltip("武器の Id (WeaponDefinition の Id と同じ値)")]
        private string _weaponId = string.Empty;

        [SerializeField, Tooltip("撃ったときの音")]
        private AudioClip _fire;

        [SerializeField, Tooltip("リロードの音。リロードの始めから鳴らす")]
        private AudioClip _reload;

        [SerializeField, Tooltip("マズルフラッシュ。+Z を銃口の向きに向けて置く")]
        private GameObject _muzzleFlash;

        [SerializeField, Tooltip("他人 (敵・相手のプレイヤー) が撃ったのを見るときのマズルフラッシュ。+Z を銃口の向きに向けて置く。空なら自分と同じマズルフラッシュ")]
        private GameObject _thirdPersonMuzzleFlash;

        public WeaponPresentationAssets()
        {
        }

        // weaponId の前後の空白を除くのは、Shooter.Core.WeaponSpec.Id と同じ形にそろえるため。
        public WeaponPresentationAssets(
            string weaponId, AudioClip fire, AudioClip reload, GameObject muzzleFlash, GameObject thirdPersonMuzzleFlash = null)
        {
            _weaponId = weaponId?.Trim() ?? string.Empty;
            _fire = fire;
            _reload = reload;
            _muzzleFlash = muzzleFlash;
            _thirdPersonMuzzleFlash = thirdPersonMuzzleFlash;
        }

        public string WeaponId => _weaponId ?? string.Empty;

        public AudioClip Fire => _fire;

        public AudioClip Reload => _reload;

        public GameObject MuzzleFlash => _muzzleFlash;

        public GameObject ThirdPersonMuzzleFlash => _thirdPersonMuzzleFlash;

        public GameObject MuzzleFlashFor(bool firstPerson)
        {
            return firstPerson || _thirdPersonMuzzleFlash == null ? _muzzleFlash : _thirdPersonMuzzleFlash;
        }
    }

    public sealed class PresentationCatalogException : Exception
    {
        public PresentationCatalogException(string message)
            : base(message)
        {
        }
    }
}
