using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    // pitch の軸を ViewRig の目にするのは、三人称でもカメラは目の子なので、目を回せば肩越しのカメラも一緒に回るため。
    [DisallowMultipleComponent]
    public sealed class FirstPersonLook : MonoBehaviour
    {
        public const string LookActionPath = InputActionPaths.Look;

        public const float DefaultMouseSensitivity = 0.1f;

        public const float DefaultStickSensitivity = 180f;

        [SerializeField, Min(0f), Tooltip("マウスの 1 ピクセルあたりの回転 (度)")]
        private float _mouseSensitivity = DefaultMouseSensitivity;

        public const float DefaultGyroSensitivity = 1f;

        [SerializeField, Min(0f), Tooltip("スティックを倒し切ったときの毎秒の回転 (度)")]
        private float _stickSensitivity = DefaultStickSensitivity;

        [SerializeField, Min(0f), Tooltip("ジャイロのある機器を 1 度傾けたときの視点の回転 (度)")]
        private float _gyroSensitivity = DefaultGyroSensitivity;

        [NonSerialized]
        private GyroLook _gyroLook = new GyroLook();

        private readonly LookAngles _angles = new LookAngles();
        private Transform _pitchPivot;
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _lookAction;
        private bool _enabledLookAction;
        private float _sensitivityMultiplier = 1f;
        private float _turnMultiplier = 1f;
        private float _assistLookScale = 1f;
        private Vector2 _pendingAssistDegrees;
        private ControlSettings _controlSettings = ControlSettings.Default;
        private bool _listeningToSettings;
        private float _recoilPitchUp;
        private float _recoilYaw;
        private float _appliedRecoilPitchUp;
        private float _pitchLimit = LookAngles.DefaultMaxPitch;

        // ずれが変わらないフレームは軸を書き換えないので、反動の無い武器では今までと同じ。
        public RecoilState Recoil { get; set; }

        public float AppliedRecoilPitchUp => _appliedRecoilPitchUp;

        public float AppliedRecoilYaw => _recoilYaw;

        public Transform PitchPivot => _pitchPivot;

        public InputAction LookAction => _lookAction;

        public bool IsInitialized => _pitchPivot != null && _lookAction != null;
        public float Yaw => _angles.Yaw;
        public float Pitch => _angles.Pitch;

        public float MaxPitch => _angles.MaxPitch;

        public float PitchLimit
        {
            get => _pitchLimit;
            set
            {
                if (!(value > 0f))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "pitch limit must be positive");
                }

                _pitchLimit = Mathf.Min(value, _angles.MaxPitch);
                if (IsInitialized && Mathf.Abs(_angles.Pitch) > _pitchLimit)
                {
                    _angles.Set(_angles.Yaw, Mathf.Clamp(_angles.Pitch, -_pitchLimit, _pitchLimit));
                    ApplyRotation();
                }
            }
        }

        public float MouseSensitivity
        {
            get => _mouseSensitivity;
            set => _mouseSensitivity = Mathf.Max(0f, value);
        }

        public float StickSensitivity
        {
            get => _stickSensitivity;
            set => _stickSensitivity = Mathf.Max(0f, value);
        }

        public float GyroSensitivity
        {
            get => _gyroSensitivity;
            set => _gyroSensitivity = Mathf.Max(0f, value);
        }

        public float EffectiveGyroSensitivity => _gyroSensitivity * _sensitivityMultiplier;

        public GyroLook Gyro
        {
            get => _gyroLook;
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (value != _gyroLook)
                {
                    _gyroLook.Release();
                    _gyroLook = value;
                }
            }
        }

        // 覗き込みの部品を参照せず値で受けるのは、覗き込みが無い体 (乗り物など) でもこの部品だけで回せるようにするため。
        public bool Aiming { get; set; }

        // 保存する設定ではないので、シリアライズしない。
        public float SensitivityMultiplier
        {
            get => _sensitivityMultiplier;
            set
            {
                if (!(value >= 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "sensitivity multiplier must be a non-negative finite number");
                }

                _sensitivityMultiplier = value;
            }
        }

        // 覗き込みは SensitivityMultiplier を毎フレーム書き直すので、怯みなどで一時的に遅くする倍率はそれと別に持って掛け合わせる。
        // 観戦の自由カメラも ReadLookDegrees を使うので、そちらには掛けず、この視点を回すときにだけ掛ける。
        public float TurnMultiplier
        {
            get => _turnMultiplier;
            set
            {
                if (!(value >= 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "turn multiplier must be a non-negative finite number");
                }

                _turnMultiplier = value;
            }
        }

        // エイムアシストはスティックの弱さを補うものなので、TurnMultiplier と違いマウスで回すフレームには掛けない。
        public float AssistLookScale
        {
            get => _assistLookScale;
            set
            {
                if (!(value >= 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "assist look scale must be a non-negative finite number");
                }

                _assistLookScale = value;
            }
        }

        public Vector2 PendingAssistDegrees => _pendingAssistDegrees;

        // 置き換えでなく足し込むのは、渡す部品とこの部品の Update の順が決まっておらず、次に回すまでに 2 度渡されても取りこぼさないため。
        // 回せば空にするので、同じ角度を 2 度回すことは無い。
        public void AddAssistDegrees(Vector2 degrees)
        {
            if (!IsFinite(degrees.x) || !IsFinite(degrees.y))
            {
                throw new ArgumentOutOfRangeException(nameof(degrees), degrees, "assist degrees must be finite");
            }

            _pendingAssistDegrees += degrees;
        }

        public void ClearAssist()
        {
            _assistLookScale = 1f;
            _pendingAssistDegrees = Vector2.zero;
        }

        public ControlSettings ControlSettings
        {
            get => _controlSettings;
            set => _controlSettings = value ?? throw new ArgumentNullException(nameof(value));
        }

        public float EffectiveMouseSensitivity =>
            _mouseSensitivity * _controlSettings.LookSensitivity(InputDeviceKind.KeyboardMouse) * _sensitivityMultiplier;

        public float EffectiveStickSensitivity =>
            _stickSensitivity * _controlSettings.LookSensitivity(InputDeviceKind.Gamepad) * _sensitivityMultiplier;

        // コンポーネントを無効にするのと違い、カーソルのロックとアクションの有効状態は保つので、止めている間にカーソルが出ない。
        public bool InputSuspended { get; set; }

        private void Awake()
        {
            // 専用サーバーには画面も入力も無く、視点を動かす相手がいない。カーソル操作も要らないので最初から止める。
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                Debug.Log($"[FirstPersonLook] '{name}': disabled on a dedicated server build", this);
                enabled = false;
                return;
            }

            _controlSettings = AppliedControlSettings.Current;
            AppliedControlSettings.Changed += HandleSettingsChanged;
            _listeningToSettings = true;
            if (!IsInitialized && !TryInitializeFromHierarchy())
            {
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (_listeningToSettings)
            {
                AppliedControlSettings.Changed -= HandleSettingsChanged;
                _listeningToSettings = false;
            }

            _gyroLook.Release();
        }

        private void HandleSettingsChanged(ControlSettings settings)
        {
            _controlSettings = settings;
            if (!settings.GyroEnabled)
            {
                _gyroLook.Release();
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

            SyncAnglesFromTransforms();

            // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。
            if (!_lookAction.enabled)
            {
                _lookAction.Enable();
                _enabledLookAction = true;
            }

            // ポーズ中やロードアウト選択中に出現し直したプレイヤーが、画面を操作しているカーソルを奪わないようにする。閉じたときに各画面がロックする。
            if (!GameUi.IsPauseOpen && !GameUi.IsLoadoutSelectOpen && !GameUi.IsInventoryOpen)
            {
                LockCursor(true);
            }
        }

        private void OnDisable()
        {
            if (_enabledLookAction && _lookAction != null)
            {
                _lookAction.Disable();
            }

            _enabledLookAction = false;
            // 止めている間 (死亡など) に溜まった吸い付きを、戻ったときに一度に回さないよう捨てる。
            _pendingAssistDegrees = Vector2.zero;
            _gyroLook.Release();
            LockCursor(false);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // フォーカスを失うと OS がロックを外すので、戻ったときにかけ直す。
            if (hasFocus && isActiveAndEnabled && !GameUi.IsPauseOpen && !GameUi.IsLoadoutSelectOpen && !GameUi.IsInventoryOpen)
            {
                LockCursor(true);
            }
        }

        // ポーズ画面を開いている間に視点操作が有効・無効になっていることがあるので、開く前の状態には戻さずシーンから決め直す。
        public static void RefreshCursorLock()
        {
            if (GameUi.IsLoadoutSelectOpen || GameUi.IsInventoryOpen)
            {
                LockCursor(false);
                return;
            }

            var anyActive = false;
            foreach (var look in FindObjectsByType<FirstPersonLook>(FindObjectsInactive.Exclude))
            {
                if (look.isActiveAndEnabled && look.IsInitialized)
                {
                    anyActive = true;
                    break;
                }
            }

            LockCursor(anyActive);
        }

        private void Update()
        {
            if (InputSuspended)
            {
                _pendingAssistDegrees = Vector2.zero;
                return;
            }

            var fromPointer = _lookAction != null && _lookAction.activeControl?.device is Pointer;
            var deltaSeconds = Time.deltaTime;
            Turn(ReadLookDegrees(deltaSeconds), fromPointer, ReadGyroDegrees(deltaSeconds));
        }

        public void Turn(Vector2 lookDegrees, bool fromPointer)
        {
            Turn(lookDegrees, fromPointer, Vector2.zero);
        }

        // ジャイロはスティックの弱さを補うものではないので、エイムアシストの倍率を掛けずに足す。
        public void Turn(Vector2 lookDegrees, bool fromPointer, Vector2 gyroDegrees)
        {
            if (!IsFinite(gyroDegrees.x) || !IsFinite(gyroDegrees.y))
            {
                throw new ArgumentOutOfRangeException(nameof(gyroDegrees), gyroDegrees, "gyro degrees must be finite");
            }

            var degrees = fromPointer ? lookDegrees : (lookDegrees * _assistLookScale) + _pendingAssistDegrees;
            degrees += gyroDegrees;
            _pendingAssistDegrees = Vector2.zero;
            degrees *= _turnMultiplier;
            if (degrees == Vector2.zero)
            {
                return;
            }

            ApplyLookDelta(degrees);
        }

        // InputSuspended でも読むのは、視点を止めている間に別のカメラ (観戦の自由カメラ) を同じ感度で回すため。
        public Vector2 ReadLookDegrees(float deltaSeconds)
        {
            return ReadLookDegrees(_lookAction, deltaSeconds);
        }

        // ポーズ中のフォトモードは、ポーズ画面が止めている共有の Look を有効にし直さず、複製したアクションを同じ感度で読む
        public Vector2 ReadLookDegrees(InputAction action, float deltaSeconds)
        {
            if (action == null)
            {
                return Vector2.zero;
            }

            var raw = action.ReadValue<Vector2>();
            if (raw == Vector2.zero)
            {
                return Vector2.zero;
            }

            return ToLookDegrees(raw, action.activeControl?.device is Pointer, deltaSeconds);
        }

        public Vector2 ReadGyroDegrees(float deltaSeconds)
        {
            return _gyroLook.ReadLookDegrees(_controlSettings, Aiming, EffectiveGyroSensitivity, deltaSeconds);
        }

        public Vector2 ToLookDegrees(Vector2 raw, bool fromPointer, float deltaSeconds)
        {
            // マウスやタッチの delta はフレームあたりの移動量なので deltaTime を掛けない。スティックは速度として扱う。
            var degrees = fromPointer ? raw * EffectiveMouseSensitivity : raw * (EffectiveStickSensitivity * deltaSeconds);
            degrees.y = _controlSettings.ApplyInvertLookY(degrees.y);
            return degrees;
        }

        // 射撃 (WeaponController.Update) が反動を足した後に向きへ反映する。
        private void LateUpdate()
        {
            ApplyRecoil();
        }

        public void ApplyRecoil()
        {
            if (!IsInitialized)
            {
                return;
            }

            var recoil = Recoil;
            var pitchUp = recoil != null ? recoil.PitchUpDegrees : 0f;
            var yaw = recoil != null ? recoil.YawDegrees : 0f;
            if (pitchUp == _recoilPitchUp && yaw == _recoilYaw)
            {
                return;
            }

            _recoilPitchUp = pitchUp;
            _recoilYaw = yaw;
            ApplyRotation();
        }

        public bool TryInitializeFromHierarchy()
        {
            var camera = FindChildCamera(transform);
            if (camera == null)
            {
                Debug.LogError(
                    $"[FirstPersonLook] '{name}' の子に Camera がありません。pitch を回すカメラを子 GameObject に置いてください。",
                    this);
                return false;
            }

            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError(
                    "[FirstPersonLook] プロジェクト全体の Input Actions が設定されていません (Project Settings > Input System Package)。",
                    this);
                return false;
            }

            var look = actions.FindAction(LookActionPath);
            if (look == null)
            {
                Debug.LogError(
                    $"[FirstPersonLook] Input Actions '{actions.name}' に '{LookActionPath}' がありません。",
                    this);
                return false;
            }

            var eye = ViewRig.FindEye(transform);
            Initialize(eye != null ? eye : camera.transform, look);
            return true;
        }

        public void Initialize(Transform pitchPivot, InputAction lookAction)
        {
            if (pitchPivot == null)
            {
                throw new ArgumentNullException(nameof(pitchPivot));
            }

            if (lookAction == null)
            {
                throw new ArgumentNullException(nameof(lookAction));
            }

            if (pitchPivot == transform || !pitchPivot.IsChildOf(transform))
            {
                throw new ArgumentException(
                    $"pitchPivot '{pitchPivot.name}' must be a descendant of '{name}' so yaw and pitch rotate separately",
                    nameof(pitchPivot));
            }

            if (_enabledLookAction && _lookAction != null && _lookAction != lookAction)
            {
                _lookAction.Disable();
                _enabledLookAction = false;
            }

            if (_pitchPivot != pitchPivot)
            {
                // 足していた反動は前の軸に書いたものなので、新しい軸の向きから引かない。次の LateUpdate で足し直す。
                _recoilPitchUp = 0f;
                _recoilYaw = 0f;
                _appliedRecoilPitchUp = 0f;
            }

            _pitchPivot = pitchPivot;
            _lookAction = lookAction;
            SyncAnglesFromTransforms();
        }

        public void ApplyLookDelta(Vector2 degrees)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("FirstPersonLook is not initialized");
            }

            _angles.Rotate(degrees.x, degrees.y);
            if (Mathf.Abs(_angles.Pitch) > _pitchLimit)
            {
                _angles.Set(_angles.Yaw, Mathf.Clamp(_angles.Pitch, -_pitchLimit, _pitchLimit));
            }

            ApplyRotation();
        }

        // 反動は向き (_angles) に混ぜず、軸に書くときだけ足す。混ぜると戻ったときに視点操作の分まで戻ってしまう。
        private void ApplyRotation()
        {
            transform.localRotation = Quaternion.Euler(0f, _angles.Yaw, 0f);
            var pitch = Mathf.Clamp(_angles.Pitch - _recoilPitchUp, -_pitchLimit, _pitchLimit);
            _appliedRecoilPitchUp = _angles.Pitch - pitch;
            _pitchPivot.localRotation = Quaternion.Euler(pitch, _recoilYaw, 0f);
        }

        public static Camera FindChildCamera(Transform body)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            foreach (var camera in body.GetComponentsInChildren<Camera>(true))
            {
                if (camera.transform != body)
                {
                    return camera;
                }
            }

            return null;
        }

        private void SyncAnglesFromTransforms()
        {
            if (_pitchPivot == null)
            {
                return;
            }

            _angles.Set(transform.localEulerAngles.y, LookAngles.ToSigned(_pitchPivot.localEulerAngles.x) + _appliedRecoilPitchUp);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
