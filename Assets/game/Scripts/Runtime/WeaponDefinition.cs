using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 武器 1 種類の値をアセットとして持つ。クライアントと専用サーバーは同じアセットを読み、<see cref="ToSpec"/> で
    /// 不変の <see cref="WeaponSpec"/> に変換して使う。検証は <see cref="WeaponSpec"/> と <see cref="DamageFalloff"/> に任せ、
    /// 失敗したらどのアセットの値かを添えて <see cref="WeaponDefinitionException"/> を投げる。
    /// 新しく作ったときの値は rifle 相当 (AssaultRifle、Auto、30/90 発、0.1 秒、リロード 2 秒、25 ダメージ、射程 100m、
    /// 20〜60m で 0.5 倍まで減衰)。Id はエディターで作ったとき (<see cref="Reset"/>) と値を検証するとき (<see cref="OnValidate"/>) に、
    /// 空なら新しい UUID を入れる。
    /// 実際に使う値はアセット (Assets/game/Weapons/*.asset、Fps.Editor.WeaponAssetBuilder が作る) の側にある。
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon", menuName = "Fps/Weapon Definition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("武器の識別子。小文字・ハイフン区切りの UUID (例: 3f2b8c1e-0a4d-4c6e-9b1a-7d2e5f8c0b13)。空なら自動で振る。作った後は変えない")]
        private string _id = string.Empty;

        [SerializeField, Tooltip("プレイヤーに見せる名前。空なら Id")]
        private string _displayName = string.Empty;

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

        public string Id
        {
            get => _id ?? string.Empty;
            set => _id = value ?? string.Empty;
        }

        public string DisplayName
        {
            get => _displayName ?? string.Empty;
            set => _displayName = value ?? string.Empty;
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
                    DisplayName,
                    _category,
                    _fireMode,
                    _magazineSize,
                    _reserveAmmo,
                    _fireInterval,
                    _reloadSeconds,
                    _damage,
                    _range,
                    falloff,
                    _explosionRadius);
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
