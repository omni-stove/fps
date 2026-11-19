using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // Bot 1 人ぶんの囮の見え方と聞こえ方。選び方は Core の DecoyLure が決め、ここはオフラインの DecoyPerception と同じく
    // 候補を集めて、見えた囮を知覚の Target に、聞こえた呼び声を物音に差し替えるだけにする。目標そのものは替えないので、
    // 囮が消えると知覚は元の相手に戻り、Brain は最後に見た囮の場所を調べに行く
    internal sealed class ServerDecoyPerception
    {
        private readonly int _botId;
        private readonly Dictionary<int, int> _heardCalls = new Dictionary<int, int>();
        private readonly List<ServerDecoy> _candidates = new List<ServerDecoy>();
        private readonly List<DecoyOption> _options = new List<DecoyOption>();
        private readonly List<int> _stale = new List<int>();
        private int? _seenId;
        private int _seenOwner;
        private string _seenKind;
        private Vector3 _seenAt;

        public ServerDecoyPerception(int botId)
        {
            _botId = botId;
        }

        public int? SeenId => _seenId;

        // canSee が false (目がくらんでいる) なら、囮も見えず、呼び声だけが届く。hasLineOfSight は Bot の目から点まで見通せるか
        public EnemyPerception Contribute(
            EnemyPerception perception,
            Side? selfSide,
            bool canSee,
            float sightRange,
            IReadOnlyList<ServerDecoy> decoys,
            Func<Vector3, bool> hasLineOfSight,
            TextWriter log)
        {
            ArgumentNullException.ThrowIfNull(decoys);
            ArgumentNullException.ThrowIfNull(hasLineOfSight);
            ArgumentNullException.ThrowIfNull(log);
            CollectOptions(perception.Self, selfSide, canSee, sightRange, decoys, hasLineOfSight);
            if (_candidates.Count == 0)
            {
                Lose(log);
                return perception;
            }

            var self = perception.Self;
            var seen = DecoyLure.SelectSeen(self, perception.Target, perception.HasLineOfSight, _options);
            if (seen >= 0)
            {
                See(_candidates[seen], log);
                return DecoyLure.Present(perception, _options[seen].Position);
            }

            Lose(log);
            var heard = DecoyLure.SelectHeard(self, _options);
            if (heard < 0 || perception.HeardNoise)
            {
                return perception;
            }

            var caller = _candidates[heard];
            log.WriteLine(DecoyLog.Heard(
                ServerDeployableLog.Owner(_botId), ServerDeployableLog.Owner(caller.OwnerId), caller.TypeId, caller.Id, caller.Position));
            return DecoyLure.Hear(perception, caller.Position);
        }

        private void CollectOptions(
            Vector3 self, Side? selfSide, bool canSee, float sightRange, IReadOnlyList<ServerDecoy> decoys, Func<Vector3, bool> hasLineOfSight)
        {
            _candidates.Clear();
            _options.Clear();
            if (decoys.Count == 0)
            {
                _heardCalls.Clear();
                return;
            }

            foreach (var decoy in decoys)
            {
                // 置いた物どうしの敵味方は、タレットと同じ決まりで見る (陣営の無い場では、Bot だけが囮の敵になる)
                var hostile = TurretTargeting.IsHostile(decoy.OwnerId == _botId, Side.FromTeam(decoy.Team), selfSide, true);
                var distance = Vector3.Distance(self, decoy.Position);
                var isSeen = hostile && canSee && distance <= sightRange && hasLineOfSight(decoy.AimPoint);

                // 呼んだ声は 1 回だけ聞く。見えていて聞かなかった声も済ませておき、見失った後に古い声を聞かないようにする
                _heardCalls.TryGetValue(decoy.Id, out var heardUpTo);
                var isHeard = hostile && decoy.Calls > heardUpTo && distance <= decoy.CallRadius;
                _heardCalls[decoy.Id] = decoy.Calls;

                _candidates.Add(decoy);
                _options.Add(new DecoyOption(hostile, decoy.Position, true, isSeen, isHeard));
            }

            if (_heardCalls.Count <= _candidates.Count)
            {
                return;
            }

            _stale.Clear();
            foreach (var id in _heardCalls.Keys)
            {
                if (!IsCandidate(id))
                {
                    _stale.Add(id);
                }
            }

            foreach (var id in _stale)
            {
                _heardCalls.Remove(id);
            }
        }

        private void See(ServerDecoy decoy, TextWriter log)
        {
            if (_seenId != decoy.Id)
            {
                Lose(log);
                _seenId = decoy.Id;
                _seenOwner = decoy.OwnerId;
                _seenKind = decoy.TypeId;
                log.WriteLine(DecoyLog.Seen(ServerDeployableLog.Owner(_botId), ServerDeployableLog.Owner(_seenOwner), _seenKind, decoy.Id));
            }

            _seenAt = decoy.Position;
        }

        // 囮が壊れた後は一覧に無いので、見えていた間に控えた持ち主・種類・番号と最後の位置でログを出す
        private void Lose(TextWriter log)
        {
            if (!_seenId.HasValue)
            {
                return;
            }

            var id = _seenId.Value;
            log.WriteLine(DecoyLog.Lost(
                ServerDeployableLog.Owner(_botId), ServerDeployableLog.Owner(_seenOwner), _seenKind, id, _seenAt, !IsCandidate(id)));
            _seenId = null;
            _seenOwner = 0;
            _seenKind = null;
        }

        private bool IsCandidate(int id)
        {
            foreach (var candidate in _candidates)
            {
                if (candidate.Id == id)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
