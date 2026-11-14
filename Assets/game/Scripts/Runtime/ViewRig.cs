using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Fps.Runtime
{
    /// <summary>
    /// 一人称と三人称のどちらにも使える視点の土台。プレイヤーの本体に付け、次の階層を前提にする:
    /// <code>
    /// 本体 (yaw を回す。ViewRig と FirstPersonLook)
    ///   └ 目 (pitch を回す。射撃の始点)
    ///       └ カメラ (目からのオフセットは ViewRig が決める)
    ///           ├ ShakeRoot (画面の揺れの演出が使う。<see cref="ShakeRoot"/>)
    ///           └ ViewmodelRoot (一人称の武器と腕を置く。<see cref="ViewmodelRoot"/>)
    /// </code>
    /// ShakeRoot と ViewmodelRoot は初期化 (<see cref="Initialize"/>) のときに名前で探し、無ければ作る。どちらも ViewRig は動かさない。
    /// <see cref="Mode"/> が <see cref="ViewMode.FirstPerson"/> ならカメラを目に重ねて自分の体を影だけにし、
    /// <see cref="ViewMode.ThirdPerson"/> なら目から <see cref="ThirdPersonOffset"/> 離して置き (壁があれば手前まで縮める)、体を描く。
    /// どちらの視点でも狙いは <see cref="ResolveAim"/> で、画面中央が指す点へ目から撃つ (<see cref="AimSolver"/>)。
    /// 三人称では、この端末のプレイヤーの視点 (<see cref="IsLocalView"/>) の間だけ <see cref="SwapShoulderActionPath"/> を読み、
    /// 押すたびにカメラを反対の肩へ <see cref="ShoulderBlend.DefaultSeconds"/> 秒で移す。カメラだけの話なので同期はしない。
    /// 参照はコードで解決するので Inspector の配線は要らない: カメラは子階層の <see cref="Camera"/>、目はその親、入力はプロジェクト全体のアクション。
    /// カメラの視野角は操作設定 (<see cref="ControlSettings.FieldOfView"/>) にし、設定画面で変えるとその場で直す (<see cref="ApplyControlSettings"/>)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ViewRig : MonoBehaviour
    {
        /// <summary>三人称のカメラの、目のローカル座標での既定の位置。右肩の上・後ろ。</summary>
        public static readonly Vector3 DefaultThirdPersonOffset = new Vector3(0.5f, 0.2f, -2.5f);

        /// <summary>カメラが壁にめり込まないよう目から飛ばす球の半径 (m)。</summary>
        public const float DefaultCollisionRadius = 0.2f;

        /// <summary><see cref="ResolveFreeMove"/> で、壁に当たる手前に残す隙間 (m)。触れたままだと次の移動で重なりとみなして除き、壁を抜けてしまうため。</summary>
        public const float FreeMoveSkin = 0.05f;

        /// <summary>狙う点を探すカメラの Ray の既定の長さ (m)。武器の射程が分からないとき (ネットの射撃など) に使う。</summary>
        public const float DefaultAimDistance = 1000f;

        /// <summary>肩を入れ替える入力のパス (プロジェクト全体の Input Actions)。</summary>
        public const string SwapShoulderActionPath = InputActionPaths.SwapShoulder;

        /// <summary>視点の状態のログに付けるタグ。<see cref="AimDownSights.LogTag"/> と揃える。</summary>
        public const string ViewLogTag = AimDownSights.LogTag;

        /// <summary>画面の揺れ用にカメラの直下に置く GameObject の名前。</summary>
        public const string ShakeRootName = "ShakeRoot";

        /// <summary>ビューモデル用にカメラの直下に置く GameObject の名前。</summary>
        public const string ViewmodelRootName = "ViewmodelRoot";

        private const string LogTag = "[ViewRig]";

        // 自分の体を除いて最も近い命中を選ぶための一時バッファ。毎フレームの確保を避ける。
        private const int MaxCastHits = 16;

        private static readonly RaycastHit[] _castBuffer = new RaycastHit[MaxCastHits];

        [SerializeField, Tooltip("一人称か三人称か")]
        private ViewMode _mode = ViewMode.FirstPerson;

        [SerializeField, Tooltip("三人称のカメラの位置 (目のローカル座標、m)")]
        private Vector3 _thirdPersonOffset = DefaultThirdPersonOffset;

        [SerializeField, Min(0f), Tooltip("カメラの壁抜け防止に飛ばす球の半径 (m)")]
        private float _collisionRadius = DefaultCollisionRadius;

        [SerializeField, Min(0f), Tooltip("壁に寄ったときも目からこれ以上は近づけない距離 (m)")]
        private float _minBoomLength = CameraBoom.DefaultMinLength;

        [SerializeField, Tooltip("カメラの壁抜け防止と狙う点の判定で当たるレイヤー")]
        private LayerMask _collisionMask = Physics.DefaultRaycastLayers;

        private Transform _eye;
        private Camera _camera;
        private Transform _shakeRoot;
        private Transform _viewmodelRoot;
        private readonly ShoulderBlend _shoulder = new ShoulderBlend();
        private InputAction _swapShoulderAction;
        private bool _enabledSwapShoulderAction;
        private bool _isLocalView;
        private bool _isLive;
        private bool _listeningToSettings;

        /// <summary>
        /// 一人称か三人称か。変えるとカメラの位置と体の描き方をすぐ直す (有効な間だけ。無効なら次に有効になったとき)。
        /// </summary>
        public ViewMode Mode
        {
            get => _mode;
            set
            {
                if (value != ViewMode.FirstPerson && value != ViewMode.ThirdPerson)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "unknown view mode");
                }

                _mode = value;
                if (isActiveAndEnabled && IsInitialized)
                {
                    ApplyBodyVisibility(true);
                    UpdateCameraPosition();
                }
            }
        }

        /// <summary>三人称のカメラの位置 (目のローカル座標、m)。</summary>
        public Vector3 ThirdPersonOffset
        {
            get => _thirdPersonOffset;
            set => _thirdPersonOffset = value;
        }

        /// <summary>
        /// 三人称のカメラをどちらの肩越しに置くか (補間の目標)。既定は <see cref="ShoulderSide.Right"/>。
        /// Left なら <see cref="ThirdPersonOffset"/> の x を反転した位置へ向かう。変えると <see cref="Tick"/> で
        /// <see cref="ShoulderBlend.DefaultSeconds"/> 秒かけて移り、<c>[View] shoulder=Left</c> のようにログに出す。
        /// </summary>
        public ShoulderSide Shoulder
        {
            get => _shoulder.Target;
            set
            {
                if (_shoulder.SetTarget(value))
                {
                    ReportShoulder();
                }
            }
        }

        /// <summary>今 <see cref="ThirdPersonOffset"/> の x に掛けている符号。右肩で +1、左肩で -1、入れ替えの途中はその間。</summary>
        public float ShoulderOffsetSign => _shoulder.OffsetSign;

        /// <summary>肩を入れ替えている途中か。</summary>
        public bool IsSwappingShoulder => _shoulder.IsBlending;

        /// <summary>
        /// この端末のプレイヤーの視点か。最後に <see cref="ApplyBodyVisibility"/> へ渡した値で、true の間だけ肩の入れ替えの入力を読む。
        /// </summary>
        public bool IsLocalView => _isLocalView;

        /// <summary>肩を入れ替える入力。見つからなかったか未設定なら null (肩は入れ替わらないが、視点はそのまま使える)。</summary>
        public InputAction SwapShoulderAction => _swapShoulderAction;

        public float CollisionRadius
        {
            get => _collisionRadius;
            set => _collisionRadius = Mathf.Max(0f, value);
        }

        /// <summary>三人称のカメラが壁に寄ったときも、目からこれ以上は近づけない距離 (m)。0 以上の有限値。</summary>
        /// <exception cref="ArgumentOutOfRangeException">負か有限でないとき。</exception>
        public float MinBoomLength
        {
            get => _minBoomLength;
            set
            {
                if (!(value >= 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "MinBoomLength must be a non-negative finite number");
                }

                _minBoomLength = value;
            }
        }

        /// <summary>pitch を回し、射撃の始点になる目。未初期化なら null。</summary>
        public Transform Eye => _eye;

        /// <summary>目の子のカメラ。未初期化なら null。</summary>
        public Camera Camera => _camera;

        /// <summary>カメラの直下の、画面の揺れの演出が使う Transform (<see cref="ShakeRootName"/>)。未初期化なら null。</summary>
        public Transform ShakeRoot => _shakeRoot;

        /// <summary>
        /// カメラの直下の、一人称の武器と腕を置く Transform (<see cref="ViewmodelRootName"/>)。<see cref="ShakeRoot"/> とは兄弟で、
        /// 揺れの演出が ShakeRoot を動かしてもこちらは動かない。未初期化なら null。
        /// </summary>
        public Transform ViewmodelRoot => _viewmodelRoot;

        public bool IsInitialized => _eye != null && _camera != null;

        private void Awake()
        {
            // 専用サーバーには画面が無く、カメラを置く意味が無い。狙いの計算 (ResolveAim) は無効でも使える。
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                enabled = false;
                return;
            }

            if (!IsInitialized && !TryInitializeFromHierarchy())
            {
                enabled = false;
                return;
            }

            AppliedControlSettings.Changed += ApplyControlSettings;
            _listeningToSettings = true;
            ApplyControlSettings(AppliedControlSettings.Current);
        }

        private void OnDestroy()
        {
            if (_listeningToSettings)
            {
                AppliedControlSettings.Changed -= ApplyControlSettings;
                _listeningToSettings = false;
            }
        }

        /// <summary>
        /// カメラの視野角を操作設定の <see cref="ControlSettings.FieldOfView"/> にする。Awake と、設定画面で変わったときに呼ばれる。
        /// 同じ本体に初期化済みの <see cref="AimDownSights"/> があれば、視野角はそちらが腰だめと覗き込みの間で毎フレーム書くので触らない
        /// (AimDownSights が同じ設定を自分で受け取る)。未初期化なら何もしない。
        /// </summary>
        public void ApplyControlSettings(ControlSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!IsInitialized)
            {
                return;
            }

            var ads = GetComponent<AimDownSights>();
            if (ads != null && ads.IsInitialized)
            {
                return;
            }

            _camera.fieldOfView = settings.FieldOfView;
        }

        private void OnEnable()
        {
            if (!IsInitialized)
            {
                // Awake で解決に失敗したまま再び有効にされた場合。エラーは解決時に出している。
                enabled = false;
                return;
            }

            _isLive = true;
            ApplyBodyVisibility(true);
            UpdateCameraPosition();
        }

        private void OnDisable()
        {
            _isLive = false;
            ApplyBodyVisibility(false);
        }

        private void LateUpdate()
        {
            // 体の移動と視点の回転 (Update) が済んでから置く。
            var swapPressed = _isLocalView && _swapShoulderAction != null && _swapShoulderAction.WasPressedThisFrame();
            Tick(swapPressed, Time.deltaTime);
        }

        /// <summary>
        /// 1 フレーム分進める。swapPressedThisFrame が true で、三人称かつこの端末のプレイヤーの視点 (<see cref="IsLocalView"/>) なら
        /// 肩を入れ替える。それから肩の補間を deltaSeconds だけ進め、カメラを置き直す (<see cref="UpdateCameraPosition"/>)。
        /// </summary>
        /// <param name="swapPressedThisFrame">今フレームに肩の入れ替えが押されたか。</param>
        /// <param name="deltaSeconds">進める秒数。0 以上の有限値。</param>
        public void Tick(bool swapPressedThisFrame, float deltaSeconds)
        {
            EnsureInitialized();
            // 入れ替えてから秒数の不正で投げると、肩だけ変わってカメラが置き直されない。
            ShoulderBlend.ValidateDeltaSeconds(deltaSeconds);

            if (swapPressedThisFrame && _isLocalView && _mode == ViewMode.ThirdPerson)
            {
                _shoulder.Toggle();
                ReportShoulder();
            }

            _shoulder.Tick(deltaSeconds);
            UpdateCameraPosition();
        }

        /// <summary>
        /// 肩の入れ替えの入力を差し替える。null なら入力を読まない。
        /// 動作中なら、自分が有効にした古いアクションを戻してから、この端末の視点のときだけ新しいものを有効にする。
        /// </summary>
        public void SetSwapShoulderAction(InputAction action)
        {
            if (action == _swapShoulderAction)
            {
                return;
            }

            ReleaseSwapShoulderAction();
            _swapShoulderAction = action;
            RefreshSwapShoulderAction();
        }

        /// <summary>肩を入れ替えたときのログ。例: <c>[View] shoulder=Left</c>。</summary>
        public static string FormatShoulderLog(ShoulderSide side)
        {
            return $"{ViewLogTag} shoulder={side}";
        }

        /// <summary>
        /// 子階層のカメラと、その親 (目) を探して初期化する。見つからなければ理由を Debug.LogError に出して false を返す。
        /// 肩の入れ替えの入力 (<see cref="SwapShoulderActionPath"/>) もプロジェクト全体のアクションから探す。
        /// こちらは無くても視点は使えるので、警告を出すだけで初期化は続ける。
        /// </summary>
        public bool TryInitializeFromHierarchy()
        {
            var camera = FirstPersonLook.FindChildCamera(transform);
            if (camera == null)
            {
                Debug.LogError($"{LogTag} '{name}' の子に Camera がありません。本体 > 目 > カメラ の順に置いてください。", this);
                return false;
            }

            var eye = camera.transform.parent;
            if (!IsEyeOf(eye, transform))
            {
                Debug.LogError(
                    $"{LogTag} '{name}' のカメラ '{camera.name}' が本体の直下にあります。本体 > 目 > カメラ の順に置いてください。",
                    this);
                return false;
            }

            Initialize(eye, camera);
            SetSwapShoulderAction(FindSwapShoulderAction());
            return true;
        }

        /// <summary>
        /// 参照を直接渡して初期化する。eye はこの本体の子孫 (本体自身ではない)、camera は eye の直下であること。
        /// カメラの直下の <see cref="ShakeRoot"/> と <see cref="ViewmodelRoot"/> は名前で探し、無ければ作る (<see cref="EnsureCameraChild"/>)。
        /// </summary>
        public void Initialize(Transform eye, Camera camera)
        {
            if (eye == null)
            {
                throw new ArgumentNullException(nameof(eye));
            }

            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            if (!IsEyeOf(eye, transform))
            {
                throw new ArgumentException($"eye '{eye.name}' must be a descendant of '{name}'", nameof(eye));
            }

            if (camera.transform.parent != eye)
            {
                throw new ArgumentException($"camera '{camera.name}' must be a direct child of the eye '{eye.name}'", nameof(camera));
            }

            _eye = eye;
            _camera = camera;
            _shakeRoot = EnsureCameraChild(camera, ShakeRootName);
            _viewmodelRoot = EnsureCameraChild(camera, ViewmodelRootName);
        }

        /// <summary>
        /// camera の直下で名前が name の Transform を返す。無ければ位置・回転・拡縮を揃えた空の GameObject をカメラと同じレイヤーで作る。
        /// 何度呼んでも同じ名前のものは 1 つだけ。
        /// </summary>
        public static Transform EnsureCameraChild(Camera camera, string name)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("child name must not be empty", nameof(name));
            }

            var parent = camera.transform;
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (string.Equals(child.name, name, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            var created = new GameObject(name) { layer = camera.gameObject.layer }.transform;
            created.SetParent(parent, false);
            created.localPosition = Vector3.zero;
            created.localRotation = Quaternion.identity;
            created.localScale = Vector3.one;
            return created;
        }

        /// <summary>
        /// body に ViewRig があり、その子のカメラが目の下にあれば目を返す。無ければ null。
        /// <see cref="FirstPersonLook"/> が pitch を回す Transform を選ぶのに使う (ViewRig の Awake より先でも決まるよう、自分で探す)。
        /// </summary>
        public static Transform FindEye(Transform body)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            var rig = body.GetComponent<ViewRig>();
            if (rig == null)
            {
                return null;
            }

            if (rig.IsInitialized)
            {
                return rig._eye;
            }

            var camera = FirstPersonLook.FindChildCamera(body);
            var eye = camera != null ? camera.transform.parent : null;
            return IsEyeOf(eye, body) ? eye : null;
        }

        /// <summary>
        /// カメラを目からのオフセット (x に <see cref="ShoulderOffsetSign"/> を掛けたもの) へ置く。
        /// 三人称では目からカメラへ球を飛ばし、自分の体以外に当たればその手前まで縮める (肩の入れ替えの途中でも同じ)。
        /// 肩の補間は進めない (進めるのは <see cref="Tick"/>)。
        /// </summary>
        public void UpdateCameraPosition()
        {
            EnsureInitialized();

            var shoulderOffset = _thirdPersonOffset;
            shoulderOffset.x *= _shoulder.OffsetSign;
            var offset = CameraBoom.Offset(_mode, shoulderOffset.ToNumerics()).ToUnity();
            var cameraTransform = _camera.transform;
            var desired = offset.magnitude;
            if (desired < 1e-6f)
            {
                cameraTransform.localPosition = Vector3.zero;
                return;
            }

            var origin = _eye.position;
            var direction = _eye.TransformPoint(offset) - origin;
            var worldLength = direction.magnitude;
            float? blockedAt = null;
            if (worldLength > 1e-6f && TryNearestCast(origin, direction / worldLength, worldLength, out var hitDistance))
            {
                // ワールドでの距離を目のローカルの長さへ直す (目の拡縮が 1 でなくても合うように)。
                blockedAt = hitDistance * (desired / worldLength);
            }

            var length = CameraBoom.Length(desired, blockedAt, _minBoomLength);
            cameraTransform.localPosition = offset * (length / desired);
        }

        /// <summary>
        /// origin からワールド座標の offset の先へカメラを置くときの位置。間に自分の体以外があれば、三人称のカメラと同じ半径・レイヤーで
        /// 球を飛ばして手前まで縮める (<see cref="CameraBoom.Length"/>)。デスカムのように目からのオフセットでなくカメラを置くときに使う。
        /// 初期化していなくても使える。
        /// </summary>
        public Vector3 ResolveBoomPosition(Vector3 origin, Vector3 offset)
        {
            return ResolveBoomPosition(origin, offset, null);
        }

        /// <summary>
        /// <see cref="ResolveBoomPosition(Vector3, Vector3)"/> の、自分の体に加えて ignoreRoot の子孫にも当たらないもの。
        /// 他のプレイヤーの目からその肩越しへカメラを置くとき (観戦) に、その体で縮まないよう ignoreRoot に渡す。null なら自分の体だけ除く。
        /// </summary>
        public Vector3 ResolveBoomPosition(Vector3 origin, Vector3 offset, Transform ignoreRoot)
        {
            var desired = offset.magnitude;
            if (!(desired >= 1e-6f) || float.IsInfinity(desired))
            {
                return origin;
            }

            var direction = offset / desired;
            float? blockedAt = TryNearestCast(origin, direction, desired, ignoreRoot, false, out var hitDistance, out _)
                ? hitDistance
                : (float?)null;
            var length = CameraBoom.Length(desired, blockedAt, _minBoomLength);
            return origin + (direction * length);
        }

        /// <summary>
        /// 自由に動かすカメラ (観戦) を from から delta だけ動かした先。三人称のカメラと同じ半径・レイヤーの球を飛ばし、
        /// 壁 (自分の体を除く) の <see cref="FreeMoveSkin"/> 手前で止め、残りは当たった面に沿って 1 回だけ滑らせる。
        /// 動き始めにもう重なっているもの (壁の中から始めたときなど) では止めないので、重なりからは抜け出せる。初期化していなくても使える。
        /// </summary>
        public Vector3 ResolveFreeMove(Vector3 from, Vector3 delta)
        {
            var position = from;
            var remaining = delta;

            // 1 回目は進む向き、2 回目は当たった面に沿って滑る分
            for (var i = 0; i < 2; i++)
            {
                var length = remaining.magnitude;
                if (!(length >= 1e-6f) || float.IsInfinity(length))
                {
                    return position;
                }

                var direction = remaining / length;
                if (!TryNearestCast(position, direction, length + FreeMoveSkin, null, true, out var hitDistance, out var normal))
                {
                    return position + remaining;
                }

                var travel = Mathf.Clamp(hitDistance - FreeMoveSkin, 0f, length);
                position += direction * travel;
                remaining = Vector3.ProjectOnPlane(remaining - (direction * travel), normal);
            }

            return position;
        }

        /// <summary>
        /// 自分の体の描き方を決める。isLocalView (この端末のプレイヤーの視点) かつ一人称なら影だけ描いて視界をふさがず、
        /// それ以外 (三人称や他人のプレイヤー) は普通に描く。<see cref="ViewmodelRoot"/> の配下は体ではないので触らない。
        /// isLocalView の間だけ肩の入れ替えの入力を読む (<see cref="IsLocalView"/>)。
        /// </summary>
        public void ApplyBodyVisibility(bool isLocalView)
        {
            _isLocalView = isLocalView;
            RefreshSwapShoulderAction();

            var shadowMode = isLocalView && _mode == ViewMode.FirstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            foreach (var bodyRenderer in GetComponentsInChildren<Renderer>(true))
            {
                // ビューモデルは体ではなく、一人称でこそ描くものなので、体の描き方に巻き込まない。
                if (bodyRenderer != null && (_viewmodelRoot == null || !bodyRenderer.transform.IsChildOf(_viewmodelRoot)))
                {
                    bodyRenderer.shadowCastingMode = shadowMode;
                }
            }
        }

        /// <summary>
        /// 画面中央で狙う射撃の始点と向き。始点は目、向きは目からカメラの正面が指す点へ。
        /// カメラが目に重なっていれば (一人称) カメラの正面をそのまま返す。
        /// </summary>
        /// <param name="aimDistance">狙う点を探す距離 (m)。武器の射程を渡す。0 より大きい有限値。</param>
        /// <param name="origin">射撃の始点 (目の位置)。</param>
        /// <param name="direction">射撃の向き (単位ベクトル)。</param>
        public void ResolveAim(float aimDistance, out Vector3 origin, out Vector3 direction)
        {
            EnsureInitialized();
            ResolveAim(_camera.transform, _eye, aimDistance, transform, _collisionMask, out origin, out direction);
        }

        /// <summary>
        /// <see cref="ResolveAim(float, out Vector3, out Vector3)"/> の本体。camera の正面の Ray をカメラと目の間を飛ばして撃ち、
        /// shooterRoot の子孫とトリガーを除いた最初の命中 (無ければ aimDistance 先) を狙う点にする。
        /// </summary>
        public static void ResolveAim(
            Transform camera,
            Transform eye,
            float aimDistance,
            Transform shooterRoot,
            LayerMask mask,
            out Vector3 origin,
            out Vector3 direction)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            if (eye == null)
            {
                throw new ArgumentNullException(nameof(eye));
            }

            if (!(aimDistance > 0f) || float.IsInfinity(aimDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(aimDistance), aimDistance, "aimDistance must be positive and finite");
            }

            origin = eye.position;
            var cameraPosition = camera.position;
            var forward = camera.forward;
            if ((cameraPosition - origin).sqrMagnitude < 1e-8f)
            {
                direction = forward;
                return;
            }

            var start = AimSolver.RayStart(cameraPosition.ToNumerics(), forward.ToNumerics(), origin.ToNumerics());
            var rayOrigin = cameraPosition + (forward * start);
            var aimPoint = TryNearestRaycast(rayOrigin, forward, aimDistance, shooterRoot, mask, out var hit)
                ? hit.point
                : rayOrigin + (forward * aimDistance);
            direction = AimSolver.DirectionFromEye(origin.ToNumerics(), aimPoint.ToNumerics(), forward.ToNumerics()).ToUnity();
        }

        private bool TryNearestCast(Vector3 origin, Vector3 direction, float distance, out float hitDistance)
        {
            return TryNearestCast(origin, direction, distance, null, false, out hitDistance, out _);
        }

        private bool TryNearestCast(
            Vector3 origin,
            Vector3 direction,
            float distance,
            Transform alsoIgnore,
            bool skipStartOverlaps,
            out float hitDistance,
            out Vector3 normal)
        {
            var count = Physics.SphereCastNonAlloc(
                origin, _collisionRadius, direction, _castBuffer, distance, _collisionMask, QueryTriggerInteraction.Ignore);
            var found = TryNearest(count, transform, alsoIgnore, skipStartOverlaps, out hitDistance, out var index);
            normal = found ? _castBuffer[index].normal : Vector3.zero;
            Array.Clear(_castBuffer, 0, count);
            return found;
        }

        private static bool TryNearestRaycast(
            Vector3 origin, Vector3 direction, float distance, Transform shooterRoot, LayerMask mask, out RaycastHit hit)
        {
            var count = Physics.RaycastNonAlloc(origin, direction, _castBuffer, distance, mask, QueryTriggerInteraction.Ignore);
            var found = TryNearest(count, shooterRoot, null, false, out _, out var index, shooterRoot);
            hit = found ? _castBuffer[index] : default;
            Array.Clear(_castBuffer, 0, count);
            return found;
        }

        // バッファの先頭 count 件から、ignoreRoot の子孫を除いた最も近い命中を選ぶ。
        private static bool TryNearest(int count, Transform ignoreRoot, out float distance, out int index)
        {
            return TryNearest(count, ignoreRoot, null, false, out distance, out index);
        }

        // skipStartOverlaps なら、飛ばし始めに重なっていた命中 (Unity は距離 0・点 0 で返す) も除く。
        private static bool TryNearest(
            int count,
            Transform ignoreRoot,
            Transform alsoIgnore,
            bool skipStartOverlaps,
            out float distance,
            out int index,
            Transform deployShooter = null)
        {
            distance = 0f;
            index = -1;
            for (var i = 0; i < count; i++)
            {
                var candidate = _castBuffer[i];
                var hitTransform = candidate.collider.transform;
                if ((ignoreRoot != null && hitTransform.IsChildOf(ignoreRoot))
                    || (alsoIgnore != null && hitTransform.IsChildOf(alsoIgnore)))
                {
                    continue;
                }

                // 弾が抜ける自分の置いた物を狙う点にすると、抜けた弾が照準の先からずれる。
                if (deployShooter != null && Deployable.LetsThrough(candidate.collider, deployShooter.gameObject))
                {
                    continue;
                }

                if (skipStartOverlaps && candidate.distance <= 0f && candidate.point == Vector3.zero)
                {
                    continue;
                }

                if (index < 0 || candidate.distance < distance)
                {
                    distance = candidate.distance;
                    index = i;
                }
            }

            return index >= 0;
        }

        private InputAction FindSwapShoulderAction()
        {
            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogWarning(
                    $"{LogTag} プロジェクト全体の Input Actions が設定されていません (Project Settings > Input System Package)。肩は入れ替えられません。",
                    this);
                return null;
            }

            var swap = actions.FindAction(SwapShoulderActionPath);
            if (swap == null)
            {
                Debug.LogWarning(
                    $"{LogTag} Input Actions '{actions.name}' に '{SwapShoulderActionPath}' がありません。肩は入れ替えられません。",
                    this);
            }

            return swap;
        }

        private void ReportShoulder()
        {
            Debug.Log(FormatShoulderLog(_shoulder.Target), this);
        }

        private void RefreshSwapShoulderAction()
        {
            if (_isLive && _isLocalView && _swapShoulderAction != null)
            {
                // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。
                if (!_swapShoulderAction.enabled)
                {
                    _swapShoulderAction.Enable();
                    _enabledSwapShoulderAction = true;
                }
            }
            else
            {
                ReleaseSwapShoulderAction();
            }
        }

        private void ReleaseSwapShoulderAction()
        {
            if (_enabledSwapShoulderAction && _swapShoulderAction != null)
            {
                _swapShoulderAction.Disable();
            }

            _enabledSwapShoulderAction = false;
        }

        private static bool IsEyeOf(Transform eye, Transform body)
        {
            return eye != null && eye != body && eye.IsChildOf(body);
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("ViewRig is not initialized");
            }
        }
    }
}
