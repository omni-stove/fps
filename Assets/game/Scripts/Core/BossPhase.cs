using System;
using System.Collections.Generic;

namespace Fps.Core
{
    public sealed class BossPhase
    {
        private readonly string[] _exposedWeakPoints;

        // spec が null の段階は、ボスが元から持っている設定に戻す。段階ごとに全部の値を書かなくて済むように。
        public BossPhase(
            string id,
            BossPhaseTrigger trigger,
            EnemySpec spec = null,
            IEnemyBehavior behavior = null,
            float moveSpeedMultiplier = 1f,
            IEnumerable<string> exposedWeakPoints = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            if (!(moveSpeedMultiplier > 0f) || float.IsInfinity(moveSpeedMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(moveSpeedMultiplier), moveSpeedMultiplier, "moveSpeedMultiplier must be positive and finite");
            }

            var weakPoints = new List<string>();
            if (exposedWeakPoints != null)
            {
                foreach (var weakPoint in exposedWeakPoints)
                {
                    if (string.IsNullOrWhiteSpace(weakPoint))
                    {
                        throw new ArgumentException("weak point ids must not be empty", nameof(exposedWeakPoints));
                    }

                    if (!weakPoints.Contains(weakPoint))
                    {
                        weakPoints.Add(weakPoint);
                    }
                }
            }

            Id = id;
            Trigger = trigger;
            Spec = spec;
            Behavior = behavior;
            MoveSpeedMultiplier = moveSpeedMultiplier;
            _exposedWeakPoints = weakPoints.ToArray();
        }

        public string Id { get; }

        public BossPhaseTrigger Trigger { get; }

        public EnemySpec Spec { get; }

        public IEnemyBehavior Behavior { get; }

        public float MoveSpeedMultiplier { get; }

        public IReadOnlyList<string> ExposedWeakPoints => _exposedWeakPoints;

        public bool Exposes(string weakPointId)
        {
            return Array.IndexOf(_exposedWeakPoints, weakPointId) >= 0;
        }
    }
}
