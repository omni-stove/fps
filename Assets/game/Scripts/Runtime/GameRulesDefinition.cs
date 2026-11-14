using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// モード 1 つ分のルールをアセットとして持つ。<see cref="ToRules"/> で不変の <see cref="GameRules"/> に変換して使う。
    /// 検証は <see cref="GameRules"/> に任せ、失敗したらどのアセットの値かを添えて <see cref="GameRulesDefinitionException"/> を投げる。
    /// 新しく作ったときの値は <see cref="GameRules.Default"/> と同じ。
    /// 実際に使う値はアセット (Assets/game/Rules/*.asset、Fps.Editor.GameRulesAssetBuilder が作る) の側にある。
    /// </summary>
    [CreateAssetMenu(fileName = "GameRules", menuName = "Fps/Game Rules")]
    public sealed class GameRulesDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("味方に弾が当たったときの扱い")]
        private FriendlyFire _friendlyFire = FriendlyFire.Off;

        [SerializeField, Tooltip("倒されたプレイヤーがいつ戻ってくるか")]
        private RespawnMode _respawnMode = RespawnMode.Individual;

        [SerializeField, Min(0.01f), Tooltip("波と波の間隔 (秒)。Respawn Mode が Wave のときだけ使う")]
        private float _waveIntervalSeconds = GameRules.DefaultWaveIntervalSeconds;

        [Header("自動回復")]
        [SerializeField, Min(0f), Tooltip("最後に被弾してから回復が始まるまでの秒数")]
        private float _regenDelaySeconds;

        [SerializeField, Min(0f), Tooltip("1 秒あたりの回復量。0 なら自動回復しない")]
        private float _regenPerSecond;

        [Header("死亡時")]
        [SerializeField, Tooltip("倒されたプレイヤーの体をどう見せるか")]
        private DeathBody _deathBody = DeathBody.Vanish;

        [SerializeField, Tooltip("倒されてからリスポーンするまで、自分のカメラに何を映すか")]
        private DeathCamera _deathCamera = DeathCamera.None;

        [Header("弾薬")]
        [SerializeField, Tooltip("オンなら予備弾が減らない")]
        private bool _infiniteReserve;

        [SerializeField, Min(0f), Tooltip("初期予備弾の倍率")]
        private float _reserveAmmoMultiplier = 1f;

        [Header("建造物")]
        [SerializeField, Tooltip("建造物を弾で壊せるか")]
        private Destruction _destruction = Destruction.Off;

        [Header("アクション")]
        [SerializeField, Tooltip("体力が 0 になったプレイヤーをダウンさせ、味方が起こせるようにするか")]
        private Downed _downed = Downed.Off;

        [SerializeField, Min(0.01f), Tooltip("ダウンしてから起こされずに倒れるまでの秒数")]
        private float _downedBleedoutSeconds = ActionRules.DefaultDownedBleedoutSeconds;

        [SerializeField, UnityEngine.Range(0.01f, 1f), Tooltip("ダウン中と起こされた後の体力の、最大体力に対する割合")]
        private float _downedHealthFraction = ActionRules.DefaultDownedHealthFraction;

        [SerializeField, Min(0.01f), Tooltip("味方を起こすのにかかる秒数")]
        private float _reviveSeconds = ActionRules.DefaultReviveSeconds;

        [SerializeField, Tooltip("遮蔽物に張り付いて身を隠せるか")]
        private Cover _cover = Cover.Off;

        [SerializeField, Tooltip("隠れたまま撃てるか。On にするなら Cover も On にする")]
        private BlindFire _blindFire = BlindFire.Off;

        [SerializeField, Tooltip("リロード中に決まった間で押すと早く終わる仕組みを使うか")]
        private ActiveReload _activeReload = ActiveReload.Off;

        [SerializeField, Tooltip("素早く横や後ろへ飛び退けるか")]
        private Dodge _dodge = Dodge.Off;

        [SerializeField, Tooltip("低い障害物を乗り越えられるか")]
        private Vault _vault = Vault.Off;

        [SerializeField, Tooltip("グレネードを投げられるか")]
        private Grenade _grenade = Grenade.Off;

        [SerializeField, Min(0), Tooltip("出撃ごとに持つグレネードの数。Grenade が Off なら使わない")]
        private int _grenadeCount;

        [Header("リスポーン")]
        [SerializeField, Min(0f), Tooltip("リスポーンしてからダメージを受けなくなる秒数。0 なら守らない")]
        private float _spawnProtectionSeconds;

        [Header("移動")]
        [SerializeField, Tooltip("高い所から落ちたときにダメージを受けるか")]
        private FallDamage _fallDamage = FallDamage.Off;

        [Header("HUD")]
        [SerializeField, Tooltip("HUD にどれだけの情報を出すか")]
        private HudDensity _hudDensity = HudDensity.Full;

        [Header("オンラインの試合")]
        [SerializeField, Tooltip("勝敗を何で決めるか。None はキル数、Domination は拠点のポイント")]
        private MatchObjective _objective = MatchObjective.None;

        [SerializeField, UnityEngine.Range(0, GameRules.MaxMinPlayers), Tooltip("人がこれより少なければ足りない分を Bot で埋める。0 なら埋めない")]
        private int _minPlayers;

        [SerializeField, Min(0f), Tooltip("入力がこの秒数無いプレイヤーをサーバーから切る。0 なら切らない")]
        private float _idleKickSeconds;

        public FriendlyFire FriendlyFire
        {
            get => _friendlyFire;
            set => _friendlyFire = value;
        }

        public RespawnMode RespawnMode
        {
            get => _respawnMode;
            set => _respawnMode = value;
        }

        public float WaveIntervalSeconds
        {
            get => _waveIntervalSeconds;
            set => _waveIntervalSeconds = value;
        }

        public float RegenDelaySeconds
        {
            get => _regenDelaySeconds;
            set => _regenDelaySeconds = value;
        }

        public float RegenPerSecond
        {
            get => _regenPerSecond;
            set => _regenPerSecond = value;
        }

        public DeathBody DeathBody
        {
            get => _deathBody;
            set => _deathBody = value;
        }

        public DeathCamera DeathCamera
        {
            get => _deathCamera;
            set => _deathCamera = value;
        }

        public bool InfiniteReserve
        {
            get => _infiniteReserve;
            set => _infiniteReserve = value;
        }

        public float ReserveAmmoMultiplier
        {
            get => _reserveAmmoMultiplier;
            set => _reserveAmmoMultiplier = value;
        }

        public Destruction Destruction
        {
            get => _destruction;
            set => _destruction = value;
        }

        public Downed Downed
        {
            get => _downed;
            set => _downed = value;
        }

        public float DownedBleedoutSeconds
        {
            get => _downedBleedoutSeconds;
            set => _downedBleedoutSeconds = value;
        }

        public float DownedHealthFraction
        {
            get => _downedHealthFraction;
            set => _downedHealthFraction = value;
        }

        public float ReviveSeconds
        {
            get => _reviveSeconds;
            set => _reviveSeconds = value;
        }

        public Cover Cover
        {
            get => _cover;
            set => _cover = value;
        }

        public BlindFire BlindFire
        {
            get => _blindFire;
            set => _blindFire = value;
        }

        public ActiveReload ActiveReload
        {
            get => _activeReload;
            set => _activeReload = value;
        }

        public Dodge Dodge
        {
            get => _dodge;
            set => _dodge = value;
        }

        public Vault Vault
        {
            get => _vault;
            set => _vault = value;
        }

        public Grenade Grenade
        {
            get => _grenade;
            set => _grenade = value;
        }

        public int GrenadeCount
        {
            get => _grenadeCount;
            set => _grenadeCount = value;
        }

        public float SpawnProtectionSeconds
        {
            get => _spawnProtectionSeconds;
            set => _spawnProtectionSeconds = value;
        }

        public FallDamage FallDamage
        {
            get => _fallDamage;
            set => _fallDamage = value;
        }

        public HudDensity HudDensity
        {
            get => _hudDensity;
            set => _hudDensity = value;
        }

        public MatchObjective Objective
        {
            get => _objective;
            set => _objective = value;
        }

        public int MinPlayers
        {
            get => _minPlayers;
            set => _minPlayers = value;
        }

        public float IdleKickSeconds
        {
            get => _idleKickSeconds;
            set => _idleKickSeconds = value;
        }

        /// <summary>定義を不変の <see cref="GameRules"/> に変換する。呼ぶたびに新しいインスタンスを返す。</summary>
        /// <exception cref="GameRulesDefinitionException">値が不正なとき。メッセージにアセット名を含む。</exception>
        public GameRules ToRules()
        {
            try
            {
                return new GameRules(
                    _friendlyFire,
                    _respawnMode,
                    _waveIntervalSeconds,
                    _regenDelaySeconds,
                    _regenPerSecond,
                    _deathBody,
                    _deathCamera,
                    _infiniteReserve,
                    _reserveAmmoMultiplier,
                    _destruction,
                    new ActionRules(
                        _downed,
                        _downedBleedoutSeconds,
                        _downedHealthFraction,
                        _reviveSeconds,
                        _cover,
                        _blindFire,
                        _activeReload,
                        _dodge,
                        _vault,
                        _grenade,
                        _grenadeCount),
                    _spawnProtectionSeconds,
                    _fallDamage,
                    _hudDensity,
                    _objective,
                    _minPlayers,
                    _idleKickSeconds);
            }
            catch (ArgumentException e)
            {
                throw new GameRulesDefinitionException($"game rules '{SourceName}' are invalid: {e.Message}", e);
            }
        }

        /// <summary>
        /// <see cref="ToRules"/> と同じ変換を、例外の代わりに戻り値で失敗を知らせて行う。
        /// 成功したら rules に結果を入れ error を null にして true、失敗したら rules を null、error にアセット名を含む理由を入れて false。
        /// </summary>
        public bool TryToRules(out GameRules rules, out string error)
        {
            try
            {
                rules = ToRules();
                error = null;
                return true;
            }
            catch (GameRulesDefinitionException e)
            {
                rules = null;
                error = e.Message;
                return false;
            }
        }

        /// <summary>rules の値をすべて写す。rules は検査済みなので、写した後は必ず <see cref="ToRules"/> に通る。</summary>
        public void CopyFrom(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            _friendlyFire = rules.FriendlyFire;
            _respawnMode = rules.RespawnMode;
            _waveIntervalSeconds = rules.WaveIntervalSeconds;
            _regenDelaySeconds = rules.RegenDelaySeconds;
            _regenPerSecond = rules.RegenPerSecond;
            _deathBody = rules.DeathBody;
            _deathCamera = rules.DeathCamera;
            _infiniteReserve = rules.InfiniteReserve;
            _reserveAmmoMultiplier = rules.ReserveAmmoMultiplier;
            _destruction = rules.Destruction;
            _spawnProtectionSeconds = rules.SpawnProtectionSeconds;
            _fallDamage = rules.FallDamage;
            _hudDensity = rules.HudDensity;
            _objective = rules.Objective;
            _minPlayers = rules.MinPlayers;
            _idleKickSeconds = rules.IdleKickSeconds;

            var actions = rules.Actions;
            _downed = actions.Downed;
            _downedBleedoutSeconds = actions.DownedBleedoutSeconds;
            _downedHealthFraction = actions.DownedHealthFraction;
            _reviveSeconds = actions.ReviveSeconds;
            _cover = actions.Cover;
            _blindFire = actions.BlindFire;
            _activeReload = actions.ActiveReload;
            _dodge = actions.Dodge;
            _vault = actions.Vault;
            _grenade = actions.Grenade;
            _grenadeCount = actions.GrenadeCount;
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed game rules)" : name;
    }

    /// <summary><see cref="GameRulesDefinition"/> の内容が不正で <see cref="GameRules"/> に変換できない。</summary>
    public sealed class GameRulesDefinitionException : Exception
    {
        public GameRulesDefinitionException(string message)
            : base(message)
        {
        }

        public GameRulesDefinitionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
