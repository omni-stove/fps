using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Runtime
{
    // 時間を止めるかはポーズ画面 (PauseMenu.TimeMode) に任せ、ここでは触らない。オンラインでは時間が進んだまま自分のカメラだけ自由にする
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PauseMenu))]
    public sealed class PhotoModeView : HudElementView
    {
        public const string LogTag = PhotoModeCamera.LogTag;
        public const string ButtonName = "pause-photo-mode";
        public const string HudRootName = "hud";

        public const string MoveActionPath = InputActionPaths.Move;
        public const string LookActionPath = InputActionPaths.Look;
        public const string CaptureActionPath = InputActionPaths.Attack;
        public const string ZoomInActionPath = InputActionPaths.Next;
        public const string ZoomOutActionPath = InputActionPaths.Previous;
        public const string RollLeftActionPath = InputActionPaths.Cover;
        public const string RollRightActionPath = InputActionPaths.Interact;
        public const string ResetLensActionPath = InputActionPaths.Reload;

        private PhotoModeSpec _spec = PhotoModeSpec.Default;
        private IPhotoCapture _photoCapture;
        private IPhotoCapture _defaultPhotoCapture;
        private Component _target;
        private PhotoModeCamera _session;
        private PauseMenu _pause;
        private Button _button;
        private VisualElement _hiddenHudRoot;
        private StyleEnum<DisplayStyle> _hudRootDisplay;

        // ポーズ画面は共有のアクション (InputSystem.actions) の Player を止め続けるので、複製を自分だけで有効にして読む。
        // 共有のものを有効にすると、プレイヤーの移動や射撃もその入力で動いてしまう
        private InputActionAsset _actions;
        private InputAction _move;
        private InputAction _look;
        private InputAction _capture;
        private InputAction _zoomIn;
        private InputAction _zoomOut;
        private InputAction _rollLeft;
        private InputAction _rollRight;
        private InputAction _resetLens;

        public override string ElementName => PauseMenu.UxmlName;

        public bool IsActive => _session != null && _session.IsActive;

        public PhotoModeCamera Session => IsActive ? _session : null;

        public PhotoModeSpec Spec
        {
            get => _spec;
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                // 動ける範囲と視野角の範囲は入った時点の値で決めているので、途中で替えると今のカメラが範囲の外に残る
                if (IsActive)
                {
                    throw new InvalidOperationException("the photo mode spec cannot change while the photo mode is active");
                }

                _spec = value;
            }
        }

        public IPhotoCapture PhotoCapture
        {
            get => _photoCapture;
            set => _photoCapture = value;
        }

        public Component Target
        {
            get => _target;
            set => _target = value;
        }

        public event Action<string> PhotoTaken;

        public event Action Entered;

        public event Action Exited;

        public bool Enter()
        {
            if (IsActive)
            {
                return true;
            }

            Exit();

            var pause = GetComponent<PauseMenu>();
            if (pause == null || !pause.IsOpen)
            {
                Debug.LogWarning(LogTag + " open the pause menu before entering the photo mode", this);
                return false;
            }

            var target = ResolveTarget();
            if (target == null)
            {
                Debug.LogWarning(LogTag + " no player view to photograph; set PhotoModeView.Target or spawn a player with a FirstPersonLook", this);
                return false;
            }

            if (IsDown(target))
            {
                Debug.LogWarning(LogTag + " " + target.name + " is dead; the photo mode waits until the player is back", this);
                return false;
            }

            var session = new PhotoModeCamera(target, _spec);
            if (!session.Begin())
            {
                return false;
            }

            _session = session;
            _pause = pause;
            _pause.Closed += HandlePauseClosed;
            HideHud();
            AcquireInput();
            if (Application.isPlaying)
            {
                // マウスで向きを変えるので、ポーズ画面が出したカーソルをしまう。抜けたら出し直す
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
            }

            Debug.Log(LogTag + " entered time=" + (Time.timeScale == 0f ? "frozen" : "running") + " input=" + (_actions != null ? "on" : "off"), this);
            Raise(Entered, nameof(Entered));
            return true;
        }

        public void Exit()
        {
            if (_session == null)
            {
                return;
            }

            var session = _session;
            _session = null;
            var pause = _pause;
            _pause = null;
            if (pause != null)
            {
                pause.Closed -= HandlePauseClosed;
            }

            ReleaseInput();
            session.End();
            ShowHud();

            var pauseOpen = pause != null && pause.IsOpen;
            if (pauseOpen)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.None;
                    UnityEngine.Cursor.visible = true;
                }

                _button?.Focus();
            }

            Debug.Log(LogTag + " exited" + (pauseOpen ? " to the pause menu" : string.Empty), this);
            Raise(Exited, nameof(Exited));
        }

        public void Advance(Vector2 move, Vector2 lookDegrees, int zoomInSteps, float rollInput, bool resetLens, float deltaSeconds)
        {
            if (!IsActive)
            {
                return;
            }

            _session.Advance(move, lookDegrees, zoomInSteps, rollInput, resetLens, deltaSeconds);
        }

        public string TakePhoto()
        {
            if (!IsActive)
            {
                Debug.LogWarning(LogTag + " enter the photo mode before taking a photo", this);
                return null;
            }

            var capture = _photoCapture ?? (_defaultPhotoCapture ??= new ScreenshotPhotoCapture());
            string path;
            try
            {
                path = capture.Capture();
            }
            catch (Exception e)
            {
                Debug.LogError(LogTag + " could not save the photo with " + capture.GetType().Name + ": " + e.Message, this);
                Debug.LogException(e, this);
                return null;
            }

            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning(LogTag + " " + capture.GetType().Name + " returned no path; nothing was saved", this);
                return null;
            }

            Debug.Log(LogTag + " saved " + path, this);
            if (PhotoTaken != null)
            {
                foreach (var handler in PhotoTaken.GetInvocationList())
                {
                    try
                    {
                        ((Action<string>)handler)(path);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e, this);
                    }
                }
            }

            return path;
        }

        protected override void OnBound(VisualElement element)
        {
            _button = element.Q<Button>(ButtonName);
            if (_button == null)
            {
                throw new InvalidOperationException(
                    $"the pause menu layout has no Button named '{ButtonName}'; check Assets/game/UI/Resources/Hud/Hud.uxml");
            }

            _button.clicked += HandleButtonClicked;
        }

        protected override void OnUnbinding(VisualElement element)
        {
            // 木を作り直すと隠した HUD の要素ごと消えるので、借りたカメラもここで戻す
            Exit();
            if (_button != null)
            {
                _button.clicked -= HandleButtonClicked;
            }

            _button = null;
        }

        private void Update()
        {
            if (_session == null)
            {
                return;
            }

            var reason = WhyCannotContinue();
            if (reason != null)
            {
                Debug.Log(LogTag + " leaving: " + reason, this);
                Exit();
                return;
            }

            // オフラインでは時間を止めているので、止まっていても進む時間で動かす
            var seconds = Time.unscaledDeltaTime;
            var zoom = (Pressed(_zoomIn) ? 1 : 0) - (Pressed(_zoomOut) ? 1 : 0);
            var roll = (Held(_rollLeft) ? 1f : 0f) - (Held(_rollRight) ? 1f : 0f);
            var lookOwner = _session.Target != null ? _session.Target.GetComponent<FirstPersonLook>() : null;
            var lookDegrees = lookOwner != null ? lookOwner.ReadLookDegrees(_look, seconds) : Vector2.zero;
            var move = _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;

            Advance(move, lookDegrees, zoom, roll, Pressed(_resetLens), seconds);
            if (Pressed(_capture))
            {
                TakePhoto();
            }
        }

        private void OnDisable()
        {
            Exit();
        }

        private void OnDestroy()
        {
            Exit();
        }

        private string WhyCannotContinue()
        {
            if (!_session.IsActive)
            {
                return "another view took over the camera";
            }

            if (_pause == null || !_pause.IsOpen)
            {
                return "the pause menu closed";
            }

            var target = _session.Target;
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                return "the player is gone";
            }

            if (IsDown(target))
            {
                return target.name + " died";
            }

            return _session.Camera == null ? "the camera is gone" : null;
        }

        private void HandleButtonClicked()
        {
            Enter();
        }

        private void HandlePauseClosed()
        {
            Exit();
        }

        private Component ResolveTarget()
        {
            if (_target != null)
            {
                return _target;
            }

            foreach (var look in FindObjectsByType<FirstPersonLook>(FindObjectsInactive.Exclude))
            {
                if (look.isActiveAndEnabled && look.IsInitialized)
                {
                    return look;
                }
            }

            return null;
        }

        // ダウン中は倒れきっていないので撮れる。死亡カメラ (PlayerDeathCamera・NetDeathCamera) が同じカメラを動かしている間は入らない
        private static bool IsDown(Component target)
        {
            var damageable = target.GetComponent<Damageable>();
            return damageable != null && damageable.IsDead && !damageable.IsDowned;
        }

        private void HideHud()
        {
            if (Element == null)
            {
                return;
            }

            var root = Element;
            while (root.parent != null && root.name != HudRootName)
            {
                root = root.parent;
            }

            // HudElementView は表示を visibility で切り替えるので、親の visibility では子が出たままになる。display で丸ごと隠す
            _hiddenHudRoot = root;
            _hudRootDisplay = root.style.display;
            root.style.display = DisplayStyle.None;
        }

        private void ShowHud()
        {
            if (_hiddenHudRoot == null)
            {
                return;
            }

            _hiddenHudRoot.style.display = _hudRootDisplay;
            _hiddenHudRoot = null;
        }

        private void AcquireInput()
        {
            // 画面なし (batchmode) や Edit Mode のテストでは入力が無い
            if (!Application.isPlaying || Application.isBatchMode)
            {
                return;
            }

            var source = InputSystem.actions;
            if (source == null)
            {
                Debug.LogWarning(LogTag + " project-wide input actions are not set; the photo camera cannot be moved", this);
                return;
            }

            _actions = Instantiate(source);
            _actions.name = source.name + " (PhotoMode)";
            try
            {
                // 設定画面で変えた割り当ては複製に付いてこないので、写し直す
                var overrides = source.SaveBindingOverridesAsJson();
                if (!string.IsNullOrEmpty(overrides))
                {
                    _actions.LoadBindingOverridesFromJson(overrides);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning(LogTag + " could not copy the rebound controls; using the default bindings: " + e.Message, this);
            }

            _move = EnableAction(MoveActionPath);
            _look = EnableAction(LookActionPath);
            _capture = EnableAction(CaptureActionPath);
            _zoomIn = EnableAction(ZoomInActionPath);
            _zoomOut = EnableAction(ZoomOutActionPath);
            _rollLeft = EnableAction(RollLeftActionPath);
            _rollRight = EnableAction(RollRightActionPath);
            _resetLens = EnableAction(ResetLensActionPath);
        }

        private InputAction EnableAction(string path)
        {
            var action = _actions.FindAction(path);
            if (action == null)
            {
                Debug.LogWarning(LogTag + " input action '" + path + "' was not found; the photo mode ignores it", this);
                return null;
            }

            action.Enable();
            return action;
        }

        private void ReleaseInput()
        {
            if (_actions != null)
            {
                _actions.Disable();
                Object.Destroy(_actions);
            }

            _actions = null;
            _move = null;
            _look = null;
            _capture = null;
            _zoomIn = null;
            _zoomOut = null;
            _rollLeft = null;
            _rollRight = null;
            _resetLens = null;
        }

        private static bool Pressed(InputAction action)
        {
            return action != null && action.WasPressedThisFrame();
        }

        private static bool Held(InputAction action)
        {
            return action != null && action.IsPressed();
        }

        private void Raise(Action handlers, string eventName)
        {
            if (handlers == null)
            {
                return;
            }

            // 1 つの購読先が投げても、ほかの購読先と出入りの続きを止めない
            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action)handler)();
                }
                catch (Exception e)
                {
                    Debug.LogError(LogTag + " a " + eventName + " handler threw: " + e.Message, this);
                    Debug.LogException(e, this);
                }
            }
        }
    }
}
