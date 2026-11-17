using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using NVector3 = System.Numerics.Vector3;

namespace Shooter.Runtime
{
    // 計算は AimAssistSolver に任せ、ここではシーンから照準・候補・覗き込みの秒数を集めて結果を FirstPersonLook に渡すだけにする。
    [DisallowMultipleComponent]
    public sealed class AimAssist : MonoBehaviour
    {
        public const string TuningResourcePath = "Tuning/aim-assist";

        private readonly List<NVector3> _candidates = new List<NVector3>();
        private AimAssistSpec _spec;
        private InputDeviceKind _device = InputDeviceKind.KeyboardMouse;
        private bool _wasAiming;
        private float _aimingSeconds;

        public static AimAssistSpec Tuning =>
            KitTuning.Load(c => c.AimAssistTuning, TuningResourcePath, AimAssistJson.Parse, AimAssistSpec.Default);

        // 入っていなければ Tuning を読む。試すときに Resources を通さず値を渡せるよう、上書きできるようにしておく。
        public AimAssistSpec Spec
        {
            get => _spec ?? Tuning;
            set => _spec = value;
        }

        public FirstPersonLook Look { get; set; }

        public Func<bool> AimingSource { get; set; }

        // ViewRig が初期化される前 (Awake の前) は照準が決まらないので null を返せるようにする。
        public Func<Ray?> AimSource { get; set; }

        public IAimAssistTargets Targets { get; set; }

        public AimAssistResult LastResult { get; private set; } = AimAssistResult.None;

        public float AimingSeconds => _aimingSeconds;

        public static AimAssist Attach(GameObject player, FirstPersonLook look, AimDownSights ads, WeaponController weapon)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            if (look == null)
            {
                throw new ArgumentNullException(nameof(look));
            }

            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            var rig = player.GetComponent<ViewRig>();
            var assist = player.GetComponent<AimAssist>();
            if (assist == null)
            {
                assist = player.AddComponent<AimAssist>();
            }

            assist.Look = look;
            assist.AimingSource = () => ads != null && ads.IsAiming;
            assist.AimSource = () =>
            {
                if (rig == null || !rig.IsInitialized)
                {
                    return null;
                }

                // 狙う点の距離で目からの向きが変わるので、射撃 (WeaponController.ResolveShot) と同じく武器の射程で探す。
                var range = weapon.HitscanRange;
                rig.ResolveAim(range > 0f && !float.IsInfinity(range) ? range : ViewRig.DefaultAimDistance, out var origin, out var direction);
                return new Ray(origin, direction);
            };
            assist.Targets = new HostileAimTargets(player.transform, weapon.HitMask);
            return assist;
        }

        private void Update()
        {
            _device = InputDeviceKinds.Of(Look != null ? Look.LookAction : null, _device);
            Tick(Time.deltaTime, _device);
        }

        private void OnDisable()
        {
            _wasAiming = false;
            _aimingSeconds = 0f;
            Release();
        }

        public void Tick(float deltaSeconds, InputDeviceKind device)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            // 機器を持ち替えても覗き込みを始めた時から数え直さないよう、効かない間も秒数は進める。
            var aiming = AimingSource != null && AimingSource();
            _aimingSeconds = aiming && _wasAiming ? _aimingSeconds + deltaSeconds : 0f;
            _wasAiming = aiming;

            if (Look == null || device != InputDeviceKind.Gamepad || !Look.ControlSettings.AimAssist || Targets == null || AimSource == null)
            {
                Release();
                return;
            }

            var aim = AimSource();
            if (!aim.HasValue)
            {
                Release();
                return;
            }

            var spec = Spec;
            var origin = aim.Value.origin.ToNumerics();
            _candidates.Clear();
            Targets.Collect(origin, spec.MaxDistance, _candidates);
            var result = AimAssistSolver.Solve(spec, deltaSeconds, aiming, _aimingSeconds, origin, aim.Value.direction.ToNumerics(), _candidates);
            _candidates.Clear();

            LastResult = result;
            Look.AssistLookScale = result.LookScale;
            Look.AddAssistDegrees(new Vector2(result.YawDelta, result.PitchUpDelta));
        }

        private void Release()
        {
            LastResult = AimAssistResult.None;
            if (Look != null)
            {
                Look.ClearAssist();
            }
        }
    }
}
