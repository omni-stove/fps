using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>
    /// ネット対戦の試合の設定 (点数・時間・体力・拠点・ロードアウト) と、プレイヤーの移動と体の寸法。
    /// .NET の専用サーバーはアセットを読めないので、書き出し (Shooter.Editor.ServerRulesExporter) がルールのアセットごとに
    /// <c>server-data/rules/&lt;ルール名&gt;.json</c> (<see cref="NetMatchFile"/>) にし、クライアントは承認のときにサーバーからその値を受け取る。
    /// 書き出すルールは入口 (<see cref="GameContent"/>) のルールと <see cref="Rules"/> の並び。
    /// ロードアウトと武器の一覧は、クライアントと同じく入口のものを先に使う (<see cref="PickLoadout"/>・<see cref="PickCatalog"/>)。
    /// </summary>
    [CreateAssetMenu(fileName = "NetMatch", menuName = "Shooter/Net Match Definition")]
    public sealed class NetMatchDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("サーバーに書き出すルール。入口 (GameContent) のルールに加えて、この並びの順に書き出す")]
        private List<GameRulesDefinition> _rules = new List<GameRulesDefinition>();

        [SerializeField, Tooltip("全員が最初に持つロードアウト。入口 (GameContent) にロードアウトがあればそちらを使う")]
        private LoadoutDefinition _loadout;

        [SerializeField, Tooltip("プレイヤーがロードアウトに選べる武器の一覧。入口 (GameContent) に一覧があればそちらを使う。どちらも空ならロードアウトの武器だけ")]
        private WeaponCatalogAsset _catalog;

        [Header("チームデスマッチ")]
        [SerializeField, Min(1), Tooltip("先に取ったチームが勝つキルの数")]
        private int _scoreLimit = NetMatchSettings.DefaultScoreLimit;

        [SerializeField, Min(1f), Tooltip("試合の制限時間 (秒)")]
        private float _timeLimitSeconds = NetMatchSettings.DefaultTimeLimitSeconds;

        [SerializeField, Min(0f), Tooltip("倒れてからリスポーンするまでの秒数")]
        private float _respawnSeconds = NetMatchSettings.DefaultRespawnSeconds;

        [SerializeField, Min(0f), Tooltip("試合が終わってから次の試合を始めるまでの秒数")]
        private float _restartDelaySeconds = NetMatchSettings.DefaultRestartDelaySeconds;

        [SerializeField, Min(1), Tooltip("プレイヤーの体力の上限")]
        private int _maxHealth = NetMatchSettings.DefaultMaxHealth;

        [SerializeField, Min(0f), Tooltip("リスポーンで避ける、敵からの距離 (m)")]
        private float _minEnemySpawnDistance = SpawnSelector.DefaultMinEnemyDistance;

        [SerializeField, Min(0f), Tooltip("切断したプレイヤーの成績を、つなぎ直すまで残す秒数")]
        private float _reconnectGraceSeconds = TeamDeathmatchSession.DefaultReconnectGraceSeconds;

        [SerializeField, Min(0f), Tooltip("サーバーが認める、射撃の始点とサーバーの目の位置のずれ (m)")]
        private float _maxShotOriginOffset = ShotValidator.DefaultMaxOriginOffset;

        [SerializeField, Range(NetMatchSettings.MinMaxPlayers, NetMatchSettings.MaxMaxPlayers), Tooltip("試合に入れる人の数。Bot は数えない。満員ならサーバーは参加を断り、ロビーにもこの人数で載せる")]
        private int _maxPlayers = NetMatchSettings.DefaultMaxPlayers;

        [Header("拠点制圧")]
        [SerializeField, Min(1), Tooltip("拠点のポイントで先に取ったチームが勝つ点数")]
        private int _dominationScoreLimit = NetMatchSettings.DefaultDominationScoreLimit;

        [SerializeField, Min(0.01f), Tooltip("1 人で拠点を取り切るまでの秒数")]
        private float _captureSeconds = NetMatchSettings.DefaultCaptureSeconds;

        [SerializeField, Min(0.01f), Tooltip("持っている拠点 1 つあたり、1 秒に入るポイント")]
        private float _pointsPerSecond = NetMatchSettings.DefaultPointsPerSecond;

        [SerializeField, Min(1), Tooltip("占領を速める人数の上限")]
        private int _maxCapturers = DominationSpec.DefaultMaxCapturers;

        [Header("プレイヤーの移動と体")]
        [SerializeField, Tooltip("移動の値。空なら入口 (GameContent) の Motor、それも無ければ Resources の MotorSettings")]
        private MotorSettings _motorSettings;

        [SerializeField, Min(0.01f), Tooltip("立ちのカプセルの高さ (m)")]
        private float _standingHeight = NetMotorSettings.DefaultStandingHeight;

        [SerializeField, Tooltip("立ちのカプセルの中心の、プレイヤーの位置からの高さ (m)")]
        private float _standingCenterY = NetMotorSettings.DefaultStandingCenterY;

        [SerializeField, Tooltip("立ちの目の、プレイヤーの位置からの高さ (m)")]
        private float _eyeY = NetMotorSettings.DefaultEyeY;

        [SerializeField, Min(0.01f), Tooltip("しゃがみのカプセルの高さ (m)。半径の 2 倍以上、立ちの高さ以下")]
        private float _crouchHeight = NetMotorSettings.DefaultCrouchHeight;

        [SerializeField, Min(0f), Tooltip("カプセルの半径 (m)")]
        private float _radius = NetMotorSettings.DefaultRadius;

        [SerializeField, Range(0f, 90f), Tooltip("登れる坂の角度 (度)")]
        private float _slopeLimitDegrees = CapsuleMoverSpec.DefaultSlopeLimitDegrees;

        [SerializeField, Min(0f), Tooltip("乗り越えられる段差の高さ (m)")]
        private float _stepOffset = CapsuleMoverSpec.DefaultStepOffset;

        [SerializeField, Min(0f), Tooltip("当たり判定のカプセルの外側に取る隙間 (m)")]
        private float _skinWidth = CapsuleMoverSpec.DefaultSkinWidth;

        public IReadOnlyList<GameRulesDefinition> Rules => _rules ??= new List<GameRulesDefinition>();

        public LoadoutDefinition Loadout
        {
            get => _loadout;
            set => _loadout = value;
        }

        public WeaponCatalogAsset Catalog
        {
            get => _catalog;
            set => _catalog = value;
        }

        public int MaxPlayers
        {
            get => _maxPlayers;
            set => _maxPlayers = value;
        }

        public MotorSettings MotorSettings
        {
            get => _motorSettings;
            set => _motorSettings = value;
        }

        public void SetRules(IEnumerable<GameRulesDefinition> rules)
        {
            _rules = rules == null ? new List<GameRulesDefinition>() : new List<GameRulesDefinition>(rules);
        }

        // クライアント (NetV2Weapons) とサーバーへの書き出しで選ぶ順がずれないよう、どちらもここで選ぶ
        public static LoadoutDefinition PickLoadout(NetMatchDefinition match)
        {
            var picked = GameContent.Pick(c => c.Loadout);
            if (picked != null)
            {
                return picked;
            }

            return match != null && match._loadout != null ? match._loadout : null;
        }

        public static WeaponCatalogAsset PickCatalog(NetMatchDefinition match)
        {
            var picked = GameContent.Pick(c => c.WeaponCatalog);
            if (picked != null)
            {
                return picked;
            }

            return match != null && match._catalog != null ? match._catalog : null;
        }

        /// <summary>試合の設定。</summary>
        /// <exception cref="ArgumentOutOfRangeException">値が <see cref="NetMatchSettings"/> の条件に合わない。</exception>
        public NetMatchSettings ToSettings()
        {
            return new NetMatchSettings(
                _scoreLimit,
                _timeLimitSeconds,
                _respawnSeconds,
                _restartDelaySeconds,
                _maxHealth,
                _minEnemySpawnDistance,
                _reconnectGraceSeconds,
                _maxShotOriginOffset,
                _dominationScoreLimit,
                _captureSeconds,
                _pointsPerSecond,
                _maxCapturers,
                _maxPlayers);
        }

        /// <summary>
        /// プレイヤーの移動と体の寸法。移動は <see cref="MotorSettings.Resolve"/>、伏せは <see cref="MotorSettings.ResolveProne"/> で、
        /// 伏せの高さはその高さをカプセルが取れる高さに収めたもの (<see cref="NetMotorSettings.FitProneHeight"/>)。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">寸法・移動・伏せの値が条件に合わない。</exception>
        public NetMotorSettings ToMotor()
        {
            var prone = MotorSettings.ResolveProne(_motorSettings);
            var stance = new StanceGeometry(
                _standingHeight,
                _standingCenterY,
                _eyeY,
                _crouchHeight,
                NetMotorSettings.FitProneHeight(prone.Height, _crouchHeight, _radius),
                _radius);
            return new NetMotorSettings(
                MotorSettings.Resolve(_motorSettings),
                stance,
                new CapsuleMoverSpec(_slopeLimitDegrees, _stepOffset, _skinWidth, CapsuleMoverSpec.DefaultLayerMask),
                prone);
        }

        /// <summary>
        /// ロードアウトのルール・武器と、選べる武器の一覧 (<see cref="PickLoadout"/>・<see cref="PickCatalog"/>) を検査し、試合で使う <see cref="ServerLoadout"/> にする。
        /// ロードアウトが未設定か、ルール・武器・一覧が不正なら false と理由を返す。
        /// </summary>
        public bool TryCreateLoadout(out ServerLoadout loadout, out string error)
        {
            loadout = null;
            var picked = PickLoadout(this);
            var catalog = PickCatalog(this);

            // 参照切れ (破棄されたアセット) も Unity の == null で拾う
            if (picked == null)
            {
                error = "neither GameContent nor net match '" + name + "' has a loadout";
                return false;
            }

            LoadoutRule rule;
            IReadOnlyList<WeaponSpec> specs;
            WeaponCatalog choices = null;
            try
            {
                rule = picked.ToRule();
                specs = picked.ToSpecs();
                if (catalog != null)
                {
                    choices = catalog.ToCatalog();
                }
            }
            catch (WeaponDefinitionException e)
            {
                error = e.Message;
                return false;
            }

            try
            {
                loadout = new ServerLoadout(rule, specs, choices, picked.SwitchSeconds);
            }
            catch (ArgumentException e)
            {
                error = "loadout '" + picked.name + "' is invalid: " + e.Message;
                return false;
            }

            error = null;
            return true;
        }
    }
}
