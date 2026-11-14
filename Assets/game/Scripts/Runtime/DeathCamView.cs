using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// 倒されてからリスポーンまでの観戦で、自分のカメラを借りて動かす部分。いつ始めて止めるか・何を映すかは持ち主
    /// (ネット対戦の NetDeathCamera、オフラインの <see cref="PlayerDeathCamera"/>) が決め、ここはカメラの置き方と入力だけを受け持つ。
    /// <list type="bullet">
    /// <item><see cref="Begin"/> で <see cref="ViewRig"/> を三人称にして止め (カメラはこちらが置く)、視点操作 (<see cref="FirstPersonLook.InputSuspended"/>) と
    /// 覗き込み (<see cref="AimDownSights"/>) を止め、Move・Next・Previous の入力を使えるようにする。</item>
    /// <item><see cref="Restore"/> でカメラの位置・ViewRig の視点・入力を元に戻す。</item>
    /// <item>倒した相手の方を向く (<see cref="Frame"/>)、味方の肩越し (<see cref="FrameAlly"/>)、自由カメラ (<see cref="MoveFree"/>) のどれかでカメラを置く。</item>
    /// </list>
    /// 壁にめり込まないよう縮めるのは <see cref="ViewRig.ResolveBoomPosition(Vector3, Vector3, Transform)"/>、自由カメラが壁を抜けないのは
    /// <see cref="ViewRig.ResolveFreeMove"/> で決める。
    /// </summary>
    public sealed class DeathCamView
    {
        public const string LogTag = "[DeathCam]";

        // 同じカメラを二つの持ち主 (死亡カメラとフォトモード) が続けて借りると、後の方が先の方の置いた位置と止めた ViewRig を
        // 「元」として覚えてしまい戻せなくなる。カメラごとに今の借り手を覚え、後から借りる方が先の方を戻させる。
        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で空にする
        private static readonly Dictionary<Camera, DeathCamView> _borrowers = new Dictionary<Camera, DeathCamView>();

        private readonly Component _owner;
        private readonly string _missingCameraHint;
        private readonly bool _readsInput;

        private ViewRig _rig;
        private FirstPersonLook _look;
        private AimDownSights _ads;

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
        private bool _engaged;

        // 自由カメラ。切り替わった時点のカメラの位置と向きから始める
        private readonly LookAngles _freeAngles = new LookAngles();
        private Vector3 _freePosition;
        private bool _freeActive;
        private float _freeRoll;
        private Vector3 _freeLimitCenter;
        private float? _freeLimitRadius;

        // 映している間だけ読む入力。プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたものだけ戻す
        private InputAction _moveAction;
        private InputAction _nextAction;
        private InputAction _previousAction;
        private bool _enabledMoveAction;
        private bool _enabledNextAction;
        private bool _enabledPreviousAction;

        /// <param name="owner">カメラを持つプレイヤー。ログの名前と、ViewRig・FirstPersonLook・AimDownSights・カメラを探す起点に使う。</param>
        /// <param name="missingCameraHint">カメラが見つからないときの警告に添える、直し方の一言。</param>
        public DeathCamView(Component owner, string missingCameraHint)
            : this(owner, missingCameraHint, true)
        {
        }

        // readsInput を false にするのは、ポーズ画面が止めている共有の Move・Next・Previous を有効にすると、
        // ポーズを閉じて戻したものを Restore で止め直してしまう持ち主 (フォトモード) のため
        public DeathCamView(Component owner, string missingCameraHint, bool readsInput)
        {
            _owner = owner;
            _missingCameraHint = missingCameraHint ?? string.Empty;
            _readsInput = readsInput;
            Resolve();
        }

        /// <summary><see cref="Begin"/> してから <see cref="Restore"/> するまでの間か。</summary>
        public bool IsEngaged => _engaged;

        /// <summary>借りているカメラ。借りていないか、カメラが見つからなければ null。</summary>
        public Camera Camera => _camera;

        /// <summary>倒れた時点の目の位置。</summary>
        public Vector3 DeathEye => _deathEye;

        /// <summary>いま自由カメラで動かしているか。</summary>
        public bool IsFreeCamera => _engaged && _freeActive;

        public float FreeRoll
        {
            get => _freeRoll;
            set => _freeRoll = float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }

        public float? FreeLimitRadius => _freeLimitRadius;

        public Vector3 FreeLimitCenter => _freeLimitCenter;

        public void LimitFree(Vector3 center, float radius)
        {
            if (!(radius >= 0f) || float.IsInfinity(radius))
            {
                throw new System.ArgumentOutOfRangeException(nameof(radius), radius, "radius must be finite and not negative");
            }

            _freeLimitCenter = center;
            _freeLimitRadius = radius;
        }

        public void ClearFreeLimit()
        {
            _freeLimitRadius = null;
        }

        // 借りている間に別の DeathCamView が同じカメラを借りたとき、戻される直前に呼ばれる (カメラはまだ借りたまま)
        public event Action Preempted;

        public static bool IsBorrowed(Camera camera)
        {
            return camera != null && _borrowers.TryGetValue(camera, out var borrower) && borrower._engaged;
        }

        // 死亡カメラは倒れたら必ず映すので先の借り手を戻させて借りるが、フォトモードのような後回しでよい持ち主は、
        // 先に借りている方を邪魔しないようこちらで借りるのをやめる
        public bool TryBegin(Vector3 deathEye, float fallbackYaw)
        {
            if (_engaged)
            {
                return true;
            }

            Resolve();
            var camera = FindCamera();
            if (camera != null && _borrowers.TryGetValue(camera, out var other) && other != this && other._engaged)
            {
                Debug.Log(LogTag + " " + _owner.name + "'s camera is already in use by " + other._owner.name + "; not borrowing it", _owner);
                return false;
            }

            Begin(deathEye, fallbackYaw);
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _borrowers.Clear();
        }

        private Camera FindCamera()
        {
            return _rig != null && _rig.Camera != null ? _rig.Camera : FirstPersonLook.FindChildCamera(_owner.transform);
        }

        private void Preempt()
        {
            Debug.Log(LogTag + " another view took over " + _owner.name + "'s camera", _owner);
            if (Preempted != null)
            {
                foreach (var handler in Preempted.GetInvocationList())
                {
                    try
                    {
                        ((Action)handler)();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e, _owner);
                    }
                }
            }

            Restore(true);
        }

        /// <summary>
        /// カメラを借りる。deathEye は倒れた目の位置、fallbackYaw は向ける相手がいないときに使う体の向き (度)。
        /// 借りている間にもう一度呼んでも何もしない。
        /// </summary>
        public void Begin(Vector3 deathEye, float fallbackYaw)
        {
            if (_engaged)
            {
                return;
            }

            Resolve();
            _engaged = true;
            _deathEye = deathEye;
            _fallbackYaw = fallbackYaw;
            _freeActive = false;

            _camera = FindCamera();
            if (_camera == null)
            {
                Debug.LogWarning(LogTag + " " + _owner.name + " has no camera; " + _missingCameraHint, _owner);
            }
            else
            {
                // 先の借り手を戻させてから、元の位置と ViewRig の状態を覚える
                if (_borrowers.TryGetValue(_camera, out var previous) && previous != this && previous._engaged)
                {
                    previous.Preempt();
                }

                _borrowers[_camera] = this;
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

        /// <summary>
        /// カメラと入力を戻す。reenableView が false なら (視点が無くなったときなど) ViewRig と覗き込みを有効に戻さない。
        /// 借りていなければ何もしない。
        /// </summary>
        public void Restore(bool reenableView)
        {
            if (!_engaged)
            {
                return;
            }

            _engaged = false;
            if (_camera != null)
            {
                _camera.transform.localPosition = _savedCameraLocalPosition;
                _camera.transform.localRotation = _savedCameraLocalRotation;
            }

            if (_rig != null)
            {
                _rig.Mode = _savedMode;
                _rig.enabled = _rigWasEnabled && reenableView;
                _rig.ApplyBodyVisibility(_rig.enabled);
                if (_rig.enabled && _rig.IsInitialized)
                {
                    _rig.UpdateCameraPosition();
                }
            }

            if (_ads != null)
            {
                _ads.enabled = _adsWasEnabled && reenableView;
            }

            if (_look != null)
            {
                _look.InputSuspended = _lookWasSuspended;
            }

            ReleaseActions();

            // 消えたカメラも Unity の == では null になるので、参照で比べて表から外す
            if ((object)_camera != null && _borrowers.TryGetValue(_camera, out var borrower) && borrower == this)
            {
                _borrowers.Remove(_camera);
            }

            _camera = null;
            _freeActive = false;
            _freeRoll = 0f;
            _freeLimitRadius = null;
        }

        /// <summary>
        /// カメラを倒れた目から <see cref="DeathCamFraming.BoomOffset"/> の先 (壁があれば手前) へ置き、target (null なら倒れた目) を向ける。
        /// 借りていなければ何もしない。
        /// </summary>
        public void Frame(Vector3? target, float height, float backDistance)
        {
            if (!_engaged || _camera == null)
            {
                return;
            }

            _freeActive = false;
            var look = target ?? _deathEye;
            var offset = DeathCamFraming.BoomOffset(
                _deathEye.ToNumerics(), look.ToNumerics(), _fallbackYaw, height, backDistance).ToUnity();
            var position = _rig != null ? _rig.ResolveBoomPosition(_deathEye, offset) : _deathEye + offset;
            DeathCamFraming.LookAt(
                position.ToNumerics(), look.ToNumerics(), _fallbackYaw, LookAngles.DefaultMaxPitch, out var yaw, out var pitch);
            _camera.transform.SetPositionAndRotation(position, Quaternion.Euler(pitch, yaw, 0f));
        }

        /// <summary>
        /// カメラを味方の目から肩越し (<see cref="DeathCamFraming.ShoulderPosition"/>、壁があれば手前) へ置き、
        /// 味方の体の向き (allyYaw、度) を水平に向ける。allyRoot は味方の本体で、カメラを縮める判定でその体を除く。借りていなければ何もしない。
        /// </summary>
        public void FrameAlly(Vector3 allyEye, float allyYaw, Transform allyRoot)
        {
            if (!_engaged || _camera == null)
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
        /// 自由カメラを 1 フレーム動かす。直前まで自由カメラでなければ、今のカメラの位置と向きから始める。
        /// lookDegrees (x は正で右、y は正で上) だけ回してから、move (Move 入力) で向いている方へ speed (m/s) で進め、
        /// 壁の手前で止める (<see cref="ViewRig.ResolveFreeMove"/>)。<see cref="LimitFree"/> していればその球の中に収め、
        /// 向きには <see cref="FreeRoll"/> の傾きを付ける。借りていなければ何もしない。
        /// </summary>
        public void MoveFree(Vector2 move, Vector2 lookDegrees, float speed, float deltaSeconds)
        {
            if (!_engaged || _camera == null)
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
                Debug.Log(LogTag + " free camera on " + _owner.name + " from " + FormatPosition(_freePosition), _owner);
            }

            _freeAngles.Rotate(lookDegrees.x, lookDegrees.y);
            var delta = DeathCamFraming.FreeMoveDelta(
                move.ToNumerics(), _freeAngles.Yaw, _freeAngles.Pitch, Mathf.Max(0f, speed), Mathf.Max(0f, deltaSeconds)).ToUnity();
            if (_freeLimitRadius.HasValue)
            {
                // 壁の判定より前に縮めておくと、範囲の外へ向かう分で壁沿いに滑ることがない
                delta = ClampToFreeLimit(_freePosition + delta) - _freePosition;
            }

            _freePosition = _rig != null ? _rig.ResolveFreeMove(_freePosition, delta) : _freePosition + delta;
            if (_freeLimitRadius.HasValue)
            {
                // 壁に沿って滑った分や、範囲の外から始めた分で出ないよう、置く前にもう一度収める
                _freePosition = ClampToFreeLimit(_freePosition);
            }

            _camera.transform.SetPositionAndRotation(_freePosition, Quaternion.Euler(_freeAngles.Pitch, _freeAngles.Yaw, _freeRoll));
        }

        private Vector3 ClampToFreeLimit(Vector3 position)
        {
            return PhotoModeSpec.ClampWithin(position.ToNumerics(), _freeLimitCenter.ToNumerics(), _freeLimitRadius.Value).ToUnity();
        }

        /// <summary>このフレームの Next と Previous の押下をまとめた数。Next で +1、Previous で -1。</summary>
        public int ReadSpectateStep()
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

        /// <summary>このフレームの Move 入力。入力が無ければ zero。</summary>
        public Vector2 ReadMove()
        {
            return _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
        }

        /// <summary>このフレームの Look 入力を、プレイヤーの感度で回転量 (度) にしたもの。</summary>
        public Vector2 ReadLookDegrees(float deltaSeconds)
        {
            return _look != null ? _look.ReadLookDegrees(deltaSeconds) : Vector2.zero;
        }

        public static string FormatPosition(Vector3 position)
        {
            return "(" + position.x.ToString("F1", CultureInfo.InvariantCulture)
                + ", " + position.y.ToString("F1", CultureInfo.InvariantCulture)
                + ", " + position.z.ToString("F1", CultureInfo.InvariantCulture) + ")";
        }

        // 持ち主の Awake より前 (Edit Mode のテストや、AddComponent の直後) に作られても、借りる時点で部品を拾い直す
        private void Resolve()
        {
            if (_owner == null)
            {
                return;
            }

            if (_rig == null)
            {
                _rig = _owner.GetComponent<ViewRig>();
            }

            if (_look == null)
            {
                _look = _owner.GetComponent<FirstPersonLook>();
            }

            if (_ads == null)
            {
                _ads = _owner.GetComponent<AimDownSights>();
            }
        }

        private void AcquireActions()
        {
            // 画面なし (batchmode) や Edit Mode のテストでは入力が無い
            if (!_readsInput || !Application.isPlaying || Application.isBatchMode)
            {
                return;
            }

            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogWarning(LogTag + " project-wide input actions are not set; " + _owner.name + " cannot move the death camera", _owner);
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
                Debug.LogWarning(LogTag + " input action '" + path + "' was not found; " + _owner.name + " ignores it while dead", _owner);
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
    }
}
