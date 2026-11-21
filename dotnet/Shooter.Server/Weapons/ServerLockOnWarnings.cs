using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 狙う側の捕捉の進み具合 (Unity の LockOnAim) はクライアントの手元にしか無いので、サーバーが知らせるのは、ロックして撃たれた誘導弾が
    // 相手を追っている間の「ロックされた」段階だけにする。乗り物を狙った弾は乗っている全員に、人を狙った弾はその人に知らせる。
    internal sealed class ServerLockOnWarnings
    {
        private readonly ServerCombat _combat;
        private readonly List<TrackingRound> _tracking = new List<TrackingRound>();
        private readonly List<int> _victims = new List<int>();
        private readonly SortedDictionary<int, TrackingRound> _threats = new SortedDictionary<int, TrackingRound>();
        private readonly SortedDictionary<int, int> _sent = new SortedDictionary<int, int>();
        private readonly List<int> _cleared = new List<int>();
        private readonly LockOnWarningMessage _message = new LockOnWarningMessage();

        public ServerLockOnWarnings(ServerCombat combat)
        {
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        }

        // 警告を出している人と、その人に知らせた誘導弾の Id。
        public IReadOnlyDictionary<int, int> Warned => _sent;

        // 弾が進んだ後に呼ぶ。知らせる弾が替わった人・新しく狙われた人には出し直し、もう追われていない人には解いたことを知らせる。
        public void Update(ServerProjectiles projectiles)
        {
            ArgumentNullException.ThrowIfNull(projectiles);
            _tracking.Clear();
            projectiles.CollectTracking(_tracking);
            _threats.Clear();
            foreach (var round in _tracking)
            {
                _victims.Clear();
                CollectVictims(round, _victims);
                foreach (var victim in _victims)
                {
                    // 同じ人を何発も追っているときは、いちばん近い弾を知らせる (Unity の LockOnWarningPublisher が近い相手を先にするのと同じ)
                    if (!_threats.TryGetValue(victim, out var known) || IsCloser(victim, round, known))
                    {
                        _threats[victim] = round;
                    }
                }
            }

            foreach (var pair in _threats)
            {
                if (_sent.TryGetValue(pair.Key, out var projectile) && projectile == pair.Value.ProjectileId)
                {
                    continue;
                }

                _sent[pair.Key] = pair.Value.ProjectileId;
                Send(pair.Key, LockOnWarningStage.Locked, pair.Value.ProjectileId, pair.Value.Position);
                _combat.Log.WriteLine(ServerCountermeasureLog.Warning(pair.Key, pair.Value.ProjectileId, pair.Value.Target));
            }

            _cleared.Clear();
            foreach (var victim in _sent.Keys)
            {
                if (!_threats.ContainsKey(victim))
                {
                    _cleared.Add(victim);
                }
            }

            foreach (var victim in _cleared)
            {
                _sent.Remove(victim);
                Send(victim, LockOnWarningStage.None, LockOnWarningMessage.NoProjectile, Vector3.Zero);
                _combat.Log.WriteLine(ServerCountermeasureLog.WarningCleared(victim));
            }
        }

        // 抜けた人には送れないので、知らせた記録だけを捨てる。
        public void Forget(int playerId)
        {
            _sent.Remove(playerId);
        }

        public void Clear()
        {
            _sent.Clear();
            _threats.Clear();
        }

        private void CollectVictims(TrackingRound round, List<int> into)
        {
            var target = round.Target;
            if (target.IsVehicle)
            {
                _combat.Vehicles?.CollectOccupants(target.Id, into);
            }
            else if (_combat.IsHittable(target.Id))
            {
                into.Add(target.Id);
            }

            // 撃った本人が狙った乗り物に乗り込んでも、自分の撃った弾は脅威として知らせない (Unity の LockOnWarningPublisher.IsAimedAt と同じ)。
            into.Remove(round.ShooterId);
        }

        private bool IsCloser(int victim, TrackingRound round, TrackingRound known)
        {
            if (!_combat.Context.Bodies.TryGet(victim, out var body))
            {
                return false;
            }

            var center = body.Center;
            return Vector3.DistanceSquared(round.Position, center) < Vector3.DistanceSquared(known.Position, center);
        }

        private void Send(int playerId, LockOnWarningStage stage, int projectileId, Vector3 source)
        {
            _message.Stage = stage;
            _message.ProjectileId = projectileId;
            _message.Source = source;
            _combat.Send(playerId, _message);
        }
    }
}
