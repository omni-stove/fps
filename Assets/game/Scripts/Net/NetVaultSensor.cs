using System;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// ネット対戦の乗り越えの障害物を物理で調べる。オフラインの <see cref="PlayerController.ProbeVault"/> と同じ Ray の当て方
    /// (足元からの高さ・上端と奥行きの測り方・軌跡の上に立ったカプセルの検査) で <see cref="VaultObstacleSample"/> を作り、
    /// <see cref="VaultProbe.Evaluate"/> に判定させる。
    /// </summary>
    /// <remarks>
    /// 向きは本体の Transform ではなく入力の yaw を使う (サーバーは tick を進めた後で本体を回すので、Transform の向きは 1 tick 古い)。
    /// 自分とプレイヤー (<see cref="NetworkPlayerMotor"/> を持つもの・CharacterController) には当てない。
    /// 予測するクライアントには他人の確定位置が無く、当てるとサーバーと毎回食い違うため (<see cref="NetCoverSensor"/> と同じ理由)。
    /// </remarks>
    public sealed class NetVaultSensor
    {
        private const int _maxCastHits = 16;

        // サーバーとクライアントのメインスレッドでだけ使う
        private static readonly RaycastHit[] _castBuffer = new RaycastHit[_maxCastHits];

        private readonly NetCoverSensor _body;
        private readonly NetStance _stance;
        private readonly Transform _root;

        /// <param name="controller">プレイヤー本体。自分のコライダーを除くのに使う。</param>
        /// <param name="body">足元の高さと体の半径を測る部品 (カバーと共用)。</param>
        /// <param name="stance">軌跡の上に立ったカプセルを置いて調べる姿勢。</param>
        /// <param name="mask">障害物として当たるレイヤー。</param>
        public NetVaultSensor(CharacterController controller, NetCoverSensor body, NetStance stance, LayerMask mask)
        {
            _root = controller != null ? controller.transform : throw new ArgumentNullException(nameof(controller));
            _body = body ?? throw new ArgumentNullException(nameof(body));
            _stance = stance ?? throw new ArgumentNullException(nameof(stance));
            Mask = mask;
        }

        public LayerMask Mask { get; }

        /// <summary>
        /// 本体が position にあり、yaw (ラジアン) を向いているときに前方の障害物を調べる。maxHeight は <see cref="MotorSpec.VaultMaxHeight"/>。
        /// 本体は position に置かれていること (軌跡の検査は本体の今の位置からずらしたカプセルで行う)。
        /// </summary>
        public VaultProbeResult Probe(Vector3 position, float yaw, float maxHeight)
        {
            var forward = NetMotorSimulation.YawForward(yaw);
            if (forward.sqrMagnitude < 1e-6f)
            {
                return VaultProbeResult.None;
            }

            forward.Normalize();
            var feetY = _body.FeetY(position);
            var origin = new Vector3(position.x, feetY + PlayerController.VaultProbeHeight, position.z);
            if (!TryCast(origin, forward, VaultProbe.MaxReach, out var hit))
            {
                return VaultProbeResult.None;
            }

            var into = -Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
            var topHeight = MeasureTop(hit, into, feetY, maxHeight);
            var depth = into == Vector3.zero ? float.PositiveInfinity : MeasureDepth(hit, into);
            var sample = new VaultObstacleSample(
                hit.distance * Vector3.Dot(forward, into), NetMath.ToNumerics(hit.normal), topHeight, depth);
            return VaultProbe.Evaluate(sample, NetMath.ToNumerics(forward), maxHeight, _body.BodyRadius, IsPathClear);
        }

        // 当たったコライダーだけに上から Ray を下ろして上端を測る。始点が中 (それより高い) なら当たらないので、始点の高さ (上限より高い) とする
        private static float MeasureTop(RaycastHit hit, Vector3 into, float feetY, float maxHeight)
        {
            var startHeight = maxHeight + PlayerController.VaultTopProbeMargin;
            var inset = hit.point + (into * PlayerController.VaultSurfaceInset);
            var ray = new Ray(new Vector3(inset.x, feetY + startHeight, inset.z), Vector3.down);
            return hit.collider.Raycast(ray, out var top, startHeight) ? Mathf.Max(0f, top.point.y - feetY) : startHeight;
        }

        // 手前の面から MaxDepth より先から当たったコライダーへ戻る Ray で向こうの面を探す。始点が中 (もっと厚い) なら当たらないので無限大とする
        private static float MeasureDepth(RaycastHit hit, Vector3 into)
        {
            var reach = VaultProbe.MaxDepth + PlayerController.VaultDepthSlack;
            var ray = new Ray(hit.point + (into * reach), -into);
            return hit.collider.Raycast(ray, out var back, reach) ? Mathf.Max(0f, reach - back.distance) : float.PositiveInfinity;
        }

        // 軌跡の頂点と各区間の中点に立った体を置き、どこも重ならなければ通れるとする (PlayerController と同じ粗さ)
        private bool IsPathClear(VaultPath path)
        {
            var points = path.Points;
            for (var i = 1; i < points.Count; i++)
            {
                var from = NetMath.ToUnity(points[i - 1]);
                var to = NetMath.ToUnity(points[i]);
                if (!_stance.IsStandingClear((from + to) * 0.5f) || !_stance.IsStandingClear(to))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryCast(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            var count = Physics.RaycastNonAlloc(origin, direction, _castBuffer, distance, Mask, QueryTriggerInteraction.Ignore);
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
