using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace Fps.Runtime
{
    /// <summary>
    /// 武器 1 種類の値をアセットとして持つ。クライアントと専用サーバーは同じアセットを読み、<see cref="ToSpec"/> で
    /// 不変の <see cref="WeaponSpec"/> に変換して使う。検証は <see cref="WeaponSpec"/> と <see cref="DamageFalloff"/> に任せ、
    /// 失敗したらどのアセットの値かを添えて <see cref="WeaponDefinitionException"/> を投げる。
    /// 新しく作ったときの値は rifle 相当 (AssaultRifle、Auto、30/90 発、0.1 秒、リロード 2 秒、25 ダメージ、射程 100m、
    /// 20〜60m で 0.5 倍まで減衰)。ただし ADS の視野角は未設定 (共通の初期値) で、ブラインドファイアの散らばりは 0°、
    /// アクティブリロードの値は <see cref="WeaponSpec"/> の既定で始まり、反動と弾の散らばりはすべて 0 (反動なし・散らばらない)。Id はエディターで作ったとき (<see cref="Reset"/>) と値を検証するとき (<see cref="OnValidate"/>) に、
    /// 空なら新しい UUID を入れる。
    /// 実際に使う値はアセット (Assets/game/Weapons/*.asset、Fps.Editor.WeaponAssetBuilder が作る) の側にある。
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon", menuName = "Fps/Weapon Definition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("武器の識別子。小文字・ハイフン区切りの UUID (例: 3f2b8c1e-0a4d-4c6e-9b1a-7d2e5f8c0b13)。空なら自動で振る。作った後は変えない")]
        private string _id = string.Empty;

        // 表示名をそのまま持っていた頃のアセットも読めるようにする。その値はキーとして引かれ、表に無ければそのまま出る
        [SerializeField, FormerlySerializedAs("_displayName"), Tooltip("プレイヤーに見せる名前を引く表のキー (例: weapon.rifle)。文は Resources/Localization/<言語>/*.json に書く。空なら Id を出す")]
        private string _nameKey = string.Empty;

        [SerializeField, Tooltip("武器の種別。ロードアウトの制約と表示にだけ使う")]
        private WeaponCategory _category = WeaponCategory.AssaultRifle;

        [SerializeField, Tooltip("射撃モード")]
        private FireMode _fireMode = FireMode.Auto;

        [SerializeField, Min(1), Tooltip("弾倉の容量")]
        private int _magazineSize = 30;

        [SerializeField, Min(0), Tooltip("予備弾の初期値")]
        private int _reserveAmmo = 90;

        [SerializeField, Min(0f), Tooltip("射撃から次に撃てるまでの秒数")]
        private float _fireInterval = 0.1f;

        [SerializeField, Min(0f), Tooltip("リロード開始から補充までの秒数")]
        private float _reloadSeconds = 2f;

        [SerializeField, Min(0), Tooltip("1 発あたりの基本ダメージ")]
        private int _damage = 25;

        [SerializeField, Min(0.01f), Tooltip("射程 (m)")]
        private float _range = 100f;

        [Header("距離減衰")]
        [SerializeField, Min(0f), Tooltip("この距離までは減衰しない (m)")]
        private float _falloffStart = 20f;

        [SerializeField, Min(0f), Tooltip("この距離以上で最小倍率になる (m)")]
        private float _falloffEnd = 60f;

        [SerializeField, UnityEngine.Range(0f, 1f), Tooltip("最大まで減衰したときの倍率")]
        private float _falloffMinMultiplier = 0.5f;

        [Header("建造物")]
        [SerializeField, Min(0f), Tooltip("建造物に当たったときに被害を広げる半径 (m)。0 なら当たった物だけ")]
        private float _explosionRadius;

        [Header("覗き込み (ADS)")]
        [SerializeField, Min(0f), Tooltip("覗き込み切ったときのカメラの縦の視野角 (度)。0 なら共通の初期値 (Fps.Core.AdsZoom.DefaultFieldOfView)")]
        private float _adsFieldOfView = UnsetAdsFieldOfView;

        [Header("ブラインドファイア")]
        [SerializeField, UnityEngine.Range(0f, 90f), Tooltip("隠れたまま撃ったときに、狙った向きから弾が散らばる最大の角度 (度)")]
        private float _blindFireSpreadDegrees;

        [Header("アクティブリロード")]
        [SerializeField, UnityEngine.Range(0f, 0.99f), Tooltip("判定の幅が始まる時点の、リロード時間に対する割合")]
        private float _activeReloadWindowStart = WeaponSpec.DefaultActiveReloadWindowStart;

        [SerializeField, UnityEngine.Range(0.01f, 1f), Tooltip("判定の幅の長さの、リロード時間に対する割合。Start との和は 1 以下")]
        private float _activeReloadWindowLength = WeaponSpec.DefaultActiveReloadWindowLength;

        [SerializeField, Min(0f), Tooltip("判定の幅を外したときにリロードに足される秒数")]
        private float _activeReloadFailSeconds = WeaponSpec.DefaultActiveReloadFailSeconds;

        [Header("反動")]
        [SerializeField, UnityEngine.Range(0f, 90f), Tooltip("1 発ごとに視線が上へ跳ねる角度 (度)")]
        private float _recoilPitchDegrees;

        [SerializeField, UnityEngine.Range(0f, 90f), Tooltip("1 発ごとに視線が左右へぶれる最大の角度 (度)")]
        private float _recoilYawDegrees;

        [SerializeField, Min(0f), Tooltip("撃ち止めてから跳ねた視線が戻る速さ (度/秒)。0 なら戻らない")]
        private float _recoilRecoveryPerSecond;

        [Header("弾の散らばり")]
        [SerializeField, UnityEngine.Range(0f, 90f), Tooltip("腰だめで撃ったときに弾が散らばる最大の角度 (度)")]
        private float _hipSpreadDegrees;

        [SerializeField, UnityEngine.Range(0f, 90f), Tooltip("覗き込み切って撃ったときに弾が散らばる最大の角度 (度)")]
        private float _adsSpreadDegrees;

        [SerializeField, UnityEngine.Range(0f, 90f), Tooltip("動きながら撃ったときに散らばりへ足す最大の角度 (度)")]
        private float _moveSpreadDegrees;

        /// <summary>
        /// <see cref="AdsFieldOfView"/> が未設定であることを表す値。この項目が無かった頃に保存したアセットも、読み込むとこの値になる。
        /// </summary>
        public const float UnsetAdsFieldOfView = 0f;

        public string Id
        {
            get => _id ?? string.Empty;
            set => _id = value ?? string.Empty;
        }

        /// <summary>画面に出す名前を引く表のキー (<see cref="WeaponSpec.NameKey"/>)。null は空文字にする。</summary>
        public string NameKey
        {
            get => _nameKey ?? string.Empty;
            set => _nameKey = value ?? string.Empty;
        }

        public WeaponCategory Category
        {
            get => _category;
            set => _category = value;
        }

        public FireMode FireMode
        {
            get => _fireMode;
            set => _fireMode = value;
        }

        public int MagazineSize
        {
            get => _magazineSize;
            set => _magazineSize = value;
        }

        public int ReserveAmmo
        {
            get => _reserveAmmo;
            set => _reserveAmmo = value;
        }

        public float FireInterval
        {
            get => _fireInterval;
            set => _fireInterval = value;
        }

        public float ReloadSeconds
        {
            get => _reloadSeconds;
            set => _reloadSeconds = value;
        }

        public int Damage
        {
            get => _damage;
            set => _damage = value;
        }

        public float Range
        {
            get => _range;
            set => _range = value;
        }

        public float FalloffStart
        {
            get => _falloffStart;
            set => _falloffStart = value;
        }

        public float FalloffEnd
        {
            get => _falloffEnd;
            set => _falloffEnd = value;
        }

        public float FalloffMinMultiplier
        {
            get => _falloffMinMultiplier;
            set => _falloffMinMultiplier = value;
        }

        public float ExplosionRadius
        {
            get => _explosionRadius;
            set => _explosionRadius = value;
        }

        /// <summary>覗き込み切ったときの視野角 (度)。<see cref="UnsetAdsFieldOfView"/> なら <see cref="ToSpec"/> は共通の初期値を使う。</summary>
        public float AdsFieldOfView
        {
            get => _adsFieldOfView;
            set => _adsFieldOfView = value;
        }

        public float BlindFireSpreadDegrees
        {
            get => _blindFireSpreadDegrees;
            set => _blindFireSpreadDegrees = value;
        }

        public float ActiveReloadWindowStart
        {
            get => _activeReloadWindowStart;
            set => _activeReloadWindowStart = value;
        }

        public float ActiveReloadWindowLength
        {
            get => _activeReloadWindowLength;
            set => _activeReloadWindowLength = value;
        }

        public float ActiveReloadFailSeconds
        {
            get => _activeReloadFailSeconds;
            set => _activeReloadFailSeconds = value;
        }

        public float RecoilPitchDegrees
        {
            get => _recoilPitchDegrees;
            set => _recoilPitchDegrees = value;
        }

        public float RecoilYawDegrees
        {
            get => _recoilYawDegrees;
            set => _recoilYawDegrees = value;
        }

        public float RecoilRecoveryPerSecond
        {
            get => _recoilRecoveryPerSecond;
            set => _recoilRecoveryPerSecond = value;
        }

        public float HipSpreadDegrees
        {
            get => _hipSpreadDegrees;
            set => _hipSpreadDegrees = value;
        }

        public float AdsSpreadDegrees
        {
            get => _adsSpreadDegrees;
            set => _adsSpreadDegrees = value;
        }

        public float MoveSpreadDegrees
        {
            get => _moveSpreadDegrees;
            set => _moveSpreadDegrees = value;
        }

        /// <summary>Id が空 (空白のみを含む) なら新しい UUID を入れて true を返す。既に値があれば変えずに false。</summary>
        public bool AssignIdIfEmpty()
        {
            if (!string.IsNullOrWhiteSpace(_id))
            {
                return false;
            }

            _id = Guid.NewGuid().ToString("D");
            return true;
        }

        private void Reset()
        {
            AssignIdIfEmpty();
        }

        private void OnValidate()
        {
            var assigned = AssignIdIfEmpty();
#if UNITY_EDITOR
            // OnValidate での書き換えは dirty にならず、保存されないまま開くたび別の Id になるため
            if (assigned)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
#endif
        }

        /// <summary>
        /// 定義を不変の <see cref="WeaponSpec"/> に変換する。呼ぶたびに新しいインスタンスを返す。
        /// Id の前後の空白は <see cref="WeaponSpec"/> が取り除く。
        /// </summary>
        /// <exception cref="WeaponDefinitionException">値が不正なとき。メッセージにアセット名を含む。</exception>
        public WeaponSpec ToSpec()
        {
            var source = string.IsNullOrEmpty(name) ? "(unnamed weapon)" : name;
            try
            {
                var falloff = new DamageFalloff(_falloffStart, _falloffEnd, _falloffMinMultiplier);
                return new WeaponSpec(
                    Id,
                    NameKey,
                    _category,
                    _fireMode,
                    _magazineSize,
                    _reserveAmmo,
                    _fireInterval,
                    _reloadSeconds,
                    _damage,
                    _range,
                    falloff,
                    _explosionRadius,
                    // 負の値などは未設定と見なさず、WeaponSpec の検査で不正として弾く。
                    _adsFieldOfView == UnsetAdsFieldOfView ? (float?)null : _adsFieldOfView,
                    _blindFireSpreadDegrees,
                    _activeReloadWindowStart,
                    _activeReloadWindowLength,
                    _activeReloadFailSeconds,
                    _recoilPitchDegrees,
                    _recoilYawDegrees,
                    _recoilRecoveryPerSecond,
                    _hipSpreadDegrees,
                    _adsSpreadDegrees,
                    _moveSpreadDegrees);
            }
            catch (ArgumentException e)
            {
                throw new WeaponDefinitionException($"weapon '{source}' (id '{Id}') is invalid: {e.Message}", e);
            }
        }
    }

    /// <summary><see cref="WeaponDefinition"/>・<see cref="WeaponCatalogAsset"/>・<see cref="LoadoutDefinition"/> の内容が不正で変換できない。</summary>
    public sealed class WeaponDefinitionException : Exception
    {
        public WeaponDefinitionException(string message)
            : base(message)
        {
        }

        public WeaponDefinitionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
