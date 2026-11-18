using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Shooter.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Runtime
{
    // トリガーより先にミッションを戻すため、既定の実行順より前に Start を走らせる。
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class CampaignRunner : MonoBehaviour
    {
        public const string LogTag = "[Campaign]";

        [SerializeField, Tooltip("セーブするミッション。空ならシーンから探す")]
        private MissionRunner _missionRunner;

        [SerializeField, Tooltip("プレイヤーと HUD を作る GameBootstrap。空ならシーンから探す")]
        private GameBootstrap _bootstrap;

        [SerializeField, Tooltip("倒れてから読み直すまでとセーブの案内の秒数を読むミッションの一覧。空なら入口 (GameContent) の一覧、それも無ければ MissionCatalog の既定の秒数")]
        private MissionCatalog _missions;

        // 一覧 (共有アセット) の値を書き換えると他のミッションにも効くので、この CampaignRunner だけ変える値は別に持つ。
        private float? _reloadDelayOverride;

        [Header("ルール")]
        [SerializeField, Tooltip("難易度を選ばずに始めたときのルール。空なら入口 (GameContent) のルール、それも無ければ GameRules.Default を使い警告を出す。シーン生成スクリプトが CampaignNormal を配線する")]
        private GameRulesDefinition _rules;

        [SerializeField, Tooltip("Easy を選んだとき (メニューか -difficulty easy) のルール (CampaignEasy)")]
        private GameRulesDefinition _easyRules;

        [SerializeField, Tooltip("Normal を選んだとき (メニューか -difficulty normal) のルール (CampaignNormal)")]
        private GameRulesDefinition _normalRules;

        [SerializeField, Tooltip("Hard を選んだとき (メニューか -difficulty hard) のルール (CampaignHard)")]
        private GameRulesDefinition _hardRules;

        private IReadOnlyList<string> _launchArgs;
        private GameRules _activeRules;
        private ISaveStorage _storage;
        private Action _sceneReloader;
        private Mission _mission;
        private string _missionId;
        private string _saveSlot;
        private Damageable _player;
        private WeaponController _weapon;
        private string _lastCheckpointId = string.Empty;

        // シーンを読み直すと CheckpointTrigger は未通過に戻るので、通ったチェックポイントの Id は読み直しをまたいで覚えておく。
        private static readonly System.Collections.Generic.HashSet<string> _passedCheckpointIds =
            new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

        private bool _reloadPending;
        private float _reloadRemaining;

        private readonly CampaignNotice _notice = new CampaignNotice();
        private HudDensity? _publishedDensity;

        private GameBootstrap _awaitedBootstrap;

        public ISaveStorage Storage
        {
            get => _storage;
            set => _storage = value;
        }

        public Action SceneReloader
        {
            get => _sceneReloader;
            set => _sceneReloader = value;
        }

        public MissionRunner Runner
        {
            get => _missionRunner;
            set => _missionRunner = value;
        }

        public GameBootstrap Bootstrap
        {
            get => _bootstrap;
            set => _bootstrap = value;
        }

        public GameRulesDefinition RulesDefinition
        {
            get => _rules;
            set => _rules = value;
        }

        public GameRulesDefinition EasyRules
        {
            get => _easyRules;
            set => _easyRules = value;
        }

        public GameRulesDefinition NormalRules
        {
            get => _normalRules;
            set => _normalRules = value;
        }

        public GameRulesDefinition HardRules
        {
            get => _hardRules;
            set => _hardRules = value;
        }

        public IReadOnlyList<string> LaunchArgs
        {
            get => _launchArgs;
            set => _launchArgs = value != null ? new List<string>(value) : null;
        }

        public GameRules Rules => _activeRules;

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

        public CampaignNotice Notice => _notice;

        public HudDensity? PublishedHudDensity => _publishedDensity;

        public MissionCatalog Missions
        {
            get => _missions;
            set => _missions = value;
        }

        public MissionCatalog EffectiveMissions => GameContent.Pick(_missions, c => c.Missions);

        public float ReloadDelaySeconds
        {
            get
            {
                if (_reloadDelayOverride.HasValue)
                {
                    return _reloadDelayOverride.Value;
                }

                var missions = EffectiveMissions;
                return missions != null ? missions.ReloadDelaySeconds : MissionCatalog.DefaultReloadDelaySeconds;
            }
            set
            {
                if (!MissionCatalog.IsValidSeconds(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "ReloadDelaySeconds must be a non-negative finite number");
                }

                _reloadDelayOverride = value;
            }
        }

        public float SavedNoticeSeconds
        {
            get
            {
                var missions = EffectiveMissions;
                return missions != null ? missions.SavedNoticeSeconds : MissionCatalog.DefaultSavedNoticeSeconds;
            }
        }

        public bool IsStarted => _mission != null;

        public string MissionId => _missionId;

        public string SaveSlot => _saveSlot;

        public string LastCheckpointId => _lastCheckpointId;

        public bool IsReloadPending => _reloadPending;

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

            if (!TryResolveRules(runner.Definition != null ? runner.Definition.Id : string.Empty, out var rules)
                || !TryApplyMissionRules(runner.Definition, ref rules))
            {
                return false;
            }

            ISaveStorage storage;
            try
            {
                storage = _storage ?? CloudSaveBootstrap.Storage;
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

            return true;
        }

        public bool TryResolveRules(string missionId, out GameRules rules)
        {
            CampaignDifficulty? difficulty = null;
            string difficultySource = null;

            var launch = CampaignLaunchSelection.Current;
            if (launch != null)
            {
                if (string.Equals(launch.MissionId, missionId, StringComparison.Ordinal))
                {
                    difficulty = launch.Difficulty;
                    difficultySource = "CampaignLaunch";
                    Debug.Log($"{LogTag} launch mission={launch.MissionId} difficulty={CampaignLaunchOptions.DifficultyName(launch.Difficulty)}", this);
                }
                else
                {
                    Debug.LogWarning(
                        $"{LogTag} メニューで選んだミッション '{launch.MissionId}' がこのシーンのミッション '{missionId}' と違うため、選んだ難易度を使いません。",
                        this);
                }
            }

            if (!difficulty.HasValue)
            {
                var args = _launchArgs ?? Environment.GetCommandLineArgs();
                if (!CampaignLaunchOptions.TryParse(args, out var options, out var parseError))
                {
                    Debug.LogWarning($"{LogTag} コマンドライン引数を無視します ({parseError})。シーンに配線したルールを使います。", this);
                    options = CampaignLaunchOptions.None;
                }

                difficulty = options.Difficulty;
                difficultySource = CampaignLaunchOptions.DifficultyFlag;
            }

            var definition = GameContent.Pick(_rules, c => c.Rules);
            if (difficulty.HasValue)
            {
                var chosen = RulesFor(difficulty.Value);
                if (chosen != null)
                {
                    definition = chosen;
                }
                else
                {
                    Debug.LogError(
                        $"{LogTag} {difficultySource} {CampaignLaunchOptions.DifficultyName(difficulty.Value)} のルールが '{name}' に配線されていないため、既定のルールを使います。シーン生成スクリプトで配線してください。",
                        this);
                }
            }

            var difficultyName = difficulty.HasValue ? CampaignLaunchOptions.DifficultyName(difficulty.Value) : "(none)";

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
                $"{LogTag} rules={source} difficulty={difficultyName} regen={rules.RegenPerSecond.ToString("0.##", CultureInfo.InvariantCulture)}/s after {rules.RegenDelaySeconds.ToString("0.##", CultureInfo.InvariantCulture)}s reserve=x{rules.ReserveAmmoMultiplier.ToString("0.##", CultureInfo.InvariantCulture)} infinite={rules.InfiniteReserve}",
                this);
            return true;
        }

        public bool TryApplyMissionRules(MissionDefinition mission, ref GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (mission == null || string.IsNullOrWhiteSpace(mission.RulesOverride))
            {
                return true;
            }

            GameRules overridden;
            try
            {
                overridden = mission.ApplyRulesOverride(rules);
            }
            catch (MissionDefinitionException e)
            {
                Debug.LogError($"{LogTag} キャンペーンを始められません: {e.Message}", this);
                return false;
            }

            Debug.Log($"{LogTag} mission={mission.Id} rules override view={overridden.ViewMode}", this);
            rules = overridden;
            return true;
        }

        public void Begin(
            MissionRunner missionRunner,
            Damageable player,
            WeaponController weapon,
            ISaveStorage storage,
            Action sceneReloader)
        {
            Begin(missionRunner, player, weapon, storage, sceneReloader, null);
        }

        // rules を効かせてからセーブを戻すのは、満タンに作り直した弾数をセーブの弾数で上書きし、セーブの予備弾に倍率を掛け直さないため。
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

            var missionId = missionRunner.Definition != null ? missionRunner.Definition.Id : string.Empty;
            if (!CampaignLaunch.IsValidMissionId(missionId))
            {
                throw new ArgumentException(
                    $"MissionRunner '{missionRunner.name}' has mission id '{missionId}', which cannot name a save slot "
                    + $"(set the MissionDefinition's id to 1-{CampaignLaunch.MaxMissionIdLength} letters, digits, '_' or '-')",
                    nameof(missionRunner));
            }

            if (rules != null)
            {
                ApplyRules(player, weapon, rules);
                ApplyDeathBody(rules);
            }

            _activeRules = rules;
            // 難易度のルールは GameBootstrap のルールと別のことがあるので、HUD の量も始めたときのルールで出し直す
            if (rules != null)
            {
                _publishedDensity = rules.HudDensity;
                GameSignals.HudDensity.Publish(_publishedDensity);
            }

            _mission = missionRunner.Mission;
            _missionId = missionId;
            _saveSlot = CampaignSaveSlots.For(missionId);
            _player = player;
            _weapon = weapon;
            _storage = storage;
            _sceneReloader = sceneReloader;
            _lastCheckpointId = string.Empty;
            _reloadPending = false;
            _reloadRemaining = 0f;
            _notice.Clear();
            GameSignals.CampaignNotice.Publish(_notice);

            _player.Died += OnPlayerDied;
            _mission.MissionCompleted += OnMissionCompleted;

            Debug.Log($"{LogTag} started mission={_missionId}", this);
            if (CampaignLaunchSelection.IgnoresSaves)
            {
                Debug.Log($"{LogTag} not launched from the menu; ignoring the save and starting mission={_missionId} from the beginning", this);
                _passedCheckpointIds.Clear();
            }
            else if (!TryRestore())
            {
                _passedCheckpointIds.Clear();
            }
        }

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
                _mission.Snapshot(),
                _weapon.SnapshotAttachments());

            try
            {
                _storage.Write(_saveSlot, save.ToJson());
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"{LogTag} failed to save checkpoint={checkpointId}: {e.Message}", this);
                return false;
            }

            _lastCheckpointId = checkpointId;
            _passedCheckpointIds.Add(checkpointId);
            CampaignLaunchSelection.MarkSaved();

            // セーブにグレネードの数を持たせないのは、チェックポイントで満タンに戻すので、再開したときの数 (満タン) と食い違わないため。
            var grenades = _player.GetComponent<GrenadeThrower>();
            if (grenades != null)
            {
                grenades.Refill();
            }

            _notice.ShowSaved(SavedNoticeSeconds);
            Debug.Log(
                $"{LogTag} saved mission={_missionId} checkpoint={checkpointId} health={save.Health} equipped={save.EquippedWeaponId} ammo={FormatAmmo(save.Weapons)}",
                this);
            return true;
        }

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

        public void Stop()
        {
            if (_publishedDensity.HasValue)
            {
                GameSignals.HudDensity.ClearIfCurrent(_publishedDensity);
                _publishedDensity = null;
            }

            _notice.Clear();
            GameSignals.CampaignNotice.ClearIfCurrent(_notice);

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
            _saveSlot = null;
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

            var rig = player.GetComponent<ViewRig>();
            if (rig != null)
            {
                ApplyView(rig, rules.ViewMode);
            }

            var deathCamera = player.GetComponent<PlayerDeathCamera>();
            if (deathCamera == null)
            {
                deathCamera = player.gameObject.AddComponent<PlayerDeathCamera>();
            }

            deathCamera.ApplyRules(rules);
            StreakController.Apply(player.gameObject, rules);
        }

        private static void ApplyView(ViewRig rig, ViewMode mode)
        {
            rig.Mode = mode;

            // Mode の setter は有効な間しかカメラと体を直さないので、Edit Mode やまだ有効になる前の rig でもここで揃える。
            if (rig.IsInitialized)
            {
                rig.ApplyBodyVisibility(rig.IsLocalView);
                rig.UpdateCameraPosition();
            }
        }

        // GameBootstrap が付けた標的ダミーの設定も、難易度のルールで上書きする。
        private void ApplyDeathBody(GameRules rules)
        {
            var targets = new List<Damageable>();
            foreach (var enemy in FindObjectsByType<EnemyAgent>(FindObjectsInactive.Include))
            {
                var damageable = enemy.GetComponent<Damageable>();
                if (damageable != null)
                {
                    targets.Add(damageable);
                }
            }

            foreach (var dummy in FindObjectsByType<TargetDummy>(FindObjectsInactive.Include))
            {
                if (dummy.Damageable != null)
                {
                    targets.Add(dummy.Damageable);
                }
            }

            var bound = DeathBodyPresenter.BindAll(targets, rules.DeathBody);
            Debug.Log($"{LogTag} deathBody={rules.DeathBody} targets={bound.ToString(CultureInfo.InvariantCulture)}", this);
        }

        private bool TryRestore()
        {
            var fromLegacy = false;
            if (!_storage.TryRead(_saveSlot, out var text))
            {
                if (!CampaignSaveSlots.ReadsLegacy(_missionId) || !_storage.TryRead(CampaignSaveSlots.Legacy, out text))
                {
                    Debug.Log($"{LogTag} no save; starting mission={_missionId} from the beginning", this);
                    return false;
                }

                fromLegacy = true;
                Debug.Log($"{LogTag} reading the legacy save slot={CampaignSaveSlots.Legacy} for mission={_missionId}", this);
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
            _weapon.RestoreLoadout(save.Weapons, save.EquippedWeaponId, save.Attachments);
            _lastCheckpointId = save.CheckpointId;

            Debug.Log(
                $"{LogTag} restored mission={_missionId} checkpoint={save.CheckpointId} health={_player.Health.Current} equipped={_weapon.Weapon.Spec.Id} ammo={FormatAmmo(_weapon.SnapshotAmmo())} state={_mission.State}",
                this);

            // 達成したらセーブを消すので普通は起きないが、消す前に落ちたセーブから戻ったときはここで片付ける。
            if (_mission.State == MissionState.Completed)
            {
                DeleteSave();
            }
            else if (fromLegacy)
            {
                MigrateLegacySave(text);
            }

            return true;
        }

        // 書けなければ古いスロットを消さずに残し、次も古いスロットから再開できるようにする。
        private void MigrateLegacySave(string text)
        {
            try
            {
                _storage.Write(_saveSlot, text);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError(
                    $"{LogTag} failed to move the legacy save slot={CampaignSaveSlots.Legacy} to slot={_saveSlot}; keeping the legacy slot: {e.Message}",
                    this);
                return;
            }

            if (TryDeleteSlot(CampaignSaveSlots.Legacy))
            {
                Debug.Log($"{LogTag} migrated save slot={CampaignSaveSlots.Legacy} to slot={_saveSlot}", this);
            }
        }

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

            var delay = ReloadDelaySeconds;
            _reloadPending = true;
            _reloadRemaining = delay;
            StopPlayerControl();
            _notice.ShowRestartCountdown(_reloadRemaining);
            Debug.Log(
                $"{LogTag} player died; reloading in {delay.ToString("0.##", CultureInfo.InvariantCulture)}s from checkpoint={(_lastCheckpointId.Length > 0 ? _lastCheckpointId : "(start)")}",
                this);
        }

        // 読み直しで作り直されるので戻す処理は要らない。
        // FirstPersonLook は止めるとカーソルのロックが外れるので、視点だけは動かせるままにする (DominationRunner と同じ)。
        private void StopPlayerControl()
        {
            // 武器を先に止める。止めると Reload アクションが無効に戻ることがあるので、他の入力より前にする。
            if (_weapon != null)
            {
                _weapon.enabled = false;
            }

            var mover = _player.GetComponent<PlayerController>();
            if (mover != null)
            {
                mover.enabled = false;
            }

            var grenades = _player.GetComponent<GrenadeThrower>();
            if (grenades != null)
            {
                grenades.enabled = false;
            }
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
            if (TryDeleteSlot(_saveSlot))
            {
                Debug.Log($"{LogTag} deleted save slot={_saveSlot}", this);
            }

            // 移す前に書けなかった古いスロットが残っていると、達成した後にまた途中から再開してしまう。
            if (CampaignSaveSlots.ReadsLegacy(_missionId))
            {
                TryDeleteSlot(CampaignSaveSlots.Legacy);
            }
        }

        private bool TryDeleteSlot(string slot)
        {
            try
            {
                _storage.Delete(slot);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"{LogTag} failed to delete save slot={slot}: {e.Message}", this);
                return false;
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
