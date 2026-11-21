using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 囮は SideMember を持たない (持たせるとモードの参加者になる) ので、敵の目標 (陣営やタグで選ぶ相手) には入れず、知覚の Target だけを差し替える。
    // 目標を替えないので、囮が消えると知覚は元の目標 (見えていない) に戻り、EnemyBrain は最後に見た囮の場所を調べに行く。
    public sealed class DecoyPerception : IPerceivedTargetContributor
    {
        private readonly EnemyAgent _agent;
        private readonly Dictionary<Decoy, int> _heardCalls = new Dictionary<Decoy, int>();
        private readonly List<Decoy> _candidates = new List<Decoy>();
        private readonly List<DecoyOption> _options = new List<DecoyOption>();
        private Damageable _perceivedTarget;
        private Decoy _seen;
        private string _seenOwner;
        private string _seenKind;
        private int _seenSerial;
        private Vector3 _seenAt;

        public DecoyPerception(EnemyAgent agent)
        {
            _agent = agent != null ? agent : throw new ArgumentNullException(nameof(agent));
        }

        public EnemyAgent Agent => _agent;

        public Damageable PerceivedTarget => _perceivedTarget;

        public Decoy Seen => _seen;

        public static DecoyPerception AttachTo(EnemyAgent agent)
        {
            if (agent == null)
            {
                throw new ArgumentNullException(nameof(agent));
            }

            foreach (var contributor in agent.PerceptionContributors)
            {
                if (contributor is DecoyPerception existing)
                {
                    return existing;
                }
            }

            var created = new DecoyPerception(agent);
            agent.AddPerceptionContributor(created);
            return created;
        }

        // 囮が置かれるまでは敵に何も足さない。囮の無いゲームの敵の知覚を、1 つも余計な手順を通さずに保つため。
        // 後から出る敵 (波など) にも付くよう、初期化を待ち受ける。購読は Play の頭に EnemyAgent が外すので、置くたびに付け直す。
        public static void AttachAll()
        {
            EnemyAgent.Initialized -= OnEnemyInitialized;
            EnemyAgent.Initialized += OnEnemyInitialized;
            foreach (var agent in UnityEngine.Object.FindObjectsByType<EnemyAgent>(FindObjectsInactive.Include))
            {
                AttachTo(agent);
            }
        }

        // 静的な購読は Play の頭まで残るので、囮を使い終えた場面 (シーンの片付けやテスト) から、後で出る敵に付けないようにする。
        // もう付いている敵からは外さない。囮が消えれば知覚は元の目標に戻るので、残っても振る舞いは変わらない。
        public static void DetachAll()
        {
            EnemyAgent.Initialized -= OnEnemyInitialized;
        }

        public EnemyPerception Contribute(EnemyPerception perception)
        {
            _perceivedTarget = null;
            CollectOptions(perception);
            if (_candidates.Count == 0)
            {
                Lose();
                return perception;
            }

            var self = perception.Self;
            var seen = DecoyLure.SelectSeen(self, perception.Target, perception.HasLineOfSight, _options);
            if (seen >= 0)
            {
                var decoy = _candidates[seen];
                See(decoy, _options[seen].Position.ToUnity());
                _perceivedTarget = decoy.Damageable;
                return DecoyLure.Present(perception, _options[seen].Position);
            }

            Lose();
            var heard = DecoyLure.SelectHeard(self, _options);
            if (heard < 0 || perception.HeardNoise)
            {
                return perception;
            }

            var caller = _candidates[heard];
            var deployable = caller.Deployable;
            Debug.Log(DecoyLog.Heard(_agent.Id, deployable.OwnerId, deployable.KindId, deployable.Serial, _options[heard].Position), _agent);
            return DecoyLure.Hear(perception, _options[heard].Position);
        }

        private void CollectOptions(EnemyPerception perception)
        {
            _candidates.Clear();
            _options.Clear();
            var decoys = Decoy.Armed;
            if (decoys.Count == 0)
            {
                _heardCalls.Clear();
                return;
            }

            var self = _agent.gameObject;
            var member = SideMember.Of(self);
            var selfSide = member != null ? member.Side : (Side?)null;
            var ground = perception.Self.ToUnity();
            var eye = _agent.EyePosition;
            var sightRange = _agent.SightRange;
            foreach (var decoy in decoys)
            {
                var damageable = decoy != null && decoy.IsArmed ? decoy.Damageable : null;
                if (damageable == null)
                {
                    continue;
                }

                // 置いた物どうしの敵味方は、タレットと同じ決まりで見る (陣営の無い場では、bot だけが囮の敵になる)。
                var owner = decoy.Owner;
                var isOwner = owner != null && self.transform.IsChildOf(owner.transform);
                var hostile = TurretTargeting.IsHostile(isOwner, decoy.Side, selfSide, true);
                var at = damageable.GroundPoint;
                var distance = Vector3.Distance(ground, at);
                var isSeen = hostile
                    && distance <= sightRange
                    && Hitscan.HasLineOfSight(eye, damageable, Vector3.Distance(eye, damageable.AimPoint) + 0.01f, _agent.transform);

                // 呼んだ声は 1 回だけ聞く。見えていて聞かなかった声も済ませておき、見失った後に古い声を聞かないようにする。
                _heardCalls.TryGetValue(decoy, out var heardUpTo);
                var isHeard = hostile && decoy.Calls > heardUpTo && distance <= decoy.Spec.CallRadius;
                _heardCalls[decoy] = decoy.Calls;

                _candidates.Add(decoy);
                _options.Add(new DecoyOption(hostile, at.ToNumerics(), !damageable.IsDead, isSeen, isHeard));
            }

            if (_heardCalls.Count > _candidates.Count)
            {
                var stale = new List<Decoy>();
                foreach (var decoy in _heardCalls.Keys)
                {
                    if (!_candidates.Contains(decoy))
                    {
                        stale.Add(decoy);
                    }
                }

                foreach (var decoy in stale)
                {
                    _heardCalls.Remove(decoy);
                }
            }
        }

        private void See(Decoy decoy, Vector3 at)
        {
            if (_seen != decoy)
            {
                Lose();
                var deployable = decoy.Deployable;
                _seen = decoy;
                _seenOwner = deployable.OwnerId;
                _seenKind = deployable.KindId;
                _seenSerial = deployable.Serial;
                Debug.Log(DecoyLog.Seen(_agent.Id, _seenOwner, _seenKind, _seenSerial), _agent);
            }

            _seenAt = at;
        }

        // 囮が壊れた後は物が無いので、見えていた間に控えた持ち主・種類・番号と最後の位置でログを出す。
        private void Lose()
        {
            if (_seenKind == null)
            {
                return;
            }

            var gone = _seen == null || !_seen.IsArmed;
            Debug.Log(DecoyLog.Lost(_agent.Id, _seenOwner, _seenKind, _seenSerial, _seenAt.ToNumerics(), gone), _agent);
            _seen = null;
            _seenOwner = null;
            _seenKind = null;
            _seenSerial = 0;
        }

        private static void OnEnemyInitialized(EnemyAgent agent)
        {
            if (agent != null)
            {
                AttachTo(agent);
            }
        }
    }
}
