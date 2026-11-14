using System;
using System.Globalization;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    public sealed partial class EnemyAgent
    {
        private Damageable _target;
        private Vector3? _pendingNoise;
        private bool _pendingHit;
        private Vector3? _pendingHitSource;
        private float _nextTargetSearch;
        private bool _warnedNoTarget;

        /// <summary>
        /// 今の目標。null なら次の Update で <see cref="TargetTag"/> から探す。<see cref="SideMember"/> があるときは
        /// <see cref="TargetSearchInterval"/> ごとに選び直すので、設定しても次の選び直しまでしか続かない。
        /// </summary>
        public Damageable Target
        {
            get => _target;
            set => _target = value;
        }

        /// <summary>
        /// position の物音を次の知覚 1 回だけ渡す。Idle・Patrol・Search 中ならその位置を Search しに行く (<see cref="EnemyBrain"/>)。
        /// 視界の外に出した敵を目標の方へ向かわせるのに使う。倒れていれば何もしない。
        /// </summary>
        public void Alert(Vector3 position)
        {
            if (_brain != null && _brain.IsDead)
            {
                return;
            }

            _pendingNoise = position;
        }

        /// <summary>
        /// 今の位置・目標・視線・体力の割合と、前の知覚の後に撃たれていれば撃たれたこと・撃った者の位置 (物音としても渡す) から、
        /// 1 tick 分の知覚を組み立てる。撃たれた情報はここで使い切るので、次の呼び出しでは撃たれていないことになる。
        /// 未初期化なら <see cref="InvalidOperationException"/>。
        /// </summary>
        public EnemyPerception Perceive()
        {
            if (_brain == null)
            {
                throw new InvalidOperationException($"EnemyAgent '{name}' is not initialized");
            }

            var noise = _pendingNoise;
            var wasHit = _pendingHit;
            var hitSource = _pendingHitSource;
            ClearPendingPerception();
            return EnemySenses.Perceive(
                GroundPosition, EyePosition, _target, _sightRange, transform, noise, wasHit, hitSource, HealthFraction(_self));
        }

        /// <summary>体力の割合 (0 以上 1 以下)。self が無ければ 1。</summary>
        public static float HealthFraction(Damageable self)
        {
            if (self == null)
            {
                return 1f;
            }

            var health = self.Health;
            return health.Max > 0 ? Mathf.Clamp01((float)health.Current / health.Max) : 0f;
        }

        // zone と kind は末尾に足す。from= までを読んでいる既存のログの読み手を変えずに済むように。
        public static string FormatDamagedLog(string id, DamageReport report)
        {
            var from = report.SourcePosition.HasValue ? report.SourcePosition.Value.ToString() : "unknown";
            var zone = report.Zone.HasValue ? report.Zone.Value.ToString() : "none";
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} id={1} damaged dmg={2} from={3} zone={4} kind={5}",
                LogTag, id, report.Amount, from, zone, report.Kind);
        }

        private void ClearPendingPerception()
        {
            _pendingNoise = null;
            _pendingHit = false;
            _pendingHitSource = null;
        }

        private void ResolveTarget(bool force)
        {
            if (!force && Time.time < _nextTargetSearch)
            {
                return;
            }

            _nextTargetSearch = Time.time + TargetSearchInterval;

            var targetObject = GameObject.FindGameObjectWithTag(TargetTag);
            var target = targetObject != null ? targetObject.GetComponentInParent<Damageable>() : null;
            if (target == null)
            {
                if (!_warnedNoTarget)
                {
                    Debug.LogWarning($"{LogTag} id={Id} '{TargetTag}' タグの Damageable が見つかりません。見つかるまで探し続けます。", this);
                    _warnedNoTarget = true;
                }

                return;
            }

            _target = target;
            _warnedNoTarget = false;
            Debug.Log($"{LogTag} id={Id} target={target.Id}", this);
        }

        /// <summary>
        /// member の陣営から見た、いちばん近い生きている敵を目標にする。force でなければ <see cref="TargetSearchInterval"/> ごとにだけ探す。
        /// 敵がいなければ目標を外す (全員倒れている間など)。目標が変わったときだけログに出す。
        /// </summary>
        private void ResolveHostile(SideMember member, bool force)
        {
            if (!force && Time.time < _nextTargetSearch)
            {
                return;
            }

            _nextTargetSearch = Time.time + TargetSearchInterval;

            var hostile = EnemySenses.SelectHostile(member, SideMember.FindAll());
            var target = hostile != null ? hostile.Damageable : null;
            if (target == _target)
            {
                return;
            }

            _target = target;
            Debug.Log($"{LogTag} id={Id} side={member.Side.Id} target={(target != null ? target.Id : "none")}", this);
        }

        private void OnDamaged(Damageable self, DamageReport report)
        {
            // 死んだ敵は反応しない (とどめの 1 発では Died の後にここへ来る)。
            if (_brain == null || _brain.IsDead || self.IsDead)
            {
                return;
            }

            // 撃った者が分からなくても、目標が見えていれば EnemyBrain はその位置から隠れられるので、撃たれたことは伝える。
            _pendingHit = true;
            if (report.SourcePosition.HasValue)
            {
                _pendingNoise = report.SourcePosition.Value;
                _pendingHitSource = report.SourcePosition.Value;
            }

            Debug.Log(FormatDamagedLog(Id, report), this);
        }
    }
}
