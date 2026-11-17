using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
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
        private float _deathPitch;
        private Vector3 _savedCameraLocalPosition;
        private Quaternion _savedCameraLocalRotation;
        private ViewMode _savedMode;
        private bool _rigWasEnabled;
        private bool _adsWasEnabled;
        private bool _lookWasSuspended;
        private bool _engaged;

        private readonly LookAngles _freeAngles = new LookAngles();
        private Vector3 _freePosition;
        private bool _freeActive;
        private float _freeRoll;
        private Vector3 _freeLimitCenter;
        private float? _freeLimitRadius;

        // 映している間だけ読む入力。プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたものだけ戻す
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _moveAction;
        [NonSerialized]
        private InputAction _nextAction;
        [NonSerialized]
        private InputAction _previousAction;
        private bool _enabledMoveAction;
        private bool _enabledNextAction;
        private bool _enabledPreviousAction;

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

        public bool IsEngaged => _engaged;

        public Camera Camera => _camera;

        public Vector3 DeathEye => _deathEye;

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

        public void Begin(Vector3 deathEye, float fallbackYaw)
        {
            Begin(deathEye, fallbackYaw, false);
        }

        // keepFirstPerson で ViewRig を一人称のままにできるのは、倒れた主観 (FrameFallen) で床の近くに下ろしたカメラを三人称の位置へ動かさず、自分の目のまま倒すため。
        public void Begin(Vector3 deathEye, float fallbackYaw, bool keepFirstPerson)
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

            _deathPitch = _camera != null ? Mathf.DeltaAngle(0f, _camera.transform.eulerAngles.x) : 0f;

            if (_rig != null)
            {
                // カメラはこちらが置くので、ViewRig の LateUpdate に目からのオフセットへ戻させない
                _rigWasEnabled = _rig.enabled;
                _savedMode = _rig.Mode;
                _rig.enabled = false;
                _rig.Mode = keepFirstPerson ? ViewMode.FirstPerson : ViewMode.ThirdPerson;
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

        public void FrameFallen(FallenView fallen, float groundY, float elapsed)
        {
            if (fallen == null)
            {
                throw new ArgumentNullException(nameof(fallen));
            }

            if (!_engaged || _camera == null)
            {
                return;
            }

            _freeActive = false;
            var eye = fallen.Eye(_deathEye.ToNumerics(), groundY, elapsed).ToUnity();
            var position = _rig != null ? _rig.ResolveBoomPosition(_deathEye, eye - _deathEye) : eye;
            var rotation = Quaternion.Euler(fallen.Pitch(_deathPitch, elapsed), _fallbackYaw, fallen.Roll(elapsed));
            _camera.transform.SetPositionAndRotation(position, rotation);
        }

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

        public void FrameEye(Vector3 eye, float yaw)
        {
            if (!_engaged || _camera == null)
            {
                return;
            }

            _freeActive = false;
            _camera.transform.SetPositionAndRotation(eye, Quaternion.Euler(0f, yaw, 0f));
        }

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

        public Vector2 ReadMove()
        {
            return _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
        }

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
