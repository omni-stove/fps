using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.NetV2
{
    // 自分のドローンがスナップショットに現れたら、オフラインの DroneControl と同じく体と視点を手放させて操縦を始め、
    // 移動・ジャンプ・しゃがみ・視点の入力をサーバーへ送る。降りるのは Attack (DroneControl と同じ)。
    // ドローンの見た目はほかの置いた物と同じく NetV2DeployablesPart が出すので、ここは操縦の視点と GameSignals.Drone だけを持つ。
    // 電池と距離の割合はサーバーから届かないので、ドローンの定義の値で手元の DroneState に数えさせて出す
    public sealed class NetV2DronePart : INetV2ClientPart
    {
        public const string ViewObjectName = "NetDroneView";

        // 届いた位置へ視点を寄せる速さ (1/秒)。スナップショットの間を飛び飛びに見せないための追従で、手元で先に動かす予測はしない
        public const float FollowSharpness = 15f;

        private const float _nearClip = 0.05f;

        private readonly Func<int> _localPlayerId;
        private readonly Func<bool> _canAct;
        private readonly ClientDrone _drone;
        private readonly Func<NetV2LocalPlayer> _localPlayer;
        private readonly Func<float, DroneInput> _readInput;
        private readonly Func<bool> _leaveHeld;
        private readonly Action<string> _log;
        private readonly Dictionary<string, DroneDeployDefinition> _definitions =
            new Dictionary<string, DroneDeployDefinition>(StringComparer.Ordinal);
        private readonly List<InputAction> _enabledActions = new List<InputAction>();
        private NetV2LocalPlayer _held;
        private FirstPersonLook _look;
        private bool _lookWasSuspended;
        private bool _restoreWhenLeaveReleased;
        private bool _appearedPending;
        private Camera _camera;
        private float _yaw;
        private float _pitch;
        private DroneState _state;
        private float _eyeHeight;
        private DroneStatus? _published;
        private bool _disposed;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _crouchAction;
        private InputAction _leaveAction;

        // 自分の Id は承認されるまで決まらないので、値でなく引く口で受け取る。canAct は自分が試合中に立っているか (NetV2ClientParts.CanAct)。
        // 入力を読む口は、テストや別の入力から差し替えられるよう受け取る。無ければ InputSystem の Move・Jump・Crouch・Attack と、自分の体の視点 (FirstPersonLook) を読む。
        // leaveHeld は降りる押下がまだ続いているか。離されるまで体を返さず、降りた押下で撃たないようにする
        public NetV2DronePart(
            Action<IMessage, TransportChannel> send,
            Func<int> localPlayerId,
            Func<bool> canAct,
            IEnumerable<DeployAbilityDefinition> definitions,
            Func<NetV2LocalPlayer> localPlayer = null,
            Action<string> log = null,
            Func<float, DroneInput> readInput = null,
            Func<bool> leaveHeld = null)
        {
            _localPlayerId = localPlayerId ?? throw new ArgumentNullException(nameof(localPlayerId));
            _canAct = canAct ?? throw new ArgumentNullException(nameof(canAct));
            _log = log ?? (line => Debug.Log(line));
            _drone = new ClientDrone(send ?? throw new ArgumentNullException(nameof(send)), _log);
            _localPlayer = localPlayer ?? (() => NetV2Client.Current != null ? NetV2Client.Current.LocalPlayer : null);
            _readInput = readInput ?? ReadInput;
            _leaveHeld = leaveHeld ?? (() => _leaveAction != null && _leaveAction.IsPressed());
            if (definitions != null)
            {
                foreach (var definition in definitions)
                {
                    if (definition is DroneDeployDefinition drone && !string.IsNullOrEmpty(drone.Id) && !_definitions.ContainsKey(drone.Id))
                    {
                        _definitions.Add(drone.Id, drone);
                    }
                }
            }

            _drone.Appeared += HandleAppeared;
            _drone.Gone += HandleGone;
        }

        public ClientDrone Drone => _drone;

        public bool IsPiloting => _drone.IsPiloting;

        public Camera ViewCamera => _camera;

        public float Pitch => _pitch;

        public DroneStatus? Published => _published;

        // 結線の登録 (NetV2ClientParts.Registered) に 1 行で足せる形。再生では操縦を送る相手がいないので部品を出さない
        public static INetV2ClientPart Create(NetV2ClientPartsContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.Replay)
            {
                return null;
            }

            var client = context.Client;
            return new NetV2DronePart(
                client.Send,
                () => client.PlayerId,
                () => NetV2ClientParts.CanAct(client),
                NetV2ClientParts.DeployDefinitionsOf(context.Abilities, context.AbilityCatalog),
                context.LocalPlayer,
                context.Log);
        }

        public bool Handle(IMessage message)
        {
            return false;
        }

        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null)
            {
                return;
            }

            _drone.Apply(snapshot, _localPlayerId());

            // 現れたのと同じスナップショットで倒れていれば、操縦は始めない (オフラインでは倒れた人は置けない)
            if (_appearedPending)
            {
                _appearedPending = false;
                if (_canAct())
                {
                    Begin();
                }
            }

            if (_drone.IsPiloting && !_canAct())
            {
                End(DronePilotEnd.Incapacitated);
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (_disposed)
            {
                return;
            }

            var delta = deltaSeconds >= 0f && !float.IsInfinity(deltaSeconds) ? deltaSeconds : 0f;
            if (_restoreWhenLeaveReleased && !_leaveHeld())
            {
                Restore();
            }

            if (_drone.IsPiloting)
            {
                Pilot(delta);
            }

            if (_state != null && _drone.DroneId.HasValue)
            {
                _state.Tick(delta, System.Numerics.Vector3.Distance(_drone.Position, _drone.Launch));
            }

            AimView(delta);
            Publish();
        }

        public bool Recall()
        {
            if (_disposed || !_drone.DroneId.HasValue)
            {
                return false;
            }

            var piloting = _drone.IsPiloting;
            var recalled = _drone.Recall();
            if (piloting)
            {
                Restore();
                HideView();
            }

            return recalled;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            End(DronePilotEnd.Disabled);
            Restore();
            _drone.Appeared -= HandleAppeared;
            _drone.Gone -= HandleGone;
            _drone.Reset();
            _state = null;
            Unpublish();
            DestroyView();
        }

        // 定義の無い種類 (カタログに無い Id) も操縦はできるよう、電池と距離は既定の値で数える
        private void HandleAppeared(int deployableId)
        {
            _definitions.TryGetValue(_drone.TypeId, out var definition);
            _state = new DroneState(definition != null ? SpecOf(definition) : DroneSpec.Default);
            _eyeHeight = DroneDeployDefinition.CollisionRadiusOf(definition != null ? DeploySpecOf(definition) : DeployType.DefaultDeploy(DeployRole.Drone));
            _appearedPending = true;
        }

        private void HandleGone(int deployableId)
        {
            Restore();
            HideView();
            _state = null;
            Unpublish();
        }

        private DroneSpec SpecOf(DroneDeployDefinition definition)
        {
            try
            {
                return definition.ToDroneSpec();
            }
            catch (AbilityDefinitionException e)
            {
                _log(e.Message);
                return DroneSpec.Default;
            }
        }

        private DeploySpec DeploySpecOf(DroneDeployDefinition definition)
        {
            try
            {
                return definition.ToDeploySpec();
            }
            catch (AbilityDefinitionException e)
            {
                _log(e.Message);
                return DeployType.DefaultDeploy(DeployRole.Drone);
            }
        }

        private bool Begin()
        {
            if (!_drone.Begin())
            {
                return false;
            }

            _yaw = _drone.Yaw;
            _pitch = 0f;
            Hold();
            ResolveActions();
            ShowView();
            return true;
        }

        private void End(DronePilotEnd reason)
        {
            if (!_drone.Leave(reason))
            {
                return;
            }

            HideView();

            // 降りる押下で同じフレームに撃たないよう、押したまま降りたときは離されるまで体を返さない (DroneControl と同じ)
            if (reason == DronePilotEnd.Interact && _leaveHeld())
            {
                _restoreWhenLeaveReleased = true;
                return;
            }

            Restore();
        }

        private void Pilot(float deltaSeconds)
        {
            var input = GameUi.IsPauseOpen ? default : _readInput(deltaSeconds);
            if (input.Leave)
            {
                End(DronePilotEnd.Interact);
                return;
            }

            _yaw = Mathf.Repeat(_yaw + input.LookDegrees.x, 360f);
            _pitch = Mathf.Clamp(_pitch - input.LookDegrees.y, -DroneControl.MaxPitchDegrees, DroneControl.MaxPitchDegrees);
            var move = Vector2.ClampMagnitude(input.Move, 1f);
            _drone.Steer(new System.Numerics.Vector2(move.x, move.y), Mathf.Clamp(input.Climb, -1f, 1f), _yaw, deltaSeconds);
        }

        // 手放させる口は NetV2StreakPart と同じく自分の体の Handoff を使う。体が送る移動は空になり、撃つ・投げるも止まる
        private void Hold()
        {
            Restore();
            var local = _localPlayer();
            if (local == null)
            {
                return;
            }

            if (local.Handoff.Claim(this))
            {
                _held = local;
            }

            _look = local.Look;
            if (_look != null)
            {
                _lookWasSuspended = _look.InputSuspended;
                _look.InputSuspended = true;
            }
        }

        private void Restore()
        {
            _restoreWhenLeaveReleased = false;
            if (_held != null)
            {
                _held.Handoff.Release(this);
            }

            _held = null;
            if (_look != null)
            {
                _look.InputSuspended = _lookWasSuspended;
            }

            _look = null;
            ReleaseActions();
        }

        private DroneInput ReadInput(float deltaSeconds)
        {
            var move = _moveAction != null ? Vector2.ClampMagnitude(_moveAction.ReadValue<Vector2>(), 1f) : Vector2.zero;
            var look = _look != null ? _look.ReadLookDegrees(deltaSeconds) : Vector2.zero;
            var climb = (Held(_jumpAction) ? 1f : 0f) - (Held(_crouchAction) ? 1f : 0f);
            return new DroneInput(move, look, climb, _leaveAction != null && _leaveAction.WasPressedThisFrame());
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

            // Interact は拾う・乗り物が手放しを見ずに受けるので、降りる操作には手放しで止まる Attack を使う (DroneControl と同じ)
            _leaveAction = Acquire(actions, InputActionPaths.Attack);
        }

        // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたものだけ後で無効に戻す
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

        // 降りた押下が離されるのを待つ間も Attack を読むので、体を返すときにまとめて戻す
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
            _leaveAction = null;
        }

        // 体の子にすると体の向きで視点が回るので、自分で持って毎回置き直す (DroneControl と同じ)
        private void ShowView()
        {
            if (_camera == null)
            {
                var view = new GameObject(ViewObjectName);
                _camera = view.AddComponent<Camera>();
                _camera.depth = DroneControl.ViewDepth;
                _camera.nearClipPlane = _nearClip;
            }

            _camera.gameObject.SetActive(true);
            _camera.transform.SetPositionAndRotation(EyeOf(_drone.Position), Quaternion.Euler(_pitch, _yaw, 0f));
        }

        private void AimView(float deltaSeconds)
        {
            if (_camera == null || !_camera.gameObject.activeSelf || !_drone.DroneId.HasValue)
            {
                return;
            }

            var target = EyeOf(_drone.Position);
            var blend = 1f - Mathf.Exp(-FollowSharpness * deltaSeconds);
            var position = Vector3.Lerp(_camera.transform.position, target, blend);
            _camera.transform.SetPositionAndRotation(position, Quaternion.Euler(_pitch, _yaw, 0f));
        }

        // 置いた物の位置は足元なので、オフラインの視点 (胴の中心) と同じ高さへ上げる
        private Vector3 EyeOf(System.Numerics.Vector3 position)
        {
            return NetV2Math.ToUnity(position) + (Vector3.up * _eyeHeight);
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
                UnityEngine.Object.Destroy(view);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(view);
            }
        }

        // 毎フレーム購読側を起こさないよう、値が変わったときだけ出す
        private void Publish()
        {
            if (_state == null || !_drone.DroneId.HasValue)
            {
                Unpublish();
                return;
            }

            var status = _state.Status(_drone.IsPiloting);
            if (_published.HasValue && _published.Value == status)
            {
                return;
            }

            _published = status;
            GameSignals.Drone.Publish(status);
        }

        // 他の側が後から出した値まで消さないよう、自分が出した値のときだけ消す
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
}
