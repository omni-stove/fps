using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fps.Runtime
{
    /// <summary>
    /// 一人称と三人称のどちらにも使える視点の土台。プレイヤーの本体に付け、次の階層を前提にする:
    /// <code>
    /// 本体 (yaw を回す。ViewRig と FirstPersonLook)
    ///   └ 目 (pitch を回す。射撃の始点)
    ///       └ カメラ (目からのオフセットは ViewRig が決める)
    /// </code>
    /// <see cref="Mode"/> が <see cref="ViewMode.FirstPerson"/> ならカメラを目に重ねて自分の体を影だけにし、
    /// <see cref="ViewMode.ThirdPerson"/> なら目から <see cref="ThirdPersonOffset"/> 離して置き (壁があれば手前まで縮める)、体を描く。
    /// どちらの視点でも狙いは <see cref="ResolveAim"/> で、画面中央が指す点へ目から撃つ (<see cref="AimSolver"/>)。
    /// 参照はコードで解決するので Inspector の配線は要らない: カメラは子階層の <see cref="Camera"/>、目はその親。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ViewRig : MonoBehaviour
    {
        /// <summary>三人称のカメラの、目のローカル座標での既定の位置。右肩の上・後ろ。</summary>
        public static readonly Vector3 DefaultThirdPersonOffset = new Vector3(0.5f, 0.2f, -2.5f);

        /// <summary>カメラが壁にめり込まないよう目から飛ばす球の半径 (m)。</summary>
        public const float DefaultCollisionRadius = 0.2f;

        /// <summary>狙う点を探すカメラの Ray の既定の長さ (m)。武器の射程が分からないとき (ネットの射撃など) に使う。</summary>
        public const float DefaultAimDistance = 1000f;

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
        private ShoulderSide _shoulder = ShoulderSide.Right;

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
        /// 三人称のカメラをどちらの肩越しに置くか。既定は <see cref="ShoulderSide.Right"/>。
        /// Left なら <see cref="ThirdPersonOffset"/> の x を反転して置く。変えると次の <see cref="UpdateCameraPosition"/> からすぐ使う (補間しない)。
        /// </summary>
        public ShoulderSide Shoulder
        {
            get => _shoulder;
            set
            {
                if (value != ShoulderSide.Right && value != ShoulderSide.Left)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "unknown shoulder side");
                }

                _shoulder = value;
            }
        }

        public float CollisionRadius
        {
            get => _collisionRadius;
            set => _collisionRadius = Mathf.Max(0f, value);
        }

        /// <summary>pitch を回し、射撃の始点になる目。未初期化なら null。</summary>
        public Transform Eye => _eye;

        /// <summary>目の子のカメラ。未初期化なら null。</summary>
        public Camera Camera => _camera;

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
            }
        }

        private void OnEnable()
        {
            if (!IsInitialized)
            {
                // Awake で解決に失敗したまま再び有効にされた場合。エラーは解決時に出している。
                enabled = false;
                return;
            }

            ApplyBodyVisibility(true);
            UpdateCameraPosition();
        }

        private void OnDisable()
        {
            ApplyBodyVisibility(false);
        }

        private void LateUpdate()
        {
            // 体の移動と視点の回転 (Update) が済んでから置く。
            UpdateCameraPosition();
        }

        /// <summary>
        /// 子階層のカメラと、その親 (目) を探して初期化する。見つからなければ理由を Debug.LogError に出して false を返す。
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
            return true;
        }

        /// <summary>参照を直接渡して初期化する。eye はこの本体の子孫 (本体自身ではない)、camera は eye の直下であること。</summary>
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
        /// カメラを目からのオフセット (<see cref="Shoulder"/> の側) へ置く。三人称では目からカメラへ球を飛ばし、自分の体以外に当たればその手前まで縮める。
        /// </summary>
        public void UpdateCameraPosition()
        {
            EnsureInitialized();

            var offset = CameraBoom.Offset(_mode, _thirdPersonOffset.ToNumerics(), _shoulder).ToUnity();
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
            var desired = offset.magnitude;
            if (!(desired >= 1e-6f) || float.IsInfinity(desired))
            {
                return origin;
            }

            var direction = offset / desired;
            float? blockedAt = TryNearestCast(origin, direction, desired, out var hitDistance) ? hitDistance : (float?)null;
            var length = CameraBoom.Length(desired, blockedAt, _minBoomLength);
            return origin + (direction * length);
        }

        /// <summary>
        /// 自分の体の描き方を決める。isLocalView (この端末のプレイヤーの視点) かつ一人称なら影だけ描いて視界をふさがず、
        /// それ以外 (三人称や他人のプレイヤー) は普通に描く。
        /// </summary>
        public void ApplyBodyVisibility(bool isLocalView)
        {
            var shadowMode = isLocalView && _mode == ViewMode.FirstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            foreach (var bodyRenderer in GetComponentsInChildren<Renderer>(true))
            {
                if (bodyRenderer != null)
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
            var count = Physics.SphereCastNonAlloc(
                origin, _collisionRadius, direction, _castBuffer, distance, _collisionMask, QueryTriggerInteraction.Ignore);
            var found = TryNearest(count, transform, out hitDistance, out _);
            Array.Clear(_castBuffer, 0, count);
            return found;
        }

        private static bool TryNearestRaycast(
            Vector3 origin, Vector3 direction, float distance, Transform shooterRoot, LayerMask mask, out RaycastHit hit)
        {
            var count = Physics.RaycastNonAlloc(origin, direction, _castBuffer, distance, mask, QueryTriggerInteraction.Ignore);
            var found = TryNearest(count, shooterRoot, out _, out var index);
            hit = found ? _castBuffer[index] : default;
            Array.Clear(_castBuffer, 0, count);
            return found;
        }

        // バッファの先頭 count 件から、ignoreRoot の子孫を除いた最も近い命中を選ぶ。
        private static bool TryNearest(int count, Transform ignoreRoot, out float distance, out int index)
        {
            distance = 0f;
            index = -1;
            for (var i = 0; i < count; i++)
            {
                var candidate = _castBuffer[i];
                if (ignoreRoot != null && candidate.collider.transform.IsChildOf(ignoreRoot))
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
