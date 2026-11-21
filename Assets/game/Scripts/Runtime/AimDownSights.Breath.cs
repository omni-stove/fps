using System;
using System.Globalization;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    public sealed partial class AimDownSights
    {
        public const string HoldBreathActionPath = InputActionPaths.HoldBreath;

        [SerializeField, Min(1f), Tooltip("息を使い切った直後の揺れの倍率")]
        private float _exhaustedSwayMultiplier = BreathHold.DefaultExhaustedSwayMultiplier;

        [SerializeField, Min(0f), Tooltip("揺れの倍率が息止めの状態に合わせて変わるおおよその秒数")]
        private float _swayBlendSeconds = BreathHold.DefaultSwayBlendSeconds;

        [SerializeField, Min(0.01f), Tooltip("スコープの揺れが 1 周する秒数")]
        private float _swayCycleSeconds = BreathHold.DefaultSwayCycleSeconds;

        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _holdBreathAction;
        private bool _enabledHoldBreathAction;
        private BreathHold _breath;
        private string _breathWeaponId;
        private float _swaySeconds;
        private Quaternion _cameraRestRotation = Quaternion.identity;
        private bool _warnedSwayOnPitchPivot;

        public InputAction HoldBreathAction => _holdBreathAction;

        public bool HoldBreathInput { get; set; }

        public BreathHold Breath => _breath;

        public bool IsHoldingBreath => _breath != null && _breath.IsHolding;

        public float SwayPitchUpDegrees { get; private set; }

        public float SwayYawDegrees { get; private set; }

        public float ExhaustedSwayMultiplier
        {
            get => _exhaustedSwayMultiplier;
            set
            {
                if (!(value >= 1f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "must be at least 1 and finite");
                }

                _exhaustedSwayMultiplier = value;
                _breath = null;
            }
        }

        public float SwayBlendSeconds
        {
            get => _swayBlendSeconds;
            set
            {
                if (!(value >= 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "must be non-negative and finite");
                }

                _swayBlendSeconds = value;
                _breath = null;
            }
        }

        public float SwayCycleSeconds
        {
            get => _swayCycleSeconds;
            set
            {
                if (!(value > 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "must be positive and finite");
                }

                _swayCycleSeconds = value;
            }
        }

        public static string FormatBreathLog(bool holding, bool exhausted, float remainingSeconds)
        {
            var state = holding ? "hold" : exhausted ? "exhausted" : "ready";
            return $"{LogTag} breath={state} remaining={remainingSeconds.ToString("0.##", CultureInfo.InvariantCulture)}";
        }

        private void StepBreath(float deltaSeconds)
        {
            var spec = WeaponSource?.Invoke();
            FollowBreathWeapon(spec);

            var sway = spec != null ? spec.Aim.SwayDegrees : 0f;
            var wasHolding = _breath.IsHolding;
            var wasExhausted = _breath.IsExhausted;
            _breath.Tick(HoldBreathInput && IsAiming && sway > 0f, deltaSeconds);
            if (_breath.IsHolding != wasHolding || _breath.IsExhausted != wasExhausted)
            {
                Debug.Log(FormatBreathLog(_breath.IsHolding, _breath.IsExhausted, _breath.RemainingSeconds), this);
            }

            // 長く覗き続けても sin の引数の精度が落ちないよう、1 周ごとに巻き戻す。
            _swaySeconds = (_swaySeconds + deltaSeconds) % _swayCycleSeconds;
            BreathHold.SwayOffset(_swaySeconds, sway * _breath.SwayMultiplier * Progress, _swayCycleSeconds, out var pitchUp, out var yaw);
            ApplySway(pitchUp, yaw);
        }

        // WeaponSource は呼ぶたびに Spec を作り直してよいので、同じ武器かは参照でなく Id で見る。
        private void FollowBreathWeapon(WeaponSpec spec)
        {
            var id = spec?.Id;
            if (_breath != null && id == _breathWeaponId)
            {
                return;
            }

            _breathWeaponId = id;
            _breath = new BreathHold(spec?.Aim.BreathHold, _exhaustedSwayMultiplier, _swayBlendSeconds);
        }

        // 揺れは視点の向き (FirstPersonLook) に混ぜずカメラだけを回す。混ぜると揺れの分まで視点操作として残り、
        // カメラの向きから撃つ (WeaponController.ResolveShot) ので撃つ向きも揺れに従う。
        private void ApplySway(float pitchUpDegrees, float yawDegrees)
        {
            SwayPitchUpDegrees = pitchUpDegrees;
            SwayYawDegrees = yawDegrees;
            var still = pitchUpDegrees == 0f && yawDegrees == 0f;
            // 揺れの乗った向きを別の部品が保存して書き戻すことがあるので、自分が掛けたかの記録ではなく実際の向きで判断する。
            // Quaternion の == は 0.1 度ほどの差を同じと見なし、止めたときに小さな揺れを残すので、Equals で成分ごとに比べる。
            if (still && _camera.transform.localRotation.Equals(_cameraRestRotation))
            {
                return;
            }

            // pitch の軸がカメラ自身だと FirstPersonLook が毎回向きを書き直し、揺れと取り合いになる。
            if (_look.PitchPivot == _camera.transform)
            {
                if (!still && !_warnedSwayOnPitchPivot)
                {
                    _warnedSwayOnPitchPivot = true;
                    Debug.LogWarning($"{LogTag} '{name}' のカメラが pitch の軸なので、スコープの揺れを掛けられません。本体 > 目 > カメラ の順に置いてください。", this);
                }

                return;
            }

            _camera.transform.localRotation = _cameraRestRotation * Quaternion.Euler(-pitchUpDegrees, yawDegrees, 0f);
        }

        private void ResetBreath()
        {
            _breath?.Reset();
            _swaySeconds = 0f;
            ApplySway(0f, 0f);
        }

        private void BindBreath(Camera camera, InputAction holdBreathAction)
        {
            _cameraRestRotation = camera.transform.localRotation;
            _breath = null;
            _swaySeconds = 0f;
            SwayPitchUpDegrees = 0f;
            SwayYawDegrees = 0f;

            if (_isLive)
            {
                ReleaseHoldBreathAction();
            }

            _holdBreathAction = holdBreathAction;
            if (_isLive)
            {
                AcquireHoldBreathAction();
            }
        }

        private void ReadHoldBreath()
        {
            HoldBreathInput = _holdBreathAction != null && _holdBreathAction.IsPressed();
        }

        private InputAction FindHoldBreathAction(InputActionAsset actions)
        {
            var action = actions.FindAction(HoldBreathActionPath);
            if (action == null)
            {
                Debug.LogWarning($"{LogTag} Input Actions '{actions.name}' に '{HoldBreathActionPath}' が無いので、入力で息止めできません。", this);
            }

            return action;
        }

        private void AcquireHoldBreathAction()
        {
            // Aim と同じく共有のアクションなので、自分が有効にしたときだけ後で無効に戻す。
            if (_holdBreathAction != null && !_holdBreathAction.enabled)
            {
                _holdBreathAction.Enable();
                _enabledHoldBreathAction = true;
            }
        }

        private void ReleaseHoldBreathAction()
        {
            if (_enabledHoldBreathAction && _holdBreathAction != null)
            {
                _holdBreathAction.Disable();
            }

            _enabledHoldBreathAction = false;
        }
    }
}
