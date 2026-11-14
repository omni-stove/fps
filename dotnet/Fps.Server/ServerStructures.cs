using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using Fps.Core;
using Fps.Protocol;

namespace Fps.Server
{
    /// <summary>
    /// マップの壊せる建物 (Unity の DestructibleStructure・StructureDamage・NetDestructibleStructure)。部品の耐久値と崩落は Core の
    /// <see cref="StructureIntegrity"/> で決め、壊れた部品は <c>.fpsmap</c> の当たりから外して、弾と爆風を通す。
    /// 壊れた部品の集合は変わったときと途中参加のときに <see cref="StructureStateMessage"/> で送る。
    /// </summary>
    /// <remarks>
    /// 部品の並び (<see cref="ServerMapData.Structures"/>) を書き出していないマップや、.fpsmap と数が合わないマップでは、部品は壊れない壁として残る。
    /// 建物は試合をまたいで壊れたまま (Unity のシーンと同じ)。
    /// </remarks>
    internal sealed class ServerStructures
    {
        public const string LogTag = "[Destruction]";

        private readonly BvhWorldCollision _collision;
        private readonly StructureIntegrity[] _integrity;
        private readonly List<PieceShape> _shapes = new List<PieceShape>();
        private readonly Dictionary<int, PieceShape> _shapeByCollider = new Dictionary<int, PieceShape>();
        private readonly StructureStateMessage _state = new StructureStateMessage();

        public ServerStructures(ServerMapData map, TextWriter log)
        {
            ArgumentNullException.ThrowIfNull(map);
            ArgumentNullException.ThrowIfNull(log);
            _collision = map.Collision;

            var pieceCounts = new SortedDictionary<int, int>();
            foreach (var collider in map.Source.Colliders)
            {
                if (!MapColliderGroups.TryGetPiece(collider.Group, out var structure, out var piece))
                {
                    continue;
                }

                pieceCounts[structure] = Math.Max(pieceCounts.TryGetValue(structure, out var count) ? count : 0, piece + 1);
                var shape = new PieceShape(collider, structure, piece);
                _shapes.Add(shape);
                _shapeByCollider[collider.Id] = shape;
            }

            _integrity = Array.Empty<StructureIntegrity>();
            if (pieceCounts.Count == 0)
            {
                return;
            }

            var layouts = map.Structures;
            if (layouts == null)
            {
                log.WriteLine(LogTag + " map " + map.Name + " has " + Text(pieceCounts.Count) + " destructible structure(s) but no "
                    + map.Name + NetMapStructures.Extension + "; they stay intact. Export it with make export-server-data");
                return;
            }

            foreach (var pair in pieceCounts)
            {
                if (pair.Key >= layouts.Structures.Count || pair.Value > layouts.Structures[pair.Key].PieceCount)
                {
                    log.WriteLine(LogTag + " " + map.Name + NetMapStructures.Extension + " does not match " + map.Name
                        + ServerMapData.CollisionExtension + " (structure " + Text(pair.Key) + "); the structures stay intact. Export both again");
                    return;
                }
            }

            _integrity = new StructureIntegrity[layouts.Structures.Count];
            for (var i = 0; i < _integrity.Length; i++)
            {
                _integrity[i] = new StructureIntegrity(layouts.Structures[i]);
            }
        }

        public int Count => _integrity.Length;

        /// <summary>破壊のルールが On のときだけ壊れる。</summary>
        public bool Enabled { get; set; }

        public bool IsPiece(int colliderId)
        {
            return _shapeByCollider.ContainsKey(colliderId);
        }

        public bool IsDestroyed(int structure, int piece)
        {
            return structure < _integrity.Length && _integrity[structure].IsDestroyed(piece);
        }

        /// <summary>
        /// 弾が colliderId に当たった。半径 0 なら当たった部品だけに、半径があれば着弾点の周りの部品に距離で減らして渡す
        /// (Unity の StructureDamage.Apply と同じ)。壊れた部品を変えたら、変わった建物の Id を changed に足す。
        /// </summary>
        public int ApplyHit(int colliderId, Vector3 point, int damage, float radius, ICollection<int> changed, TextWriter log)
        {
            if (radius > 0f)
            {
                return ApplyAround(point, damage, radius, changed, log);
            }

            if (!Enabled || !_shapeByCollider.TryGetValue(colliderId, out var shape) || shape.Structure >= _integrity.Length)
            {
                return 0;
            }

            Damage(shape.Structure, shape.Piece, damage, changed, log);
            return 1;
        }

