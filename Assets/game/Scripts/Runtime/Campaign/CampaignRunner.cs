using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Fps.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Runtime
{
    /// <summary>
    /// キャンペーンのセーブと再開を受け持つ。
    /// <list type="bullet">
    /// <item><see cref="CheckpointTrigger"/> から <see cref="SaveAt"/> を呼ばれたら、ミッションの進み具合・プレイヤーの体力・弾数・
    /// チェックポイントの Id から <see cref="CampaignSave"/> を作り、スロット <see cref="SaveSlot"/> に書く。</item>
    /// <item>プレイヤーが倒れたら <see cref="ReloadDelaySeconds"/> 秒後にシーンを読み直す。</item>
    /// <item>始めるときにセーブがあれば、ミッションの進み具合・チェックポイントの位置・体力・弾数を戻す。
    /// 別のミッションのセーブや壊れたセーブは警告を出して無視し、最初から始める。</item>
    /// <item>ミッションを達成したらセーブを消す。</item>
    /// </list>
    /// Start で <see cref="MissionRunner"/> と <see cref="GameBootstrap"/> (どちらも Inspector で指定しなければシーンから) を拾い、
    /// GameBootstrap がプレイヤーを作り終えていればすぐ、まだなら <see cref="GameBootstrap.Built"/> を待って始める。
    /// トリガーより先にミッションを戻すため、既定の実行順より前に Start を走らせる。
    /// セーブの場所 (<see cref="Storage"/>) とシーンの読み直し (<see cref="SceneReloader"/>) は差し替えられる。
    /// セーブしたことと再開までの秒数は <see cref="Notice"/> に出し、GameBootstrap の HUD があればそこに表示する。
    /// 始めるときにルール (<see cref="RulesDefinition"/>、コマンドライン引数 <c>-difficulty easy|normal|hard</c> があれば
    /// <see cref="EasyRules"/> / <see cref="NormalRules"/> / <see cref="HardRules"/>) を読み、プレイヤーの自動回復 (<see cref="HealthRegen"/>) と
    /// 武器の予備弾 (<see cref="WeaponController.ApplyRules"/>) に効かせてからセーブを戻す。セーブの予備弾には倍率を掛け直さない。
    /// 同じルールの <see cref="GameRules.DeathBody"/> で、シーンの敵と標的ダミーのやられ演出 (<see cref="DeathBodyPresenter"/>) も決める。
    /// 状態の変化は <see cref="LogTag"/> を付けて Debug.Log に出す。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class CampaignRunner : MonoBehaviour
    {
        public const string LogTag = "[Campaign]";

        /// <summary>キャンペーンのセーブを置くスロット名。</summary>
        public const string SaveSlot = "campaign";

        public const float DefaultReloadDelaySeconds = 3f;

        /// <summary>セーブしたときに HUD へ「セーブしました」を出しておく秒数。</summary>
        public const float SavedNoticeSeconds = 2.5f;

        [SerializeField, Tooltip("セーブするミッション。空ならシーンから探す")]
        private MissionRunner _missionRunner;

        [SerializeField, Tooltip("プレイヤーと HUD を作る GameBootstrap。空ならシーンから探す")]
        private GameBootstrap _bootstrap;

        [SerializeField, Min(0f), Tooltip("プレイヤーが倒れてからシーンを読み直すまでの秒数")]
        private float _reloadDelaySeconds = DefaultReloadDelaySeconds;

        [Header("ルール")]
        [SerializeField, Tooltip("-difficulty が無いときのルール。空なら GameRules.Default を使い警告を出す。シーン生成スクリプトが CampaignNormal を配線する")]
        private GameRulesDefinition _rules;

        [SerializeField, Tooltip("-difficulty easy のときのルール (CampaignEasy)")]
        private GameRulesDefinition _easyRules;

        [SerializeField, Tooltip("-difficulty normal のときのルール (CampaignNormal)")]
        private GameRulesDefinition _normalRules;

        [SerializeField, Tooltip("-difficulty hard のときのルール (CampaignHard)")]
        private GameRulesDefinition _hardRules;

        private IReadOnlyList<string> _launchArgs;
        private GameRules _activeRules;
        private ISaveStorage _storage;
        private Action _sceneReloader;
        private Mission _mission;
        private string _missionId;
        private Damageable _player;
        private WeaponController _weapon;
        private string _lastCheckpointId = string.Empty;

        // シーンを読み直すと CheckpointTrigger は未通過に戻るので、通ったチェックポイントの Id は読み直しをまたいで覚えておく。
        // 復元できなかったとき (最初から始めるとき) と達成でセーブを消すときに空にする。
        private static readonly System.Collections.Generic.HashSet<string> _passedCheckpointIds =
            new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

        private bool _reloadPending;
        private float _reloadRemaining;

        private readonly CampaignNotice _notice = new CampaignNotice();
        private Hud _hud;

        // Start の時点でプレイヤーがまだ無かったときに、Built を待っている GameBootstrap。
        private GameBootstrap _awaitedBootstrap;

        /// <summary>セーブを置く場所。null なら Start で <see cref="FileSaveStorage.CreateDefault"/> を使う。開始後は差し替えない。</summary>
        public ISaveStorage Storage
        {
            get => _storage;
            set => _storage = value;
        }

        /// <summary>シーンを読み直す処理。null なら今のシーンを Build Settings の番号で読み直す。</summary>
        public Action SceneReloader
        {
            get => _sceneReloader;
            set => _sceneReloader = value;
        }

        /// <summary>Start で使う MissionRunner。null ならシーンから探す。</summary>
        public MissionRunner Runner
        {
            get => _missionRunner;
            set => _missionRunner = value;
        }

        /// <summary>Start でプレイヤーと HUD を受け取る GameBootstrap。null ならシーンから探す。</summary>
        public GameBootstrap Bootstrap
        {
            get => _bootstrap;
            set => _bootstrap = value;
        }

        /// <summary><c>-difficulty</c> が無いときに使うルール。null なら <see cref="GameRules.Default"/> を使い、開始時に警告を出す。</summary>
        public GameRulesDefinition RulesDefinition
        {
            get => _rules;
            set => _rules = value;
        }

        /// <summary><c>-difficulty easy</c> のときに使うルール。</summary>
        public GameRulesDefinition EasyRules
        {
            get => _easyRules;
            set => _easyRules = value;
        }

        /// <summary><c>-difficulty normal</c> のときに使うルール。</summary>
        public GameRulesDefinition NormalRules
        {
            get => _normalRules;
            set => _normalRules = value;
        }

        /// <summary><c>-difficulty hard</c> のときに使うルール。</summary>
        public GameRulesDefinition HardRules
        {
            get => _hardRules;
            set => _hardRules = value;
        }

        /// <summary>
        /// <c>-difficulty</c> を探すコマンドライン引数。null なら <see cref="Environment.GetCommandLineArgs"/>。テストで差し替える。
        /// 設定すると写しを持つ。
        /// </summary>
        public IReadOnlyList<string> LaunchArgs
        {
            get => _launchArgs;
            set => _launchArgs = value != null ? new List<string>(value) : null;
        }

        /// <summary>開始時にプレイヤーへ効かせたルール。開始前か、ルールを渡さずに <see cref="Begin(MissionRunner, Damageable, WeaponController, ISaveStorage, Action)"/> したときは null。</summary>
        public GameRules Rules => _activeRules;

        /// <summary>difficulty のときに使うルールの定義。配線されていなければ null。</summary>
        public GameRulesDefinition RulesFor(CampaignDifficulty difficulty)
        {
            switch (difficulty)
            {
                case CampaignDifficulty.Easy:
                    return _easyRules;
                case CampaignDifficulty.Normal:
                    return _normalRules;
                case CampaignDifficulty.Hard:
                    return _hardRules;
                default:
                    throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "unknown difficulty");
            }
        }

        /// <summary>HUD に一時的に出す案内 (セーブした・再開まで N 秒)。</summary>
        public CampaignNotice Notice => _notice;

        /// <summary>案内を出している HUD。HUD が無い (専用サーバーなど) か開始前なら null。</summary>
        public Hud BoundHud => _hud;

        /// <summary>プレイヤーが倒れてからシーンを読み直すまでの秒数。0 以上。</summary>
        public float ReloadDelaySeconds
        {
            get => _reloadDelaySeconds;
            set
            {
                if (!(value >= 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "ReloadDelaySeconds must be a non-negative finite number");
                }

                _reloadDelaySeconds = value;
            }
        }

        public bool IsStarted => _mission != null;

        /// <summary>セーブとの照合に使うミッションの Id (MissionDefinition のアセット名)。開始前は null。</summary>
        public string MissionId => _missionId;

        /// <summary>最後にセーブした (または再開した) チェックポイントの Id。無ければ空文字。</summary>
        public string LastCheckpointId => _lastCheckpointId;

        /// <summary>プレイヤーが倒れ、シーンの読み直しを待っていれば true。</summary>
        public bool IsReloadPending => _reloadPending;

        /// <summary>プレイヤーがまだ無く、<see cref="GameBootstrap.Built"/> を待っていれば true。</summary>
        public bool IsWaitingForBootstrap => _awaitedBootstrap != null;

        private void Start()
        {
            if (!BeginFromSceneOrWait())
            {
                enabled = false;
            }
        }

        private void Update()
        {
            if (IsStarted)
            {
                Tick(Time.deltaTime);
            }
        }

        private void OnDestroy()
        {
            StopWaitingForBootstrap();
            Stop();
        }

        /// <summary>
        /// <see cref="Bootstrap"/> (null ならシーンの GameBootstrap) がプレイヤーを作り終えていれば <see cref="TryBeginWith"/> で始め、
        /// まだなら <see cref="GameBootstrap.Built"/> を購読して、作り終えたときに始める。Start から呼ぶ。
        /// 既に始まっているか待っていれば何もせず true。GameBootstrap が無い・始められないときは理由を Debug.LogError に出して false。
        /// </summary>
        public bool BeginFromSceneOrWait()
        {
            if (IsStarted || IsWaitingForBootstrap)
            {
                return true;
            }

            var bootstrap = ResolveBootstrap();
            if (bootstrap == null)
            {
                return false;
            }

            if (bootstrap.World != null)
            {
                return TryBeginWith(bootstrap.World);
            }

            _awaitedBootstrap = bootstrap;
            bootstrap.Built += OnBootstrapBuilt;
            Debug.LogWarning(
                $"{LogTag} GameBootstrap '{bootstrap.name}' がまだプレイヤーを生成していないため、生成し終えてから始めます (生成に失敗していれば上のエラーを確認してください)。",
                this);
            return true;
        }

        /// <summary>
        /// シーンの MissionRunner と <see cref="GameBootstrap"/> のプレイヤーを拾って <see cref="Begin"/> する。
        /// 既に始まっていれば何もせず true。GameBootstrap がまだプレイヤーを作っていない・始められないときは
        /// 理由を Debug.LogError に出して false を返す (作り終えるのを待つなら <see cref="BeginFromSceneOrWait"/> を使う)。
        /// </summary>
        public bool TryBeginFromScene()
        {
            if (IsStarted)
            {
                return true;
            }

            var bootstrap = ResolveBootstrap();
            if (bootstrap == null)
            {
                return false;
            }

            if (bootstrap.World == null)
            {
                Debug.LogError(
                    $"{LogTag} GameBootstrap '{bootstrap.name}' がプレイヤーを生成していません。上のエラーを確認してください。",
                    this);
                return false;
            }

            return TryBeginWith(bootstrap.World);
        }

        /// <summary>
        /// world (GameBootstrap の生成物) のプレイヤーと、<see cref="Runner"/> (null ならシーンの MissionRunner) で <see cref="Begin"/> し、
        /// world に HUD があれば <see cref="Notice"/> を出させる。セーブの置き場所は <see cref="Storage"/>、無ければ
        /// <see cref="FileSaveStorage.CreateDefault"/> のスロット <see cref="SaveSlot"/>。ルールは <see cref="TryResolveRules"/> で決める。
        /// 既に始まっていれば何もせず true。始められなければ理由を Debug.LogError に出して false を返す。
        /// </summary>
        public bool TryBeginWith(BootstrapWorld world)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (IsStarted)
            {
                return true;
            }

            var runner = _missionRunner != null ? _missionRunner : FindAnyObjectByType<MissionRunner>();
            if (runner == null)
            {
                Debug.LogError($"{LogTag} シーンに MissionRunner がありません。", this);
                return false;
            }

            if (!TryResolveRules(out var rules))
            {
                return false;
            }

            ISaveStorage storage;
            try
            {
                storage = _storage ?? FileSaveStorage.CreateDefault();
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{LogTag} セーブの置き場所を決められません: {e.Message}", this);
                return false;
            }

            try
            {
                Begin(runner, world.PlayerDamageable, world.Weapon, storage, _sceneReloader ?? ReloadActiveScene, rules);
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                Debug.LogError($"{LogTag} キャンペーンを始められません: {e.Message}", this);
                return false;
            }

            BindHud(world.Hud);
            return true;
        }

        /// <summary>
        /// 開始時に使うルールを決める。<see cref="LaunchArgs"/> に <c>-difficulty</c> があればその難易度のルール
        /// (<see cref="RulesFor"/>)、無ければ <see cref="RulesDefinition"/>。
        /// <list type="bullet">
        /// <item>引数が読めない (値が無い・知らない難易度など) ときは警告を出し、<c>-difficulty</c> が無いものとして扱う。</item>
        /// <item>選んだ難易度のルールが配線されていないときはエラーを出し、<see cref="RulesDefinition"/> を使う。</item>
        /// <item>使うルールが無ければ警告を出して <see cref="GameRules.Default"/>。</item>
        /// </list>
        /// 使うルールの値が不正なら理由を Debug.LogError に出して false (rules は null)。
        /// </summary>
        public bool TryResolveRules(out GameRules rules)
        {
            var args = _launchArgs ?? Environment.GetCommandLineArgs();
            if (!CampaignLaunchOptions.TryParse(args, out var options, out var parseError))
            {
                Debug.LogWarning($"{LogTag} コマンドライン引数を無視します ({parseError})。シーンに配線したルールを使います。", this);
                options = CampaignLaunchOptions.None;
            }

            var definition = _rules;
            if (options.Difficulty.HasValue)
            {
                var chosen = RulesFor(options.Difficulty.Value);
                if (chosen != null)
                {
                    definition = chosen;
                }
                else
                {
                    Debug.LogError(
                        $"{LogTag} {CampaignLaunchOptions.DifficultyFlag} {options} のルールが '{name}' に配線されていないため、既定のルールを使います。シーン生成スクリプトで配線してください。",
                        this);
                }
            }

            string source;
            if (definition == null)
            {
                Debug.LogWarning(
                    $"{LogTag} '{name}' に GameRulesDefinition が設定されていないため GameRules.Default を使います。シーン生成スクリプトでルールを配線してください。",
                    this);
                rules = GameRules.Default;
                source = "(default)";
            }
            else if (definition.TryToRules(out rules, out var ruleError))
            {
                source = definition.name;
            }
            else
            {
                Debug.LogError($"{LogTag} キャンペーンを始められません: {ruleError}", this);
                return false;
            }

            Debug.Log(
                $"{LogTag} rules={source} difficulty={options} regen={rules.RegenPerSecond.ToString("0.##", CultureInfo.InvariantCulture)}/s after {rules.RegenDelaySeconds.ToString("0.##", CultureInfo.InvariantCulture)}s reserve=x{rules.ReserveAmmoMultiplier.ToString("0.##", CultureInfo.InvariantCulture)} infinite={rules.InfiniteReserve}",
                this);
            return true;
        }

        /// <summary>
        /// <see cref="Notice"/> を出す HUD を差し替える。null なら出さない。前の HUD からは外す。
        /// <see cref="TryBeginWith"/> が GameBootstrap の HUD で呼ぶ。<see cref="Begin"/> を直接呼んだときは自分で呼ぶ。
        /// </summary>
        public void BindHud(Hud hud)
        {
            if (_hud != null && _hud != hud)
            {
                _hud.BindNotice(null);
            }

            _hud = hud;
            if (_hud != null)
            {
                _hud.BindNotice(_notice);
            }
        }

        /// <summary>
        /// プレイヤーの死亡とミッションの達成を購読し、storage にセーブがあれば戻す。
        /// missionRunner はまだなら始める。始められない・参照が足りないときは例外を投げ、何も購読しない。
        /// </summary>
        /// <param name="missionRunner">セーブするミッション。</param>
        /// <param name="player">プレイヤーの体力。この GameObject ごとチェックポイントへ移す。</param>
        /// <param name="weapon">プレイヤーの武器。初期化済みであること。</param>
        /// <param name="storage">セーブの置き場所。</param>
        /// <param name="sceneReloader">プレイヤーが倒れたあとにシーンを読み直す処理。</param>
        /// <remarks>ルールは効かせ直さず、プレイヤーの今の自動回復と武器のままにする。</remarks>
        public void Begin(
            MissionRunner missionRunner,
            Damageable player,
            WeaponController weapon,
            ISaveStorage storage,
            Action sceneReloader)
        {
            Begin(missionRunner, player, weapon, storage, sceneReloader, null);
        }

        /// <summary>
        /// <see cref="Begin(MissionRunner, Damageable, WeaponController, ISaveStorage, Action)"/> と同じだが、セーブを戻す前に rules を
        /// プレイヤーに効かせる。武器は rules の予備弾で満タンに作り直し (<see cref="WeaponController.ApplyRules"/>)、
        /// プレイヤーの <see cref="HealthRegen"/> (無ければ付ける) とアクション (<see cref="PlayerActions.Attach"/>)、落下ダメージ (<see cref="PlayerController.ApplyRules"/>) を rules で付け直す。セーブがあればその後で弾数を戻すので、
        /// セーブの予備弾には倍率を掛け直さない。rules が null ならルールは効かせ直さない。
        /// </summary>
        public void Begin(
            MissionRunner missionRunner,
            Damageable player,
            WeaponController weapon,
            ISaveStorage storage,
            Action sceneReloader,
            GameRules rules)
        {
            if (missionRunner == null)
            {
                throw new ArgumentNullException(nameof(missionRunner));
            }

            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            if (storage == null)
            {
                throw new ArgumentNullException(nameof(storage));
            }

            if (sceneReloader == null)
            {
                throw new ArgumentNullException(nameof(sceneReloader));
            }

            if (IsStarted)
            {
                throw new InvalidOperationException($"CampaignRunner '{name}' has already started");
            }

            if (!weapon.IsInitialized)
            {
                throw new ArgumentException($"WeaponController '{weapon.name}' is not initialized", nameof(weapon));
            }

            if (!missionRunner.IsStarted && !missionRunner.TryStart())
            {
                throw new ArgumentException($"MissionRunner '{missionRunner.name}' cannot start its mission", nameof(missionRunner));
            }

            if (rules != null)
            {
                ApplyRules(player, weapon, rules);
                ApplyDeathBody(rules);
            }

            _activeRules = rules;
            _mission = missionRunner.Mission;
            _missionId = missionRunner.Definition != null ? missionRunner.Definition.name : string.Empty;
            _player = player;
            _weapon = weapon;
            _storage = storage;
            _sceneReloader = sceneReloader;
            _lastCheckpointId = string.Empty;
            _reloadPending = false;
            _reloadRemaining = 0f;
            _notice.Clear();

            _player.Died += OnPlayerDied;
            _mission.MissionCompleted += OnMissionCompleted;

            Debug.Log($"{LogTag} started mission={_missionId}", this);
            if (!TryRestore())
            {
                _passedCheckpointIds.Clear();
            }
        }

        /// <summary>
        /// 今の状態をチェックポイント checkpointId でセーブし、できたら true。
        /// 開始前、ミッションの終了後、プレイヤーが倒れた後、書き込みに失敗したときは理由をログに出して false。
        /// </summary>
        public bool SaveAt(string checkpointId)
        {
            if (!IsStarted)
            {
                Debug.LogError($"{LogTag} '{name}' がまだ始まっていないため、チェックポイント '{checkpointId}' でセーブできません。", this);
                return false;
            }

            if (string.IsNullOrWhiteSpace(checkpointId))
            {
                Debug.LogError($"{LogTag} チェックポイントの Id が空のためセーブできません。", this);
                return false;
            }

            if (_mission.IsFinished)
            {
                Debug.Log($"{LogTag} skipped save checkpoint={checkpointId} reason=mission {_mission.State}", this);
                return false;
            }

            if (_reloadPending || _player.IsDead)
            {
                Debug.Log($"{LogTag} skipped save checkpoint={checkpointId} reason=player dead", this);
                return false;
            }

            var save = new CampaignSave(
                _missionId,
                checkpointId,
                _player.Health.Current,
                _weapon.SnapshotAmmo(),
                _weapon.Weapon.Spec.Id,
                _mission.Snapshot());

            try
            {
                _storage.Write(SaveSlot, save.ToJson());
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"{LogTag} failed to save checkpoint={checkpointId}: {e.Message}", this);
                return false;
            }

            _lastCheckpointId = checkpointId;
            _passedCheckpointIds.Add(checkpointId);
            _notice.ShowSaved(SavedNoticeSeconds);
            Debug.Log(
                $"{LogTag} saved mission={_missionId} checkpoint={checkpointId} health={save.Health} equipped={save.EquippedWeaponId} ammo={FormatAmmo(save.Weapons)}",
                this);
            return true;
        }

        /// <summary>
        /// シーンの読み直しまでの時間と、HUD の案内の残り時間を deltaSeconds 進める。読み直しを待つ間は
        /// 「チェックポイントから再開まで N 秒」を更新する。Play 中は Update から呼ぶ。Edit Mode のテストでは直接呼ぶ。
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            _notice.Tick(deltaSeconds);
            if (!_reloadPending)
            {
                return;
            }

            _reloadRemaining -= deltaSeconds;
            if (_reloadRemaining > 0f)
            {
                _notice.ShowRestartCountdown(_reloadRemaining);
                return;
            }

            _reloadPending = false;
            _reloadRemaining = 0f;
            _notice.Clear();
            Debug.Log($"{LogTag} reloading scene", this);
            _sceneReloader();
        }

        /// <summary>購読を解除してミッションから離れ、HUD の案内を外す。破棄時にも呼ばれる。</summary>
        public void Stop()
        {
            BindHud(null);
            _notice.Clear();

            if (_player != null)
            {
                _player.Died -= OnPlayerDied;
            }

            if (_mission != null)
            {
                _mission.MissionCompleted -= OnMissionCompleted;
            }

            _mission = null;
            _missionId = null;
            _player = null;
            _weapon = null;
            _activeRules = null;
            _reloadPending = false;
            _reloadRemaining = 0f;
        }

        private static void ApplyRules(Damageable player, WeaponController weapon, GameRules rules)
        {
            weapon.ApplyRules(rules);

            var regen = player.GetComponent<HealthRegen>();
            if (regen == null)
            {
                regen = player.gameObject.AddComponent<HealthRegen>();
            }

            regen.Bind(player, rules);
            PlayerActions.Attach(player.gameObject, rules);

            // Begin は Damageable しか求めていないので、PlayerController の無いプレイヤーも渡され得る
            var mover = player.GetComponent<PlayerController>();
            if (mover != null)
            {
                mover.ApplyRules(rules);
            }
        }

        /// <summary>
        /// 読み込んだシーンの敵 (<see cref="EnemyAgent"/>) と標的ダミー (<see cref="TargetDummy"/>) のやられ演出を rules の
        /// <see cref="GameRules.DeathBody"/> にする (<see cref="DeathBodyPresenter"/>)。GameBootstrap が付けた標的ダミーの設定は、難易度のルールで上書きする。
        /// </summary>
        private void ApplyDeathBody(GameRules rules)
        {
            var targets = new List<Damageable>();
            foreach (var enemy in FindObjectsByType<EnemyAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var damageable = enemy.GetComponent<Damageable>();
                if (damageable != null)
                {
                    targets.Add(damageable);
                }
            }

            foreach (var dummy in FindObjectsByType<TargetDummy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (dummy.Damageable != null)
                {
                    targets.Add(dummy.Damageable);
                }
            }

            var bound = DeathBodyPresenter.BindAll(targets, rules.DeathBody);
            Debug.Log($"{LogTag} deathBody={rules.DeathBody} targets={bound.ToString(CultureInfo.InvariantCulture)}", this);
        }

        /// <summary>セーブがあれば今のシーンに戻す。戻せたら true。戻せないときは理由をログに出し、最初からの状態のままにする。</summary>
        private bool TryRestore()
        {
            if (!_storage.TryRead(SaveSlot, out var text))
            {
                Debug.Log($"{LogTag} no save; starting mission={_missionId} from the beginning", this);
                return false;
            }

            CampaignSave save;
            try
            {
                save = CampaignSave.FromJson(text);
            }
            catch (FormatException e)
            {
                Debug.LogWarning($"{LogTag} ignored a broken save; starting mission={_missionId} from the beginning: {e.Message}", this);
                return false;
            }

            if (!string.Equals(save.MissionId, _missionId, StringComparison.Ordinal))
            {
                Debug.LogWarning(
                    $"{LogTag} ignored the save of mission={save.MissionId}; starting mission={_missionId} from the beginning",
                    this);
                return false;
            }

            // 目標の定義が変わっていると戻せない。Restore は失敗したらミッションを変えないので、ここで止めれば最初から始められる。
            try
            {
                _mission.Restore(save.Objectives);
            }
            catch (ArgumentException e)
            {
                Debug.LogWarning(
                    $"{LogTag} ignored a save that does not match mission={_missionId}; starting from the beginning: {e.Message}",
                    this);
                return false;
            }

            MovePlayerTo(save.CheckpointId);
            _player.RestoreHealth(save.Health);
            _weapon.RestoreLoadout(save.Weapons, save.EquippedWeaponId);
            _lastCheckpointId = save.CheckpointId;

            Debug.Log(
                $"{LogTag} restored mission={_missionId} checkpoint={save.CheckpointId} health={_player.Health.Current} equipped={_weapon.Weapon.Spec.Id} ammo={FormatAmmo(_weapon.SnapshotAmmo())} state={_mission.State}",
                this);

            // 達成したらセーブを消すので普通は起きないが、消す前に落ちたセーブから戻ったときはここで片付ける。
            if (_mission.State == MissionState.Completed)
            {
                DeleteSave();
            }

            return true;
        }

        /// <summary>ログ用の「Id:弾倉/予備」をカンマで並べた文字列。</summary>
        private static string FormatAmmo(System.Collections.Generic.IReadOnlyList<WeaponAmmo> weapons)
        {
            var parts = new string[weapons.Count];
            for (var i = 0; i < parts.Length; i++)
            {
                var weapon = weapons[i];
                parts[i] = weapon.WeaponId + ":" + weapon.AmmoInMagazine.ToString(CultureInfo.InvariantCulture) + "/"
                    + weapon.ReserveAmmo.ToString(CultureInfo.InvariantCulture);
            }

            return string.Join(",", parts);
        }

        private void MovePlayerTo(string checkpointId)
        {
            if (string.IsNullOrEmpty(checkpointId))
            {
                return;
            }

            var checkpoint = CheckpointTrigger.Find(checkpointId);
            if (checkpoint == null)
            {
                Debug.LogWarning($"{LogTag} checkpoint={checkpointId} is not in the scene; the player stays at the spawn point", this);
                return;
            }

            checkpoint.MarkReached();
            _passedCheckpointIds.Add(checkpointId);
            foreach (var passedId in _passedCheckpointIds)
            {
                CheckpointTrigger.Find(passedId)?.MarkReached();
            }

            // CharacterController は有効なままだと Transform の書き換えを次の移動で上書きするので、移す間だけ切る。
            var controller = _player.GetComponent<CharacterController>();
            var wasEnabled = controller != null && controller.enabled;
            if (wasEnabled)
            {
                controller.enabled = false;
            }

            _player.transform.position = checkpoint.RespawnPosition;

            if (wasEnabled)
            {
                controller.enabled = true;
            }
        }

        private void OnPlayerDied(Damageable _)
        {
            if (_reloadPending)
            {
                return;
            }

            _reloadPending = true;
            _reloadRemaining = _reloadDelaySeconds;
            _notice.ShowRestartCountdown(_reloadRemaining);
            Debug.Log(
                $"{LogTag} player died; reloading in {_reloadDelaySeconds.ToString("0.##", CultureInfo.InvariantCulture)}s from checkpoint={(_lastCheckpointId.Length > 0 ? _lastCheckpointId : "(start)")}",
                this);
        }

        private GameBootstrap ResolveBootstrap()
        {
            var bootstrap = _bootstrap != null ? _bootstrap : FindAnyObjectByType<GameBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError($"{LogTag} シーンに GameBootstrap がありません。", this);
            }

            return bootstrap;
        }

        private void OnBootstrapBuilt(BootstrapWorld world)
        {
            StopWaitingForBootstrap();
            if (!TryBeginWith(world))
            {
                enabled = false;
            }
        }

        private void StopWaitingForBootstrap()
        {
            if (_awaitedBootstrap != null)
            {
                _awaitedBootstrap.Built -= OnBootstrapBuilt;
            }

            _awaitedBootstrap = null;
        }

        private void OnMissionCompleted()
        {
            Debug.Log($"{LogTag} mission={_missionId} completed", this);
            DeleteSave();
        }

        private void DeleteSave()
        {
            _passedCheckpointIds.Clear();
            try
            {
                _storage.Delete(SaveSlot);
                Debug.Log($"{LogTag} deleted save slot={SaveSlot}", this);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"{LogTag} failed to delete save slot={SaveSlot}: {e.Message}", this);
            }
        }

        private void ReloadActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex < 0)
            {
                Debug.LogError(
                    $"{LogTag} シーン '{scene.path}' が Build Settings にないため再読み込みできません。",
                    this);
                return;
            }

            SceneManager.LoadScene(scene.buildIndex);
        }
    }
}
