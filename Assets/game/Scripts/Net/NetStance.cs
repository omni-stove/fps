using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// ネット対戦のプレイヤー 1 人の姿勢 (立ち・しゃがみ)。<see cref="StanceGeometry"/> の寸法を CharacterController の height / center、
    /// 目 (<see cref="NetPlayer.ViewName"/>)、見た目 (<see cref="BodyName"/>) に当て、頭上に立ち上がる余地があるかを調べる。
    /// <see cref="NetworkPlayerMotor"/> の予測・サーバー・補間表示と、<see cref="HitboxRewinder"/> の巻き戻しが同じものを使う。
    /// </summary>
    public sealed class NetStance
    {
        /// <summary>しゃがみで縦に縮める見た目 (MeshRenderer) の子 GameObject の名前。本体の直下に置く。</summary>
        public const string BodyName = "Body";

        // 自分のコライダーを除いて頭上の障害物を探すための一時バッファ。毎 tick の確保を避ける。サーバーのメインスレッドでだけ使う
        private const int _maxOverlapHits = 16;

        private static readonly Collider[] _overlapBuffer = new Collider[_maxOverlapHits];

        private readonly CharacterController _controller;
        private readonly Transform _root;
        private readonly Transform _eye;
        private readonly Transform _body;

        // 立ちの center・目・見た目の値。しゃがみでは Y (見た目は Y の位置と大きさ) だけ変え、残りは元のまま保つ
        private readonly Vector3 _standingCenter;
        private readonly Vector3 _standingEyeLocal;
        private readonly Vector3 _standingBodyPosition;
        private readonly Vector3 _standingBodyScale;
        private readonly Quaternion _standingBodyRotation;

        /// <summary>
        /// 今の controller の height / center / radius と目の位置を立ちの寸法として測る。立った状態で呼ぶこと。
        /// </summary>
        /// <param name="controller">プレイヤー本体の CharacterController。</param>
        /// <param name="eye">しゃがみで下げる目。本体の子孫。null なら目は動かさず、目の高さは本体の中心から <see cref="NetPlayer.EyeHeight"/>。</param>
        /// <param name="body">しゃがみで縮める見た目。本体の直下の子。null なら見た目は変えない。</param>
        /// <param name="crouchHeight">しゃがみのカプセルの高さ。<see cref="StanceGeometry"/> の条件に合わなければ ArgumentOutOfRangeException。</param>
        public NetStance(CharacterController controller, Transform eye, Transform body, float crouchHeight)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            _controller = controller;
            _root = controller.transform;

            if (eye != null && (eye == _root || !eye.IsChildOf(_root)))
            {
                throw new ArgumentException("eye must be a descendant of '" + _root.name + "'", nameof(eye));
            }

            if (body != null && body.parent != _root)
            {
                throw new ArgumentException("body must be a direct child of '" + _root.name + "'", nameof(body));
            }

            _eye = eye;
            _body = body;
            _standingCenter = controller.center;
            _standingEyeLocal = eye != null ? _root.InverseTransformPoint(eye.position) : new Vector3(0f, NetPlayer.EyeHeight, 0f);
            _standingBodyPosition = body != null ? body.localPosition : Vector3.zero;
            _standingBodyScale = body != null ? body.localScale : Vector3.one;
            _standingBodyRotation = body != null ? body.localRotation : Quaternion.identity;
            Geometry = new StanceGeometry(controller.height, _standingCenter.y, _standingEyeLocal.y, crouchHeight, controller.radius);
        }

        /// <summary>
        /// controller の GameObject から目 (<see cref="NetPlayer.FindView"/>) と見た目 (<see cref="BodyName"/>) を探して作る。
        /// 見つからないものは動かさない。
        /// </summary>
        public static NetStance FromHierarchy(CharacterController controller, float crouchHeight)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            var root = controller.transform;
            return new NetStance(controller, NetPlayer.FindView(root), root.Find(BodyName), crouchHeight);
        }

        /// <summary>立ちとしゃがみの寸法。</summary>
        public StanceGeometry Geometry { get; }

        /// <summary>今当てている姿勢がしゃがみか。</summary>
        public bool IsCrouched { get; private set; }

        /// <summary>今の姿勢の寸法。</summary>
        public StanceShape Current => Geometry.Shape(IsCrouched);

        /// <summary>しゃがみで下げる目。無ければ null。</summary>
        public Transform Eye => _eye;

        /// <summary>しゃがみで縮める見た目。無ければ null。</summary>
        public Transform Body => _body;

        /// <summary>今の姿勢での目のワールド座標。目の Transform が無くても同じ規則で計算する (サーバーの射撃の検査に使う)。</summary>
        public Vector3 EyePosition => _root.TransformPoint(new Vector3(_standingEyeLocal.x, Current.EyeY, _standingEyeLocal.z));

        /// <summary>
        /// 姿勢の寸法を CharacterController・目・見た目に当てる。足元の高さは変わらない。
        /// 見た目を寝かせている間 (<see cref="SetLying"/>) は、見た目だけ寝かせたままにする。
        /// </summary>
        public void Apply(bool crouched)
        {
            var shape = Geometry.Shape(crouched);
            _controller.height = shape.Height;
            _controller.center = new Vector3(_standingCenter.x, shape.CenterY, _standingCenter.z);

            if (_eye != null)
            {
                _eye.position = _root.TransformPoint(new Vector3(_standingEyeLocal.x, shape.EyeY, _standingEyeLocal.z));
            }

            IsCrouched = crouched;
            ApplyBody();
        }

        /// <summary>見た目 (<see cref="BodyName"/>) を寝かせているか (ダウン中)。</summary>
        public bool IsLying { get; private set; }

        /// <summary>
        /// 見た目だけを寝かせる・起こす。CharacterController と目は姿勢 (<see cref="Apply"/>) のままにする。
        /// 寝かせると、見た目を立ちの大きさのまま前後に倒し、足元の高さに半径ぶん浮かせて置く。
        /// </summary>
        public void SetLying(bool lying)
        {
            if (IsLying == lying)
            {
                return;
            }

            IsLying = lying;
            ApplyBody();
        }

        private void ApplyBody()
        {
            if (_body == null)
            {
                return;
            }

            var standing = Geometry.Standing;
            if (IsLying)
            {
                var feetY = standing.CenterY - (standing.Height / 2f);
                _body.localRotation = _standingBodyRotation * Quaternion.Euler(90f, 0f, 0f);
                _body.localPosition = new Vector3(_standingBodyPosition.x, feetY + Geometry.Radius, _standingBodyPosition.z);
                _body.localScale = _standingBodyScale;
                return;
            }

            var shape = Geometry.Shape(IsCrouched);
            _body.localRotation = _standingBodyRotation;
            _body.localPosition = new Vector3(
                _standingBodyPosition.x, _standingBodyPosition.y + (shape.CenterY - standing.CenterY), _standingBodyPosition.z);
            _body.localScale = new Vector3(
                _standingBodyScale.x, _standingBodyScale.y * (shape.Height / standing.Height), _standingBodyScale.z);
        }

        /// <summary>
        /// 立った高さのカプセルが、自分とプレイヤー (CharacterController) 以外のコライダー (トリガーを除く) に重ならないか。
        /// 床や横の壁に触れているだけで塞がっていると見なさないよう、カプセルを skinWidth だけ細くして調べる。
        /// </summary>
        /// <remarks>
        /// 他のプレイヤーを数えないのは、予測するクライアントでは他人の CharacterController が無効 (補間表示だけ) で、
        /// サーバーと同じ答えにならず、しゃがみが毎回食い違って巻き戻されるため。プレイヤー同士は移動でも衝突させていない。
        /// </remarks>
        public bool HasHeadroomToStand()
        {
            return IsStandingClear(Vector3.zero);
        }

        /// <summary>
        /// 立った高さのカプセルを今の位置から offset (ワールド、m) だけずらした所に置いたとき、<see cref="HasHeadroomToStand"/> と同じ規則で
        /// 何にも重ならないか。乗り越えの軌跡の上を体が通れるかの検査 (<see cref="NetVaultSensor"/>) に使う。
        /// </summary>
        public bool IsStandingClear(Vector3 offset)
        {
            Geometry.SphereCenters(Geometry.Standing, out var bottomY, out var topY);
            var scale = _root.lossyScale;
            var radius = Geometry.Radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            var margin = Mathf.Min(_controller.skinWidth, radius * 0.5f);
            var bottom = _root.TransformPoint(new Vector3(_standingCenter.x, bottomY, _standingCenter.z)) + offset;
            var top = _root.TransformPoint(new Vector3(_standingCenter.x, topY, _standingCenter.z)) + offset;

            var count = Physics.OverlapCapsuleNonAlloc(
                bottom, top, radius - margin, _overlapBuffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var blocked = false;
            for (var i = 0; i < count; i++)
            {
                var hit = _overlapBuffer[i];
                if (hit != null && !hit.transform.IsChildOf(_root) && !(hit is CharacterController))
                {
                    blocked = true;
                    break;
                }
            }

            Array.Clear(_overlapBuffer, 0, count);
            return !blocked;
        }
    }
}
