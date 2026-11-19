using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    // Edit Mode やテストでは Awake が走らないので、参照は Awake で探すだけでなく Bind でも渡せるようにする。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerVehicleRider : MonoBehaviour, IVehicleRider, IMovementOverride, IHandedOffTrigger
    {
        public const string LogTag = VehicleLog.Tag;

        // 座席に座っている間は、スライディングやはしごなどより優先する。
        public const int MovementOverridePriority = 1000;

        public const string InteractActionPath = InputActionPaths.Interact;
        public const string NextSeatActionPath = InputActionPaths.Next;
        public const string PreviousSeatActionPath = InputActionPaths.Previous;
        public const string FireActionPath = InputActionPaths.Attack;
        public const string AimActionPath = InputActionPaths.Aim;
        public const string ReloadActionPath = InputActionPaths.Reload;

        private readonly RiderVisibility _hiddenBody = new RiderVisibility();
        private readonly RiderVisibility _hiddenViewmodel = new RiderVisibility();
        private readonly List<Behaviour> _suspendWhileRiding = new List<Behaviour>();
        private readonly List<Behaviour> _suspended = new List<Behaviour>();
        private readonly ActionSlot _interact = new ActionSlot();
        private readonly ActionSlot _next = new ActionSlot();
        private readonly ActionSlot _previous = new ActionSlot();
        private readonly ActionSlot _fire = new ActionSlot();
        private readonly ActionSlot _aim = new ActionSlot();
        private readonly ActionSlot _reload = new ActionSlot();

        private PlayerController _mover;
        private WeaponController _weapon;
        private ViewRig _rig;
        private FirstPersonLook _look;
        private Damageable _damageable;
        private VehicleCamera _camera;
        private VehicleSeat _seat;
        private float _standingHeight;
        private float _lastSeatYaw;
        private bool _snapYaw;
        private bool _isLive;
        private bool _claimedWeaponHandoff;
        private OwnWeaponSeatModifier _ownWeapon;
        private VehicleSeatFiring _firing;
        private Weapon _seatWeapon;

        public event Action<Weapon> SeatWeaponChanged;

        public PlayerController Mover => _mover;

        public WeaponController Weapon => _weapon;

        public bool IsBound => _mover != null;

        public Vehicle Vehicle => _seat != null ? _seat.Vehicle : null;

        public bool IsRiding => _seat != null;

        public VehicleSeatFiring Firing => _seat != null ? _firing : VehicleSeatFiring.None;

        public Weapon SeatWeapon => _seat != null ? _seatWeapon : null;

        public OwnWeaponSeatModifier OwnWeaponModifier => _ownWeapon;

        public IVehicleSeatInputFilter SeatInputFilter { get; set; }

        public VehicleCameraView CameraView => _camera != null ? _camera.View : VehicleCameraView.Chase;

        public IReadOnlyList<Behaviour> SuspendedComponents => _suspended;

        public string RiderName => name;

        public Transform RiderRoot => transform;

        public float FeetBelowRoot
        {
            get
            {
                var controller = _mover != null ? _mover.Controller : null;
                if (controller == null)
                {
                    return 0f;
                }

                return ((controller.height * 0.5f) - controller.center.y) * Mathf.Abs(transform.lossyScale.y);
            }
        }

        public float ClearanceRadius
        {
            get
            {
                var controller = _mover != null ? _mover.Controller : null;
                if (controller == null)
                {
                    return 0.5f;
                }

                var scale = transform.lossyScale;
                return controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            }
        }

        // 座っている間はしゃがみの高さに縮んでいるので、降りて立ち上がれる高さで調べる。
        public float ClearanceHeight => _standingHeight;

        public bool CanBoard =>
            enabled
            && gameObject.activeInHierarchy
            && _mover != null
            && _mover.IsInitialized
            && !_mover.IsControlHandedOff
            && (!_mover.IsMovementOverridden || ReferenceEquals(_mover.ActiveMovementOverride, this))
            && (_damageable == null || (!_damageable.IsDead && !_damageable.IsDowned));

        public VehicleSeat Seat => _seat;

        int IMovementOverride.Priority => MovementOverridePriority;

        bool IMovementOverride.IsActive => _seat != null;

        PlayerActionConditions IMovementOverride.Conditions => PlayerActionConditions.InVehicle;

        private void Awake()
        {
            if (!IsBound && !TryBindFromHierarchy())
            {
                enabled = false;
                return;
            }

            TryResolveActions();
        }

        private void OnEnable()
        {
            _isLive = true;
            _interact.Acquire();
            _next.Acquire();
            _previous.Acquire();
            if (_interact.Action != null)
            {
                _interact.Action.performed += OnInteractPerformed;
            }
        }

        private void OnDisable()
        {
            _isLive = false;
            if (_interact.Action != null)
            {
                _interact.Action.performed -= OnInteractPerformed;
            }

            var vehicle = Vehicle;
            if (vehicle != null)
            {
                vehicle.ForceExit(this, VehicleLeaveReason.Forced);
            }

            _interact.Release();
            _next.Release();
            _previous.Release();
        }

        private void OnDestroy()
        {
            if (_damageable != null)
            {
                _damageable.Died -= OnDied;
            }

            if (_mover != null)
            {
                _mover.UnregisterMovementOverride(this);
            }
        }

        private void Update()
        {
            if (!ReferenceEquals(_seat, null) && _seat == null)
            {
                // 座席ごと消えて降ろしてもらえなかったときは、その場で降りたことにする。
                ((IVehicleRider)this).OnLeft(
                    null,
                    VehicleLeaveReason.RiderRemoved,
                    transform.position - (Vector3.up * FeetBelowRoot),
                    transform.rotation);
                return;
            }

            if (_seat == null)
            {
                return;
            }

            if (_next.WasPressedThisFrame())
            {
                SwitchSeat(1);
            }
            else if (_previous.WasPressedThisFrame())
            {
                SwitchSeat(-1);
            }

            SubmitSeatInput(Time.deltaTime);
        }

        private void LateUpdate()
        {
            // フォトモードなど DeathCamView の借り手がカメラを動かしている間は、置き直して取り返さない。
            if (_seat != null && !DeathCamView.IsBorrowed(_camera.Camera))
            {
                _camera.Place(_seat);
                _camera.StepFieldOfView(_seat.Vehicle, Time.deltaTime);
            }
        }

        public bool TryBindFromHierarchy()
        {
            var mover = GetComponent<PlayerController>();
            if (mover == null)
            {
                Debug.LogError($"{LogTag} '{name}' に PlayerController がありません。プレイヤーの本体に付けてください。", this);
                return false;
            }

            Bind(mover, GetComponentInChildren<WeaponController>(true));
            return true;
        }

        public void Bind(PlayerController mover, WeaponController weapon)
        {
            if (mover == null)
            {
                throw new ArgumentNullException(nameof(mover));
            }

            if (mover.gameObject != gameObject)
            {
                throw new ArgumentException($"mover must be on '{name}'", nameof(mover));
            }

            if (weapon != null && !weapon.transform.IsChildOf(transform))
            {
                throw new ArgumentException($"weapon must be on '{name}' or its children", nameof(weapon));
            }

            if (_seat != null)
            {
                throw new InvalidOperationException($"cannot rebind '{name}' while riding");
            }

            if (_mover != null)
            {
                _mover.UnregisterMovementOverride(this);
            }

            if (_damageable != null)
            {
                _damageable.Died -= OnDied;
            }

            _mover = mover;
            _weapon = weapon;
            _rig = GetComponent<ViewRig>();
            _look = GetComponent<FirstPersonLook>();
            _damageable = GetComponent<Damageable>();
            _camera = new VehicleCamera(_rig);
            if (_damageable != null)
            {
                _damageable.Died += OnDied;
            }

            var controller = mover.Controller != null ? mover.Controller : GetComponent<CharacterController>();
            _standingHeight = controller != null ? controller.height * Mathf.Abs(transform.lossyScale.y) : 2f;

            _suspendWhileRiding.Clear();
            SuspendWhileRiding(GetComponent<AimDownSights>());
            SuspendWhileRiding(GetComponent<CoverController>());
            SuspendWhileRiding(GetComponent<GrenadeThrower>());
        }

        public bool SuspendWhileRiding(Behaviour component)
        {
            if (component == null || _suspendWhileRiding.Contains(component))
            {
                return false;
            }

            _suspendWhileRiding.Add(component);
            return true;
        }

        public bool TryResolveActions()
        {
            AppliedInputBindings.EnsureLoaded();
            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogWarning($"{LogTag} プロジェクト全体の Input Actions が設定されていないため、入力では乗り降りできません。", this);
                return false;
            }

            SetActions(
                Find(actions, InteractActionPath),
                Find(actions, NextSeatActionPath),
                Find(actions, PreviousSeatActionPath),
                Find(actions, FireActionPath),
                Find(actions, AimActionPath),
                Find(actions, ReloadActionPath));
            return true;
        }

        public void SetActions(
            InputAction interact, InputAction nextSeat, InputAction previousSeat, InputAction fire, InputAction aim, InputAction reload)
        {
            if (_isLive && _interact.Action != null)
            {
                _interact.Action.performed -= OnInteractPerformed;
            }

            _interact.Set(interact, _isLive);
            _next.Set(nextSeat, _isLive);
            _previous.Set(previousSeat, _isLive);
            _fire.Set(fire, _seat != null);
            _aim.Set(aim, _seat != null);
            _reload.Set(reload, _seat != null);

            if (_isLive && _interact.Action != null)
            {
                _interact.Action.performed += OnInteractPerformed;
            }
        }

        // Interact は拾う・押すなどと共用なので、近くに乗り込める乗り物が無くてもログは出さない。
        public bool Interact()
        {
            if (_seat != null)
            {
                return Exit();
            }

            var vehicle = FindNearestVehicle(Vehicle.Active);
            return vehicle != null && vehicle.TryEnter(this);
        }

        public Vehicle FindNearestVehicle(IEnumerable<Vehicle> candidates)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            Vehicle best = null;
            var bestDistance = float.PositiveInfinity;
            foreach (var candidate in candidates)
            {
                if (candidate == null || !candidate.IsLive || candidate.Seats.Count == 0)
                {
                    continue;
                }

                if (candidate.FindBoardableSeat(transform.position, out var distance) >= 0 && distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return best;
        }

        public bool Exit()
        {
            var vehicle = Vehicle;
            return vehicle != null && vehicle.TryExit(this);
        }

        public bool SwitchSeat(int direction)
        {
            var vehicle = Vehicle;
            return vehicle != null && vehicle.TrySwitchSeat(this, direction);
        }

        public void SetCameraView(VehicleCameraView view)
        {
            if (!Enum.IsDefined(typeof(VehicleCameraView), view))
            {
                throw new ArgumentOutOfRangeException(nameof(view), view, "view is not a defined VehicleCameraView");
            }

            if (_seat == null)
            {
                return;
            }

            _camera.SetView(view);
            _camera.Place(_seat);
        }

        public VehicleSeatInput ReadSeatInput()
        {
            var move = _mover != null && _mover.MoveAction != null && _mover.MoveAction.enabled
                ? _mover.MoveAction.ReadValue<Vector2>()
                : Vector2.zero;
            move = new Vector2(Mathf.Clamp(move.x, -1f, 1f), Mathf.Clamp(move.y, -1f, 1f));

            var vertical = 0f;
            if (_mover != null && HeldInput.IsHeld(_mover.JumpAction))
            {
                vertical += 1f;
            }

            if (_mover != null && HeldInput.IsHeld(_mover.CrouchAction))
            {
                vertical -= 1f;
            }

            var view = _camera != null && _camera.Camera != null
                ? _camera.Camera.transform
                : (_rig != null && _rig.Eye != null ? _rig.Eye : transform);
            return new VehicleSeatInput(
                move,
                vertical,
                _mover != null && HeldInput.IsHeld(_mover.SprintAction),
                HeldInput.IsHeld(_fire.Action),
                HeldInput.IsHeld(_aim.Action),
                _reload.WasPressedThisFrame(),
                view.position,
                view.forward);
        }

        // Update から呼ぶ。Update の走らない Edit Mode のテストからも、差し込み (SeatInputFilter) を通した入力を流せるよう public にする。
        public bool SubmitSeatInput(float deltaSeconds)
        {
            var seat = _seat;
            if (seat == null)
            {
                return false;
            }

            var input = ReadSeatInput();
            SeatInputFilter?.Filter(seat, ref input);
            return seat.SubmitInput(this, input, deltaSeconds);
        }

        void IVehicleRider.OnBoarded(VehicleSeat seat)
        {
            if (seat == null)
            {
                throw new ArgumentNullException(nameof(seat));
            }

            _seat = seat;
            _snapYaw = true;

            // 撃つ・持ち替える操作は WeaponController が同じ口を見ていれば一緒に止まる。別の口なら両方に宣言する。
            _mover.Handoff.Claim(this);
            _claimedWeaponHandoff = _weapon != null && _weapon.Handoff != null && !ReferenceEquals(_weapon.Handoff, _mover.Handoff)
                && _weapon.Handoff.Claim(this);

            // 止める部品は共有のアクションを無効に戻すことがあるので、止めてから自分の読むアクションを有効にする。
            SuspendComponents();
            _fire.Acquire();
            _aim.Acquire();
            _reload.Acquire();

            ApplyPose(seat);
            ApplyFiring(seat);
            _mover.RegisterMovementOverride(this);
            PlaceBody(seat.SitPosition, seat.SitYawDegrees);
            _camera.Begin(seat.CameraView);
            _camera.Place(seat);
        }

        void IVehicleRider.OnSeatChanged(VehicleSeat from, VehicleSeat to)
        {
            if (to == null)
            {
                throw new ArgumentNullException(nameof(to));
            }

            _seat = to;
            _snapYaw = true;
            ApplyPose(to);
            ApplyFiring(to);
            PlaceBody(to.SitPosition, to.SitYawDegrees);
            _camera.Begin(to.CameraView);
            _camera.Place(to);
        }

        void IVehicleRider.OnLeft(VehicleSeat seat, VehicleLeaveReason reason, Vector3 feetPosition, Quaternion rotation)
        {
            _seat = null;
            ClearFiring();
            if (_mover == null)
            {
                return;
            }

            _mover.UnregisterMovementOverride(this);
            var knockback = GetComponent<KnockbackMovement>();
            if (knockback != null)
            {
                knockback.ClearFlight();
            }

            _camera.Restore();
            _hiddenBody.Restore();
            _hiddenViewmodel.Restore();

            _fire.Release();
            _aim.Release();
            _reload.Release();
            ResumeComponents();

            _mover.Handoff.Release(this);
            if (_claimedWeaponHandoff && _weapon != null && _weapon.Handoff != null)
            {
                _weapon.Handoff.Release(this);
            }

            _claimedWeaponHandoff = false;
            PlaceBody(feetPosition, rotation.eulerAngles.y);
            if (_mover.Motor != null)
            {
                _mover.Motor.ResetVelocity();
            }
        }

        void IMovementOverride.Begin(PlayerController mover)
        {
        }

        MovementOverrideStep IMovementOverride.Step(PlayerController mover, MovementOverrideInput input, float deltaSeconds)
        {
            var seat = _seat;
            if (seat == null)
            {
                return MovementOverrideStep.Move(Vector3.zero, Vector3.zero);
            }

            var seatYaw = seat.SitYawDegrees;

            // 乗り物が曲がった分だけ体も回し、視点を動かさなければ乗り物の正面を向いたままにする。
            var yaw = _snapYaw ? seatYaw : CurrentYaw() + Mathf.DeltaAngle(_lastSeatYaw, seatYaw);
            _snapYaw = false;
            _lastSeatYaw = seatYaw;

            // PlayerController の置き直しは Collider を切って戻すので無視設定が消える。自分で置いて掛け直す。
            SetBodyPosition(RootPositionFor(seat.SitPosition));
            seat.Vehicle.ReapplyIgnoredCollisions(this);
            return new MovementOverrideStep(
                Vector3.zero,
                Vector3.zero,
                null,
                yaw,
                seat.Spec.CrouchesRider);
        }

        void IMovementOverride.End(PlayerController mover)
        {
        }

        bool IHandedOffTrigger.TryReadTrigger(out bool isPressed, out bool wasPressedThisFrame, out bool reloadPressed)
        {
            isPressed = false;
            wasPressedThisFrame = false;
            reloadPressed = false;
            if (Firing != VehicleSeatFiring.OwnWeapon)
            {
                return false;
            }

            isPressed = HeldInput.IsHeld(_fire.Action);
            wasPressedThisFrame = _fire.WasPressedThisFrame();
            reloadPressed = _reload.WasPressedThisFrame();
            return true;
        }

        private void ApplyFiring(VehicleSeat seat)
        {
            ClearFiring();
            _firing = seat.Vehicle != null ? seat.Vehicle.FiringOf(seat) : VehicleSeatFiring.None;
            if (_firing == VehicleSeatFiring.OwnWeapon && _weapon != null)
            {
                _ownWeapon = new OwnWeaponSeatModifier(seat, _weapon.transform);
                _weapon.AddModifier(_ownWeapon);
                _weapon.HandedOffTrigger = this;
            }
            else if (_rig != null && _rig.ViewmodelRoot != null)
            {
                _hiddenViewmodel.Hide(_rig.ViewmodelRoot);
            }

            if (_firing == VehicleSeatFiring.VehicleWeapon)
            {
                var mounts = seat.Vehicle.WeaponMountsOf(seat.Index);
                _seatWeapon = mounts.Count > 0 ? mounts[0].Weapon : null;
            }

            Debug.Log(VehicleLog.Firing(seat.Vehicle != null ? seat.Vehicle.name : "-", RiderName, seat.Index, _firing), this);
            if (_seatWeapon != null)
            {
                SeatWeaponChanged?.Invoke(_seatWeapon);
            }
        }

        private void ClearFiring()
        {
            var hadSeatWeapon = _seatWeapon != null;
            if (_ownWeapon != null && _weapon != null)
            {
                _weapon.RemoveModifier(_ownWeapon);
                if (ReferenceEquals(_weapon.HandedOffTrigger, this))
                {
                    _weapon.HandedOffTrigger = null;
                }
            }

            _ownWeapon = null;
            _seatWeapon = null;
            _firing = VehicleSeatFiring.None;
            _hiddenViewmodel.Restore();
            if (hadSeatWeapon)
            {
                SeatWeaponChanged?.Invoke(null);
            }
        }

        private void ApplyPose(VehicleSeat seat)
        {
            _hiddenBody.Restore();
            if (seat.Pose == VehicleSeatPose.Hidden)
            {
                _hiddenBody.Hide(transform);
            }
        }

        // CharacterController は有効な間 transform の位置を自分の位置で上書きするので、止めてから置く。
        private void PlaceBody(Vector3 feetPosition, float yawDegrees)
        {
            var controller = _mover.Controller;
            var wasEnabled = controller != null && controller.enabled;
            if (wasEnabled)
            {
                controller.enabled = false;
            }

            transform.position = RootPositionFor(feetPosition);
            if (_look != null && _look.IsInitialized)
            {
                _look.ApplyLookDelta(new Vector2(Mathf.DeltaAngle(_look.Yaw, yawDegrees), 0f));
            }
            else
            {
                transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            }

            if (wasEnabled)
            {
                controller.enabled = true;
            }

            if (_seat != null)
            {
                _seat.Vehicle.ReapplyIgnoredCollisions(this);
            }

            _lastSeatYaw = yawDegrees;
        }

        private void SetBodyPosition(Vector3 rootPosition)
        {
            var controller = _mover.Controller;
            var wasEnabled = controller != null && controller.enabled;
            if (wasEnabled)
            {
                controller.enabled = false;
            }

            transform.position = rootPosition;
            if (wasEnabled)
            {
                controller.enabled = true;
            }
        }

        private Vector3 RootPositionFor(Vector3 feetPosition)
        {
            return feetPosition + (Vector3.up * FeetBelowRoot);
        }

        private float CurrentYaw()
        {
            return _look != null && _look.IsInitialized ? _look.Yaw : transform.eulerAngles.y;
        }

        private void SuspendComponents()
        {
            _suspended.Clear();
            foreach (var component in _suspendWhileRiding)
            {
                if (component != null && component.enabled)
                {
                    component.enabled = false;
                    _suspended.Add(component);
                }
            }
        }

        private void ResumeComponents()
        {
            foreach (var component in _suspended)
            {
                if (component != null)
                {
                    component.enabled = true;
                }
            }

            _suspended.Clear();
        }

        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            Interact();
        }

        private void OnDied(Damageable _)
        {
            var vehicle = Vehicle;
            if (vehicle != null)
            {
                vehicle.ForceExit(this, VehicleLeaveReason.RiderDied);
            }
        }

        private InputAction Find(InputActionAsset actions, string path)
        {
            var action = actions.FindAction(path);
            if (action == null)
            {
                Debug.LogWarning($"{LogTag} Input Actions '{actions.name}' に '{path}' がないため、乗り物ではそれを読みません。", this);
            }

            return action;
        }

        // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたかを覚え、それだけ後で無効に戻す。
        private sealed class ActionSlot
        {
            private bool _enabledByMe;
            private bool _acquired;

            public InputAction Action { get; private set; }

            public void Set(InputAction action, bool acquire)
            {
                Release();
                Action = action;
                if (acquire)
                {
                    Acquire();
                }
            }

            public void Acquire()
            {
                _acquired = true;
                _enabledByMe = Action != null && !Action.enabled;
                if (_enabledByMe)
                {
                    Action.Enable();
                }
            }

            public void Release()
            {
                if (_enabledByMe && Action != null)
                {
                    Action.Disable();
                }

                _enabledByMe = false;
                _acquired = false;
            }

            public bool WasPressedThisFrame()
            {
                return _acquired && Action != null && Action.enabled && Action.WasPressedThisFrame();
            }
        }
    }
}