        /// <summary>
        /// point を中心に半径 radius の球に触れる部品へ、部品の point に最も近い点までの距離で減らした被害 (中心で damage、縁で 0) を
        /// 部品ごとに 1 回だけ渡す (Unity の StructureDamage.ApplyAround と同じ)。渡した部品の数を返す。
        /// </summary>
        public int ApplyAround(Vector3 point, int damage, float radius, ICollection<int> changed, TextWriter log)
        {
            if (!Enabled || _integrity.Length == 0 || !(radius > 0f))
            {
                return 0;
            }

            var best = new SortedDictionary<(int Structure, int Piece), int>();
            foreach (var shape in _shapes)
            {
                if (shape.Structure >= _integrity.Length || _integrity[shape.Structure].IsDestroyed(shape.Piece))
                {
                    continue;
                }

                var distance = Vector3.Distance(point, shape.ClosestPoint(point));
                if (distance > radius)
                {
                    continue;
                }

                var scaled = Scale(damage, distance, radius);
                var key = (shape.Structure, shape.Piece);
                if (!best.TryGetValue(key, out var existing) || scaled > existing)
                {
                    best[key] = scaled;
                }
            }

            foreach (var pair in best)
            {
                Damage(pair.Key.Structure, pair.Key.Piece, pair.Value, changed, log);
            }

            return best.Count;
        }

        /// <summary>中心からの距離 distance での被害。中心で damage、radius 以上で 0 (Unity の StructureDamage.Scale と同じ丸め)。</summary>
        public static int Scale(int damage, float distance, float radius)
        {
            var ratio = 1f - Math.Clamp(distance / radius, 0f, 1f);
            return Math.Clamp((int)MathF.Round(damage * ratio, MidpointRounding.ToEven), 0, damage);
        }

        /// <summary>今の壊れた部品の集合を送る。何も壊れていない建物は送らない (最初からそうなので)。</summary>
        public void SendState(ServerMessenger messenger, ConnectionId connection)
        {
            for (var i = 0; i < _integrity.Length; i++)
            {
                if (_integrity[i].DestroyedCount > 0)
                {
                    Send(messenger, connection, i);
                }
            }
        }

        public void Send(ServerMessenger messenger, ConnectionId connection, int structure)
        {
            _state.StructureId = structure;
            _state.SetWords(_integrity[structure].ToBitset());
            messenger.Send(connection, TransportChannel.Reliable, _state);
        }

        private void Damage(int structure, int piece, int damage, ICollection<int> changed, TextWriter log)
        {
            var integrity = _integrity[structure];
            if (integrity.IsDestroyed(piece))
            {
                return;
            }

            var result = integrity.ApplyDamage(piece, damage);
            if (!result.Any)
            {
                return;
            }

            foreach (var destroyed in result.Destroyed)
            {
                _collision.SetGroupEnabled(MapColliderGroups.Piece(structure, destroyed), false);
            }

            foreach (var collapsed in result.Collapsed)
            {
                _collision.SetGroupEnabled(MapColliderGroups.Piece(structure, collapsed), false);
            }

            log.WriteLine(LogTag + " structure=" + Text(structure) + " destroyed=" + Ids(result.Destroyed) + " collapsed=" + Ids(result.Collapsed));
            changed?.Add(structure);
        }

        private static string Ids(IReadOnlyList<int> ids)
        {
            if (ids.Count == 0)
            {
                return "-";
            }

            var builder = new StringBuilder();
            for (var i = 0; i < ids.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append(ids[i].ToString(CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private sealed class PieceShape
        {
            private readonly WorldCollider _collider;
            private readonly Vector3 _min;
            private readonly Vector3 _max;

            public PieceShape(WorldCollider collider, int structure, int piece)
            {
                _collider = collider;
                Structure = structure;
                Piece = piece;
                if (collider.Kind == WorldShapeKind.Mesh)
                {
                    _min = new Vector3(float.PositiveInfinity);
                    _max = new Vector3(float.NegativeInfinity);
                    foreach (var vertex in collider.Vertices)
                    {
                        _min = Vector3.Min(_min, vertex);
                        _max = Vector3.Max(_max, vertex);
                    }
                }
            }

            public int Structure { get; }

            public int Piece { get; }

            // Unity の Collider.ClosestPoint は凸でないメッシュに使えず外接箱で近づけていたので、メッシュは外接箱で測る
            public Vector3 ClosestPoint(Vector3 point)
            {
                switch (_collider.Kind)
                {
                    case WorldShapeKind.Box:
                        var inverse = Quaternion.Inverse(_collider.Rotation);
                        var local = Vector3.Transform(point - _collider.Center, inverse);
                        var clamped = Vector3.Clamp(local, -_collider.HalfExtents, _collider.HalfExtents);
                        return _collider.Center + Vector3.Transform(clamped, _collider.Rotation);
                    case WorldShapeKind.Sphere:
                        return OnSphere(point, _collider.Center, _collider.Radius);
                    case WorldShapeKind.Capsule:
                        var axis = _collider.Point1 - _collider.Point0;
                        var lengthSq = axis.LengthSquared();
                        var t = lengthSq > 0f ? Math.Clamp(Vector3.Dot(point - _collider.Point0, axis) / lengthSq, 0f, 1f) : 0f;
                        return OnSphere(point, _collider.Point0 + (axis * t), _collider.Radius);
                    default:
                        return Vector3.Clamp(point, _min, _max);
                }
            }

            private static Vector3 OnSphere(Vector3 point, Vector3 center, float radius)
            {
                var offset = point - center;
                var length = offset.Length();
                return length <= radius ? point : center + (offset * (radius / length));
            }
        }
    }
}
