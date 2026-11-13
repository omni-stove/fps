using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// やられ演出のデスカム。試合のルール (<see cref="NetTeamDeathmatch.SyncedRules"/>) が <see cref="DeathCamera.Killer"/> のとき、
    /// 自分のプレイヤー (<see cref="NetPlayer.HasLocalView"/>) が倒れてからリスポーンするまで、カメラを倒れた位置の少し上へ置き、
    /// 倒した相手 (<see cref="NetPlayerStats.Killer"/>) の方を向ける。相手がいない (自滅・相手が抜けた) ときは倒れた位置を見下ろす。
    /// 置き場所と向きは <see cref="DeathCamFraming"/>、壁にめり込まないよう縮めるのは <see cref="ViewRig.ResolveBoomPosition"/> で決める。
    /// <para>
    /// 映している間は <see cref="ViewRig"/> を三人称にして止め (カメラはこちらが置く)、視点操作 (<see cref="FirstPersonLook.InputSuspended"/>) と
    /// 覗き込み (<see cref="AimDownSights"/>) と射撃 (<see cref="NetPlayer"/> が <see cref="IsActive"/> を見る) を止める。
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

        /// <summary>いまデスカムを映しているか。</summary>
        public bool IsActive => _switch.IsActive;

        /// <summary>映している相手のクライアント Id。映していないか、相手がいなければ null。</summary>
        public ulong? KillerClientId => IsActive && _hasKiller ? _killerId : null;

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

            if (IsActive)
            {
                Frame(ResolveKillerEye(match));
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
        /// 今の状態を渡して、デスカムを始めるか止める。始めたらその場でカメラを倒れた位置の上へ置き (相手はまだ分からないので見下ろす)、
        /// 止めたら元に戻す。視点の有無は <see cref="NetPlayer.HasLocalView"/> を見る。
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
                    Debug.Log(LogTag + " started on " + name + " at " + FormatPosition(_deathEye), this);
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

            var target = killerEye ?? _deathEye;
            var offset = DeathCamFraming.BoomOffset(
                _deathEye.ToNumerics(), target.ToNumerics(), _fallbackYaw, _height, _backDistance).ToUnity();
            var position = _rig != null ? _rig.ResolveBoomPosition(_deathEye, offset) : _deathEye + offset;
            DeathCamFraming.LookAt(
                position.ToNumerics(), target.ToNumerics(), _fallbackYaw, LookAngles.DefaultMaxPitch, out var yaw, out var pitch);
            _camera.transform.SetPositionAndRotation(position, Quaternion.Euler(pitch, yaw, 0f));
        }

        private void Begin()
        {
            _deathEye = _player.AimOrigin();
            _fallbackYaw = _player.transform.eulerAngles.y;
            _killerId = null;
            _killer = null;
            _hasKiller = false;
            _targetResolved = false;

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

            _camera = null;
            _killerId = null;
            _killer = null;
            _hasKiller = false;
            _targetResolved = false;
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
