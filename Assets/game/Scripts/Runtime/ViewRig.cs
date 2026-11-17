using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Shooter.Runtime
{
    // 肩の入れ替えはカメラだけの話なので同期はしない。
    [DisallowMultipleComponent]
    public sealed class ViewRig : MonoBehaviour
    {
        public static readonly Vector3 DefaultThirdPersonOffset = new Vector3(0.5f, 0.2f, -2.5f);

        public const float DefaultCollisionRadius = 0.2f;

        // 触れたままだと次の移動で重なりとみなして除き、壁を抜けてしまうので、壁の手前にこれだけ隙間を残す。
        public const float FreeMoveSkin = 0.05f;

        public const float DefaultAimDistance = 1000f;

        public const string SwapShoulderActionPath = InputActionPaths.SwapShoulder;

        public const string ViewLogTag = AimDownSights.LogTag;

        public const string ShakeRootName = "ShakeRoot";

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
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _swapShoulderAction;
        private bool _enabledSwapShoulderAction;
        private bool _isLocalView;
        private bool _isLive;
        private bool _listeningToSettings;
        private AimDownSights _ads;
        private bool _bodyHiddenBySight;

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

        public Vector3 ThirdPersonOffset
        {
            get => _thirdPersonOffset;
            set => _thirdPersonOffset = value;
        }

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

        public Func<SightView> SightSource { get; set; }

        public bool IsBodyHiddenBySight => _bodyHiddenBySight && isActiveAndEnabled;

        public float ShoulderOffsetSign => _shoulder.OffsetSign;

        public bool IsSwappingShoulder => _shoulder.IsBlending;

        public bool IsLocalView => _isLocalView;

        public InputAction SwapShoulderAction => _swapShoulderAction;

        public float CollisionRadius
        {
            get => _collisionRadius;
            set => _collisionRadius = Mathf.Max(0f, value);
        }

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

        public Transform Eye => _eye;

        public Camera Camera => _camera;

        public Transform ShakeRoot => _shakeRoot;

        // ShakeRoot の子にせず兄弟にするのは、揺れの演出が ShakeRoot を動かしても一人称の武器と腕を動かさないため。
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

        // 同じ本体に初期化済みの AimDownSights があれば、視野角はそちらが腰だめと覗き込みの間で毎フレーム書き、
        // 同じ設定を自分で受け取るので触らない。
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
            // 無効の間は UpdateCameraPosition が呼ばれず戻せないので、ここで隠す指定を外す。
            _bodyHiddenBySight = false;
            ApplyBodyVisibility(false);
        }

        private void LateUpdate()
        {
            // 体の移動と視点の回転 (Update) が済んでから置く。
            var swapPressed = _isLocalView && _swapShoulderAction != null && _swapShoulderAction.WasPressedThisFrame();
            Tick(swapPressed, Time.deltaTime);
        }

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

        public static string FormatShoulderLog(ShoulderSide side)
        {
            return $"{ViewLogTag} shoulder={side}";
        }

        // 肩の入れ替えの入力は無くても視点は使えるので、見つからなくても警告を出すだけで初期化は続ける。
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

        // ViewRig の Awake より先でも決まるよう、初期化済みの値を読まずに自分で探す。
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

        public void UpdateCameraPosition()
        {
            EnsureInitialized();

            var sight = _mode == ViewMode.ThirdPerson ? CurrentSightView() : SightView.None;
            HideBodyForSight(sight.HidesBody);

            var shoulderOffset = _thirdPersonOffset;
            shoulderOffset.x *= _shoulder.OffsetSign;
            var offset = CameraBoom.TowardEye(CameraBoom.Offset(_mode, shoulderOffset.ToNumerics()), sight.EyeBlend).ToUnity();
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

        public Vector3 ResolveBoomPosition(Vector3 origin, Vector3 offset)
        {
            return ResolveBoomPosition(origin, offset, null);
        }

        // ignoreRoot を除けるのは、他のプレイヤーの目からその肩越しへカメラを置くとき (観戦) に、その体で縮まないようにするため。
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

        // 動き始めにもう重なっているもの (壁の中から始めたときなど) では止めないので、重なりからは抜け出せる。
        public Vector3 ResolveFreeMove(Vector3 from, Vector3 delta)
        {
            var position = from;
            var remaining = delta;

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

        // 自分の視点で一人称のときは、視界をふさがないよう影だけ描く。ViewmodelRoot の配下は体ではないので触らない。
        public void ApplyBodyVisibility(bool isLocalView)
        {
            _isLocalView = isLocalView;
            RefreshSwapShoulderAction();

            var hidden = _mode == ViewMode.FirstPerson || _bodyHiddenBySight;
            var shadowMode = isLocalView && hidden ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            foreach (var bodyRenderer in GetComponentsInChildren<Renderer>(true))
            {
                // ビューモデルは体ではなく、一人称でこそ描くものなので、体の描き方に巻き込まない。
                if (bodyRenderer != null && (_viewmodelRoot == null || !bodyRenderer.transform.IsChildOf(_viewmodelRoot)))
                {
                    bodyRenderer.shadowCastingMode = shadowMode;
                }
            }
        }

        private SightView CurrentSightView()
        {
            if (SightSource != null)
            {
                return SightSource();
            }

            // 組み立てる側が AimDownSights を ViewRig より後に付けるので、見つかるまで探し直す。
            if (_ads == null)
            {
                _ads = GetComponent<AimDownSights>();
            }

            return _ads != null && _ads.IsInitialized ? _ads.SightView : SightView.None;
        }

        // 体の描き方を全部の Renderer に書き直すので、隠すかが変わったときだけ直す。
        private void HideBodyForSight(bool hide)
        {
            if (hide == _bodyHiddenBySight)
            {
                return;
            }

            _bodyHiddenBySight = hide;
            ApplyBodyVisibility(_isLocalView);
        }

        public void ResolveAim(float aimDistance, out Vector3 origin, out Vector3 direction)
        {
            EnsureInitialized();
            ResolveAim(_camera.transform, _eye, aimDistance, transform, _collisionMask, out origin, out direction);
        }

        public static void ResolveAim(
            Transform camera,
            Transform eye,
            float aimDistance,
            Transform shooterRoot,
            LayerMask mask,
            out Vector3 origin,
            out Vector3 direction)
        {
            ValidateAimArguments(camera, eye, aimDistance);

            origin = eye.position;
            var forward = camera.forward;
            if ((camera.position - origin).sqrMagnitude < 1e-8f)
            {
                direction = forward;
                return;
            }

            var aimPoint = FindAimPoint(camera, origin, aimDistance, shooterRoot, mask);
            direction = AimSolver.DirectionFromEye(origin.ToNumerics(), aimPoint.ToNumerics(), forward.ToNumerics()).ToUnity();
        }

        public bool TryFindAimBlock(float aimDistance, float margin, out Vector3 blockPoint)
        {
            EnsureInitialized();
            return TryFindAimBlock(_camera.transform, _eye, aimDistance, transform, _collisionMask, margin, out blockPoint);
        }

        public static bool TryFindAimBlock(
            Transform camera,
            Transform eye,
            float aimDistance,
            Transform shooterRoot,
            LayerMask mask,
            float margin,
            out Vector3 blockPoint)
        {
            ValidateAimArguments(camera, eye, aimDistance);
            if (!(margin >= 0f) || float.IsInfinity(margin))
            {
                throw new ArgumentOutOfRangeException(nameof(margin), margin, "margin must be a non-negative finite number");
            }

            var origin = eye.position;
            blockPoint = origin;
            var aimPoint = FindAimPoint(camera, origin, aimDistance, shooterRoot, mask);
            var toAim = aimPoint - origin;
            var length = toAim.magnitude;
            if (length <= margin)
            {
                return false;
            }

            if (!TryNearestRaycast(origin, toAim / length, length, shooterRoot, mask, out var hit)
                || !AimSolver.IsBlocked(origin.ToNumerics(), aimPoint.ToNumerics(), hit.distance, margin))
            {
                return false;
            }

            blockPoint = hit.point;
            return true;
        }

        private static void ValidateAimArguments(Transform camera, Transform eye, float aimDistance)
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
        }

        private static Vector3 FindAimPoint(Transform camera, Vector3 eye, float aimDistance, Transform shooterRoot, LayerMask mask)
        {
            var cameraPosition = camera.position;
            var forward = camera.forward;
            var start = AimSolver.RayStart(cameraPosition.ToNumerics(), forward.ToNumerics(), eye.ToNumerics());
            var rayOrigin = cameraPosition + (forward * start);
            return TryNearestRaycast(rayOrigin, forward, aimDistance, shooterRoot, mask, out var hit)
                ? hit.point
                : rayOrigin + (forward * aimDistance);
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
