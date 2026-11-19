using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    // 座席の入力 (VehicleSeatInput) にスロットルの欄が無いので、スロットルのアクションはこの部品が自分で持って読む。
    [DisallowMultipleComponent]
    public sealed class AircraftStickControlScheme : MonoBehaviour, IVehicleControlScheme, IVehicleControlSchemeReset
    {
        public const string LogTag = "[AircraftStickControlScheme]";

        [SerializeField, Tooltip("操縦桿の値を読む機体のアセット (Stick の欄)。空なら既定値")]
        private AircraftDefinition _definition;

        [SerializeField, Tooltip("スロットルの開き具合 (0〜1) を読むアクション。空ならブーストを押している間だけ開く")]
        private InputActionReference _throttleAction;

        private AircraftStickSpec _configuredSpec;
        private AircraftStickSpec _definitionSpec;
        private float _throttle;

        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう。
        [NonSerialized]
        private InputAction _enabledAction;

        public AircraftDefinition Definition
        {
            get => _definition;
            set
            {
                _definition = value;
                _configuredSpec = null;
                _definitionSpec = null;
            }
        }

        public AircraftStickSpec Spec => _configuredSpec ?? (_definitionSpec ??= BuildSpec());

        public InputActionReference ThrottleAction
        {
            get => _throttleAction;
            set
            {
                ReleaseAction();
                _throttleAction = value;
                if (isActiveAndEnabled)
                {
                    AcquireAction();
                }
            }
        }

        public float Throttle => _throttle;

        public void Configure(AircraftStickSpec spec)
        {
            _configuredSpec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public void ResetState()
        {
            _throttle = 0f;
        }

        public VehicleMotorInput ToMotorInput(VehicleSeat seat, VehicleSeatInput input, float deltaSeconds)
        {
            var spec = Spec;
            _throttle = AircraftStick.StepThrottle(spec, _throttle, ThrottleTarget(input), Mathf.Max(0f, deltaSeconds));
            return AircraftStick.Compute(spec, input.Move.y, input.Move.x, input.Vertical, _throttle);
        }

        private void OnEnable()
        {
            AcquireAction();
        }

        private void OnDisable()
        {
            ReleaseAction();
        }

        private void OnValidate()
        {
            _definitionSpec = null;
        }

        private float ThrottleTarget(VehicleSeatInput input)
        {
            var action = ReadableAction();
            if (action == null)
            {
                return input.Boost ? 1f : 0f;
            }

            var value = action.ReadValue<float>();
            return float.IsNaN(value) ? 0f : Mathf.Clamp01(value);
        }

        private InputAction ReadableAction()
        {
            var action = _throttleAction != null ? _throttleAction.action : null;
            return action != null && IsFloatAction(action) ? action : null;
        }

        // 2 軸などの値を ReadValue<float> で読むと毎フレーム例外になるので、1 軸とボタンのアクションだけを読む。
        private static bool IsFloatAction(InputAction action)
        {
            var controlType = action.expectedControlType;
            // Control Type が Any（空）のアクションは Vector2 などが割り当たりうるので、ボタン型のときだけ通す。
            return (string.IsNullOrEmpty(controlType) && action.type == InputActionType.Button)
                || controlType == "Axis"
                || controlType == "Button"
                || controlType == "Analog";
        }

        private void AcquireAction()
        {
            if (_throttleAction == null)
            {
                return;
            }

            var action = _throttleAction.action;
            if (action == null)
            {
                Debug.LogError(AircraftStickLog.MissingThrottleAction(_throttleAction.name), this);
                return;
            }

            if (!IsFloatAction(action))
            {
                Debug.LogError(AircraftStickLog.ThrottleActionNotOneAxis(action.name, action.expectedControlType), this);
                return;
            }

            // 割り当てたアクションは他の部品と共有かもしれないので、自分が有効にしたときだけ後で無効に戻す。
            if (!action.enabled)
            {
                action.Enable();
                _enabledAction = action;
            }
        }

        private void ReleaseAction()
        {
            if (_enabledAction != null)
            {
                _enabledAction.Disable();
                _enabledAction = null;
            }
        }

        private AircraftStickSpec BuildSpec()
        {
            return _definition != null ? _definition.ToStickSpec() : AircraftStickSpec.Default;
        }
    }
}
