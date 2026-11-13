using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// ラグ補償の当たり判定。対象を <see cref="Fps.Core.LagCompensator"/> で巻き戻した位置 (と姿勢) へ一時的に動かして Raycast し、
    /// 例外が出ても必ず元の位置と姿勢に戻す。動かした後と戻した後の両方で <see cref="Physics.SyncTransforms"/> を呼び、
    /// 物理側のコライダーも Transform に合わせる。サーバーのメインスレッドでだけ使う。
    /// </summary>
    /// <remarks>
    /// 巻き戻した位置が無い対象 (その時点にまだいなかった・リスポーン直後) と当ててはいけない対象は動かさず、
    /// Raycast もそのコライダーを素通りさせる。撃った本人 (shooterRoot の子孫) とトリガーも無視する。
    /// </remarks>
    public sealed class HitboxRewinder
    {
        /// <summary>1 回の Raycast で調べる命中の上限。これを超える数のコライダーが一直線に並ぶと遠いものを取りこぼす。</summary>
        public const int MaxHits = 32;

        private readonly RaycastHit[] _buffer = new RaycastHit[MaxHits];
        private readonly List<Transform> _movedRoots = new List<Transform>();
        private readonly List<Vector3> _originalPositions = new List<Vector3>();
        private readonly List<NetStance> _changedStances = new List<NetStance>();
        private readonly List<bool> _originalCrouched = new List<bool>();

        /// <summary>
        /// targets を rewound の位置へ動かし、origin から direction へ range まで Raycast して、最も近い命中を返してから元に戻す。
        /// 姿勢は今のまま (巻き戻さない)。
        /// </summary>
        /// <param name="targets">巻き戻す対象。Root が null のものは無視する。</param>
        /// <param name="rewound">id ごとの巻き戻した位置 (<see cref="Fps.Core.LagCompensator.Rewind(float, int)"/> の結果)。</param>
        /// <param name="origin">始点。</param>
        /// <param name="direction">向き。長さは問わないが 0 ではないこと。</param>
        /// <param name="range">射程。0 より大きい有限値。</param>
        /// <param name="shooterRoot">撃った本人。この子孫のコライダーには当たらない。null なら何も除かない。</param>
        /// <param name="hit">最も近い命中。</param>
        /// <returns>何かに当たったら true。</returns>
        public bool Raycast(
            IReadOnlyList<RewindTarget> targets,
            IReadOnlyDictionary<int, System.Numerics.Vector3> rewound,
            Vector3 origin,
            Vector3 direction,
            float range,
            Transform shooterRoot,
            out RewindHit hit)
        {
            return Raycast(targets, rewound, null, origin, direction, range, shooterRoot, out hit);
        }

        /// <summary>
        /// targets を rewound の位置と rewoundCrouched の姿勢 (<see cref="RewindTarget.Stance"/> を持つ対象だけ) へ動かし、
        /// origin から direction へ range まで Raycast して、最も近い命中を返してから位置も姿勢も元に戻す。
        /// </summary>
        /// <param name="rewoundCrouched">
        /// id ごとの巻き戻した時点のしゃがみ (<see cref="Fps.Core.LagCompensator.RewindCrouched"/> の結果)。
        /// null か id が無ければその対象の姿勢は今のまま。
        /// </param>
        public bool Raycast(
            IReadOnlyList<RewindTarget> targets,
            IReadOnlyDictionary<int, System.Numerics.Vector3> rewound,
            IReadOnlyDictionary<int, bool> rewoundCrouched,
            Vector3 origin,
            Vector3 direction,
            float range,
            Transform shooterRoot,
            out RewindHit hit)
        {
            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            if (rewound == null)
            {
                throw new ArgumentNullException(nameof(rewound));
            }

            if (!NetMath.IsFinite(origin))
            {
                throw new ArgumentException("origin must be finite", nameof(origin));
            }

            if (!NetMath.IsFinite(direction) || direction.sqrMagnitude < 1e-12f)
            {
                throw new ArgumentException("direction must be finite and not zero", nameof(direction));
            }

            if (!(range > 0f) || float.IsInfinity(range))
            {
                throw new ArgumentOutOfRangeException(nameof(range), range, "range must be positive and finite");
            }

            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i].Id == RewindHit.NoTarget)
                {
                    throw new ArgumentException("target id " + RewindHit.NoTarget + " is reserved for hits on non-targets", nameof(targets));
                }
            }

            hit = default;
            _movedRoots.Clear();
            _originalPositions.Clear();
            _changedStances.Clear();
            _originalCrouched.Clear();
            try
            {
                MoveToRewound(targets, rewound, rewoundCrouched);
                Physics.SyncTransforms();

                var count = Physics.RaycastNonAlloc(
                    origin,
                    direction.normalized,
                    _buffer,
                    range,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);

                return TryPickNearest(count, targets, rewound, shooterRoot, out hit);
            }
            finally
            {
                Restore();
                Array.Clear(_buffer, 0, _buffer.Length);
            }
        }

        private void MoveToRewound(
            IReadOnlyList<RewindTarget> targets,
            IReadOnlyDictionary<int, System.Numerics.Vector3> rewound,
            IReadOnlyDictionary<int, bool> rewoundCrouched)
        {
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target.Root == null || !target.Hittable || !rewound.TryGetValue(target.Id, out var position))
                {
                    continue;
                }

                // 先に控えてから動かす。動かした後に例外が出ても、控えた分は finally で戻せる
                _movedRoots.Add(target.Root);
                _originalPositions.Add(target.Root.position);
                target.Root.position = NetMath.ToUnity(position);

                if (target.Stance != null && rewoundCrouched != null && rewoundCrouched.TryGetValue(target.Id, out var crouched))
                {
                    _changedStances.Add(target.Stance);
                    _originalCrouched.Add(target.Stance.IsCrouched);
                    target.Stance.Apply(crouched);
                }
            }
        }

        private void Restore()
        {
            // 同じ姿勢が 2 度入っていても最初に控えた姿勢に戻るよう、逆順に戻す
            for (var i = _changedStances.Count - 1; i >= 0; i--)
            {
                _changedStances[i].Apply(_originalCrouched[i]);
            }

            _changedStances.Clear();
            _originalCrouched.Clear();

            if (_movedRoots.Count == 0)
            {
                return;
            }

            // 同じ Root が 2 度入っていても最初に控えた位置に戻るよう、逆順に戻す
            for (var i = _movedRoots.Count - 1; i >= 0; i--)
            {
                var root = _movedRoots[i];
                if (root != null)
                {
                    root.position = _originalPositions[i];
                }
            }

            _movedRoots.Clear();
            _originalPositions.Clear();
            Physics.SyncTransforms();
        }

        private bool TryPickNearest(
            int count,
            IReadOnlyList<RewindTarget> targets,
            IReadOnlyDictionary<int, System.Numerics.Vector3> rewound,
            Transform shooterRoot,
            out RewindHit nearest)
        {
            nearest = default;
            var found = false;
            for (var i = 0; i < count; i++)
            {
                var candidate = _buffer[i];
                if (candidate.collider == null)
                {
                    continue;
                }

                var colliderTransform = candidate.collider.transform;
                if (shooterRoot != null && colliderTransform.IsChildOf(shooterRoot))
                {
                    continue;
                }

                if (found && candidate.distance >= nearest.RaycastHit.distance)
                {
                    continue;
                }

                var owner = FindOwner(targets, colliderTransform);
                if (owner < 0)
                {
                    nearest = new RewindHit(candidate, RewindHit.NoTarget, Vector3.zero);
                    found = true;
                    continue;
                }

                var target = targets[owner];
                if (!target.Hittable || !rewound.TryGetValue(target.Id, out var position))
                {
                    // 当てられない対象・その時点にいなかった対象は素通りする
                    continue;
                }

                var crouched = target.Stance != null && target.Stance.IsCrouched;
                nearest = new RewindHit(candidate, target.Id, NetMath.ToUnity(position), crouched);
                found = true;
            }

            return found;
        }

        private static int FindOwner(IReadOnlyList<RewindTarget> targets, Transform colliderTransform)
        {
            for (var i = 0; i < targets.Count; i++)
            {
                var root = targets[i].Root;
                if (root != null && colliderTransform.IsChildOf(root))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
