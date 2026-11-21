using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // 防衛物の部品は ServerCombat の ServerStructures (マップの当たりと耐久値) のまま戻し、全員へ同じ StructureStateMessage で送る。
    // ServerStructures はマップが替わると作り直されるので、持ち続けずに毎回今のものを引き、替わっていたら始まりの姿から数え直す
    internal sealed class ServerHordeFortifications : IHordeFortifications
    {
        private readonly Func<ServerStructures> _structures;
        private readonly Func<ServerBodies> _bodies;
        private readonly Func<int, bool> _isAlive;
        private readonly Action<int> _send;
        private readonly Func<TextWriter> _log;

        // 置かれている (始めから置かれているか、通貨で置いた) 防衛物の建物の番号
        private readonly HashSet<int> _built = new HashSet<int>();
        private readonly List<int> _pieces = new List<int>();
        private ServerStructures _prepared;
        private bool _pending = true;

        // isAlive は生きている人 (Bot も含む)。オフラインの HordeFortification.IsBlocked と同じく、倒れた人の体は部品を止めない
        public ServerHordeFortifications(
            Func<ServerStructures> structures, Func<ServerBodies> bodies, Func<int, bool> isAlive, Action<int> send, Func<TextWriter> log)
        {
            _structures = structures ?? throw new ArgumentNullException(nameof(structures));
            _bodies = bodies ?? throw new ArgumentNullException(nameof(bodies));
            _isAlive = isAlive ?? throw new ArgumentNullException(nameof(isAlive));
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public static ServerHordeFortifications For(ServerCombat combat)
        {
            ArgumentNullException.ThrowIfNull(combat);
            return new ServerHordeFortifications(
                () => combat.Structures, () => combat.Context?.Bodies, combat.IsHittable, combat.SendStructure, () => combat.Log);
        }

        // ルールのモジュールは戦闘より先に始まり、そのときはまだ建物が無いので、ここでは頼まれたことだけを覚えて次の Tick で戻す
        public void Prepare()
        {
            _pending = true;
        }

        public void Tick()
        {
            Current();
        }

        public bool IsBuilt(int fortificationId)
        {
            return Current() != null && _built.Contains(fortificationId);
        }

        public HordeFortificationAction PendingAction(int fortificationId)
        {
            var structures = Current();
            return structures == null ? HordeFortificationAction.None : PendingAction(structures, fortificationId);
        }

        public HordeFortificationTarget? FindInReach(Vector3 position, float radius)
        {
            var structures = Current();
            if (structures == null)
            {
                return null;
            }

            MapFortification nearest = null;
            var nearestAction = HordeFortificationAction.None;
            var best = float.PositiveInfinity;

            // 並びは建物の番号の昇順なので、近さが同じなら番号の小さいほうを選ぶ
            foreach (var fortification in structures.Fortifications)
            {
                var action = PendingAction(structures, fortification.Structure);
                if (action == HordeFortificationAction.None)
                {
                    continue;
                }

                var distance = fortification.HorizontalDistanceTo(position);
                if (distance <= radius && distance < best)
                {
                    best = distance;
                    nearest = fortification;
                    nearestAction = action;
                }
            }

            if (nearest == null)
            {
                return null;
            }

            CollectRestorable(structures, nearest.Structure);
            return new HordeFortificationTarget(nearest.Structure, nearest.NameKey, nearestAction, _pieces.Count);
        }

        public int Restore(int fortificationId, HordeFortificationAction action)
        {
            if (action != HordeFortificationAction.Build && action != HordeFortificationAction.Repair)
            {
                throw new ArgumentOutOfRangeException(nameof(action), action, "only Build and Repair restore pieces");
            }

            var structures = Current();
            if (structures == null || !IsFortification(structures, fortificationId))
            {
                return 0;
            }

            CollectRestorable(structures, fortificationId);
            var restored = structures.Restore(fortificationId, _pieces, _log() ?? TextWriter.Null);
            if (action == HordeFortificationAction.Build)
            {
                _built.Add(fortificationId);
            }

            if (restored > 0)
            {
                _send(fortificationId);
            }

            return restored;
        }

        private ServerStructures Current()
        {
            var structures = _structures();
            if (structures == null)
            {
                return null;
            }

            if (_pending || !ReferenceEquals(structures, _prepared))
            {
                Reset(structures);
            }

            return structures;
        }

        // 始めから置かれている防衛物は壊れたまま残す。建物は試合をまたいで壊れたままにする (ServerStructures) ので、置く場所の防衛物だけを外し直す
        private void Reset(ServerStructures structures)
        {
            _prepared = structures;
            _pending = false;
            _built.Clear();
            var log = _log() ?? TextWriter.Null;
            foreach (var fortification in structures.Fortifications)
            {
                if (fortification.StartsBuilt)
                {
                    _built.Add(fortification.Structure);
                    continue;
                }

                if (structures.RemoveAll(fortification.Structure, log))
                {
                    _send(fortification.Structure);
                }
            }
        }

        private HordeFortificationAction PendingAction(ServerStructures structures, int fortificationId)
        {
            if (!IsFortification(structures, fortificationId))
            {
                return HordeFortificationAction.None;
            }

            if (!_built.Contains(fortificationId))
            {
                return HordeFortificationAction.Build;
            }

            return structures.DestroyedCount(fortificationId) > 0 ? HordeFortificationAction.Repair : HordeFortificationAction.None;
        }

        private void CollectRestorable(ServerStructures structures, int fortificationId)
        {
            _pieces.Clear();
            var count = structures.PieceCount(fortificationId);
            for (var piece = 0; piece < count; piece++)
            {
                if (structures.IsDestroyed(fortificationId, piece) && !IsBlocked(structures, fortificationId, piece))
                {
                    _pieces.Add(piece);
                }
            }
        }

        private bool IsBlocked(ServerStructures structures, int fortificationId, int piece)
        {
            var bodies = _bodies();
            if (bodies == null)
            {
                return false;
            }

            foreach (var body in bodies.All)
            {
                if (!body.HasSpawned || !_isAlive(body.PlayerId))
                {
                    continue;
                }

                body.Capsule(out var bottom, out var top);
                if (structures.PieceOverlaps(fortificationId, piece, bottom, top, body.Radius, MapFortification.BlockCheckShrink))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFortification(ServerStructures structures, int fortificationId)
        {
            foreach (var fortification in structures.Fortifications)
            {
                if (fortification.Structure == fortificationId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
