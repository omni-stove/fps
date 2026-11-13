using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Net
{
    /// <summary>
    /// 倒されてからリスポーンするまでのカメラ。試合のルール (<see cref="NetTeamDeathmatch.SyncedRules"/>) の <see cref="DeathCamera"/> が
    /// None 以外のとき、自分のプレイヤー (<see cref="NetPlayer.HasLocalView"/>) が倒れてからリスポーンするまで、ルールに従ってカメラを動かす。
    /// <list type="bullet">
    /// <item><see cref="DeathCamera.Killer"/>: カメラを倒れた位置の少し上へ置き、倒した相手 (<see cref="NetPlayerStats.Killer"/>) の方を向ける。
    /// 相手がいない (自滅・相手が抜けた) ときは倒れた位置を見下ろす。置き場所と向きは <see cref="DeathCamFraming"/>。</item>
    /// <item><see cref="DeathCamera.Ally"/>: 生きている味方の肩越し (三人称のカメラと同じオフセット) に置き、味方の体の向きを向く。
    /// 味方の位置と向きは他のクライアントに同期されている値 (<see cref="NetworkPlayerMotor"/> が補間した本体) を読む。
    /// どの味方を映すかは <see cref="SpectateTargets"/> で決め、Next / Previous で次・前の味方へ移る。
    /// 映せる味方がいなければ (<see cref="SpectateTargets.ResolveMode"/>) Free と同じにし、味方が現れたらその視点へ移る。</item>
    /// <item><see cref="DeathCamera.Free"/>: 倒れた位置の上から、Move で向いている方へ飛び、Look で向きを変える。
    /// 壁の中には入らない (<see cref="ViewRig.ResolveFreeMove"/>)。</item>
    /// </list>
    /// 壁にめり込まないよう縮めるのは <see cref="ViewRig.ResolveBoomPosition(Vector3, Vector3, Transform)"/> で決める。
    /// 映している間にルールが別の演出に変わったら、次のフレームからそちらで映す。
    /// <para>
    /// 映している間は <see cref="ViewRig"/> を三人称にして止め (カメラはこちらが置く)、視点操作 (<see cref="FirstPersonLook.InputSuspended"/>) と
    /// 覗き込み (<see cref="AimDownSights"/>) と射撃・蘇生 (<see cref="NetPlayer"/> が <see cref="IsActive"/> を見る) を止める。
    /// リスポーンしたらカメラの位置・ViewRig の視点・入力を元に戻す。試合が終わったときは試合の終わりの見せ方
    /// (<see cref="NetSpectatorCamera"/> など) を優先してすぐ戻す。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetPlayer))]
    public sealed class NetDeathCamera : MonoBehaviour
    {
        public const string LogTag = "[DeathCam]";

        [SerializeField, Min(0f), Tooltip("倒れた目からカメラを持ち上げる高さ (m)")]
        private float _height = DeathCamFraming.DefaultHeight;

        [SerializeField, Min(0f), Tooltip("倒れた目から、相手と反対側へカメラを下げる水平の距離 (m)")]
        private float _backDistance = DeathCamFraming.DefaultBackDistance;

        [SerializeField, Min(0f), Tooltip("自由カメラ (Free) を倒し切ったときの速さ (m/s)")]
        private float _freeSpeed = DeathCamFraming.DefaultFreeSpeed;

        private readonly DeathCamSwitch _switch = new DeathCamSwitch();

        private NetPlayer _player;
        private ViewRig _rig;
        private FirstPersonLook _look;
        private AimDownSights _ads;
        private bool _resolved;

        // 映している間だけ使う。倒れた時点の目と体の向き、戻すための元の状態
        private Camera _camera;
        private Vector3 _deathEye;
        private float _fallbackYaw;
        private Vector3 _savedCameraLocalPosition;
        private Quaternion _savedCameraLocalRotation;
        private ViewMode _savedMode;
        private bool _rigWasEnabled;
        private bool _adsWasEnabled;
        private bool _lookWasSuspended;

        // 映す相手。Id が変わったときだけ探し直す (見つからない Id を毎フレーム探さない)
        private ulong? _killerId;
        private NetPlayer _killer;
        private bool _hasKiller;
        private bool _targetResolved;

        // Ally で映している味方。_allyResolved は最初の選択をログに出すため
        private readonly List<SpectateCandidate> _candidates = new List<SpectateCandidate>();
        private ulong? _allyId;
        private NetPlayer _ally;
        private bool _allyResolved;

        // Free (と、Ally で映せる味方がいないとき) のカメラ。切り替わった時点のカメラの位置と向きから始める
        private readonly LookAngles _freeAngles = new LookAngles();
        private Vector3 _freePosition;
        private bool _freeActive;

        // 映している間だけ読む入力。プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたものだけ戻す
        private InputAction _moveAction;
        private InputAction _nextAction;
        private InputAction _previousAction;
        private bool _enabledMoveAction;
        private bool _enabledNextAction;
        private bool _enabledPreviousAction;

        /// <summary>いま倒されてからのカメラを映しているか。</summary>
        public bool IsActive => _switch.IsActive;

        /// <summary>映している相手のクライアント Id。映していないか、相手がいなければ null。</summary>
        public ulong? KillerClientId => IsActive && _hasKiller ? _killerId : null;

        /// <summary>肩越しに映している味方のクライアント Id。映していないか、味方を映していなければ null。</summary>
        public ulong? AllyClientId => IsActive && !_freeActive && _ally != null ? _allyId : null;

        /// <summary>いま自由カメラ (Free、または Ally で映せる味方がいないとき) で動かしているか。</summary>
        public bool IsFreeCamera => IsActive && _freeActive;

        public float Height
        {
            get => _height;
            set => _height = Mathf.Max(0f, value);
        }

        public float BackDistance
        {
            get => _backDistance;
            set => _backDistance = Mathf.Max(0f, value);
        }

        public float FreeSpeed
        {
            get => _freeSpeed;
            set => _freeSpeed = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            EnsureResolved();
        }

        private void LateUpdate()
        {
            EnsureResolved();
            var match = NetTeamDeathmatch.Current;
            var mode = match != null ? match.SyncedRules.DeathCamera : DeathCamera.None;
            var matchEnded = match != null && match.State == GameModeState.Ended;
            Step(mode, _player.IsAlive, matchEnded);

            if (!IsActive)
            {
                return;
            }

            switch (mode)
            {
                case DeathCamera.Killer:
                    Frame(ResolveKillerEye(match));
                    break;
                case DeathCamera.Ally:
                    WatchAlly(ReadSpectateStep());
                    break;
                default:
                    MoveFree(ReadMove(), ReadLookDegrees(), Time.deltaTime);
                    break;
            }
        }

        private void OnDisable()
        {
            // プレイヤーが消えるときも、カメラと入力を戻して次に有効になったときに持ち越さない
            if (_switch.Reset())
            {
                Restore();
                Debug.Log(LogTag + " stopped on " + name + " (disabled)", this);
            }
        }

        /// <summary>
        /// 今の状態を渡して、カメラを始めるか止める。始めたらその場でカメラを倒れた位置の上へ置き (相手はまだ分からないので見下ろす。
        /// Free はここから飛び始める)、止めたら元に戻す。視点の有無は <see cref="NetPlayer.HasLocalView"/> を見る。
        /// </summary>
        internal DeathCamTransition Step(DeathCamera mode, bool isAlive, bool matchEnded)
        {
            EnsureResolved();
            var transition = _switch.Update(mode, _player.HasLocalView, isAlive, matchEnded);
            switch (transition)
            {
                case DeathCamTransition.None:
                    break;
                case DeathCamTransition.Started:
                    Begin();
                    Debug.Log(LogTag + " started on " + name + " (" + mode + ") at " + FormatPosition(_deathEye), this);
                    Frame(null);
                    break;
                default:
                    Restore();
                    Debug.Log(LogTag + " stopped on " + name + " (" + transition + ")", this);
                    break;
            }

            return transition;
        }

        /// <summary>
        /// 映している間、カメラを倒れた目から <see cref="DeathCamFraming.BoomOffset"/> の先 (壁があれば手前) へ置き、
        /// killerEye (null なら倒れた目) を向ける。映していなければ何もしない。
        /// </summary>
        internal void Frame(Vector3? killerEye)
        {
            if (!IsActive || _camera == null)
            {
                return;
            }

            _freeActive = false;
            var target = killerEye ?? _deathEye;
            var offset = DeathCamFraming.BoomOffset(
                _deathEye.ToNumerics(), target.ToNumerics(), _fallbackYaw, _height, _backDistance).ToUnity();
            var position = _rig != null ? _rig.ResolveBoomPosition(_deathEye, offset) : _deathEye + offset;
            DeathCamFraming.LookAt(
                position.ToNumerics(), target.ToNumerics(), _fallbackYaw, LookAngles.DefaultMaxPitch, out var yaw, out var pitch);
            _camera.transform.SetPositionAndRotation(position, Quaternion.Euler(pitch, yaw, 0f));
        }

        /// <summary>
        /// 映している間、カメラを味方の目から肩越し (<see cref="DeathCamFraming.ShoulderPosition"/>、壁があれば手前) へ置き、
        /// 味方の体の向き (allyYaw、度) を水平に向ける。allyRoot は味方の本体で、カメラを縮める判定でその体を除く。映していなければ何もしない。
        /// </summary>
        internal void FrameAlly(Vector3 allyEye, float allyYaw, Transform allyRoot)
        {
            if (!IsActive || _camera == null)
            {
                return;
            }

            _freeActive = false;
            var shoulderOffset = _rig != null ? _rig.ThirdPersonOffset : ViewRig.DefaultThirdPersonOffset;
            var desired = DeathCamFraming.ShoulderPosition(allyEye.ToNumerics(), allyYaw, shoulderOffset.ToNumerics()).ToUnity();
            var position = _rig != null ? _rig.ResolveBoomPosition(allyEye, desired - allyEye, allyRoot) : desired;
            _camera.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, allyYaw, 0f));
        }

        /// <summary>
        /// 映している間、自由カメラを 1 フレーム動かす。直前まで自由カメラでなければ、今のカメラの位置と向きから始める。
        /// lookDegrees (x は正で右、y は正で上) だけ回してから、move (Move 入力) で向いている方へ <see cref="FreeSpeed"/> で進め、
        /// 壁の手前で止める (<see cref="ViewRig.ResolveFreeMove"/>)。映していなければ何もしない。
        /// </summary>
        internal void MoveFree(Vector2 move, Vector2 lookDegrees, float deltaSeconds)
        {
            if (!IsActive || _camera == null)
            {
                return;
            }

            if (!_freeActive)
            {
                _freeActive = true;
                var cameraTransform = _camera.transform;
                _freePosition = cameraTransform.position;
                var angles = cameraTransform.eulerAngles;
                _freeAngles.Set(angles.y, angles.x);
                Debug.Log(LogTag + " free camera on " + name + " from " + FormatPosition(_freePosition), this);
            }

            _freeAngles.Rotate(lookDegrees.x, lookDegrees.y);
            var delta = DeathCamFraming.FreeMoveDelta(
                move.ToNumerics(), _freeAngles.Yaw, _freeAngles.Pitch, _freeSpeed, Mathf.Max(0f, deltaSeconds)).ToUnity();
            _freePosition = _rig != null ? _rig.ResolveFreeMove(_freePosition, delta) : _freePosition + delta;
            _camera.transform.SetPositionAndRotation(_freePosition, Quaternion.Euler(_freeAngles.Pitch, _freeAngles.Yaw, 0f));
        }

        // 同期されている各プレイヤーのチームと生死から映す味方を選び、その肩越しへ置く。映せる味方がいなければ自由カメラにする
        private void WatchAlly(int step)
        {
            _candidates.Clear();
            var players = NetPlayer.SpawnedPlayers;
            for (var i = 0; i < players.Count; i++)
            {
                var candidate = players[i];
                if (candidate != null && candidate.IsSpawned)
                {
                    _candidates.Add(new SpectateCandidate(candidate.OwnerClientId, candidate.Team, candidate.IsAlive));
                }
            }

            var allyId = SpectateTargets.Pick(_candidates, _player.OwnerClientId, _player.Team, _allyId, step);
            if (!_allyResolved || allyId != _allyId || (allyId.HasValue && _ally == null))
            {
                _allyResolved = true;
                _allyId = allyId;
                _ally = allyId.HasValue ? FindSpawnedPlayer(allyId.Value) : null;
                if (_ally != null)
                {
                    Debug.Log(LogTag + " watching ally client " + allyId.Value.ToString(CultureInfo.InvariantCulture) + " from " + name, this);
                }
                else
                {
                    Debug.Log(LogTag + " no ally to watch for " + name + "; using the free camera", this);
                }
            }

            if (SpectateTargets.ResolveMode(DeathCamera.Ally, _ally != null) == DeathCamera.Free)
            {
                MoveFree(ReadMove(), ReadLookDegrees(), Time.deltaTime);
                return;
            }

            FrameAlly(_ally.AimOrigin(), _ally.transform.eulerAngles.y, _ally.transform);
        }

        private NetPlayer FindSpawnedPlayer(ulong clientId)
        {
            var players = NetPlayer.SpawnedPlayers;
            for (var i = 0; i < players.Count; i++)
            {
                var candidate = players[i];
                if (candidate != null && candidate != _player && candidate.IsSpawned && candidate.OwnerClientId == clientId)
                {
                    return candidate;
                }
            }

            return null;
        }

        private void Begin()
        {
            _deathEye = _player.AimOrigin();
            _fallbackYaw = _player.transform.eulerAngles.y;
            ClearTargets();

            _camera = _rig != null && _rig.Camera != null ? _rig.Camera : FirstPersonLook.FindChildCamera(transform);
            if (_camera == null)
            {
                Debug.LogWarning(LogTag + " " + name + " has no camera; run Fps.Editor.NetSceneBuilder.Build", this);
            }
            else
            {
                _savedCameraLocalPosition = _camera.transform.localPosition;
                _savedCameraLocalRotation = _camera.transform.localRotation;
            }

            if (_rig != null)
            {
                // カメラはこちらが置くので、ViewRig の LateUpdate に目からのオフセットへ戻させない
                _rigWasEnabled = _rig.enabled;
                _savedMode = _rig.Mode;
                _rig.enabled = false;
                _rig.Mode = ViewMode.ThirdPerson;
                _rig.ApplyBodyVisibility(true);
            }

            if (_ads != null)
            {
                _adsWasEnabled = _ads.enabled;
                _ads.enabled = false;
            }

            if (_look != null)
            {
                // 無効にするとカーソルのロックが外れてしまうので、入力だけ止める
                _lookWasSuspended = _look.InputSuspended;
                _look.InputSuspended = true;
            }

            AcquireActions();
        }

        private void Restore()
        {
            if (_camera != null)
            {
                _camera.transform.localPosition = _savedCameraLocalPosition;
                _camera.transform.localRotation = _savedCameraLocalRotation;
            }

            // 映している間に視点が無くなっていたら (プレイヤーが消えるときなど)、NetPlayer が止めた部品を有効に戻さない
            var hasLocalView = _player != null && _player.HasLocalView;
            if (_rig != null)
            {
                _rig.Mode = _savedMode;
                _rig.enabled = _rigWasEnabled && hasLocalView;
                _rig.ApplyBodyVisibility(_rig.enabled);
                if (_rig.enabled && _rig.IsInitialized)
                {
                    _rig.UpdateCameraPosition();
                }
            }

            if (_ads != null)
            {
                _ads.enabled = _adsWasEnabled && hasLocalView;
            }

            if (_look != null)
            {
                _look.InputSuspended = _lookWasSuspended;
            }

            ReleaseActions();
            _camera = null;
            ClearTargets();
        }

        private void ClearTargets()
        {
            _killerId = null;
            _killer = null;
            _hasKiller = false;
            _targetResolved = false;
            _allyId = null;
            _ally = null;
            _allyResolved = false;
            _freeActive = false;
        }

        // 試合の行から自分を倒した相手を引き、その目の位置を返す。相手がいない・見つからない・抜けたなら null (倒れた位置を見下ろす)
        private Vector3? ResolveKillerEye(NetTeamDeathmatch match)
        {
            var killerId = FindKillerId(match, _player.OwnerClientId);
            if (!_targetResolved || killerId != _killerId)
            {
                _targetResolved = true;
                _killerId = killerId;
                _killer = killerId.HasValue ? FindPlayer(killerId.Value) : null;
                _hasKiller = _killer != null;
                if (_hasKiller)
                {
                    Debug.Log(LogTag + " watching killer client " + killerId.Value.ToString(CultureInfo.InvariantCulture) + " from " + name, this);
                }
                else if (killerId.HasValue)
                {
                    Debug.Log(LogTag + " killer client " + killerId.Value.ToString(CultureInfo.InvariantCulture)
                        + " is gone; looking down at " + name, this);
                }
                else
                {
                    Debug.Log(LogTag + " no killer for " + name + "; looking down at the death spot", this);
                }
            }
            else if (_hasKiller && (_killer == null || !_killer.IsSpawned))
            {
                _hasKiller = false;
                _killer = null;
                Debug.Log(LogTag + " killer client " + (_killerId ?? 0).ToString(CultureInfo.InvariantCulture)
                    + " left; looking down at " + name, this);
            }

            return _hasKiller ? _killer.AimOrigin() : (Vector3?)null;
        }

        /// <summary>
        /// match の <see cref="NetTeamDeathmatch.Players"/> から、victim を最後に倒した相手のクライアント Id を引く。
        /// 試合が無い・行が無い・倒した相手がいない・自分自身なら null。
        /// </summary>
        internal static ulong? FindKillerId(NetTeamDeathmatch match, ulong victim)
        {
            if (match == null || !match.IsSpawned)
            {
                return null;
            }

            var players = match.Players;
            for (var i = 0; i < players.Count; i++)
            {
                var row = players[i];
                if (row.ClientId == victim)
                {
                    var killer = row.Killer;
                    return killer.HasValue && killer.Value != victim ? killer : null;
                }
            }

            return null;
        }

        // プレイヤーのオブジェクトは所有者のクライアント Id で引ける。クライアントでも生成済みの一覧は持っている
        private NetPlayer FindPlayer(ulong clientId)
        {
            var manager = _player.NetworkManager;
            if (manager == null || manager.SpawnManager == null)
            {
                return null;
            }

            foreach (var networkObject in manager.SpawnManager.SpawnedObjectsList)
            {
                if (networkObject != null && networkObject.IsPlayerObject && networkObject.OwnerClientId == clientId
                    && networkObject.TryGetComponent<NetPlayer>(out var candidate) && candidate != _player)
                {
                    return candidate;
                }
            }

            return null;
        }

        private int ReadSpectateStep()
        {
            var step = 0;
            if (_nextAction != null && _nextAction.WasPressedThisFrame())
            {
                step++;
            }

            if (_previousAction != null && _previousAction.WasPressedThisFrame())
            {
                step--;
            }

            return step;
        }

        private Vector2 ReadMove()
        {
            return _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
        }

        private Vector2 ReadLookDegrees()
        {
            return _look != null ? _look.ReadLookDegrees(Time.deltaTime) : Vector2.zero;
        }

        private void AcquireActions()
        {
            // 画面なし (batchmode) や Edit Mode のテストでは入力が無い
            if (!Application.isPlaying || Application.isBatchMode)
            {
                return;
            }

            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogWarning(LogTag + " project-wide input actions are not set; " + name + " cannot move the death camera", this);
                return;
            }

            _moveAction = AcquireAction(actions, InputActionPaths.Move, ref _enabledMoveAction);
            _nextAction = AcquireAction(actions, InputActionPaths.Next, ref _enabledNextAction);
            _previousAction = AcquireAction(actions, InputActionPaths.Previous, ref _enabledPreviousAction);
        }

        private InputAction AcquireAction(InputActionAsset actions, string path, ref bool enabledHere)
        {
            var action = actions.FindAction(path);
            if (action == null)
            {
                Debug.LogWarning(LogTag + " input action '" + path + "' was not found; " + name + " ignores it while dead", this);
                return null;
            }

            if (!action.enabled)
            {
                action.Enable();
                enabledHere = true;
            }

            return action;
        }

        private void ReleaseActions()
        {
            ReleaseAction(ref _moveAction, ref _enabledMoveAction);
            ReleaseAction(ref _nextAction, ref _enabledNextAction);
            ReleaseAction(ref _previousAction, ref _enabledPreviousAction);
        }

        private static void ReleaseAction(ref InputAction action, ref bool enabledHere)
        {
            if (enabledHere && action != null)
            {
                action.Disable();
            }

            enabledHere = false;
            action = null;
        }

        // Awake を経ない (Edit Mode のテストなど) ときも、使う時点で解決する
        private void EnsureResolved()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;
            _player = GetComponent<NetPlayer>();
            _rig = GetComponent<ViewRig>();
            _look = GetComponent<FirstPersonLook>();
            _ads = GetComponent<AimDownSights>();
        }

        private static string FormatPosition(Vector3 position)
        {
            return "(" + position.x.ToString("F1", CultureInfo.InvariantCulture)
                + ", " + position.y.ToString("F1", CultureInfo.InvariantCulture)
                + ", " + position.z.ToString("F1", CultureInfo.InvariantCulture) + ")";
        }
    }
}
