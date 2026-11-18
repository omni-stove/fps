using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.NetV2
{
    // フレームで押したもの (ジャンプ・切り替え・回避・射撃など) は次の tick まで覚えておく。tick はフレームより遅いことがあり、取りこぼすため。
    // サーバーは押し方を知らないので、ダッシュとしゃがみは操作設定 (ControlSettings) の押し方を解釈し、送るのは解釈した後の「したいか」だけにする。
    // 射撃は押した瞬間だけでなく押している間も読む。撃つ間合いと押した・離したを武器の撃ち方 (ClientTrigger) で決めるため。
    [DisallowMultipleComponent]
    public sealed class NetV2LocalPlayer : MonoBehaviour
    {
        public const string ObjectName = "NetV2Player";

        private const string _eyeName = "Eye";
        private const string _cameraName = "Camera";

        // InputActionPaths に置かないのは、入力アクションの資産に弾薬の切り替えを足すのがオフラインの射撃の側 (#685) で、それより前にも動かすため。
        private const string _switchAmmoTypePath = "Player/SwitchAmmoType";

        // シーンに置かれたカメラ (メニューの背景など) より前に描く
        private const float _cameraDepth = 10f;

        private readonly List<InputAction> _enabledActions = new List<InputAction>();
        private StanceGeometry _geometry;
        private Transform _eye;
        private Camera _camera;
        private FirstPersonLook _look;
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _move;
        [NonSerialized]
        private InputAction _jump;
        [NonSerialized]
        private InputAction _sprint;
        [NonSerialized]
        private InputAction _crouch;
        [NonSerialized]
        private InputAction _cover;
        [NonSerialized]
        private InputAction _aim;
        [NonSerialized]
        private InputAction _dodge;
        [NonSerialized]
        private InputAction _attack;
        [NonSerialized]
        private InputAction _reload;
        [NonSerialized]
        private InputAction _grenade;
        [NonSerialized]
        private InputAction _ping;
        [NonSerialized]
        private InputAction _prone;
        [NonSerialized]
        private InputAction _switchAmmoType;
        [NonSerialized]
        private InputAction _switchGrenade;
        [NonSerialized]
        private InputAction _interact;
        [NonSerialized]
        private InputAction _nextSeat;
        [NonSerialized]
        private InputAction _previousSeat;
        [NonSerialized]
        private InputAction _leanLeft;
        [NonSerialized]
        private InputAction _leanRight;
        [NonSerialized]
        private InputAction _melee;
        private SprintInput _sprintInput;
        private CrouchInput _crouchInput;
        private LeanInput _leanInput = new LeanInput();
        private MotorSpec _motor;
        private ControlSettings _controlSettings = ControlSettings.Default;
        private bool _listeningToSettings;
        private InputDeviceKind _device = InputDeviceKind.KeyboardMouse;
        private InputDeviceKind _crouchDevice = InputDeviceKind.KeyboardMouse;
        private bool _jumpLatched;
        private bool _sprintLatched;
        private bool _crouchLatched;
        private bool _coverLatched;
        private bool _dodgeLatched;
        private bool _attackLatched;
        private bool _reloadLatched;
        private bool _grenadeLatched;
        private bool _pingLatched;
        private bool _proneLatched;
        private bool _switchAmmoTypeLatched;
        private bool _switchGrenadeLatched;
        private bool _interactLatched;
        private bool _nextSeatLatched;
        private bool _previousSeatLatched;
        private bool _leanLeftLatched;
        private bool _leanRightLatched;
        private bool _meleeLatched;

        public Camera Camera => _camera;

        // オフラインの LeanController と同じく、押している間だけ傾けるか押すたびに切り替えるかを選べる。
        public HoldMode LeanMode
        {
            get => _leanInput.Mode;
            set => _leanInput.Mode = value;
        }

        public FirstPersonLook Look => _look;

        public Vector3 EyePosition => _eye.position;

        public Vector3 AimDirection => _camera.transform.forward;

        public static NetV2LocalPlayer Create(StanceGeometry geometry, MotorSpec motor, Func<bool> canStand, Vector3 position, float yaw)
        {
            if (geometry == null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            if (motor == null)
            {
                throw new ArgumentNullException(nameof(motor));
            }

            if (canStand == null)
            {
                throw new ArgumentNullException(nameof(canStand));
            }

            var root = new GameObject(ObjectName);
            root.transform.SetPositionAndRotation(position, NetV2Math.YawRotation(yaw));
            var eye = new GameObject(_eyeName).transform;
            eye.SetParent(root.transform, false);
            eye.localPosition = new Vector3(0f, geometry.Standing.EyeY, 0f);
            var cameraObject = new GameObject(_cameraName);
            cameraObject.transform.SetParent(eye, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.depth = _cameraDepth;
            if (FindAnyObjectByType<AudioListener>() == null)
            {
                cameraObject.AddComponent<AudioListener>();
            }

            var player = root.AddComponent<NetV2LocalPlayer>();
            AttachPresentation(root, true);
            player._geometry = geometry;
            player._motor = motor;
            player._eye = eye;
            player._camera = camera;
            player._sprintInput = new SprintInput();
            player._crouchInput = new CrouchInput(canStand);

            // FirstPersonLook は Awake で子のカメラを探すので、カメラを置いてから付ける
            player._look = root.AddComponent<FirstPersonLook>();
            return player;
        }

        // CharacterComponents.Attach を使わないのは、v2 の本体には人型のリグも手榴弾を投げる見た目も無いため。
        public static WeaponPresentation AttachPresentation(GameObject body, bool local)
        {
            var presentation = body.AddComponent<WeaponPresentation>();
            presentation.IsLocal = local;
            body.AddComponent<ShellEjector>();
            body.AddComponent<DryFireSound>();
            body.AddComponent<BloodPresenter>();
            return presentation;
        }

        // 撃つ位置は目なので、サーバーが当たりの頭をずらすのと同じ量 (LeanStep.EyeOffset) だけ目を横へずらす。
        // 本体は yaw で回っているので、ずらす向きは本体の右 (ローカルの x) になる。
        public void Show(CharacterMotorState state)
        {
            transform.position = NetV2Math.ToUnity(state.Position);
            var eyeY = _geometry.Shape(state.Stance).EyeY;
            var side = state.Lean.IsValid ? state.Lean.Amount * _motor.LeanSideOffset : 0f;
            var eye = new Vector3(side, eyeY, 0f);
            if (_eye.localPosition != eye)
            {
                _eye.localPosition = eye;
            }
        }

        // 空中で押したジャンプでしゃがみを解かないよう、今の予測で接地しているかを受け取る。
        public void Sample(bool grounded, out Vector2 move, out float yaw, out InputButtons buttons)
        {
            var paused = GameUi.IsPauseOpen;
            move = paused || _move == null ? Vector2.zero : Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f);
            var jump = Take(ref _jumpLatched) && !paused;
            var sprintPressed = Take(ref _sprintLatched);
            var sprint = !paused && _sprintInput.Update(
                _controlSettings.Get(_device, HoldAction.Sprint), NetV2Math.ToNumerics(move), HeldInput.IsHeld(_sprint), sprintPressed);
            var crouchPressed = Take(ref _crouchLatched);
            var crouch = _crouchInput.Update(
                _controlSettings.Get(_crouchDevice, HoldAction.Crouch),
                !paused && _crouch != null && _crouch.IsPressed(),
                crouchPressed && !paused,
                jump && grounded,
                sprint);
            var cover = Take(ref _coverLatched) && !paused;
            var dodge = Take(ref _dodgeLatched) && !paused;
            var prone = Take(ref _proneLatched) && !paused;
            var leanLeftPressed = Take(ref _leanLeftLatched);
            var leanRightPressed = Take(ref _leanRightLatched);

            // ポーズ画面のボタンを押したつもりで傾かないよう、開いている間は押していないことにする (LeanController と同じ)
            _leanInput.Update(
                !paused && _leanLeft != null && _leanLeft.IsPressed(),
                leanLeftPressed && !paused,
                !paused && _leanRight != null && _leanRight.IsPressed(),
                leanRightPressed && !paused);
            var aim = IsAiming;

            // 押して離すまでが tick より短くても振れるよう、そのフレームの間に押したことも押している扱いにする
            var melee = (Take(ref _meleeLatched) || IsMeleeHeld) && !paused;
            IsMeleeSent = melee;
            var degrees = _look != null && _look.enabled && _look.IsInitialized ? _look.Yaw : transform.eulerAngles.y;
            yaw = degrees * Mathf.Deg2Rad;
            buttons = SnapshotMotion.Buttons(
                sprint, jump, crouch, cover, aim, dodge, prone, _leanInput.IsLeftActive, _leanInput.IsRightActive, melee);
        }

        public bool IsMeleeHeld => !GameUi.IsPauseOpen && _melee != null && _melee.IsPressed();

        // 最後の tick で近接を押していると送ったか。手元の振る見た目 (ClientMelee) をサーバーと同じ押し始めで始めるため
        public bool IsMeleeSent { get; private set; }

        // 覗き込みを止める撃ち方 (ITriggerModel.BlocksAds) は持っている武器で決まり、武器はサーバーのスナップショットで分かるので外から渡す。
        public bool BlocksAds { get; set; }

        public bool IsAiming => !GameUi.IsPauseOpen && !BlocksAds && _aim != null && _aim.IsPressed();

        public bool TakeAttack() => Take(ref _attackLatched);

        public bool IsAttackHeld => !GameUi.IsPauseOpen && _attack != null && _attack.IsPressed();

        public void FaceYaw(float yaw)
        {
            var degrees = yaw * Mathf.Rad2Deg;
            if (_look != null && _look.IsInitialized)
            {
                _look.ApplyLookDelta(new Vector2(Mathf.DeltaAngle(_look.Yaw, degrees), 0f));
                return;
            }

            transform.rotation = NetV2Math.YawRotation(yaw);
        }

        public bool TakeReload() => Take(ref _reloadLatched);

        public bool TakeGrenade() => Take(ref _grenadeLatched);

        public bool TakePing() => Take(ref _pingLatched);

        public bool TakeSwitchAmmoType() => Take(ref _switchAmmoTypeLatched);

        public bool TakeSwitchGrenade() => Take(ref _switchGrenadeLatched);

        public bool TakeInteract() => Take(ref _interactLatched);

        public bool TakeNextSeat() => Take(ref _nextSeatLatched);

        public bool TakePreviousSeat() => Take(ref _previousSeatLatched);

        public void ReadDrive(out Vector2 move, out bool boost, out float vertical)
        {
            var paused = GameUi.IsPauseOpen;
            move = paused || _move == null ? Vector2.zero : Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f);
            boost = !paused && _sprint != null && _sprint.IsPressed();
            vertical = paused ? 0f : (_jump != null && _jump.IsPressed() ? 1f : 0f) - (_crouch != null && _crouch.IsPressed() ? 1f : 0f);
        }

        public void ShowSeated(Vector3 origin, bool crouched)
        {
            Show(new CharacterMotorState(NetV2Math.ToNumerics(origin), System.Numerics.Vector3.Zero, true, crouched));
        }

        public void ReleaseSprint(string reason)
        {
            _sprintInput.Release(reason);
        }

        public void ReleaseHolds(string reason)
        {
            _crouchInput.Release(reason);
            _sprintInput.Release(reason);
            _leanInput.Release();
        }

        private void Awake()
        {
            _controlSettings = AppliedControlSettings.Current;
            AppliedControlSettings.Changed += HandleSettingsChanged;
            _listeningToSettings = true;
            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError("[Net] the project has no input actions; the NetV2 player cannot move", this);
                return;
            }

            _move = Find(actions, InputActionPaths.Move);
            _jump = Find(actions, InputActionPaths.Jump);
            _sprint = Find(actions, InputActionPaths.Sprint);
            _crouch = Find(actions, InputActionPaths.Crouch);
            _cover = Find(actions, InputActionPaths.Cover);
            _aim = Find(actions, InputActionPaths.Aim);
            _dodge = Find(actions, InputActionPaths.Dodge);
            _attack = Find(actions, InputActionPaths.Attack);
            _reload = Find(actions, InputActionPaths.Reload);
            _grenade = Find(actions, InputActionPaths.Grenade);
            _ping = Find(actions, InputActionPaths.Ping);
            _prone = Find(actions, InputActionPaths.Prone);
            _switchAmmoType = Find(actions, _switchAmmoTypePath, false);
            _switchGrenade = Find(actions, InputActionPaths.SwitchGrenade, false);
            _interact = Find(actions, InputActionPaths.Interact, false);
            _nextSeat = Find(actions, InputActionPaths.Next, false);
            _previousSeat = Find(actions, InputActionPaths.Previous, false);
            _leanLeft = Find(actions, InputActionPaths.LeanLeft);
            _leanRight = Find(actions, InputActionPaths.LeanRight);
            _melee = Find(actions, InputActionPaths.Melee, false);
        }

        private void OnDestroy()
        {
            if (_listeningToSettings)
            {
                AppliedControlSettings.Changed -= HandleSettingsChanged;
                _listeningToSettings = false;
            }

            // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたものだけ戻す
            foreach (var action in _enabledActions)
            {
                action.Disable();
            }

            _enabledActions.Clear();
        }

        private void Update()
        {
            if (GameUi.IsPauseOpen)
            {
                return;
            }

            _jumpLatched |= Pressed(_jump);
            _sprintLatched |= Pressed(_sprint);
            _crouchLatched |= Pressed(_crouch);
            _coverLatched |= Pressed(_cover);
            _dodgeLatched |= Pressed(_dodge);
            _attackLatched |= Pressed(_attack);
            _reloadLatched |= Pressed(_reload);
            _grenadeLatched |= Pressed(_grenade);
            _pingLatched |= Pressed(_ping);
            _proneLatched |= Pressed(_prone);
            _switchAmmoTypeLatched |= Pressed(_switchAmmoType);
            _switchGrenadeLatched |= Pressed(_switchGrenade);
            _interactLatched |= Pressed(_interact);
            _nextSeatLatched |= Pressed(_nextSeat);
            _previousSeatLatched |= Pressed(_previousSeat);
            _leanLeftLatched |= Pressed(_leanLeft);
            _leanRightLatched |= Pressed(_leanRight);
            _meleeLatched |= Pressed(_melee);

            // activeControl は動かしている間しか取れないので、tick ではなくフレームごとに見て直前の機器を覚えておく
            _device = InputDeviceKinds.Of(_sprint, InputDeviceKinds.Of(_move, _device));
            _crouchDevice = InputDeviceKinds.Of(_crouch, InputDeviceKinds.Of(_move, _crouchDevice));
        }

        private void HandleSettingsChanged(ControlSettings settings)
        {
            _controlSettings = settings;
        }

        // 必須でない操作は、入力アクションの資産に無いのが正しい版 (弾薬の切り替えを足す前) もあるので警告しない。
        private InputAction Find(InputActionAsset actions, string path, bool required = true)
        {
            var action = actions.FindAction(path);
            if (action == null)
            {
                if (required)
                {
                    Debug.LogWarning("[Net] input action " + path + " is missing; the NetV2 player ignores it", this);
                }

                return null;
            }

            if (!action.enabled)
            {
                action.Enable();
                _enabledActions.Add(action);
            }

            return action;
        }

        private static bool Pressed(InputAction action)
        {
            return action != null && action.WasPressedThisFrame();
        }

        private static bool Take(ref bool latched)
        {
            var value = latched;
            latched = false;
            return value;
        }
    }
}
