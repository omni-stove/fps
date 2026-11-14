using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 音とエフェクトの参照をまとめて持つ。武器ごとのもの (銃声・リロード音・マズルフラッシュ) は武器の Id
    /// (<see cref="Fps.Core.WeaponSpec.Id"/>) で <see cref="TryGetWeapon"/> から引き、どの武器にも共通のもの
    /// (着弾・曳光弾・足音・着地・被弾の声・爆発・ヒットマーカーの音) はプロパティで引く。
    /// ヒットマーカーは Resources の <see cref="ResourcePath"/> に置いたものを <see cref="Load"/> で読み、
    /// 置いていなければ音を鳴らさずに見た目だけ出す。
    /// 武器の演出 (<see cref="WeaponPresentation"/>) も同じく <see cref="Load"/> で読む。
    /// 中身は Fps.Editor.PresentationAssetBuilder が生成した仮の素材 (Assets/game/Presentation/Resources/Presentation/PresentationCatalog.asset) で、
    /// 本物の素材に替えるときはこのアセットの参照を差し替える。
    /// </summary>
    /// <remarks>
    /// 演出を足すときの規約 (このカタログもこれに従う):
    /// <list type="bullet">
    /// <item>カタログの置き場所: 演出ごとに自分の ScriptableObject のカタログを 1 つ持ち、どこかの Resources フォルダの
    /// <c>Presentation/&lt;名前&gt;Catalog.asset</c> に置く (Resources からのパスは <see cref="CatalogResourcePath"/>)。
    /// このカタログに枠を足さず、演出ごとにファイルを分ける。</item>
    /// <item>仮素材の生成: 仮素材とカタログは Fps.Editor.IPresentationAssetStep の実装が作り、Fps.Editor.PresentationAssetSteps.BuildAll から
    /// まとめて実行される。既存のアセットは GUID を保って中身だけ更新する。</item>
    /// <item>任意の枠: 素材の枠は空でもよい。空の枠の演出だけを出さずに続行し、戦闘や他の演出は止めない
    /// (カタログ自体が無いときも同じで、警告は 1 度だけ)。必須にするのは仮素材が必ず埋める枠だけで、Validate で確かめる。</item>
    /// </list>
    /// </remarks>
    [CreateAssetMenu(fileName = "PresentationCatalog", menuName = "Fps/Presentation Catalog")]
    public sealed class PresentationCatalog : ScriptableObject
    {
        /// <summary>演出のカタログを置く、Resources からのフォルダ。</summary>
        public const string ResourceFolder = "Presentation";

        /// <summary>演出のカタログのファイル名の後ろに付ける語。</summary>
        public const string CatalogSuffix = "Catalog";

        /// <summary>Resources からのパス (どこかの Resources フォルダの Presentation/PresentationCatalog.asset)。<see cref="CatalogResourcePath"/> の "Presentation"。</summary>
        public const string ResourcePath = ResourceFolder + "/PresentationCatalog";

        /// <summary>
        /// 名前が name の演出のカタログの、Resources からのパス (<c>Presentation/&lt;name&gt;Catalog</c>、拡張子なし)。
        /// <see cref="Resources.Load(string)"/> にそのまま渡せる。
        /// </summary>
        /// <exception cref="ArgumentException">name が空、空白を含む、パスの区切りを含む、または <see cref="CatalogSuffix"/> で終わる。</exception>
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

        /// <summary>定義順の武器ごとの音とエフェクト。中身を書き換えるときは <see cref="SetWeapons"/> を使う。</summary>
        public IReadOnlyList<WeaponPresentationAssets> Weapons => _weapons ??= new List<WeaponPresentationAssets>();

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

        /// <summary>当てたときの音。無ければ null。</summary>
        public AudioClip HitMarkerSound
        {
            get => _hitMarkerSound;
            set => _hitMarkerSound = value;
        }

        /// <summary>倒したときの音。無ければ null。</summary>
        public AudioClip KillMarkerSound
        {
            get => _killMarkerSound;
            set => _killMarkerSound = value;
        }

        /// <summary>ヒットマーカーの音量 ([0, 1])。</summary>
        public float HitMarkerVolume
        {
            get => _hitMarkerVolume;
            set => _hitMarkerVolume = Mathf.Clamp01(value);
        }

        /// <summary>killed かどうかで鳴らす音。倒したときの音が無ければ当てたときの音。どちらも無ければ null。</summary>
        public AudioClip HitMarkerSoundFor(bool killed)
        {
            return killed && _killMarkerSound != null ? _killMarkerSound : _hitMarkerSound;
        }

        /// <summary>入口 (<see cref="GameContent"/>) のもの、無ければ Resources の <see cref="ResourcePath"/> から読む。どちらも無ければ null。</summary>
        public static PresentationCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.Presentation);
            return fromContent != null ? fromContent : Resources.Load<PresentationCatalog>(ResourcePath);
        }

        /// <summary>武器ごとの音とエフェクトをまとめて差し替える。null は空にする。渡した列は複製して持つ。</summary>
        public void SetWeapons(IEnumerable<WeaponPresentationAssets> weapons)
        {
            _weapons = weapons == null ? new List<WeaponPresentationAssets>() : new List<WeaponPresentationAssets>(weapons);
        }

        /// <summary>
        /// Id が weaponId の武器の音とエフェクトを探す。Id は大文字小文字を区別して比べる。
        /// 無い・weaponId が null なら false と null を返す (演出が無い武器は鳴らさないだけで、撃つ処理は止めない)。
        /// </summary>
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

        /// <summary>
        /// 武器が 1 つ以上あり、どの武器も Id が空でなく重複せず、すべての参照が埋まっていることを確かめる。
        /// 足りないものはすべて並べて 1 つの例外にする。
        /// </summary>
        /// <exception cref="PresentationCatalogException">どれかが欠けている・重複しているとき。</exception>
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

    /// <summary>武器 1 種類の音とエフェクト。<see cref="PresentationCatalog"/> の要素。</summary>
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

        public WeaponPresentationAssets()
        {
        }

        /// <summary>weaponId は前後の空白を除いて持つ (<see cref="Fps.Core.WeaponSpec.Id"/> と同じ形にそろえる)。</summary>
        public WeaponPresentationAssets(string weaponId, AudioClip fire, AudioClip reload, GameObject muzzleFlash)
        {
            _weaponId = weaponId?.Trim() ?? string.Empty;
            _fire = fire;
            _reload = reload;
            _muzzleFlash = muzzleFlash;
        }

        public string WeaponId => _weaponId ?? string.Empty;

        public AudioClip Fire => _fire;

        public AudioClip Reload => _reload;

        public GameObject MuzzleFlash => _muzzleFlash;
    }

    /// <summary><see cref="PresentationCatalog"/> の中身が足りない・重複しているときの例外。</summary>
    public sealed class PresentationCatalogException : Exception
    {
        public PresentationCatalogException(string message)
            : base(message)
        {
        }
    }
}
