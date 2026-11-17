using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class GamepadRumble : MonoBehaviour
    {
        public const string LogTag = RumbleLog.Tag;

        public const string TuningResourcePath = "Tuning/rumble";

        public const string SourceFire = "fire";
        public const string SourceDamage = "damage";
        public const string SourceExplosion = "explosion";

        public const string StopExpired = "expired";
        public const string StopPaused = "paused";
        public const string StopSettingsOff = "settingsOff";
        public const string StopKeyboardMouse = "keyboardMouse";
        public const string StopDisabled = "disabled";

        private readonly RumbleModel _model = new RumbleModel();
        private RumbleSpec _spec;
        private ViewRig _rig;
        private bool _subscribed;
        private Gamepad _pad;
        private RumbleMotors _applied = RumbleMotors.Zero;

        public static RumbleSpec Tuning =>
            KitTuning.Load(c => c.RumbleTuning, TuningResourcePath, RumbleJson.Parse, RumbleSpec.Default);

        // 入っていなければ Tuning を読む。試すときに Resources を通さず値を渡せるよう、上書きできるようにしておく。
        public RumbleSpec Spec
        {
            get => _spec ?? Tuning;
            set => _spec = value;
        }

        public Func<ControlSettings> SettingsSource { get; set; }

        public ViewRig Rig => _rig;

        public InputDeviceKind ActiveDevice { get; internal set; } = InputDeviceKind.KeyboardMouse;

        public RumbleMotors LastOutput => _applied;

        public int ActivePulses => _model.ActiveCount;

        public bool IsLocalView => _rig != null && _rig.enabled && _rig.gameObject.activeInHierarchy && _rig.IsLocalView;

        private ControlSettings Settings
        {
            get
            {
                var source = SettingsSource;
                return (source != null ? source() : null) ?? AppliedControlSettings.Current;
            }
        }

        private void Awake()
        {
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                enabled = false;
                return;
            }

            if (_rig == null)
            {
                var rig = GetComponent<ViewRig>();
                if (rig == null)
                {
                    Debug.LogError($"{LogTag} '{name}' has no {nameof(ViewRig)}; the gamepad does not rumble", this);
                    enabled = false;
                    return;
                }

                Bind(rig);
            }
        }

        private void OnEnable()
        {
            if (_rig != null)
            {
                Subscribe();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            Stop(StopDisabled);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            Stop(StopDisabled);
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime, GameUi.IsPauseOpen);
        }

        public void Bind(ViewRig rig)
        {
            if (rig == null)
            {
                throw new ArgumentNullException(nameof(rig));
            }

            if (rig.gameObject != gameObject)
            {
                throw new ArgumentException($"view rig '{rig.name}' must be on the same GameObject as the gamepad rumble '{name}'", nameof(rig));
            }

            _rig = rig;
        }

        // 時間を進める前に今の強さをモーターに入れるのは、1 フレームより短い振動も 1 フレームは揺らすため。
        public void Tick(float deltaSeconds, bool paused)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            var settings = Settings;
            var refusal = Refusal(settings, paused);
            if (refusal != null)
            {
                Stop(refusal);
                return;
            }

            Output(_model.Sample(settings.RumbleEnabled, settings.RumbleStrength), StopExpired);
            _model.Tick(deltaSeconds);
        }

        public void Stop(string reason)
        {
            _model.Reset();
            Output(RumbleMotors.Zero, reason);
        }

        internal void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            _subscribed = true;
            CombatEvents.ShotFired += HandleShotFired;
            CombatEvents.DamageTaken += HandleDamageTaken;
            CombatEvents.Exploded += HandleExploded;
            InputSystem.onActionChange += HandleActionChange;
        }

        internal void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            _subscribed = false;
            CombatEvents.ShotFired -= HandleShotFired;
            CombatEvents.DamageTaken -= HandleDamageTaken;
            CombatEvents.Exploded -= HandleExploded;
            InputSystem.onActionChange -= HandleActionChange;
        }

        internal void HandleShotFired(ShotFiredInfo shot)
        {
            if (!IsOwnBody(shot.Shooter))
            {
                return;
            }

            AddPulse(SourceFire, Spec.Fire, RumbleLog.Weapon(shot.WeaponId));
        }

        // DamageTaken は撃たれた本人の画面にだけ届き、どの本体宛てかは載らないので、この画面の視点かどうかだけで決める。
        internal void HandleDamageTaken(DamageTakenInfo damage)
        {
            if (damage.Amount <= 0)
            {
                return;
            }

            AddPulse(SourceDamage, Spec.Damage, RumbleLog.Amount(damage.Amount));
        }

        internal void HandleExploded(ExplosionInfo explosion)
        {
            var spec = Spec;
            var distance = Vector3.Distance(ViewOrigin(), explosion.Center);
            if (float.IsNaN(distance) || float.IsInfinity(distance))
            {
                return;
            }

            AddPulse(
                SourceExplosion,
                RumbleModel.ForExplosion(spec.Explosion, distance, spec.ExplosionRadius),
                RumbleLog.Weapon(explosion.WeaponId) + " " + RumbleLog.Distance(distance));
        }

        internal void HandleActionChange(object target, InputActionChange change)
        {
            if (change != InputActionChange.ActionPerformed || !(target is InputAction action))
            {
                return;
            }

            NoteDevice(action.activeControl?.device);
        }

        internal void NoteDevice(InputDevice device)
        {
            ActiveDevice = InputDeviceKinds.Of(device, ActiveDevice);
        }

        // 揺らせない間に受けた振動を溜めると、ゲームパッドに持ち替えた・設定を入れた瞬間にまとめて揺れるので、受けた時点で捨てる。
        private void AddPulse(string source, RumblePulse pulse, string detail)
        {
            if (!IsLocalView || Refusal(Settings, GameUi.IsPauseOpen) != null)
            {
                return;
            }

            if (_model.Add(pulse))
            {
                Debug.Log(RumbleLog.Pulse(source, pulse, detail), this);
            }
        }

        private string Refusal(ControlSettings settings, bool paused)
        {
            if (paused)
            {
                return StopPaused;
            }

            if (!settings.RumbleEnabled || settings.RumbleStrength <= 0f)
            {
                return StopSettingsOff;
            }

            return ActiveDevice != InputDeviceKind.Gamepad ? StopKeyboardMouse : null;
        }

        private void Output(RumbleMotors motors, string stopReason)
        {
            var pad = Gamepad.current;
            if (motors.IsZero)
            {
                if (_applied.IsZero)
                {
                    return;
                }

                SetMotors(_pad, RumbleMotors.Zero);
                _pad = null;
                _applied = RumbleMotors.Zero;
                Debug.Log(RumbleLog.Stopped(stopReason), this);
                return;
            }

            if (pad != _pad && _pad != null)
            {
                SetMotors(_pad, RumbleMotors.Zero);
                Debug.Log(RumbleLog.DeviceChanged(_pad.name, pad != null ? pad.name : null), this);
            }
            else if (pad == _pad && motors.Equals(_applied))
            {
                return;
            }

            SetMotors(pad, motors);
            _pad = pad;
            _applied = motors;
        }

        private static void SetMotors(Gamepad pad, RumbleMotors motors)
        {
            if (pad == null || !pad.added)
            {
                return;
            }

            pad.SetMotorSpeeds(motors.Low, motors.High);
        }

        // オフラインの WeaponController は撃った者を照準のルートで渡すので、本体と同じか、本体を含む側か、本体の下のどれでも自分とみなす。
        private bool IsOwnBody(GameObject other)
        {
            if (other == null)
            {
                return false;
            }

            var otherTransform = other.transform;
            return otherTransform == transform || transform.IsChildOf(otherTransform) || otherTransform.IsChildOf(transform);
        }

        private Vector3 ViewOrigin()
        {
            if (_rig != null && _rig.Camera != null)
            {
                return _rig.Camera.transform.position;
            }

            return transform.position;
        }
    }
}
