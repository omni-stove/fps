using System;
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
    /// Start で <see cref="MissionRunner"/> (Inspector で指定しなければシーンから) と <see cref="GameBootstrap"/> が作ったプレイヤーを拾う。
    /// トリガーより先にミッションを戻すため、既定の実行順より前に Start を走らせる。
    /// セーブの場所 (<see cref="Storage"/>) とシーンの読み直し (<see cref="SceneReloader"/>) は差し替えられる。
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

        [SerializeField, Tooltip("セーブするミッション。空ならシーンから探す")]
        private MissionRunner _missionRunner;

        [SerializeField, Min(0f), Tooltip("プレイヤーが倒れてからシーンを読み直すまでの秒数")]
        private float _reloadDelaySeconds = DefaultReloadDelaySeconds;

        private ISaveStorage _storage;
        private Action _sceneReloader;
        private Mission _mission;
        private string _missionId;
        private Damageable _player;
        private WeaponController _weapon;
        private string _lastCheckpointId = string.Empty;
        private bool _reloadPending;
        private float _reloadRemaining;

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

        private void Start()
        {
            if (!IsStarted && !TryBeginFromScene())
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
            Stop();
        }

        /// <summary>
        /// シーンの MissionRunner と <see cref="GameBootstrap"/> のプレイヤーを拾って <see cref="Begin"/> する。
        /// 既に始まっていれば何もせず true。始められなければ理由を Debug.LogError に出して false を返す。
        /// </summary>
        public bool TryBeginFromScene()
        {
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

            var bootstrap = FindAnyObjectByType<GameBootstrap>();
            var world = bootstrap != null ? bootstrap.World : null;
            if (world == null)
            {
                Debug.LogError($"{LogTag} GameBootstrap がプレイヤーを生成していません。シーンに GameBootstrap があるか、上のエラーを確認してください。", this);
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
                Begin(runner, world.PlayerDamageable, world.Weapon, storage, _sceneReloader ?? ReloadActiveScene);
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                Debug.LogError($"{LogTag} キャンペーンを始められません: {e.Message}", this);
                return false;
            }

            return true;
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
        public void Begin(
            MissionRunner missionRunner,
            Damageable player,
            WeaponController weapon,
            ISaveStorage storage,
            Action sceneReloader)
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

            _mission = missionRunner.Mission;
            _missionId = missionRunner.Definition != null ? missionRunner.Definition.name : string.Empty;
            _player = player;
            _weapon = weapon;
            _storage = storage;
            _sceneReloader = sceneReloader;
            _lastCheckpointId = string.Empty;
            _reloadPending = false;
            _reloadRemaining = 0f;

            _player.Died += OnPlayerDied;
            _mission.MissionCompleted += OnMissionCompleted;

            Debug.Log($"{LogTag} started mission={_missionId}", this);
            TryRestore();
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

            var weapon = _weapon.Weapon;
            var save = new CampaignSave(
                _missionId,
                checkpointId,
                _player.Health.Current,
                weapon.AmmoInMagazine,
                weapon.ReserveAmmo,
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
            Debug.Log(
                $"{LogTag} saved mission={_missionId} checkpoint={checkpointId} health={save.Health} ammo={save.AmmoInMagazine}/{save.ReserveAmmo}",
                this);
            return true;
        }

        /// <summary>
        /// シーンの読み直しまでの時間を deltaSeconds 進める。Play 中は Update から呼ぶ。Edit Mode のテストでは直接呼ぶ。
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (!_reloadPending)
            {
                return;
            }

            _reloadRemaining -= deltaSeconds;
            if (_reloadRemaining > 0f)
            {
                return;
            }

            _reloadPending = false;
            _reloadRemaining = 0f;
            Debug.Log($"{LogTag} reloading scene", this);
            _sceneReloader();
        }

        /// <summary>購読を解除してミッションから離れる。破棄時にも呼ばれる。</summary>
        public void Stop()
        {
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
            _reloadPending = false;
            _reloadRemaining = 0f;
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
            _weapon.RestoreAmmo(save.AmmoInMagazine, save.ReserveAmmo);
            _lastCheckpointId = save.CheckpointId;

            Debug.Log(
                $"{LogTag} restored mission={_missionId} checkpoint={save.CheckpointId} health={_player.Health.Current} ammo={_weapon.Weapon.AmmoInMagazine}/{_weapon.Weapon.ReserveAmmo} state={_mission.State}",
                this);

            // 達成したらセーブを消すので普通は起きないが、消す前に落ちたセーブから戻ったときはここで片付ける。
            if (_mission.State == MissionState.Completed)
            {
                DeleteSave();
            }

            return true;
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
            Debug.Log(
                $"{LogTag} player died; reloading in {_reloadDelaySeconds.ToString("0.##", CultureInfo.InvariantCulture)}s from checkpoint={(_lastCheckpointId.Length > 0 ? _lastCheckpointId : "(start)")}",
                this);
        }

        private void OnMissionCompleted()
        {
            Debug.Log($"{LogTag} mission={_missionId} completed", this);
            DeleteSave();
        }

        private void DeleteSave()
        {
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
