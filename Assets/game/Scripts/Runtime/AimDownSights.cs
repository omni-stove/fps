using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// 覗き込み (ADS)。<see cref="AimActionPath"/> で覗き込んでいる間、カメラの視野角を今の武器の
    /// <see cref="WeaponSpec.AdsFieldOfView"/> へ <see cref="TransitionSeconds"/> かけて狭め、やめると腰だめの視野角へ戻す。
    /// ボタンは <see cref="ControlSettings"/> のモードで解釈する (押している間／押すたびに切り替え。機器は最後に Aim を押した機器)。
    /// リロードや持ち替えでは <see cref="Release"/>、死亡・リスポーンでは <see cref="ResetView"/> を、配線した側が呼ぶ。
    /// 視野角に合わせて同じ GameObject の <see cref="FirstPersonLook.SensitivityMultiplier"/> を下げる。計算は <see cref="AdsZoom"/>。
    /// 覗き込み始めと終わりに <see cref="AimingChanged"/> を呼び、<c>[View] ads=true</c> / <c>[View] ads=false</c> をログに出す
    /// (ダッシュの解除は購読した側が行う)。見た目だけの機能で、サーバーには何も送らない。
    /// 参照はコードで解決するので Inspector の配線は要らない: カメラは子階層の <see cref="Camera"/>、<see cref="FirstPersonLook"/> は同じ
    /// GameObject、入力はプロジェクト全体のアクション (<see cref="InputSystem.actions"/>) の <see cref="AimActionPath"/>。
    /// 解決できなければエラーを出して自分を無効にする。無効になるとカメラと感度を腰だめに戻す。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AimDownSights : MonoBehaviour
    {
        /// <summary>Assets/InputSystem_Actions.inputactions の Aim アクション。</summary>
        public const string AimActionPath = InputActionPaths.Aim;

        public const string LogTag = "[View]";

        [SerializeField, Min(0f), Tooltip("腰だめから覗き込み切るまで (と戻るまで) の秒数")]
        private float _transitionSeconds = AdsZoom.DefaultTransitionSeconds;

        private Camera _camera;
        private FirstPersonLook _look;
        private InputAction _aimAction;
        private bool _enabledAimAction;
        private bool _isLive;
        private AdsZoom _zoom;
        private float _hipFieldOfView;

        private readonly HoldToggleState _aimState = new HoldToggleState(HoldMode.Hold);
        private ControlSettings _controlSettings = ControlSettings.Default;
        private InputDeviceKind _device = InputDeviceKind.KeyboardMouse;

        /// <summary>
        /// 覗き込み始めたとき (true) と、やめたとき (false) に呼ばれる。
        /// <see cref="Tick(bool, bool, InputDeviceKind, float)"/> では視野角を変える前に呼び、<see cref="Release"/> と <see cref="ResetView"/> でも呼ぶ。
        /// </summary>
        public event Action<bool> AimingChanged;

        /// <summary>
        /// 今持っている武器の値を返す。視野角はその <see cref="WeaponSpec.AdsFieldOfView"/> へ寄せる。
        /// null か、null を返すときは <see cref="AdsZoom.DefaultFieldOfView"/>。オフラインは GameBootstrap、オンラインは NetPlayer が渡す。
        /// </summary>
        public Func<WeaponSpec> WeaponSource { get; set; }

        /// <summary>視野角を変えるカメラ。未初期化なら null。</summary>
        public Camera Camera => _camera;

        /// <summary>感度の倍率を変える視点操作。未初期化なら null。</summary>
        public FirstPersonLook Look => _look;

        /// <summary>読んでいる Aim アクション。未初期化なら null。</summary>
        public InputAction AimAction => _aimAction;

        public bool IsInitialized => _camera != null && _look != null && _aimAction != null && _zoom != null;

        /// <summary>覗き込んでいるか (ボタンの判定。視野角はまだ動いている途中のことがある)。</summary>
        public bool IsAiming => _aimState.IsActive;

        /// <summary>
        /// ADS のモードを決める操作設定。Awake で <see cref="ControlSettingsStore"/> から読む (Edit Mode では Awake が走らないので初期値)。
        /// 変えると次の <see cref="Tick(bool, bool, InputDeviceKind, float)"/> から使う。
        /// </summary>
        public ControlSettings ControlSettings
        {
            get => _controlSettings;
            set => _controlSettings = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>最後に Aim を押した機器。ADS のモードはこの機器の設定を使う。</summary>
        public InputDeviceKind ActiveDevice => _device;

        /// <summary>今使っている ADS のモード。</summary>
        public HoldMode Mode => _aimState.Mode;

        /// <summary>覗き込みの進み具合。0 が腰だめ、1 が覗き込み切った状態。未初期化なら 0。</summary>
        public float Progress => _zoom != null ? _zoom.Progress : 0f;

        /// <summary>
        /// 腰だめの視野角 (度)。初期化したときのカメラの値。変えると、次の <see cref="Tick(bool, bool, InputDeviceKind, float)"/> からその値を基準にする。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">0 以下か 180 以上のとき。</exception>
        public float HipFieldOfView
        {
            get => _hipFieldOfView;
            set => _hipFieldOfView = AdsZoom.ValidateFieldOfView(value, nameof(value));
        }

        /// <summary>腰だめから覗き込み切るまでの秒数 (Inspector の値。初期化のときに読む)。</summary>
        public float TransitionSeconds => _transitionSeconds;

        /// <summary>今の武器で覗き込み切ったときの視野角 (度)。</summary>
        public float TargetFieldOfView
        {
            get
            {
                var spec = WeaponSource?.Invoke();
                return spec != null ? spec.AdsFieldOfView : AdsZoom.DefaultFieldOfView;
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

            _controlSettings = new ControlSettingsStore().Load();
            if (!IsInitialized && !TryInitializeFromHierarchy())
            {
                enabled = false;
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
            Tick(
                _aimAction.IsPressed(),
                _aimAction.WasPressedThisFrame(),
                InputDeviceKinds.Of(_aimAction, _device),
                Time.deltaTime);
        }

        /// <summary>
        /// 最後に Aim を押した機器 (<see cref="ActiveDevice"/>) のモードで <see cref="Tick(bool, bool, InputDeviceKind, float)"/> を呼ぶ。
        /// </summary>
        public void Tick(bool isPressed, bool wasPressedThisFrame, float deltaSeconds)
        {
            Tick(isPressed, wasPressedThisFrame, _device, deltaSeconds);
        }

        /// <summary>
        /// 1 フレーム分の Aim ボタンの状態を device の <see cref="ControlSettings"/> のモードで解釈して覗き込むかを決め、
        /// 視野角を deltaSeconds だけ寄せてカメラと感度に反映する。モードが変わると覗き込みをやめてから入れ替える。
        /// </summary>
        /// <param name="isPressed">今 Aim が押されているか。</param>
        /// <param name="wasPressedThisFrame">今フレームに Aim が押されたか。</param>
        /// <param name="device">Aim を操作している機器。</param>
        /// <param name="deltaSeconds">進める秒数。0 以上の有限値。</param>
        public void Tick(bool isPressed, bool wasPressedThisFrame, InputDeviceKind device, float deltaSeconds)
        {
            EnsureInitialized();

            var mode = _controlSettings.Get(device, HoldAction.Aim);
            _device = device;
            var wasAiming = _aimState.IsActive;
            _aimState.Mode = mode;
            var aiming = _aimState.Update(isPressed, wasPressedThisFrame);
            if (aiming != wasAiming)
            {
                ReportAiming(aiming);
            }

            // AimingChanged の購読側が同じフレームで Release することがある (回避中など) ので、決めた後の状態で寄せる。
            _zoom.Step(_aimState.IsActive, deltaSeconds);
            Apply(_zoom.FieldOfView(_hipFieldOfView, TargetFieldOfView));
        }

        /// <summary>
        /// 覗き込みをやめる (リロードを始めたときや持ち替えたときなど)。視野角は次の <see cref="Tick(bool, bool, InputDeviceKind, float)"/> から腰だめへ補間して戻す。
        /// 覗き込んでいたなら <see cref="AimingChanged"/> を false で呼んで true。ボタンを押し続けていても、押し直すまで覗き込まない。
        /// 未初期化でも呼べる (覗き込んでいないので何もしない)。
        /// </summary>
        public bool Release()
        {
            if (!_aimState.IsActive)
            {
                return false;
            }

            _aimState.Release();
            ReportAiming(false);
            return true;
        }

        /// <summary>
        /// 覗き込みをやめ、カメラの視野角と感度を即座に腰だめに戻す (死んだときや視点を止めたときなど)。
        /// 覗き込んでいたなら <see cref="AimingChanged"/> を false で呼ぶ。ボタンを押し続けていても、押し直すまで覗き込まない。
        /// </summary>
        public void ResetView()
        {
            EnsureInitialized();

            if (_aimState.IsActive)
            {
                _aimState.Release();
                ReportAiming(false);
            }

            _zoom.Reset();
            Apply(_hipFieldOfView);
        }

        /// <summary>
        /// 子階層のカメラ、同じ GameObject の <see cref="FirstPersonLook"/>、プロジェクト全体の Aim アクションを探して初期化する。
        /// 見つからないか設定値が不正なら理由を Debug.LogError に出して false を返す。
        /// </summary>
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

            try
            {
                Initialize(camera, look, aim);
            }
            catch (ArgumentOutOfRangeException e)
            {
                Debug.LogError($"{LogTag} '{name}' の ADS の設定値が不正です: {e.Message}", this);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 参照を直接渡して初期化する。camera はこの Transform の子孫、look はこの GameObject のものであること。
        /// 腰だめの視野角は、このときの camera の視野角にする。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">camera の視野角か <see cref="TransitionSeconds"/> が不正なとき。</exception>
        public void Initialize(Camera camera, FirstPersonLook look, InputAction aimAction)
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

            // OnEnable 済み (動作中) に差し替えるなら、自分が有効にした古いアクションを戻してから新しいものを有効にする。
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

            if (_isLive)
            {
                AcquireAction();
            }
        }

        /// <summary>ADS の状態のログ。例: <c>[View] ads=true</c>。</summary>
        public static string FormatAdsLog(bool aiming)
        {
            return $"{LogTag} ads={(aiming ? "true" : "false")}";
        }

        private void Apply(float fieldOfView)
        {
            _camera.fieldOfView = fieldOfView;
            _look.SensitivityMultiplier = AdsZoom.SensitivityMultiplier(_hipFieldOfView, fieldOfView);
        }

        private void ReportAiming(bool aiming)
        {
            Debug.Log(FormatAdsLog(aiming), this);
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
        }

        private void ReleaseAction()
        {
            if (_enabledAimAction && _aimAction != null)
            {
                _aimAction.Disable();
            }

            _enabledAimAction = false;
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
