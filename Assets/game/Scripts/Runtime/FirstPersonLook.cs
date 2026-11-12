using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// 一人称の視点操作。Look 入力で本体 (この GameObject) を yaw、子のカメラを pitch (±85°) で回す。
    /// 有効な間はカーソルをロックする。参照はコードで解決するので Inspector の配線は要らない:
    /// カメラは子階層の <see cref="Camera"/>、入力はプロジェクト全体のアクション
    /// (<see cref="InputSystem.actions"/>) の <see cref="LookActionPath"/>。
    /// 解決できなければエラーを出して自分を無効にする。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FirstPersonLook : MonoBehaviour
    {
        /// <summary>Assets/InputSystem_Actions.inputactions の Look アクション。</summary>
        public const string LookActionPath = "Player/Look";

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

        /// <summary>pitch を回す Transform (カメラ)。未初期化なら null。</summary>
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

        private void Awake()
        {
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

            SyncAnglesFromTransforms();

            // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。
            if (!_lookAction.enabled)
            {
                _lookAction.Enable();
                _enabledLookAction = true;
            }

            LockCursor(true);
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
            if (hasFocus && isActiveAndEnabled)
            {
                LockCursor(true);
            }
        }

        private void Update()
        {
            var raw = _lookAction.ReadValue<Vector2>();
            if (raw == Vector2.zero)
            {
                return;
            }

            // マウスやタッチの delta はフレームあたりの移動量なので deltaTime を掛けない。スティックは速度として扱う。
            var isPointer = _lookAction.activeControl?.device is Pointer;
            var degrees = isPointer ? raw * _mouseSensitivity : raw * (_stickSensitivity * Time.deltaTime);
            ApplyLookDelta(degrees);
        }

        /// <summary>
        /// 子階層のカメラとプロジェクト全体の Look アクションを探して初期化する。
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

            Initialize(camera.transform, look);
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
            transform.localRotation = Quaternion.Euler(0f, _angles.Yaw, 0f);
            _pitchPivot.localRotation = Quaternion.Euler(_angles.Pitch, 0f, 0f);
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

            _angles.Set(transform.localEulerAngles.y, _pitchPivot.localEulerAngles.x);
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
