using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Protocol;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.NetV2
{
    /// <summary>
    /// ネット対戦の自分のプレイヤーの視点と入力。視点は <see cref="FirstPersonLook"/> が本体の yaw と目の pitch を回し、
    /// 移動の入力は Netcode のクライアント (<c>NetworkPlayerMotor</c>) と同じ読み方で tick ごとに 1 つ作る。
    /// 位置は予測の結果 (<see cref="Show"/>) を書き写すだけで、自分では動かさない。
    /// </summary>
    /// <remarks>
    /// フレームで押したもの (ジャンプ・切り替え・回避・射撃など) は次の tick まで覚えておく。tick はフレームより遅いことがあり、取りこぼすため。
    /// ダッシュとしゃがみは操作設定 (<see cref="ControlSettings"/>) の押し方を解釈し、送るのは解釈した後の「したいか」だけにする (サーバーは押し方を知らない)。
    /// 射撃は押した瞬間だけでなく押している間も読む。撃つ間合いと押した・離したを武器の撃ち方 (<see cref="ClientTrigger"/>) で決めるため。
    /// </remarks>
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
        private InputAction _move;
        private InputAction _jump;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _cover;
        private InputAction _aim;
        private InputAction _dodge;
        private InputAction _attack;
        private InputAction _reload;
        private InputAction _grenade;
        private InputAction _ping;
        private InputAction _prone;
        private InputAction _switchAmmoType;
        private InputAction _switchGrenade;
        private SprintInput _sprintInput;
        private CrouchInput _crouchInput;
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

        public Camera Camera => _camera;

        /// <summary>目の位置 (射撃・投げ・印の出どころ)。</summary>
        public Vector3 EyePosition => _eye.position;

        /// <summary>見ている向き (長さ 1)。</summary>
        public Vector3 AimDirection => _camera.transform.forward;

        /// <summary>
        /// 自分のプレイヤーを作る。geometry は予測の体と同じ寸法、canStand は今の位置で立ち上がれるか (しゃがみの自動解除に使う)。
        /// </summary>
        public static NetV2LocalPlayer Create(StanceGeometry geometry, Func<bool> canStand, Vector3 position, float yaw)
        {
            if (geometry == null)
            {
                throw new ArgumentNullException(nameof(geometry));
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
            player._eye = eye;
            player._camera = camera;
            player._sprintInput = new SprintInput();
            player._crouchInput = new CrouchInput(canStand);

            // FirstPersonLook は Awake で子のカメラを探すので、カメラを置いてから付ける
            player._look = root.AddComponent<FirstPersonLook>();
            return player;
        }

        /// <summary>
        /// 撃つ人の本体に付ける武器の演出 (銃声・マズルフラッシュ・曳光弾・薬莢・空撃ちの音・血)。Netcode の NetPlayer と同じ部品で、
        /// 中身 (音とエフェクト) はそれぞれのカタログから武器の Id で引く。local は自分の本体か (自分の音は 2D で鳴らす)。
        /// </summary>
        /// <remarks>CharacterComponents.Attach を使わないのは、v2 の本体には人型のリグも手榴弾を投げる見た目も無いため。</remarks>
        public static WeaponPresentation AttachPresentation(GameObject body, bool local)
        {
            var presentation = body.AddComponent<WeaponPresentation>();
            presentation.IsLocal = local;
            body.AddComponent<ShellEjector>();
            body.AddComponent<DryFireSound>();
            body.AddComponent<BloodPresenter>();
            return presentation;
        }

        /// <summary>予測した状態を書き写す。目の高さは姿勢に合わせる。</summary>
        public void Show(CharacterMotorState state)
        {
            transform.position = NetV2Math.ToUnity(state.Position);
            var eyeY = _geometry.Shape(state.Stance).EyeY;
            if (!Mathf.Approximately(_eye.localPosition.y, eyeY))
            {
                _eye.localPosition = new Vector3(0f, eyeY, 0f);
            }
        }

        /// <summary>
        /// 次の tick の入力を作る。移動は X が右・Y が前、yaw は Y 軸まわりのラジアン。
        /// grounded は今の予測で接地しているか (空中で押したジャンプでしゃがみを解かないため)。
        /// </summary>
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
            var aim = !paused && !BlocksAds && _aim != null && _aim.IsPressed();
            var degrees = _look != null && _look.enabled && _look.IsInitialized ? _look.Yaw : transform.eulerAngles.y;
            yaw = degrees * Mathf.Deg2Rad;
            buttons = SnapshotMotion.Buttons(sprint, jump, crouch, cover, aim, dodge, prone);
        }

        // 覗き込みを止める撃ち方 (ITriggerModel.BlocksAds) は持っている武器で決まり、武器はサーバーのスナップショットで分かるので外から渡す。
        public bool BlocksAds { get; set; }

        /// <summary>射撃を押したか。読むと忘れる。</summary>
        public bool TakeAttack() => Take(ref _attackLatched);

        /// <summary>射撃を押し続けているか (止めのメニューを開いている間は押していないことにする)。</summary>
        public bool IsAttackHeld => !GameUi.IsPauseOpen && _attack != null && _attack.IsPressed();

        /// <summary>
        /// 視点の yaw (ラジアン) を向きに合わせる。はしごにつかまったとき、オフラインの LadderMovement と同じくはしごへ向けるのに使う。
        /// </summary>
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

        /// <summary>リロードを押したか。読むと忘れる。</summary>
        public bool TakeReload() => Take(ref _reloadLatched);

        /// <summary>グレネードを押したか。読むと忘れる。</summary>
        public bool TakeGrenade() => Take(ref _grenadeLatched);

        /// <summary>印を押したか。読むと忘れる。</summary>
        public bool TakePing() => Take(ref _pingLatched);

        public bool TakeSwitchAmmoType() => Take(ref _switchAmmoTypeLatched);

        public bool TakeSwitchGrenade() => Take(ref _switchGrenadeLatched);

        /// <summary>撃ったときにダッシュを解く (Netcode のクライアントと同じ)。reason はログに出す理由。</summary>
        public void ReleaseSprint(string reason)
        {
            _sprintInput.Release(reason);
        }

        /// <summary>死亡・置き直しのとき、切り替えのしゃがみとダッシュを解く。reason はログに出す理由。</summary>
        public void ReleaseHolds(string reason)
        {
            _crouchInput.Release(reason);
            _sprintInput.Release(reason);
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
