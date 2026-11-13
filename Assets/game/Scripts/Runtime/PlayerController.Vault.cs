using System;
using System.Globalization;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="PlayerController"/> のうち、乗り越えの処理。
    /// <see cref="VaultRule"/> が <see cref="Vault.On"/> で接地して立っているときに Jump を押すと、前方の障害物を物理で調べ
    /// (<see cref="ProbeVault"/>)、<see cref="VaultProbe"/> が乗り越えられると決めたらジャンプの代わりにその軌跡 (<see cref="VaultPath"/>) をたどる。
    /// 乗り越えられなければ (高すぎる壁など) ふつうにジャンプする。たどっている間は <see cref="TryOverrideMove"/> で移動を差し替え、
    /// しゃがみ・ダッシュ・ジャンプを止める。撃たせないのは <see cref="GameBootstrap"/> が武器に配線する。
    /// </summary>
    public sealed partial class PlayerController
    {
        /// <summary>前方の障害物を探す Ray の、足元からの高さ (m)。<see cref="VaultProbe.MinHeight"/> より上で、低い障害物にも当たる高さ。</summary>
        public const float VaultProbeHeight = 0.35f;

        // 上端を測る Ray は乗り越えられる高さよりこれだけ上から下ろす。上限を少し超える壁も「高すぎる」と測れるようにするため。
        private const float VaultTopProbeMargin = 0.3f;

        // 上端と奥行きを測る Ray を、当たった面から障害物の内側へずらす量 (m)。面のちょうど上を通って外さないため。
        private const float VaultSurfaceInset = 0.02f;

        // 奥行きは、手前の面から MaxDepth よりこれだけ先から戻る Ray で測る。ちょうど MaxDepth の厚さも測れるようにするため。
        private const float VaultDepthSlack = 0.05f;

        private const int MaxVaultCastHits = 16;

        private static readonly RaycastHit[] _vaultCastBuffer = new RaycastHit[MaxVaultCastHits];

        private Vault _vaultRule = Vault.Off;
        private VaultPath _vaultPath;
        private float _vaultElapsed;

        /// <summary>乗り越え始めたときに、乗り越える向き (ワールドの水平の単位ベクトル) を渡して呼ばれる。</summary>
        public event Action<Vector3> VaultStarted;

        /// <summary>乗り越え終えたとき (途中でやめたときを含む) に呼ばれる。</summary>
        public event Action VaultEnded;

        /// <summary>乗り越えの最中か。</summary>
        public bool IsVaulting => _vaultPath != null;

        /// <summary>今たどっている軌跡。乗り越えていなければ null。</summary>
        public VaultPath VaultPath => _vaultPath;

        /// <summary>最後に <see cref="ProbeVault"/> で調べた結果。</summary>
        public VaultProbeResult LastVaultProbe { get; private set; }

        /// <summary>
        /// 乗り越えを使うか。既定は <see cref="Vault.Off"/>。<see cref="PlayerActions.Attach"/> がルールの <see cref="ActionRules.Vault"/> を入れる。
        /// Off にすると乗り越えている最中でもやめる。
        /// </summary>
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

        /// <summary>乗り越えのログ。例: <c>[Player] vaulting=true</c>。</summary>
        public static string FormatVaultingLog(bool vaulting)
        {
            return $"{LogTag} vaulting={(vaulting ? "true" : "false")}";
        }

        /// <summary>障害物を調べた結果のログ。例: <c>[Player] vault=TooHigh height=2.00 depth=0.40</c>。</summary>
        public static string FormatVaultProbeLog(VaultProbeResult result)
        {
            var depth = float.IsPositiveInfinity(result.Depth) ? "inf" : result.Depth.ToString("F2", CultureInfo.InvariantCulture);
            return $"{LogTag} vault={result.Verdict} height={result.Height.ToString("F2", CultureInfo.InvariantCulture)} depth={depth}";
        }

        /// <summary>
        /// 向いている方向 (この GameObject の水平の前) の障害物を物理で調べ、<see cref="VaultProbe.Evaluate"/> で乗り越えられるかを決める。
        /// 結果は <see cref="LastVaultProbe"/> にも残す。ルールや接地は問わない。
        /// </summary>
        public VaultProbeResult ProbeVault()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("PlayerController is not initialized");
            }

            LastVaultProbe = MeasureVault();
            return LastVaultProbe;
        }

        /// <summary>
        /// 前方の障害物を乗り越え始める。<see cref="VaultRule"/> が Off・未初期化・接地していない・しゃがんでいる・乗り越え中か回避中なら
        /// 調べずに false。調べて障害物があれば結果を <see cref="FormatVaultProbeLog"/> でログに出し、乗り越えられなければ false。
        /// 始めたら <c>[Player] vaulting=true</c> をログに出して <see cref="VaultStarted"/> を呼ぶ。動くのは次の <see cref="Tick"/> から。
        /// </summary>
        public bool TryStartVault()
        {
            if (_vaultRule != Vault.On || !IsInitialized || !IsGrounded || _isCrouched || IsVaulting || IsDodging)
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

        /// <summary>乗り越えている最中ならやめる (その場から重力で落ちる)。やめたら true。</summary>
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

        /// <summary>
        /// 乗り越えの最中か、このフレームの Jump で乗り越え始めたら、この <see cref="Tick"/> で動かす変位を displacement に入れ、overridden を true にする。
        /// 回避より先に呼ばれる (<see cref="TryOverrideMove"/>)。乗り越えられなければ何もせず、Jump は <see cref="PlayerMotor"/> のジャンプになる。
        /// </summary>
        partial void TryOverrideMoveForVault(
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
            var origin = new Vector3(body.position.x, feetY + VaultProbeHeight, body.position.z);
            if (!TryVaultCast(origin, forward, VaultProbe.MaxReach, out var hit))
            {
                return VaultProbeResult.None;
            }

            var into = -Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
            var maxHeight = _motor.Spec.VaultMaxHeight;
            var topHeight = MeasureVaultTop(hit, into, feetY, maxHeight);
            var depth = into == Vector3.zero ? float.PositiveInfinity : MeasureVaultDepth(hit, into);
            var sample = new VaultObstacleSample(hit.distance * Vector3.Dot(forward, into), hit.normal.ToNumerics(), topHeight, depth);
            return VaultProbe.Evaluate(sample, forward.ToNumerics(), maxHeight, VaultBodyRadius(), IsVaultPathClear);
        }

        // 当たったコライダーだけに上から Ray を下ろして上端を測る。始点が中 (それより高い) なら当たらないので、始点の高さ (上限より高い) とする。
        private static float MeasureVaultTop(RaycastHit hit, Vector3 into, float feetY, float maxHeight)
        {
            var startHeight = maxHeight + VaultTopProbeMargin;
            var inset = hit.point + (into * VaultSurfaceInset);
            var ray = new Ray(new Vector3(inset.x, feetY + startHeight, inset.z), Vector3.down);
            return hit.collider.Raycast(ray, out var top, startHeight) ? Mathf.Max(0f, top.point.y - feetY) : startHeight;
        }

        // 手前の面から MaxDepth より先から当たったコライダーへ戻る Ray で向こうの面を探す。始点が中 (もっと厚い) なら当たらないので無限大とする。
        private static float MeasureVaultDepth(RaycastHit hit, Vector3 into)
        {
            var reach = VaultProbe.MaxDepth + VaultDepthSlack;
            var ray = new Ray(hit.point + (into * reach), -into);
            return hit.collider.Raycast(ray, out var back, reach) ? Mathf.Max(0f, reach - back.distance) : float.PositiveInfinity;
        }

        // 軌跡の頂点と各区間の中点に立った体を置き、どこも自分以外に重ならなければ通れるとする。
        // 越える区間も MaxDepth までの奥行きなので中点を足せば間は体の幅ほどになり、CapsuleCast で掃くほどの粗さではない。
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
