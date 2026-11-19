using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "GameRules", menuName = "Shooter/Game Rules")]
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

        [SerializeField, Min(0.01f), Tooltip("味方を起こせる距離 (m)。起こす人と起こされる人の本体の中心どうしで測る")]
        private float _reviveRange = ReviveReach.DefaultRange;

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

        [SerializeField, Tooltip("近接攻撃を使えるか")]
        private Melee _melee = Melee.Off;

        [SerializeField, Tooltip("被弾したときに怯むか")]
        private Stagger _stagger = Stagger.Off;

        [SerializeField, Tooltip("走りながらしゃがんでスライディングできるか")]
        private Slide _slide = Slide.Off;

        [SerializeField, Tooltip("乗り越えられない高さの段や崖の縁によじ登れるか")]
        private Mantle _mantle = Mantle.Off;

        [SerializeField, Tooltip("伏せられるか")]
        private Prone _prone = Prone.Off;

        [SerializeField, Tooltip("はしごを上り下りできるか")]
        private Ladder _ladder = Ladder.Off;

        [SerializeField, Tooltip("ジップラインを使えるか")]
        private Zipline _zipline = Zipline.Off;

        [SerializeField, Tooltip("落ちている最中にパラシュートを開けるか")]
        private Parachute _parachute = Parachute.Off;

        [SerializeField, Tooltip("敵が発見ゲージを溜めて、気づき・警戒・交戦と段階を踏むか")]
        private Stealth _stealth = Stealth.Off;

        [SerializeField, Tooltip("近くを飛ぶ弾で制圧され、狙いや動きが鈍るか")]
        private Suppression _suppression = Suppression.Off;

        [SerializeField, Tooltip("敵や場所に印を付けられるか")]
        private Shooter.Core.Ping _ping = Shooter.Core.Ping.Off;

        [SerializeField, Tooltip("能力 (回数・待ち時間・入力の枠に乗るもの) を使えるか")]
        private Abilities _abilities = Abilities.Off;

        [SerializeField, Tooltip("時間の速さを変えられるか。オフラインに限る")]
        private SlowMotion _slowMotion = SlowMotion.Off;

        [SerializeField, Tooltip("覗き込んで構えている間しか撃てないか (腰撃ちを禁じる)。ブラインドファイアは構えずに撃てる")]
        private AimToFire _aimToFire = AimToFire.Off;

        [SerializeField, Tooltip("ふつうのジャンプで跳べるか。Off でも Jump は乗り越え・崖のぼり・伏せから立つ・はしごやジップラインから離れる・パラシュートを開くに使う")]
        private Jump _jump = Jump.On;

        [SerializeField, Tooltip("エモートを使えるか")]
        private Emote _emote = Emote.Off;

        [SerializeField, Tooltip("走る・跳ぶなどでスタミナを使い、尽きたらできなくなるか")]
        private Stamina _stamina = Stamina.Off;

        [SerializeField, Tooltip("始めたときにリロードを取り消し、している間はリロードを始めさせない動作。空ならどの動作でも止めない。Aim は構え始めたときに取り消すだけで、構えたままのリロードは止めない")]
        private ReloadInterrupt[] _reloadInterruptedBy = { ReloadInterrupt.Melee, ReloadInterrupt.Grenade };

        [SerializeField, Tooltip("リロード中に構えられるか。Off ならリロードが終わるまで構えない (reloadInterruptedBy に Aim があればそちらを優先し、構えでリロードを取り消す)")]
        private AimWhileReloading _aimWhileReloading = AimWhileReloading.On;

        [Header("体力とダメージ")]
        [SerializeField, Min(1), Tooltip("プレイヤーの最大体力")]
        private int _playerMaxHealth = GameRules.DefaultPlayerMaxHealth;

        // ZoneDamageMultipliers が double で持つので、JSON から取り込んだ値を丸めずに持てるよう double にする。
        [SerializeField, Tooltip("頭に当たったときのダメージ倍率。0 以上")]
        private double _headDamageMultiplier = ZoneDamageMultipliers.Default.Head;

        [SerializeField, Tooltip("胴に当たったときのダメージ倍率。0 以上")]
        private double _bodyDamageMultiplier = ZoneDamageMultipliers.Default.Body;

        [SerializeField, Tooltip("手足に当たったときのダメージ倍率。0 以上")]
        private double _limbDamageMultiplier = ZoneDamageMultipliers.Default.Limb;

        [Header("リスポーン")]
        [SerializeField, Min(0f), Tooltip("リスポーンしてからダメージを受けなくなる秒数。0 なら守らない")]
        private float _spawnProtectionSeconds;

        [SerializeField, Tooltip("倒された人を戻す場所の候補。上から試し、使えない候補 (分隊がいない・ビーコンが無い) は飛ばす。1 つ以上、重ねずに並べる")]
        private RespawnAnchor[] _respawnAnchors = { RespawnAnchor.Base };

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

        [SerializeField, Min(0.01f), Tooltip("放置で切る何秒前に警告を出すか。切る秒数以上なら数え始めてすぐ警告する")]
        private float _idleWarningSeconds = GameRules.DefaultIdleWarningSeconds;

        [Header("武器")]
        [SerializeField, UnityEngine.Range(0, GameRules.MaxWeaponsLimit), Tooltip("1 人が同時に持てる武器の数。上限で別の武器を拾うと今の武器を落として持ち替える。0 なら上限なし")]
        private int _maxWeapons;

        [SerializeField, Tooltip("Auto なら撃ち切ったら (弾倉が空で引き金を引いたときも) 自動でリロードする。Manual なら Reload を押したときだけ")]
        private ReloadMode _reloadMode = ReloadMode.Auto;

        [SerializeField, Tooltip("プレイヤーの視点。一人称か三人称 (肩越し)")]
        private ViewMode _viewMode = ViewMode.FirstPerson;

        [Header("ストリーク")]
        [SerializeField, Tooltip("倒れずに倒した数で UAV・空爆を得るストリークをプレイヤーに付けるか")]
        private Streaks _streaks = Streaks.On;

        [Header("チャット・観戦")]
        [SerializeField, Tooltip("試合中に文字のチャットを送れるか")]
        private TextChat _textChat = TextChat.On;

        [SerializeField, Tooltip("試合を観戦できるか")]
        private Spectating _spectating = Spectating.On;

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

        public float ReviveRange
        {
            get => _reviveRange;
            set => _reviveRange = value;
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

        public Melee Melee
        {
            get => _melee;
            set => _melee = value;
        }

        public Stagger Stagger
        {
            get => _stagger;
            set => _stagger = value;
        }

        public Slide Slide
        {
            get => _slide;
            set => _slide = value;
        }

        public Mantle Mantle
        {
            get => _mantle;
            set => _mantle = value;
        }

        public Prone Prone
        {
            get => _prone;
            set => _prone = value;
        }

        public Ladder Ladder
        {
            get => _ladder;
            set => _ladder = value;
        }

        public Zipline Zipline
        {
            get => _zipline;
            set => _zipline = value;
        }

        public Parachute Parachute
        {
            get => _parachute;
            set => _parachute = value;
        }

        public Stealth Stealth
        {
            get => _stealth;
            set => _stealth = value;
        }

        public Suppression Suppression
        {
            get => _suppression;
            set => _suppression = value;
        }

        public Shooter.Core.Ping Ping
        {
            get => _ping;
            set => _ping = value;
        }

        public Abilities Abilities
        {
            get => _abilities;
            set => _abilities = value;
        }

        public SlowMotion SlowMotion
        {
            get => _slowMotion;
            set => _slowMotion = value;
        }

        public AimToFire AimToFire
        {
            get => _aimToFire;
            set => _aimToFire = value;
        }

        public Jump Jump
        {
            get => _jump;
            set => _jump = value;
        }

        public Emote Emote
        {
            get => _emote;
            set => _emote = value;
        }

        public Stamina Stamina
        {
            get => _stamina;
            set => _stamina = value;
        }

        public ReloadInterrupt[] ReloadInterruptedBy
        {
            get => _reloadInterruptedBy != null ? (ReloadInterrupt[])_reloadInterruptedBy.Clone() : null;
            set => _reloadInterruptedBy = value != null ? (ReloadInterrupt[])value.Clone() : null;
        }

        public AimWhileReloading AimWhileReloading
        {
            get => _aimWhileReloading;
            set => _aimWhileReloading = value;
        }

        // 写しを渡し受けするのは、呼んだ側が配列を書き換えてもアセットの値が黙って変わらないようにするため。
        public RespawnAnchor[] RespawnAnchors
        {
            get => _respawnAnchors != null ? (RespawnAnchor[])_respawnAnchors.Clone() : null;
            set => _respawnAnchors = value != null ? (RespawnAnchor[])value.Clone() : null;
        }

        public int PlayerMaxHealth
        {
            get => _playerMaxHealth;
            set => _playerMaxHealth = value;
        }

        public double HeadDamageMultiplier
        {
            get => _headDamageMultiplier;
            set => _headDamageMultiplier = value;
        }

        public double BodyDamageMultiplier
        {
            get => _bodyDamageMultiplier;
            set => _bodyDamageMultiplier = value;
        }

        public double LimbDamageMultiplier
        {
            get => _limbDamageMultiplier;
            set => _limbDamageMultiplier = value;
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

        public float IdleWarningSeconds
        {
            get => _idleWarningSeconds;
            set => _idleWarningSeconds = value;
        }

        public int MaxWeapons
        {
            get => _maxWeapons;
            set => _maxWeapons = value;
        }

        public ReloadMode ReloadMode
        {
            get => _reloadMode;
            set => _reloadMode = value;
        }

        public ViewMode ViewMode
        {
            get => _viewMode;
            set => _viewMode = value;
        }

        public Streaks Streaks
        {
            get => _streaks;
            set => _streaks = value;
        }

        public TextChat TextChat
        {
            get => _textChat;
            set => _textChat = value;
        }

        public Spectating Spectating
        {
            get => _spectating;
            set => _spectating = value;
        }

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
                        _grenadeCount,
                        _melee,
                        _stagger,
                        _slide,
                        _mantle,
                        _prone,
                        _ladder,
                        _zipline,
                        _parachute,
                        _stealth,
                        _suppression,
                        _ping,
                        _abilities,
                        _slowMotion,
                        _aimToFire,
                        _jump,
                        _reviveRange,
                        _emote,
                        _stamina,
                        _reloadInterruptedBy ?? Array.Empty<ReloadInterrupt>(),
                        _aimWhileReloading),
                    _spawnProtectionSeconds,
                    _fallDamage,
                    _hudDensity,
                    _objective,
                    _minPlayers,
                    _idleKickSeconds,
                    _maxWeapons,
                    _reloadMode,
                    _viewMode,
                    _playerMaxHealth,
                    new ZoneDamageMultipliers(
                        _headDamageMultiplier,
                        _bodyDamageMultiplier,
                        _limbDamageMultiplier,
                        ZoneDamageMultipliers.Default.WeakPoint),
                    _streaks,
                    _textChat,
                    _spectating,
                    // 空の配列を null と同じく既定へ戻さず断るのは、インスペクターで消し切ったことに気づけるようにするため。
                    _respawnAnchors ?? Array.Empty<RespawnAnchor>(),
                    _idleWarningSeconds);
            }
            catch (ArgumentException e)
            {
                throw new GameRulesDefinitionException($"game rules '{SourceName}' are invalid: {e.Message}", e);
            }
        }

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

        // rules は検査済みなので、写した後は検査し直さなくても必ず ToRules に通る。
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
            _idleWarningSeconds = rules.IdleWarningSeconds;
            _maxWeapons = rules.MaxWeapons;
            _reloadMode = rules.ReloadMode;
            _viewMode = rules.ViewMode;
            _playerMaxHealth = rules.PlayerMaxHealth;
            _headDamageMultiplier = rules.ZoneDamage.Head;
            _bodyDamageMultiplier = rules.ZoneDamage.Body;
            _limbDamageMultiplier = rules.ZoneDamage.Limb;
            _streaks = rules.Streaks;
            _textChat = rules.TextChat;
            _spectating = rules.Spectating;
            _respawnAnchors = new RespawnAnchor[rules.RespawnAnchors.Count];
            for (var i = 0; i < _respawnAnchors.Length; i++)
            {
                _respawnAnchors[i] = rules.RespawnAnchors[i];
            }

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
            _melee = actions.Melee;
            _stagger = actions.Stagger;
            _slide = actions.Slide;
            _mantle = actions.Mantle;
            _prone = actions.Prone;
            _ladder = actions.Ladder;
            _zipline = actions.Zipline;
            _parachute = actions.Parachute;
            _stealth = actions.Stealth;
            _suppression = actions.Suppression;
            _ping = actions.Ping;
            _abilities = actions.Abilities;
            _slowMotion = actions.SlowMotion;
            _aimToFire = actions.AimToFire;
            _jump = actions.Jump;
            _reviveRange = actions.ReviveRange;
            _emote = actions.Emote;
            _stamina = actions.Stamina;
            _reloadInterruptedBy = new ReloadInterrupt[actions.ReloadInterruptedBy.Count];
            for (var i = 0; i < _reloadInterruptedBy.Length; i++)
            {
                _reloadInterruptedBy[i] = actions.ReloadInterruptedBy[i];
            }

            _aimWhileReloading = actions.AimWhileReloading;
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed game rules)" : name;
    }

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
