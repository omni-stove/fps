using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class EnemyAgent
    {
        private readonly List<IPerceptionContributor> _contributors = new List<IPerceptionContributor>();
        private Damageable _target;
        private Damageable _perceivedTarget;
        private Damageable _fallenTarget;
        private Vector3? _pendingNoise;
        private bool _pendingHit;
        private Vector3? _pendingHitSource;
        private float _nextTargetSearch;
        private bool _warnedNoTarget;

        public Damageable Target
        {
            get => _target;
            set => _target = value;
        }

        public Damageable PerceivedTarget => _perceivedTarget != null ? _perceivedTarget : _target;

        public void Alert(Vector3 position)
        {
            if (_brain != null && _brain.IsDead)
            {
                return;
            }

            _pendingNoise = position;
        }

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
            var perception = EnemySenses.Perceive(
                GroundPosition, EyePosition, _target, _sightRange, transform, noise, wasHit, hitSource, HealthFraction(_self));
            return Contribute(perception);
        }

        public IReadOnlyList<IPerceptionContributor> PerceptionContributors => _contributors;

        public bool AddPerceptionContributor(IPerceptionContributor contributor)
        {
            if (contributor == null)
            {
                throw new ArgumentNullException(nameof(contributor));
            }

            if (_contributors.Contains(contributor))
            {
                return false;
            }

            _contributors.Add(contributor);
            return true;
        }

        public bool RemovePerceptionContributor(IPerceptionContributor contributor)
        {
            if (contributor == null)
            {
                throw new ArgumentNullException(nameof(contributor));
            }

            return _contributors.Remove(contributor);
        }

        // 1 つが投げても敵を止めず、その contributor を飛ばして残りを通す。
        private EnemyPerception Contribute(EnemyPerception perception)
        {
            _perceivedTarget = null;
            if (_contributors.Count == 0)
            {
                return perception;
            }

            foreach (var contributor in _contributors.ToArray())
            {
                try
                {
                    perception = contributor.Contribute(perception);
                    if (contributor is IPerceivedTargetContributor substitute && substitute.PerceivedTarget != null)
                    {
                        _perceivedTarget = substitute.PerceivedTarget;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }

            return perception;
        }

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

        // タグで狙う相手は一度見つけたら選び直さないが、タレットは置かれたり消えたりするので、置かれている間だけ間隔ごとに比べ直す。
        private void ResolveTurretTarget(bool force)
        {
            if (!force && Time.time < _nextTargetSearch)
            {
                return;
            }

            _nextTargetSearch = Time.time + TargetSearchInterval;

            var tagged = GameObject.FindGameObjectWithTag(TargetTag);
            var player = tagged != null ? tagged.GetComponentInParent<Damageable>() : null;
            var target = EnemySenses.PreferNearerTurret(
                gameObject, null, GroundPosition, player, player != null ? player.GroundPoint : Vector3.zero, Turret.Armed);
            if (target == null || target == _target)
            {
                return;
            }

            _target = target;
            Debug.Log($"{LogTag} id={Id} target={target.Id}", this);
        }

        // 倒れた目標は見えなくなるので、放っておくと見失ったとみなして倒れた位置へ駆け寄ってしまう。
        // 同じ目標が倒れたままの間は呼び直さず、その間に聞いた物音を Search しに行けるようにする。
        internal bool StandDownIfTargetFell()
        {
            var target = _target;
            if (target == null || !target.IsDead)
            {
                _fallenTarget = null;
                return false;
            }

            if (target == _fallenTarget)
            {
                return false;
            }

            _fallenTarget = target;
            var previous = _brain.State;
            if (!_brain.StandDown())
            {
                return false;
            }

            Debug.Log(FormatStandDownLog(Id, target.Id, previous), this);
            return true;
        }

        public static string FormatStandDownLog(string id, string targetId, EnemyState previous)
        {
            return $"{LogTag} id={id} stand-down target={targetId} prev={previous}";
        }

        private void ResolveHostile(SideMember member, bool force)
        {
            if (!force && Time.time < _nextTargetSearch)
            {
                return;
            }

            _nextTargetSearch = Time.time + TargetSearchInterval;

            var hostile = EnemySenses.SelectHostile(member, SideMember.FindAll());
            var target = hostile != null ? hostile.Damageable : null;

            // 印の付いた敵は、近くにタレットがあってもそちらを優先する。
            if (!PingController.IsMarkedFor(member.Side, target))
            {
                target = EnemySenses.PreferNearerTurret(
                    gameObject, member.Side, GroundPosition, target, hostile != null ? hostile.GroundPosition : Vector3.zero, Turret.Armed);
            }

            if (target == _target)
            {
                return;
            }

            _target = target;
            Debug.Log($"{LogTag} id={Id} side={member.Side.Id} target={(target != null ? target.Id : "none")}", this);
            if (PingController.IsMarkedFor(member.Side, target))
            {
                Debug.Log(PingLog.AllyTarget(Id, member.Side, target.Id), this);
            }
        }

        private void OnDamaged(Damageable self, DamageReport report)
        {
            // 死んだ敵は反応しない (とどめの 1 発では Died の後にここへ来る)。
            if (_brain == null || _brain.IsDead || self.IsDead)
            {
                return;
            }

            Knockback.ApplyFromReport(self, report);

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
