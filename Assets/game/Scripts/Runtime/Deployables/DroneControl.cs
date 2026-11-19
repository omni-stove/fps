using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    // 持ち主の側に付けて、ドローンを置いてから消えるまでの状態を GameSignals.Drone に出し、操縦している間は体と武器を手放させる。
    // 降りてもドローンは残って見回し続け、戻すのは置く能力の短押し (DroneDeployDefinition.Detonate) で受ける。
    // 操縦中は体を手放しているので能力の枠も使えず (AbilityHost.IsIncapacitated)、戻すには先に降りる。
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(110)]
    public sealed class DroneControl : MonoBehaviour
    {
        public const string LogTag = DroneLog.Tag;

        public const float MaxPitchDegrees = 80f;

        // 見下ろすカメラ (StreakTargeting) と同じく、プレイヤーのカメラ (深さ 0 前後) より後に描いて画面を覆う。
        public const float ViewDepth = 50f;

        public const string ViewObjectName = "DroneView";

        private const float _nearClip = 0.05f;

        private readonly List<ControlHandoff> _claimed = new List<ControlHandoff>();
        private readonly List<InputAction> _enabledActions = new List<InputAction>();

        private ReconDrone _drone;
        private bool _piloting;
        private FirstPersonLook _look;
        private bool _lookWasSuspended;
        private AimDownSights _ads;
        private bool _adsWasEnabled;
        private Camera _camera;
        private float _pitch;
        private int _begunFrame = -1;
        private int _restoreAfterFrame = -1;
        private DroneStatus? _published;
        private Damageable _self;

        [NonSerialized]
        private InputAction _moveAction;
        [NonSerialized]
        private InputAction _jumpAction;
        [NonSerialized]
        private InputAction _crouchAction;
        [NonSerialized]
        private InputAction _interactAction;
        [NonSerialized]
        private InputAction _leaveHeldAction;

        public ReconDrone Drone => _drone != null && _drone.IsArmed ? _drone : null;

        public bool IsPiloting => _piloting;

        public Camera ViewCamera => _camera;

        public float Pitch => _pitch;

        public DroneStatus? Published => _published;

        public static DroneControl GetOrAdd(GameObject owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            var control = owner.GetComponent<DroneControl>();
            return control != null ? control : owner.AddComponent<DroneControl>();
        }

        // 手放させる口は StreakController と同じく PlayerController と WeaponController のものを使う。
        public bool Begin(ReconDrone drone)
        {
            var mover = GetComponent<PlayerController>();
            var weapon = GetComponentInChildren<WeaponController>(true);
            var look = mover != null ? mover.Look : GetComponent<FirstPersonLook>();
            var handoffs = new List<ControlHandoff>(2);
            if (mover != null)
            {
                handoffs.Add(mover.Handoff);
            }

            // オフラインでは移動と武器が同じ口を共有している (GameBootstrap) ので、別のときだけ武器の口も握る。
            if (weapon != null && weapon.Handoff != null && (mover == null || !ReferenceEquals(weapon.Handoff, mover.Handoff)))
            {
                handoffs.Add(weapon.Handoff);
            }

            return Begin(drone, handoffs, look);
        }

        public bool Begin(ReconDrone drone, IReadOnlyList<ControlHandoff> handoffs, FirstPersonLook look)
        {
            if (drone == null)
            {
                throw new ArgumentNullException(nameof(drone));
            }

            if (!drone.IsArmed)
            {
                throw new ArgumentException($"drone '{drone.name}' must be armed before it is piloted", nameof(drone));
            }

            if (_piloting)
            {
                return false;
            }

            _self = GetComponent<Damageable>();
            if (_self != null && (_self.IsDead || _self.IsDowned))
            {
                return false;
            }

            Watch(drone);
            _piloting = true;
            _drone.IsPiloted = true;
            _pitch = 0f;
            _begunFrame = Time.frameCount;
            Hold(handoffs, look);
            ResolveActions();
            ShowView();
            var deployable = _drone.Deployable;
            Debug.Log(DroneLog.PilotStarted(deployable.OwnerId, deployable.KindId, deployable.Serial), this);
            Publish();
            return true;
        }

        public bool Leave(DronePilotEnd reason)
        {
            if (!_piloting)
            {
                return false;
            }

            _piloting = false;
            // ReleaseActions が _interactAction を捨てるので、離されるまで待つための参照を先に取っておく。
            _leaveHeldAction = _interactAction;
            ReleaseActions();
            HideView();
            if (_drone != null)
            {
                _drone.IsPiloted = false;
                var deployable = _drone.Deployable;
                Debug.Log(DroneLog.PilotEnded(deployable.OwnerId, deployable.KindId, deployable.Serial, reason), this);
            }

            // Edit Mode ではフレームが進まないので、すぐ返す。倒れた・無効になったときは押下が残らないので、その場で返す。
            if (reason == DronePilotEnd.Interact && Application.isPlaying)
            {
                _restoreAfterFrame = Time.frameCount;
            }
            else
            {
                Restore();
            }

            Publish();
            return true;
        }

        public void Step(DroneInput input, float deltaSeconds)
        {
            if (!_piloting || _drone == null || !_drone.IsArmed)
            {
                return;
            }

            if (input.Leave)
            {
                Leave(DronePilotEnd.Interact);
                return;
            }

            _drone.Turn(input.LookDegrees.x);
            _pitch = Mathf.Clamp(_pitch - input.LookDegrees.y, -MaxPitchDegrees, MaxPitchDegrees);
            var move = Vector2.ClampMagnitude(input.Move, 1f);
            var flat = (_drone.transform.right * move.x) + (_drone.transform.forward * move.y);
            var direction = flat + (Vector3.up * Mathf.Clamp(input.Climb, -1f, 1f));
            _drone.Fly(Vector3.ClampMagnitude(direction, 1f), deltaSeconds);
            AimView();
            Publish();
        }

        private void Update()
        {
            if (_drone == null)
            {
                return;
            }

            if (_piloting && _self != null && (_self.IsDead || _self.IsDowned))
            {
                Leave(DronePilotEnd.Incapacitated);
                return;
            }

            // 始めた押下 (置く能力を離した) と同じフレームの押下を、降りる操作として読まない。
            if (_piloting && !GameUi.IsPauseOpen && Time.frameCount != _begunFrame)
            {
                Step(ReadInput(Time.deltaTime), Time.deltaTime);
            }

            Publish();
        }

        // 降りるのに使った押下で、同じフレームに撃たないよう、返すのは押したフレームが終わってからにする。
        private void LateUpdate()
        {
            // 押したままだと手放しが返った直後に全自動武器が撃ち始めるので、離されるまで待つ。
            if (_restoreAfterFrame >= 0 && Time.frameCount > _restoreAfterFrame
                && (_leaveHeldAction == null || !_leaveHeldAction.IsPressed()))
            {
                Restore();
            }
        }

        private void OnDisable()
        {
            Leave(DronePilotEnd.Disabled);
            Restore();
        }

        private void OnDestroy()
        {
            Leave(DronePilotEnd.Disabled);
            Restore();
            Unwatch();
            Unpublish();
            DestroyView();
        }

        private DroneInput ReadInput(float deltaSeconds)
        {
            var move = _moveAction != null ? Vector2.ClampMagnitude(_moveAction.ReadValue<Vector2>(), 1f) : Vector2.zero;
            var look = _look != null ? _look.ReadLookDegrees(deltaSeconds) : Vector2.zero;
            var climb = (Held(_jumpAction) ? 1f : 0f) - (Held(_crouchAction) ? 1f : 0f);
            return new DroneInput(move, look, climb, _interactAction != null && _interactAction.WasPressedThisFrame());
        }

        private void Watch(ReconDrone drone)
        {
            if (_drone == drone)
            {
                return;
            }

            Unwatch();
            _drone = drone;
            _drone.Deployable.Removed += OnDroneRemoved;

            // 降りた後も電池と見つけた数は減り増えするので、ドローンが進むたびに出し直す (Edit Mode では Update が呼ばれない)。
            _drone.Changed += OnDroneChanged;
            _self = GetComponent<Damageable>();
            if (_self != null)
            {
                _self.Died += OnSelfDied;
            }
        }

        private void Unwatch()
        {
            if (_self != null)
            {
                _self.Died -= OnSelfDied;
            }

            if (_drone == null)
            {
                return;
            }

            if (_drone.Deployable != null)
            {
                _drone.Deployable.Removed -= OnDroneRemoved;
            }

            _drone.Changed -= OnDroneChanged;
            _drone.IsPiloted = false;
            _drone = null;
        }

        private void OnDroneChanged(ReconDrone drone)
        {
            Publish();
        }

        private void OnDroneRemoved(Deployable deployable, DeployRemoval reason)
        {
            Leave(DronePilotEnd.DroneGone);
            Unwatch();
            Unpublish();
        }

        private void OnSelfDied(Damageable self)
        {
            Leave(DronePilotEnd.Incapacitated);
        }

        private void Hold(IReadOnlyList<ControlHandoff> handoffs, FirstPersonLook look)
        {
            Restore();
            if (handoffs != null)
            {
                foreach (var handoff in handoffs)
                {
                    if (handoff != null && !_claimed.Contains(handoff) && handoff.Claim(this))
                    {
                        _claimed.Add(handoff);
                    }
                }
            }

            _look = look;
            if (_look != null)
            {
                _lookWasSuspended = _look.InputSuspended;
                _look.InputSuspended = true;
            }

            // 覗き込みは手放しの口を見ないので、操縦している間は止める。
            _ads = GetComponent<AimDownSights>();
            if (_ads != null)
            {
                _ads.Release();
                _adsWasEnabled = _ads.enabled;
                _ads.enabled = false;
            }
        }

        private void Restore()
        {
            _restoreAfterFrame = -1;
            _leaveHeldAction = null;
            foreach (var handoff in _claimed)
            {
                handoff.Release(this);
            }

            _claimed.Clear();
            if (_look != null)
            {
                _look.InputSuspended = _lookWasSuspended;
            }

            _look = null;
            if (_ads != null)
            {
                _ads.enabled = _adsWasEnabled;
            }

            _ads = null;
        }

        private void ResolveActions()
        {
            var actions = InputSystem.actions;
            if (actions == null)
            {
                return;
            }

            _moveAction = Acquire(actions, InputActionPaths.Move);
            _jumpAction = Acquire(actions, InputActionPaths.Jump);
            _crouchAction = Acquire(actions, InputActionPaths.Crouch);
            // Interact は拾う・補給・乗り物が手放しを見ずに受けるので、降りる操作には手放しで止まる Attack を使う。
            _interactAction = Acquire(actions, InputActionPaths.Attack);
        }

        // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたものだけ後で無効に戻す。
        private InputAction Acquire(InputActionAsset actions, string path)
        {
            var action = actions.FindAction(path);
            if (action != null && !action.enabled)
            {
                action.Enable();
                _enabledActions.Add(action);
            }

            return action;
        }

        private void ReleaseActions()
        {
            foreach (var action in _enabledActions)
            {
                action.Disable();
            }

            _enabledActions.Clear();
            _moveAction = null;
            _jumpAction = null;
            _crouchAction = null;
            _interactAction = null;
        }

        // ドローンの子にすると、差し替えた見た目が消えるときにカメラも一緒に消えて作り直しになるので、自分で持って毎回置き直す。
        private void ShowView()
        {
            if (_camera == null)
            {
                var view = new GameObject(ViewObjectName);
                if (gameObject.scene.IsValid() && view.scene != gameObject.scene)
                {
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(view, gameObject.scene);
                }

                _camera = view.AddComponent<Camera>();
                _camera.depth = ViewDepth;
                _camera.nearClipPlane = _nearClip;
            }

            _camera.gameObject.SetActive(true);
            AimView();
        }

        private void AimView()
        {
            if (_camera == null || _drone == null)
            {
                return;
            }

            var anchor = _drone.transform.Find(DroneDeployDefinition.CameraAnchorName);
            var origin = anchor != null ? anchor.position : _drone.transform.position + (Vector3.up * _drone.CollisionRadius);
            var rotation = Quaternion.Euler(_pitch, _drone.transform.eulerAngles.y, 0f);
            _camera.transform.SetPositionAndRotation(origin, rotation);
        }

        private void HideView()
        {
            if (_camera != null)
            {
                _camera.gameObject.SetActive(false);
            }
        }

        private void DestroyView()
        {
            if (_camera == null)
            {
                return;
            }

            var view = _camera.gameObject;
            _camera = null;
            if (Application.isPlaying)
            {
                Destroy(view);
            }
            else
            {
                DestroyImmediate(view);
            }
        }

        // 毎フレーム購読側を起こさないよう、値が変わったときだけ出す。
        private void Publish()
        {
            var status = _drone != null ? _drone.Status : null;
            if (!status.HasValue)
            {
                Unpublish();
                return;
            }

            if (_published.HasValue && _published.Value == status.Value)
            {
                return;
            }

            _published = status;
            GameSignals.Drone.Publish(status);
        }

        // 後から別の持ち主が出した値までは消さない。
        private void Unpublish()
        {
            if (_published.HasValue)
            {
                GameSignals.Drone.ClearIfCurrent(_published);
                _published = null;
            }
        }

        private static bool Held(InputAction action)
        {
            return action != null && action.IsPressed();
        }
    }

    public readonly struct DroneInput
    {
        public DroneInput(Vector2 move, Vector2 lookDegrees, float climb, bool leave)
        {
            Move = move;
            LookDegrees = lookDegrees;
            Climb = climb;
            Leave = leave;
        }

        public Vector2 Move { get; }

        // x は右回りの向き、y は上向きの角度 (FirstPersonLook.ReadLookDegrees と同じ)。
        public Vector2 LookDegrees { get; }

        // 上へ 1、下へ -1。
        public float Climb { get; }

        public bool Leave { get; }
    }
}
