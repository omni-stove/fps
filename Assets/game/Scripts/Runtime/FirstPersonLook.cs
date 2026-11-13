using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// 視点操作 (一人称・三人称共通)。Look 入力で本体 (この GameObject) を yaw、子の pitch の軸を pitch (±85°) で回す。
    /// 有効な間はカーソルをロックする (<see cref="PauseMenu"/> が開いている間を除く)。参照はコードで解決するので Inspector の配線は要らない:
    /// pitch の軸は、本体に <see cref="ViewRig"/> があればその目 (カメラの親)、無ければ子階層の <see cref="Camera"/>。
    /// 入力はプロジェクト全体のアクション (<see cref="InputSystem.actions"/>) の <see cref="LookActionPath"/>。
    /// 三人称でもカメラは目の子なので、目を回せば肩越しのカメラも一緒に回る。
    /// <see cref="Recoil"/> があれば、その反動のずれを pitch の軸にだけ足す (本体の yaw と <see cref="Yaw"/> / <see cref="Pitch"/> は変えない)。
    /// 解決できなければエラーを出して自分を無効にする。
    /// 感度には操作設定 (<see cref="ControlSettings"/>) のマウス・ゲームパッドごとの倍率を掛け、設定画面で変えるとその場で効く。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FirstPersonLook : MonoBehaviour
    {
        /// <summary>Assets/InputSystem_Actions.inputactions の Look アクション。</summary>
        public const string LookActionPath = InputActionPaths.Look;

        /// <summary>マウスの 1 ピクセルあたりの回転 (度)。</summary>
        public const float DefaultMouseSensitivity = 0.1f;

        /// <summary>スティックを倒し切ったときの毎秒の回転 (度)。</summary>
        public const float DefaultStickSensitivity = 180f;

        [SerializeField, Min(0f), Tooltip("マウスの 1 ピクセルあたりの回転 (度)")]
        private float _mouseSensitivity = DefaultMouseSensitivity;

        [SerializeField, Min(0f), Tooltip("スティックを倒し切ったときの毎秒の回転 (度)")]
        private float _stickSensitivity = DefaultStickSensitivity;

        private readonly LookAngles _angles = new LookAngles();
        private Transform _pitchPivot;
        private InputAction _lookAction;
        private bool _enabledLookAction;
        private float _sensitivityMultiplier = 1f;
        private ControlSettings _controlSettings = ControlSettings.Default;
        private bool _listeningToSettings;
        private float _recoilPitchUp;
        private float _recoilYaw;
        private float _appliedRecoilPitchUp;

        /// <summary>
        /// 視点に足す反動。null なら足さない。射撃 (<see cref="WeaponController.Recoil"/>) と同じものを配線する。
        /// ずれは毎フレームの LateUpdate で pitch の軸に足す。ずれが変わらないフレームは軸を書き換えないので、反動の無い武器では今までと同じ。
        /// </summary>
        public RecoilState Recoil { get; set; }

        /// <summary>今 pitch の軸に足している反動の上向きの角度 (度)。視点の上限で削られた後の値。</summary>
        public float AppliedRecoilPitchUp => _appliedRecoilPitchUp;

        /// <summary>今 pitch の軸に足している反動の左右の角度 (度)。正で右。</summary>
        public float AppliedRecoilYaw => _recoilYaw;

        /// <summary>pitch を回す Transform (ViewRig の目、無ければカメラ)。未初期化なら null。</summary>
        public Transform PitchPivot => _pitchPivot;

        /// <summary>読んでいる Look アクション。未初期化なら null。</summary>
        public InputAction LookAction => _lookAction;

        public bool IsInitialized => _pitchPivot != null && _lookAction != null;
        public float Yaw => _angles.Yaw;
        public float Pitch => _angles.Pitch;

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

        /// <summary>
        /// マウスとスティックの両方の感度に掛ける倍率。既定は 1。覗き込み (<see cref="AimDownSights"/>) が視野角に合わせて下げる。
        /// 保存する設定ではないので、シリアライズしない。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">負か、有限でない値のとき。</exception>
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

        /// <summary>
        /// 感度の倍率 (<see cref="ControlSettings.LookSensitivity"/>) を決める操作設定。Awake で <see cref="AppliedControlSettings.Current"/> を読み、
        /// 設定画面で変わるたびに入れ直す (Edit Mode では Awake が走らないので初期値)。変えると次のフレームから使う。
        /// </summary>
        public ControlSettings ControlSettings
        {
            get => _controlSettings;
            set => _controlSettings = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// 今効いているマウスの感度 (<see cref="MouseSensitivity"/> × 設定のマウス感度 × <see cref="SensitivityMultiplier"/>)。
        /// </summary>
        public float EffectiveMouseSensitivity =>
            _mouseSensitivity * _controlSettings.LookSensitivity(InputDeviceKind.KeyboardMouse) * _sensitivityMultiplier;

        /// <summary>
        /// 今効いているスティックの感度 (<see cref="StickSensitivity"/> × 設定のゲームパッド感度 × <see cref="SensitivityMultiplier"/>)。
        /// </summary>
        public float EffectiveStickSensitivity =>
            _stickSensitivity * _controlSettings.LookSensitivity(InputDeviceKind.Gamepad) * _sensitivityMultiplier;

        /// <summary>
        /// Look 入力を読まずに向きを止めるか。既定は false。デスカムの間のように、すぐ戻す一時停止に使う。
        /// 無効にするのと違い、カーソルのロックとアクションの有効状態は保つので、止めている間にカーソルが出ない。
        /// </summary>
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
        }

        private void HandleSettingsChanged(ControlSettings settings)
        {
            _controlSettings = settings;
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
            if (!PauseMenu.IsAnyOpen && !LoadoutSelectView.IsAnyOpen)
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
            LockCursor(false);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // フォーカスを失うと OS がロックを外すので、戻ったときにかけ直す。
            if (hasFocus && isActiveAndEnabled && !PauseMenu.IsAnyOpen && !LoadoutSelectView.IsAnyOpen)
            {
                LockCursor(true);
            }
        }

        /// <summary>
        /// 有効で初期化済みの視点操作がシーンにあればカーソルをロックして隠し、無ければ外して出す。
        /// <see cref="PauseMenu"/> を閉じたときに呼ぶ (開いている間に視点操作が有効・無効になっていることがあるため、開く前の状態には戻さない)。
        /// </summary>
        public static void RefreshCursorLock()
        {
            if (LoadoutSelectView.IsAnyOpen)
            {
                LockCursor(false);
                return;
            }

            var anyActive = false;
            foreach (var look in FindObjectsByType<FirstPersonLook>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
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
                return;
            }

            var degrees = ReadLookDegrees(Time.deltaTime);
            if (degrees == Vector2.zero)
            {
                return;
            }

            ApplyLookDelta(degrees);
        }

        /// <summary>
        /// このフレームの Look 入力を、今の感度で回転量 (度) にしたもの。x は正で右、y は正で上。
        /// <see cref="InputSuspended"/> でも読めるので、視点を止めている間に別のカメラ (観戦の自由カメラ) を同じ感度で回すのに使う。
        /// 未初期化なら zero。
        /// </summary>
        public Vector2 ReadLookDegrees(float deltaSeconds)
        {
            if (_lookAction == null)
            {
                return Vector2.zero;
            }

            var raw = _lookAction.ReadValue<Vector2>();
            if (raw == Vector2.zero)
            {
                return Vector2.zero;
            }

            // マウスやタッチの delta はフレームあたりの移動量なので deltaTime を掛けない。スティックは速度として扱う。
            var isPointer = _lookAction.activeControl?.device is Pointer;
            return isPointer ? raw * EffectiveMouseSensitivity : raw * (EffectiveStickSensitivity * deltaSeconds);
        }

        // 射撃 (WeaponController.Update) が反動を足した後に向きへ反映する。
        private void LateUpdate()
        {
            ApplyRecoil();
        }

        /// <summary>
        /// <see cref="Recoil"/> の今のずれを pitch の軸に反映する。足しているずれと同じなら何もしない。Play 中は LateUpdate が毎フレーム呼ぶ。
        /// </summary>
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

        /// <summary>
        /// pitch の軸 (<see cref="ViewRig"/> の目か、子階層のカメラ) とプロジェクト全体の Look アクションを探して初期化する。
        /// 見つからなければ理由を Debug.LogError に出して false を返す。
        /// </summary>
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

        /// <summary>参照を直接渡して初期化する。pitchPivot はこの Transform の子孫であること。</summary>
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

        /// <summary>
        /// 回転量 (度) を加える。x は正で右、y は正で上を向く。本体は yaw だけ、カメラは pitch だけ回す。
        /// </summary>
        public void ApplyLookDelta(Vector2 degrees)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("FirstPersonLook is not initialized");
            }

            _angles.Rotate(degrees.x, degrees.y);
            ApplyRotation();
        }

        // 反動は向き (_angles) に混ぜず、軸に書くときだけ足す。混ぜると戻ったときに視点操作の分まで戻ってしまう。
        private void ApplyRotation()
        {
            transform.localRotation = Quaternion.Euler(0f, _angles.Yaw, 0f);
            var pitch = Mathf.Clamp(_angles.Pitch - _recoilPitchUp, -_angles.MaxPitch, _angles.MaxPitch);
            _appliedRecoilPitchUp = _angles.Pitch - pitch;
            _pitchPivot.localRotation = Quaternion.Euler(pitch, _recoilYaw, 0f);
        }

        /// <summary>子孫 (自分自身は除く) から最初の Camera を探す。無ければ null。</summary>
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

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
