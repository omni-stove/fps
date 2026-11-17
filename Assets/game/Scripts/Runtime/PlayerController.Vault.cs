using System;
using System.Globalization;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class PlayerController
    {
        public const float VaultProbeHeight = VaultProbe.LowestProbeHeight;

        // 上限を少し超える壁も「高すぎる」と測れるよう、乗り越えられる高さよりこれだけ上から下ろす。ネット対戦 (NetVaultSensor) も同じ値で測る。
        public const float VaultTopProbeMargin = 0.3f;

        // 面のちょうど上を通って外さないよう、当たった面から障害物の内側へずらす。ネット対戦も同じ値で測る。
        public const float VaultSurfaceInset = 0.02f;

        // ちょうど奥行きの上限の厚さも測れるよう、VaultMaxDepth よりこれだけ先から戻る Ray で測る。ネット対戦も同じ値で測る。
        public const float VaultDepthSlack = 0.05f;

        private const int MaxVaultCastHits = 16;

        private static readonly RaycastHit[] _vaultCastBuffer = new RaycastHit[MaxVaultCastHits];

        private VaultProbe.FaceRaycast _vaultFaceRaycast;

        private Vault _vaultRule = Vault.Off;
        private VaultPath _vaultPath;
        private float _vaultElapsed;

        public event Action<Vector3> VaultStarted;

        public event Action VaultEnded;

        public bool IsVaulting => _vaultPath != null;

        public VaultPath VaultPath => _vaultPath;

        public VaultProbeResult LastVaultProbe { get; private set; }

        public Vault VaultRule
        {
            get => _vaultRule;
            set
            {
                if (!Enum.IsDefined(typeof(Vault), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "value is not a defined Vault");
                }

                _vaultRule = value;
                if (value != Vault.On)
                {
                    CancelVault();
                }
            }
        }

        public static string FormatVaultingLog(bool vaulting)
        {
            return $"{LogTag} vaulting={(vaulting ? "true" : "false")}";
        }

        public static string FormatVaultProbeLog(VaultProbeResult result)
        {
            var depth = float.IsPositiveInfinity(result.Depth) ? "inf" : result.Depth.ToString("F2", CultureInfo.InvariantCulture);
            return $"{LogTag} vault={result.Verdict} height={result.Height.ToString("F2", CultureInfo.InvariantCulture)} depth={depth}";
        }

        public VaultProbeResult ProbeVault()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("PlayerController is not initialized");
            }

            LastVaultProbe = MeasureVault();
            return LastVaultProbe;
        }

        public bool TryStartVault()
        {
            if (_vaultRule != Vault.On || !IsInitialized || !IsGrounded || _currentStance != PlayerStance.Standing || IsVaulting || IsMantling || IsDodging || IsMovementOverridden)
            {
                return false;
            }

            var result = ProbeVault();
            if (result.Verdict != VaultVerdict.NoObstacle)
            {
                Debug.Log(FormatVaultProbeLog(result), this);
            }

            if (!result.CanVault)
            {
                return false;
            }

            _vaultPath = result.Path;
            _vaultElapsed = 0f;
            Debug.Log(FormatVaultingLog(true), this);
            VaultStarted?.Invoke(result.Direction.ToUnity());
            return true;
        }

        public bool CancelVault()
        {
            if (!IsVaulting)
            {
                return false;
            }

            FinishVault();
            return true;
        }

        // 乗り越えている間は姿勢とジャンプを止める。カバーの差し込み (MoveFilter) の後に呼ぶので、こちらが優先する。
        private void FilterInputForVault(ref bool sprint, ref bool crouch, ref bool jumpPressed)
        {
            if (!IsVaulting)
            {
                return;
            }

            sprint = false;
            crouch = false;
            jumpPressed = false;
        }

        private void TryOverrideMoveForVault(
            Vector2 move, bool jumpPressed, float deltaSeconds, ref bool overridden, ref Vector3 displacement)
        {
            if (!IsVaulting && (!jumpPressed || !TryStartVault()))
            {
                return;
            }

            var before = _vaultElapsed;
            var after = Mathf.Min(_vaultPath.Duration, before + deltaSeconds);
            _vaultElapsed = after;
            displacement = _vaultPath.Displacement(before, after).ToUnity();
            overridden = true;

            // 射撃のばらつきなど速度を読む側のために、たどっている速度を入れておく。
            if (deltaSeconds > 0f)
            {
                _motor.SetVelocity((displacement / deltaSeconds).ToNumerics());
            }

            if (after >= _vaultPath.Duration)
            {
                FinishVault();
            }
        }

        // 降りきった勢いを持ち越すと、着地で落下ダメージを受けたり跳ね返ったりするので、速度を 0 から始め直す。
        private void FinishVault()
        {
            _vaultPath = null;
            _vaultElapsed = 0f;
            _motor?.ResetVelocity();
            Debug.Log(FormatVaultingLog(false), this);
            VaultEnded?.Invoke();
        }

        private VaultProbeResult MeasureVault()
        {
            var body = transform;
            var forward = Vector3.ProjectOnPlane(body.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-6f)
            {
                return VaultProbeResult.None;
            }

            forward.Normalize();
            var feetY = VaultFeetY();
            var spec = _motor.Spec;
            var maxHeight = spec.VaultMaxHeight;
            if (!TryFindVaultFace(feetY, forward, maxHeight, spec.VaultReach, out var hit))
            {
                return VaultProbeResult.None;
            }

            var into = -Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
            var topHeight = MeasureVaultTop(hit, into, feetY, maxHeight);
            var depth = into == Vector3.zero ? float.PositiveInfinity : MeasureVaultDepth(hit, into, spec.VaultMaxDepth);
            var radius = VaultBodyRadius();
            var width = MeasureVaultFaceWidth(hit, into, radius);
            var sample = new VaultObstacleSample(hit.distance * Vector3.Dot(forward, into), hit.normal.ToNumerics(), topHeight, depth, width);
            return VaultProbe.Evaluate(sample, forward.ToNumerics(), spec, radius, IsVaultPathClear);
        }

        // 当たったコライダーだけに上から Ray を下ろして上端を測る。始点が中 (それより高い) なら当たらないので、始点の高さ (上限より高い) とする。
        private static float MeasureVaultTop(RaycastHit hit, Vector3 into, float feetY, float maxHeight)
        {
            var startHeight = maxHeight + VaultTopProbeMargin;
            var inset = hit.point + (into * VaultSurfaceInset);
            var ray = new Ray(new Vector3(inset.x, feetY + startHeight, inset.z), Vector3.down);
            return hit.collider.Raycast(ray, out var top, startHeight) ? Mathf.Max(0f, top.point.y - feetY) : startHeight;
        }

        // 手前の面から奥行きの上限より先から当たったコライダーへ戻る Ray で向こうの面を探す。始点が中 (もっと厚い) なら当たらないので無限大とする。
        private static float MeasureVaultDepth(RaycastHit hit, Vector3 into, float maxDepth)
        {
            var reach = maxDepth + VaultDepthSlack;
            var ray = new Ray(hit.point + (into * reach), -into);
            return hit.collider.Raycast(ray, out var back, reach) ? Mathf.Max(0f, reach - back.distance) : float.PositiveInfinity;
        }

        // 軌跡の頂点と各区間の中点に立った体を置き、どこも自分以外に重ならなければ通れるとする。
        // 越える区間も奥行きの上限 (VaultMaxDepth) までなので中点を足せば間は体の幅ほどになり、CapsuleCast で掃くほどの粗さではない。
        private bool IsVaultPathClear(VaultPath path)
        {
            var points = path.Points;
            for (var i = 1; i < points.Count; i++)
            {
                var from = points[i - 1].ToUnity();
                var to = points[i].ToUnity();
                if (!IsStandingCapsuleClear((from + to) * 0.5f) || !IsStandingCapsuleClear(to))
                {
                    return false;
                }
            }

            return true;
        }

        // 1 本では張り出した縁の下をくぐるので高さを並べる。並べ方と選び方はネット対戦の CharacterVaultSensor と揃える。
        private bool TryFindVaultFace(float feetY, Vector3 forward, float maxHeight, float reach, out RaycastHit face)
        {
            var found = false;
            face = default;
            var position = transform.position;
            var count = VaultProbe.ProbeHeightCount(maxHeight);
            for (var i = 0; i < count; i++)
            {
                var origin = new Vector3(position.x, feetY + VaultProbe.ProbeHeightAt(i), position.z);
                if (TryVaultCast(origin, forward, reach, out var hit)
                    && VaultProbe.IsBetterFace(found, face.normal.ToNumerics(), face.distance, hit.normal.ToNumerics(), hit.distance))
                {
                    face = hit;
                    found = true;
                }
            }

            return found;
        }

        private float MeasureVaultFaceWidth(RaycastHit hit, Vector3 into, float radius)
        {
            if (into == Vector3.zero)
            {
                return 0f;
            }

            _vaultFaceRaycast ??= VaultFaceRaycast;
            return VaultProbe.MeasureFaceWidth(hit.point.ToNumerics(), into.ToNumerics(), radius, _vaultFaceRaycast);
        }

        private bool VaultFaceRaycast(
            System.Numerics.Vector3 origin, System.Numerics.Vector3 direction, float maxDistance, out System.Numerics.Vector3 normal, out float distance)
        {
            if (TryVaultCast(origin.ToUnity(), direction.ToUnity(), maxDistance, out var hit))
            {
                normal = hit.normal.ToNumerics();
                distance = hit.distance;
                return true;
            }

            normal = System.Numerics.Vector3.Zero;
            distance = 0f;
            return false;
        }

        private bool TryVaultCast(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            var count = Physics.RaycastNonAlloc(
                origin, direction, _vaultCastBuffer, distance, _headroomMask, QueryTriggerInteraction.Ignore);
            var found = false;
            nearest = default;
            var body = transform;
            for (var i = 0; i < count; i++)
            {
                var hit = _vaultCastBuffer[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(body))
                {
                    continue;
                }

                if (!found || hit.distance < nearest.distance)
                {
                    nearest = hit;
                    found = true;
                }
            }

            Array.Clear(_vaultCastBuffer, 0, count);
            return found;
        }

        private float VaultFeetY()
        {
            return transform.TransformPoint(_controller.center).y - (_controller.height * 0.5f * Mathf.Abs(transform.lossyScale.y));
        }

        private float VaultBodyRadius()
        {
            var scale = transform.lossyScale;
            return _controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        }
    }
}
