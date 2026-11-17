using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed partial class AimDownSights : MonoBehaviour
    {
        public const string AimActionPath = InputActionPaths.Aim;

        public const string LogTag = "[View]";

        [SerializeField, Min(0f), Tooltip("腰だめから覗き込み切るまで (と戻るまで) の秒数")]
        private float _transitionSeconds = AdsZoom.DefaultTransitionSeconds;

        [SerializeField, Min(0.01f), Tooltip("武器を持っていないときに覗き込み切ったときの視野角 (度)。0 より大きく 180 未満")]
        private float _fallbackFieldOfView = AdsZoom.DefaultFieldOfView;

        private Camera _camera;
        private FirstPersonLook _look;
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _aimAction;
        private bool _enabledAimAction;
        private bool _isLive;
        private AdsZoom _zoom;
        private float _hipFieldOfView;

        private readonly HoldToggleState _aimState = new HoldToggleState(HoldMode.Hold);
        private ControlSettings _controlSettings = ControlSettings.Default;
        private bool _listeningToSettings;
        private InputDeviceKind _device = InputDeviceKind.KeyboardMouse;

        // 止められている間もボタンの状態 (_aimState) は残す。ボルトを引く間だけ覗き込みを解き、終われば押し直さずに戻すため。
        private bool _blocked;

        public event Action<bool> AimingChanged;

        public Func<WeaponSpec> WeaponSource { get; set; }

        // 撃ち方の今の状態 (ITriggerModel.BlocksAds) は WeaponSpec に無いので、WeaponSource とは別の口にしている。
        public Func<bool> AdsBlocker { get; set; }

        public Camera Camera => _camera;

        public FirstPersonLook Look => _look;

        public InputAction AimAction => _aimAction;

        public bool IsInitialized => _camera != null && _look != null && _aimAction != null && _zoom != null;

        public bool IsAiming => _aimState.IsActive && !_blocked;

        public ControlSettings ControlSettings
        {
            get => _controlSettings;
            set => _controlSettings = value ?? throw new ArgumentNullException(nameof(value));
        }

        public void ApplyControlSettings(ControlSettings settings)
        {
            ControlSettings = settings;
            if (!IsInitialized)
            {
                return;
            }

            HipFieldOfView = settings.FieldOfView;
            Apply(_zoom.FieldOfView(_hipFieldOfView, TargetFieldOfView));
        }

        public InputDeviceKind ActiveDevice => _device;

        public HoldMode Mode => _aimState.Mode;

        public float Progress => _zoom != null ? _zoom.Progress : 0f;

        public float HipFieldOfView
        {
            get => _hipFieldOfView;
            set => _hipFieldOfView = AdsZoom.ValidateFieldOfView(value, nameof(value));
        }

        public float TransitionSeconds
        {
            get => _transitionSeconds;
            set
            {
                _transitionSeconds = AdsZoom.ValidateTransitionSeconds(value, nameof(value));
                if (_zoom != null)
                {
                    _zoom.TransitionSeconds = _transitionSeconds;
                }
            }
        }

        public float FallbackFieldOfView
        {
            get => _fallbackFieldOfView;
            set => _fallbackFieldOfView = AdsZoom.ValidateFieldOfView(value, nameof(value));
        }

        public float TargetFieldOfView
        {
            get
            {
                return ScopeFieldOfView();
            }
        }

        private void Awake()
        {
            // 専用サーバーには画面も入力も無く、覗き込む相手がいない。
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                enabled = false;
                return;
            }

            AppliedControlSettings.Changed += ApplyControlSettings;
            _listeningToSettings = true;
            if (!IsInitialized && !TryInitializeFromHierarchy())
            {
                enabled = false;
            }

            ApplyControlSettings(AppliedControlSettings.Current);
        }

        private void OnDestroy()
        {
            if (_listeningToSettings)
            {
                AppliedControlSettings.Changed -= ApplyControlSettings;
                _listeningToSettings = false;
            }
        }

        private void OnEnable()
        {
            if (!IsInitialized)
            {
                // Awake で解決に失敗したまま再び有効にされた場合。エラーは解決時に出している。
                enabled = false;
                return;
            }

            _isLive = true;
            AcquireAction();
        }

        private void OnDisable()
        {
            _isLive = false;
            ReleaseAction();
            if (IsInitialized)
            {
                ResetView();
            }
        }

        private void Update()
        {
            ReadCycleMagnification();
            ReadHoldBreath();
            Tick(
                _aimAction.IsPressed(),
                _aimAction.WasPressedThisFrame(),
                InputDeviceKinds.Of(_aimAction, _device),
                Time.deltaTime);
        }

        public void Tick(bool isPressed, bool wasPressedThisFrame, float deltaSeconds)
        {
            Tick(isPressed, wasPressedThisFrame, _device, deltaSeconds);
        }

        public void Tick(bool isPressed, bool wasPressedThisFrame, InputDeviceKind device, float deltaSeconds)
        {
            EnsureInitialized();

            var mode = _controlSettings.Get(device, HoldAction.Aim);
            _device = device;
            var wasAiming = IsAiming;
            _aimState.Mode = mode;
            _aimState.Update(isPressed, wasPressedThisFrame);
            _blocked = AdsBlocker != null && AdsBlocker();
            var aiming = IsAiming;
            if (aiming != wasAiming)
            {
                ReportAiming(aiming);
            }

            // AimingChanged の購読側が同じフレームで Release することがある (回避中など) ので、決めた後の状態で寄せる。
            _zoom.Step(IsAiming, deltaSeconds);
            StepZoomLevel(deltaSeconds);
            Apply(_zoom.FieldOfView(_hipFieldOfView, TargetFieldOfView));
            StepBreath(deltaSeconds);
            StepSight();
        }

        public bool Release()
        {
            var wasAiming = IsAiming;
            _aimState.Release();
            if (!wasAiming)
            {
                return false;
            }

            ReportAiming(false);
            return true;
        }

        public void ResetView()
        {
            EnsureInitialized();

            var wasAiming = IsAiming;
            _aimState.Release();
            if (wasAiming)
            {
                ReportAiming(false);
            }

            _zoom.Reset();
            SnapToFirstZoomLevel();
            Apply(_hipFieldOfView);
            ResetBreath();
            StepSight();
        }

        public bool TryInitializeFromHierarchy()
        {
            var camera = FirstPersonLook.FindChildCamera(transform);
            if (camera == null)
            {
                Debug.LogError($"{LogTag} '{name}' の子に Camera がありません。視野角を変えるカメラを子 GameObject に置いてください。", this);
                return false;
            }

            var look = GetComponent<FirstPersonLook>();
            if (look == null)
            {
                Debug.LogError($"{LogTag} '{name}' に FirstPersonLook がありません。ADS 中の感度はそこで下げます。", this);
                return false;
            }

            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError(
                    $"{LogTag} プロジェクト全体の Input Actions が設定されていません (Project Settings > Input System Package)。",
                    this);
                return false;
            }

            var aim = actions.FindAction(AimActionPath);
            if (aim == null)
            {
                Debug.LogError($"{LogTag} Input Actions '{actions.name}' に '{AimActionPath}' がありません。", this);
                return false;
            }

            var cycleMagnification = FindCycleMagnificationAction(actions);
            var holdBreath = FindHoldBreathAction(actions);

            try
            {
                Initialize(camera, look, aim, cycleMagnification, holdBreath);
            }
            catch (ArgumentOutOfRangeException e)
            {
                Debug.LogError($"{LogTag} '{name}' の ADS の設定値が不正です: {e.Message}", this);
                return false;
            }

            return true;
        }

        public void Initialize(Camera camera, FirstPersonLook look, InputAction aimAction, InputAction cycleMagnificationAction = null, InputAction holdBreathAction = null)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            if (look == null)
            {
                throw new ArgumentNullException(nameof(look));
            }

            if (aimAction == null)
            {
                throw new ArgumentNullException(nameof(aimAction));
            }

            if (camera.transform == transform || !camera.transform.IsChildOf(transform))
            {
                throw new ArgumentException($"camera '{camera.name}' must be a descendant of '{name}'", nameof(camera));
            }

            if (look.gameObject != gameObject)
            {
                throw new ArgumentException($"look must be on '{name}'", nameof(look));
            }

            // 前のカメラをズームしたまま残さない。
            if (IsInitialized)
            {
                ResetView();
            }

            var hip = AdsZoom.ValidateFieldOfView(camera.fieldOfView, nameof(camera));
            var zoom = new AdsZoom(_transitionSeconds);
            AdsZoom.ValidateFieldOfView(_fallbackFieldOfView, nameof(FallbackFieldOfView));

            if (_isLive)
            {
                ReleaseAction();
            }

            _camera = camera;
            _look = look;
            _aimAction = aimAction;
            _hipFieldOfView = hip;
            _zoom = zoom;
            _aimState.Release();
            SnapToFirstZoomLevel();

            if (_isLive)
            {
                AcquireAction();
            }

            BindCycleMagnificationAction(cycleMagnificationAction);
            BindBreath(camera, holdBreathAction);
        }

        public static string FormatAdsLog(bool aiming)
        {
            return $"{LogTag} ads={(aiming ? "true" : "false")}";
        }

        private void Apply(float fieldOfView)
        {
            _camera.fieldOfView = fieldOfView;

            var adsScale = 1f + ((_controlSettings.AdsSensitivity - 1f) * _zoom.Progress);
            _look.SensitivityMultiplier = AdsZoom.SensitivityMultiplier(_hipFieldOfView, fieldOfView) * adsScale;
        }

        private void ReportAiming(bool aiming)
        {
            Debug.Log(FormatAdsLog(aiming), this);
            if (!aiming)
            {
                ReturnToFirstZoomLevel();
            }

            AimingChanged?.Invoke(aiming);
        }

        private void AcquireAction()
        {
            // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。
            if (!_aimAction.enabled)
            {
                _aimAction.Enable();
                _enabledAimAction = true;
            }

            AcquireCycleMagnificationAction();
            AcquireHoldBreathAction();
        }

        private void ReleaseAction()
        {
            if (_enabledAimAction && _aimAction != null)
            {
                _aimAction.Disable();
            }

            _enabledAimAction = false;
            ReleaseCycleMagnificationAction();
            ReleaseHoldBreathAction();
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("AimDownSights is not initialized");
            }
        }
    }
}
