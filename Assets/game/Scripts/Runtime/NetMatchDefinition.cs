using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // .NET の専用サーバーはアセットを読めないので、書き出し (Shooter.Editor.ServerRulesExporter) がルールのアセットごとに
    // server-data/rules/<ルール名>.json にし、クライアントは承認のときにサーバーからその値を受け取る。
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

        [Header("ラウンド制 (全滅)")]
        [SerializeField, Min(1), Tooltip("先に取ったチームが勝つラウンドの数")]
        private int _roundWinsToWin = NetMatchSettings.DefaultRoundWinsToWin;

        [SerializeField, Min(0f), Tooltip("ラウンドの頭で全員が出直してから撃ち合いを始めるまでの秒数")]
        private float _roundPreparationSeconds = NetMatchSettings.DefaultRoundPreparationSeconds;

        [SerializeField, Min(1f), Tooltip("1 ラウンドの制限時間 (秒)。切れたら立っている人の多いチームが取る")]
        private float _roundTimeLimitSeconds = NetMatchSettings.DefaultRoundTimeLimitSeconds;

        [Header("ほかのモード")]
        [SerializeField, Tooltip("旗取りの調整値。空なら入口 (GameContent) のもの、それも無ければ既定値")]
        private CaptureTheFlagDefinition _captureTheFlag;

        [SerializeField, Tooltip("移る拠点 (Hardpoint) の調整値。拠点の数はマップで決まる。空なら入口 (GameContent) のもの、それも無ければ既定値")]
        private HardpointDefinition _hardpoint;

        [SerializeField, Tooltip("爆破 (Search and Destroy) の調整値。爆破地点の数はマップで決まる。空なら入口 (GameContent) のもの、それも無ければ既定値")]
        private SearchAndDestroyDefinition _searchAndDestroy;

        [SerializeField, Tooltip("個人戦 (FFA) の調整値。空なら入口 (GameContent) のもの、それも無ければ既定値")]
        private FreeForAllDefinition _freeForAll;

        [SerializeField, Tooltip("キル確定 (Kill Confirmed) の調整値。空なら入口 (GameContent) のもの、それも無ければ既定値")]
        private KillConfirmedDefinition _killConfirmed;

        [SerializeField, Tooltip("ガンゲームの武器の並びと調整値。空なら入口 (GameContent) のもの、それも無ければ武器の並びの無い既定値")]
        private GunGameLadderDefinition _gunGameLadder;

        [Header("天候と時刻")]
        [SerializeField, Tooltip("試合の天候の Id。英数字と - _ で 64 文字まで。空ならマップの既定")]
        private string _weatherId = string.Empty;

        [SerializeField, Tooltip("試合の時刻の Id。英数字と - _ で 64 文字まで。空ならマップの既定")]
        private string _timeOfDayId = string.Empty;

        [Header("試合の間")]
        [SerializeField, Tooltip("次の試合のマップの候補 (マップのシーン名)。空なら今のマップのまま続ける")]
        private List<string> _mapPool = new List<string>();

        [SerializeField, Min(0f), Tooltip("次のマップの投票を受け付ける秒数。0 なら投票しない")]
        private float _mapVoteSeconds = NetMatchSettings.DefaultMapVoteSeconds;

        [SerializeField, Min(0), Tooltip("チームの人数の差の上限。超えたら次の試合の前に並べ直す。0 なら並べ直さない")]
        private int _maxTeamSizeDifference = NetMatchSettings.DefaultMaxTeamSizeDifference;

        [Header("非公開の試合")]
        [SerializeField, Tooltip("ロビーに載せず、参加コードで入る試合にする")]
        private bool _privateMatch;

        [SerializeField, Tooltip("参加コード。英数字と - _ で 16 文字まで。空なら無し")]
        private string _joinCode = string.Empty;

        [Header("チャット")]
        [SerializeField, Min(0f), Tooltip("1 人がチャットを続けて送れる最短の間隔 (秒)")]
        private float _chatIntervalSeconds = NetMatchSettings.DefaultChatIntervalSeconds;

        [SerializeField, Range(1, NetMatchSettings.MaxMaxChatLength), Tooltip("チャットの 1 行の文字数の上限")]
        private int _maxChatLength = NetMatchSettings.DefaultMaxChatLength;

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

        public IReadOnlyList<string> MapPool => _mapPool ??= new List<string>();

        public bool PrivateMatch
        {
            get => _privateMatch;
            set => _privateMatch = value;
        }

        public string JoinCode
        {
            get => _joinCode;
            set => _joinCode = value ?? string.Empty;
        }

        public int MaxTeamSizeDifference
        {
            get => _maxTeamSizeDifference;
            set => _maxTeamSizeDifference = value;
        }

        public CaptureTheFlagDefinition CaptureTheFlag
        {
            get => _captureTheFlag;
            set => _captureTheFlag = value;
        }

        public HardpointDefinition Hardpoint
        {
            get => _hardpoint;
            set => _hardpoint = value;
        }

        public SearchAndDestroyDefinition SearchAndDestroy
        {
            get => _searchAndDestroy;
            set => _searchAndDestroy = value;
        }

        public FreeForAllDefinition FreeForAll
        {
            get => _freeForAll;
            set => _freeForAll = value;
        }

        public KillConfirmedDefinition KillConfirmed
        {
            get => _killConfirmed;
            set => _killConfirmed = value;
        }

        public GunGameLadderDefinition GunGameLadder
        {
            get => _gunGameLadder;
            set => _gunGameLadder = value;
        }

        public string WeatherId
        {
            get => _weatherId;
            set => _weatherId = value ?? string.Empty;
        }

        public string TimeOfDayId
        {
            get => _timeOfDayId;
            set => _timeOfDayId = value ?? string.Empty;
        }

        public void SetRules(IEnumerable<GameRulesDefinition> rules)
        {
            _rules = rules == null ? new List<GameRulesDefinition>() : new List<GameRulesDefinition>(rules);
        }

        public void SetMapPool(IEnumerable<string> mapIds)
        {
            _mapPool = mapIds == null ? new List<string>() : new List<string>(mapIds);
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
                _maxPlayers,
                _mapPool,
                _mapVoteSeconds,
                _maxTeamSizeDifference,
                _privateMatch,
                _joinCode,
                _chatIntervalSeconds,
                _maxChatLength,
                _roundWinsToWin,
                _roundPreparationSeconds,
                _roundTimeLimitSeconds,
                _weatherId,
                _timeOfDayId,
                ToCaptureTheFlag(),
                ToHardpoint(),
                ToSearchAndDestroy(),
                ToFreeForAll(),
                ToKillConfirmed(),
                ToGunGame());
        }

        // オフラインのモードと同じ値で動くよう、各モードのランナーと同じ順 (この欄、入口) で調整値のアセットを選ぶ
        private CaptureTheFlagSpec ToCaptureTheFlag()
        {
            var definition = GameContent.Pick(_captureTheFlag, c => c.CaptureTheFlag);
            return definition != null ? definition.ToSpec() : CaptureTheFlagSpec.Default;
        }

        private HardpointSpec ToHardpoint()
        {
            var definition = GameContent.Pick(_hardpoint, c => c.Hardpoint);
            return definition != null ? definition.ToSpec(HardpointSpec.Default.PointCount) : HardpointSpec.Default;
        }

        private SearchAndDestroySpec ToSearchAndDestroy()
        {
            var definition = GameContent.Pick(_searchAndDestroy, c => c.SearchAndDestroy);
            return definition != null ? definition.ToSpec(SearchAndDestroySpec.Default.SiteCount) : SearchAndDestroySpec.Default;
        }

        private FreeForAllSpec ToFreeForAll()
        {
            var definition = GameContent.Pick(_freeForAll, c => c.FreeForAll);
            if (definition == null)
            {
                return FreeForAllSpec.Default;
            }

            try
            {
                return new FreeForAllSpec(definition.ScoreLimit, definition.TimeLimitSeconds, definition.RespawnSeconds, definition.PointsPerKill);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new InvalidOperationException($"free for all definition '{definition.name}' is invalid: {e.Message}", e);
            }
        }

        private KillConfirmedSpec ToKillConfirmed()
        {
            var definition = GameContent.Pick(_killConfirmed, c => c.KillConfirmed);
            return definition != null ? definition.ToSpec() : KillConfirmedSpec.Default;
        }

        private GunGameSpec ToGunGame()
        {
            var definition = GameContent.Pick(_gunGameLadder, c => c.GunGameLadder);
            if (definition == null)
            {
                return GunGameSpec.Default;
            }

            // 段の無い並びで書き出しを止めないのは、ガンゲームを遊ばないルールまで書き出せなくなるため。要るかは NetMatchFile が目的を見て確かめる
            var tierIds = new List<string>();
            var meleeIds = new List<string>();
            if (definition.Tiers.Count > 0)
            {
                var ladder = definition.ToLadder();
                foreach (var tier in ladder.Tiers)
                {
                    tierIds.Add(tier.WeaponId);
                }

                meleeIds.AddRange(ladder.MeleeWeaponIds);
            }

            try
            {
                return new GunGameSpec(definition.TimeLimitSeconds, definition.RespawnSeconds, tierIds, meleeIds, definition.DemotesOnMelee);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException($"gun game ladder '{definition.name}' is invalid: {e.Message}", e);
            }
        }

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
            var traversal = TraversalTuning.Current;
            return new NetMotorSettings(
                MotorSettings.Resolve(_motorSettings),
                stance,
                new CapsuleMoverSpec(_slopeLimitDegrees, _stepOffset, _skinWidth, CapsuleMoverSpec.DefaultLayerMask),
                prone,
                traversal.Swim,
                traversal.WallRun);
        }

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
