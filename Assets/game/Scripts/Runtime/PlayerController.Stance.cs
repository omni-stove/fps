using System;
using System.Globalization;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="PlayerController"/> のうち、立ち・しゃがみ・伏せの姿勢の処理。
    /// Crouch を <see cref="ControlSettings"/> のモード (押している間／押すたびに切り替え) で <see cref="CrouchInput"/> が解釈し、
    /// ジャンプとダッシュの開始で解除する (頭上が塞がっていれば解除しない)。死亡・リスポーンでの解除は <see cref="ReleaseCrouch"/> を呼ぶ。
    /// 伏せたいか (.Prone.cs の <see cref="IsProneActive"/>) はしゃがみより優先する。
    /// 低い姿勢になると CharacterController の height と center を縮め (足元は動かさない)、目も下げる。カプセルはそのまま当たり判定 (<see cref="Hitbox"/>) なので、
    /// 当たり判定も同じ高さになる。高い姿勢へ戻るときは、その高さのカプセルが自分以外のコライダーに重ならないときだけ戻り、
    /// 伏せから立てなければしゃがみまでは起き上がる。姿勢が変わるたびに <see cref="FormatStanceLog"/> の 1 行を出す。
    /// </summary>
    public sealed partial class PlayerController
    {
        // 自分のコライダーを除いて頭上の障害物を探すための一時バッファ。毎フレームの確保を避ける。
        private const int MaxOverlapHits = 16;

        private static readonly Collider[] _overlapBuffer = new Collider[MaxOverlapHits];

        private StanceGeometry _stance;
        private Transform _eye;

        // 立ちの目の位置 (本体のローカル座標)。低い姿勢では Y だけ変える。
        private Vector3 _standingEyeLocal;

        // 立ちの center。低い姿勢では Y だけ変え、X と Z は元のまま保つ。
        private Vector3 _standingCenter;
        private PlayerStance _currentStance = PlayerStance.Standing;
        private InputDeviceKind _crouchDevice = InputDeviceKind.KeyboardMouse;
        private CrouchInput _crouchInput;

        /// <summary>立ち・しゃがみ・伏せの寸法。未初期化なら null。</summary>
        public StanceGeometry Stance => _stance;

        /// <summary>低い姿勢で上下させる目。目もカメラも無ければ null (カプセルだけ縮める)。</summary>
        public Transform Eye => _eye;

        /// <summary>今の実際の姿勢 (頭上が塞がって戻れないときも含む)。</summary>
        public PlayerStance CurrentStance => _currentStance;

        /// <summary>しゃがんでいるか。伏せているときは false。</summary>
        public bool IsCrouched => _currentStance == PlayerStance.Crouching;

        /// <summary>伏せているか (実際の姿勢)。</summary>
        public bool IsProne => _currentStance == PlayerStance.Prone;

        /// <summary>今の当たり判定 (CharacterController のカプセル) のワールドでの高さ (m)。未初期化なら 0。</summary>
        public float HitboxHeight => _controller != null ? _controller.height * Mathf.Abs(transform.lossyScale.y) : 0f;

        /// <summary>最後にしゃがみか移動の入力をした機器。しゃがみのモードはこの機器の設定を使う。</summary>
        public InputDeviceKind CrouchDevice => _crouchDevice;

        /// <summary>
        /// しゃがみたいか (ボタンをモードどおりに解釈し、自動解除した後の値)。
        /// 頭上が塞がっていると、false でも <see cref="IsCrouched"/> は true のことがある。
        /// </summary>
        public bool IsCrouchActive => Crouch.IsActive;

        /// <summary>今使っているしゃがみのモード。</summary>
        public HoldMode CrouchMode => Crouch.Mode;

        private CrouchInput Crouch
        {
            get
            {
                // Edit Mode では Awake が走らないので、初めて触れたときに作る。
                if (_crouchInput == null)
                {
                    _crouchInput = new CrouchInput(CanStandForCrouchInput);
                    _crouchInput.Changed += OnCrouchInputChanged;
                }

                return _crouchInput;
            }
        }

        /// <summary>
        /// 1 フレーム分のしゃがみボタンを device の <see cref="ControlSettings"/> のモードで解釈し、しゃがみたいかを返す。
        /// しゃがんでいる間にジャンプするかダッシュを始めると、頭上が空いていれば解除する (<see cref="CrouchInput"/>)。
        /// jumpPressed は実際に跳ぶジャンプ (接地中の押下) だけを渡す。
        /// 変化は <c>[Player] crouch=..</c> でログに出す。結果は <see cref="Tick"/> の crouch に渡す。
        /// </summary>
        public bool UpdateCrouch(bool isPressed, bool wasPressedThisFrame, bool jumpPressed, bool sprinting, InputDeviceKind device)
        {
            var mode = _controlSettings.Get(device, HoldAction.Crouch);
            _crouchDevice = device;
            return Crouch.Update(mode, isPressed, wasPressedThisFrame, jumpPressed, sprinting);
        }

        /// <summary>
        /// しゃがみを解除する (死亡・リスポーンなど)。頭上は確かめないが、実際に立つのは次の <see cref="Tick"/> で頭上が空いているときだけ。
        /// Hold でもボタンを押し直すまでしゃがまない。しゃがみたい状態だったら true。
        /// </summary>
        /// <param name="reason">ログに出す理由 (<see cref="CrouchInput.ReasonDeath"/> など)。</param>
        public bool ReleaseCrouch(string reason)
        {
            return Crouch.Release(reason);
        }

        // 未初期化では頭上を調べられないので、立てるものとして解除させる (姿勢を当てる Tick も動かない)。
        private bool CanStandForCrouchInput()
        {
            return !IsInitialized || HasHeadroomToStand();
        }

        private void OnCrouchInputChanged(CrouchInput input, string reason)
        {
            Debug.Log(CrouchInput.FormatLog(input.IsActive, input.Mode, reason), this);
        }

        /// <summary>
        /// 立った高さのカプセルが自分以外のコライダー (トリガーを除く) に重ならないか。
        /// 床や横の壁に触れているだけで塞がっていると見なさないよう、カプセルを CharacterController の skinWidth だけ細くして調べる。
        /// </summary>
        public bool HasHeadroomToStand()
        {
            return HasHeadroomFor(PlayerStance.Standing);
        }

        /// <summary>
        /// stance の高さのカプセルが、今の位置で自分以外のコライダー (トリガーを除く) に重ならないか。
        /// <see cref="HasHeadroomToStand"/> と同じ規則で調べる。
        /// </summary>
        /// <exception cref="InvalidOperationException">未初期化。</exception>
        /// <exception cref="ArgumentOutOfRangeException">stance が定義済みの値でない。</exception>
        public bool HasHeadroomFor(PlayerStance stance)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("PlayerController is not initialized");
            }

            return IsCapsuleClear(_stance.Shape(stance), Vector3.zero);
        }

        // 立った高さのカプセルを今の位置から offset (ワールド) だけずらした所に置き、自分以外に重ならないか (乗り越えの軌跡の検査と共用)。
        private bool IsStandingCapsuleClear(Vector3 offset)
        {
            return IsCapsuleClear(_stance.Standing, offset);
        }

        private bool IsCapsuleClear(StanceShape shape, Vector3 offset)
        {
            _stance.SphereCenters(shape, out var bottomY, out var topY);
            var body = transform;
            var scale = body.lossyScale;
            var radius = _stance.Radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            var margin = Mathf.Min(_controller.skinWidth, radius * 0.5f);
            var bottom = body.TransformPoint(new Vector3(_standingCenter.x, bottomY, _standingCenter.z)) + offset;
            var top = body.TransformPoint(new Vector3(_standingCenter.x, topY, _standingCenter.z)) + offset;

            // Physics.CheckCapsule では自分の CharacterController にも当たってしまうので、重なりを列挙して自分を除く。
            var count = Physics.OverlapCapsuleNonAlloc(
                bottom, top, radius - margin, _overlapBuffer, _headroomMask, QueryTriggerInteraction.Ignore);
            var blocked = false;
            for (var i = 0; i < count; i++)
            {
                var hit = _overlapBuffer[i];
                if (hit != null && !hit.transform.IsChildOf(body))
                {
                    blocked = true;
                    break;
                }
            }

            Array.Clear(_overlapBuffer, 0, count);
            return !blocked;
        }

        /// <summary>しゃがみ状態のログ。例: <c>[Player] crouched=true</c>。</summary>
        public static string FormatCrouchedLog(bool crouched)
        {
            return $"{LogTag} crouched={(crouched ? "true" : "false")}";
        }

        /// <summary>姿勢が変わったときのログ。例: <c>[Player] stance=Prone hitboxHeight=1.00</c>。</summary>
        /// <param name="stance">変わった後の姿勢。</param>
        /// <param name="hitboxHeight">変わった後の当たり判定のワールドでの高さ (m)。</param>
        public static string FormatStanceLog(PlayerStance stance, float hitboxHeight)
        {
            return $"{LogTag} stance={stance} hitboxHeight={hitboxHeight.ToString("F2", CultureInfo.InvariantCulture)}";
        }

        // 低い姿勢へはすぐ移る。高い姿勢へは、望む高さから順に今の高さの 1 つ上まで、頭上が空いている最も高いものへ戻る。
        private void UpdateStance(bool wantsCrouch, bool wantsProne)
        {
            var target = wantsProne ? PlayerStance.Prone : wantsCrouch ? PlayerStance.Crouching : PlayerStance.Standing;
            if (target == _currentStance)
            {
                return;
            }

            if (target > _currentStance)
            {
                ApplyStance(target);
                return;
            }

            for (var stance = target; stance < _currentStance; stance++)
            {
                if (HasHeadroomFor(stance))
                {
                    ApplyStance(stance);
                    return;
                }
            }
        }

        private void ApplyStance(PlayerStance stance)
        {
            var shape = _stance.Shape(stance);
            _controller.height = shape.Height;
            _controller.center = new Vector3(_standingCenter.x, shape.CenterY, _standingCenter.z);
            if (_eye != null)
            {
                _eye.position = transform.TransformPoint(new Vector3(_standingEyeLocal.x, shape.EyeY, _standingEyeLocal.z));
            }

            if (_currentStance == stance)
            {
                return;
            }

            var wasCrouched = IsCrouched;
            _currentStance = stance;
            ApplyProneLook();
            if (wasCrouched != IsCrouched)
            {
                Debug.Log(FormatCrouchedLog(IsCrouched), this);
            }

            Debug.Log(FormatStanceLog(stance, HitboxHeight), this);
        }

        // ViewRig の Awake より先でも決まるよう、ViewRig.FindEye も自分で階層を探す。
        private Transform ResolveEye()
        {
            var eye = ViewRig.FindEye(transform);
            if (eye != null)
            {
                return eye;
            }

            var camera = FirstPersonLook.FindChildCamera(transform);
            return camera != null ? camera.transform : null;
        }
    }
}
