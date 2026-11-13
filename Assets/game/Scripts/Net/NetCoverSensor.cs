using System;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// ネット対戦のカバーの壁を物理で調べる。オフラインの <see cref="CoverController"/> と同じ Ray の当て方 (足元からの高さ・上端の測り方・端の検査) で
    /// <see cref="CoverWallSample"/> を作り、<see cref="CoverProbe.Evaluate"/> に判定させる。
    /// </summary>
    /// <remarks>
    /// 自分とプレイヤー (<see cref="NetworkPlayerMotor"/> を持つもの・CharacterController) には当てない。
    /// 予測するクライアントには他人の確定位置が無く、当てるとサーバーと毎回食い違うため (<see cref="NetStance.HasHeadroomToStand"/> と同じ理由)。
    /// </remarks>
    public sealed class NetCoverSensor
    {
        // 上端を下ろす Ray で探すとき、当たった面から壁の内側へ入れる距離 (m)。CoverController と同じ値
        private const float _topProbeInset = 0.02f;

        // 端の検査の Ray を、壁までの距離よりこれだけ長く飛ばす (m)。CoverController と同じ値
        private const float _edgeProbeSlack = 0.3f;

        private const int _maxCastHits = 16;

        // サーバーとクライアントのメインスレッドでだけ使う
        private static readonly RaycastHit[] _castBuffer = new RaycastHit[_maxCastHits];

        private readonly CharacterController _controller;
        private readonly Transform _root;

        /// <param name="controller">プレイヤー本体。足元の高さと体の半径をここから測る。</param>
        /// <param name="wallMask">カバーにする壁として当たるレイヤー。</param>
        public NetCoverSensor(CharacterController controller, LayerMask wallMask)
        {
            _controller = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
            _root = controller.transform;
            WallMask = wallMask;
        }

        public LayerMask WallMask { get; }

        /// <summary>壁を探す Ray の、足元からの高さ (m)。</summary>
        public float ProbeHeight => CoverController.DefaultProbeHeight;

        /// <summary>壁の上端を探す Ray の始点の、足元からの高さ (m)。</summary>
        public float TopProbeHeight => CoverController.DefaultTopProbeHeight;

        /// <summary>体の半径 (m、本体の拡大を含む)。</summary>
        public float BodyRadius
        {
            get
            {
                var scale = _root.lossyScale;
                return _controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            }
        }

        /// <summary>本体が position にあるときの足元の高さ (ワールドの y)。しゃがんでも変わらない。</summary>
        public float FeetY(Vector3 position)
        {
            return position.y + ((_controller.center.y - (_controller.height * 0.5f)) * Mathf.Abs(_root.lossyScale.y));
        }

        /// <summary>
        /// 本体が position にあり、direction (水平) を向いているときに正面の壁を調べる。
        /// facing は入れるかの角度の判定に使う向き。カバー中は壁へ向かう方向を両方に渡す。
        /// 壁なら topY に壁の上端の高さ (ワールドの y) を返す。
        /// </summary>
        public CoverProbeResult Probe(Vector3 position, Vector3 direction, Vector3 facing, out float topY)
        {
            topY = 0f;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-12f)
            {
                return CoverProbeResult.None;
            }

            direction.Normalize();
            var feetY = FeetY(position);
            var origin = new Vector3(position.x, feetY + ProbeHeight, position.z);
            if (!TryCast(origin, direction, CoverProbe.MaxEnterDistance, out var hit))
            {
                return CoverProbeResult.None;
            }

            var normal = hit.normal;
            var topHeight = MeasureTopHeight(hit, feetY);
            var flatNormal = new Vector3(normal.x, 0f, normal.z);
            var wallOnLeft = true;
            var wallOnRight = true;
            if (flatNormal.sqrMagnitude > 1e-12f)
            {
                flatNormal.Normalize();
                var right = NetMath.ToUnity(CoverProbe.RightAlong(NetMath.ToNumerics(flatNormal)));
                var reach = hit.distance + _edgeProbeSlack;
                wallOnLeft = TryCast(origin - (right * CoverProbe.EdgeProbeDistance), -flatNormal, reach, out _);
                wallOnRight = TryCast(origin + (right * CoverProbe.EdgeProbeDistance), -flatNormal, reach, out _);
            }

            var sample = new CoverWallSample(hit.distance, NetMath.ToNumerics(normal), topHeight, wallOnLeft, wallOnRight);
            var result = CoverProbe.Evaluate(sample, NetMath.ToNumerics(facing));
            if (result.IsWall)
            {
                topY = feetY + topHeight;
            }

            return result;
        }

        // 当たった壁のコライダーだけに、上から下ろした Ray を当てて上端を測る。始点が壁の中 (それより高い壁) なら当たらないので上限の高さとする
        private float MeasureTopHeight(RaycastHit hit, float feetY)
        {
            var flat = new Vector3(hit.normal.x, 0f, hit.normal.z);
            var inset = hit.point - (flat.sqrMagnitude > 1e-12f ? flat.normalized * _topProbeInset : Vector3.zero);
            var start = new Vector3(inset.x, feetY + TopProbeHeight, inset.z);
            var ray = new Ray(start, Vector3.down);
            return hit.collider.Raycast(ray, out var top, TopProbeHeight) ? top.point.y - feetY : TopProbeHeight;
        }

        private bool TryCast(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            var count = Physics.RaycastNonAlloc(origin, direction, _castBuffer, distance, WallMask, QueryTriggerInteraction.Ignore);
            var found = false;
            nearest = default;
            for (var i = 0; i < count; i++)
            {
                var hit = _castBuffer[i];
                if (hit.collider == null || IsPlayer(hit.collider))
                {
                    continue;
                }

                if (!found || hit.distance < nearest.distance)
                {
                    nearest = hit;
                    found = true;
                }
            }

            Array.Clear(_castBuffer, 0, count);
            return found;
        }

        private bool IsPlayer(Collider collider)
        {
            return collider is CharacterController
                || collider.transform.IsChildOf(_root)
                || collider.GetComponentInParent<NetworkPlayerMotor>() != null;
        }
    }
}
