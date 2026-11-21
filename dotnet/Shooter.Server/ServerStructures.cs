using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 建物は Unity のシーンと同じく同じマップで続ける試合をまたいで壊れたままにする。マップを替えると作り直し、どの建物も壊れていない状態に戻る。
    internal sealed class ServerStructures
    {
        public const string LogTag = "[Destruction]";

        private readonly BvhWorldCollision _collision;
        private readonly StructureIntegrity[] _integrity;
        private readonly List<PieceShape> _shapes = new List<PieceShape>();
        private readonly Dictionary<int, PieceShape> _shapeByCollider = new Dictionary<int, PieceShape>();
        private readonly Dictionary<(int Structure, int Piece), List<PieceShape>> _shapesByPiece = new Dictionary<(int Structure, int Piece), List<PieceShape>>();
        private readonly StructureStateMessage _state = new StructureStateMessage();
        private readonly IReadOnlyList<MapFortification> _fortifications = Array.Empty<MapFortification>();

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

                // マップの当たりは読んだマップごとに 1 つで、前にこのマップで遊んだ試合で外した部品が残っているので、壊れていない建物から始める
                _collision.SetGroupEnabled(collider.Group, true);
                pieceCounts[structure] = Math.Max(pieceCounts.TryGetValue(structure, out var count) ? count : 0, piece + 1);
                var shape = new PieceShape(collider, structure, piece);
                _shapes.Add(shape);
                _shapeByCollider[collider.Id] = shape;
                if (!_shapesByPiece.TryGetValue((structure, piece), out var pieceShapes))
                {
                    pieceShapes = new List<PieceShape>();
                    _shapesByPiece[(structure, piece)] = pieceShapes;
                }

                pieceShapes.Add(shape);
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

            _fortifications = layouts.Fortifications;
        }

        public int Count => _integrity.Length;

        // Horde の防衛物。建物の耐久値を使えないマップ (書き出しが無い・合わない) では、直す部品も無いので空にする
        public IReadOnlyList<MapFortification> Fortifications => _fortifications;

        public int PieceCount(int structure)
        {
            return structure >= 0 && structure < _integrity.Length ? _integrity[structure].Layout.PieceCount : 0;
        }

        public int DestroyedCount(int structure)
        {
            return structure >= 0 && structure < _integrity.Length ? _integrity[structure].DestroyedCount : 0;
        }

        // Horde の防衛物を戻す。壊すのと違って Enabled を見ないのは、壊れないルール (Destruction.Off) でも置く場所の防衛物は置けるようにするため。
        // 戻した部品の数を返す。耐久値も満タンに戻る (オフラインの DestructibleStructure.ApplyDestroyedBitset と同じ)
        public int Restore(int structure, IReadOnlyList<int> pieces, TextWriter log)
        {
            ArgumentNullException.ThrowIfNull(pieces);
            ArgumentNullException.ThrowIfNull(log);
            if (structure < 0 || structure >= _integrity.Length)
            {
                return 0;
            }

            var integrity = _integrity[structure];
            var bitset = integrity.ToBitset();
            var restored = new List<int>();
            foreach (var piece in pieces)
            {
                if (piece >= 0 && piece < integrity.Layout.PieceCount && integrity.IsDestroyed(piece) && !restored.Contains(piece))
                {
                    bitset[piece / 64] &= ~(1UL << (piece % 64));
                    restored.Add(piece);
                }
            }

            if (restored.Count == 0)
            {
                return 0;
            }

            integrity.ApplyDestroyedSet(bitset);
            restored.Sort();
            foreach (var piece in restored)
            {
                _collision.SetGroupEnabled(MapColliderGroups.Piece(structure, piece), true);
            }

            log.WriteLine(LogTag + " structure=" + Text(structure) + " restored=" + Ids(restored));
            return restored.Count;
        }

        // Horde の置く場所の防衛物を、まだ置かれていない姿 (部品が全部無い) にする。崩れの破片を出さないオフラインの HordeFortification.EnsurePrepared と同じ。
        // 外した部品があれば true
        public bool RemoveAll(int structure, TextWriter log)
        {
            ArgumentNullException.ThrowIfNull(log);
            if (structure < 0 || structure >= _integrity.Length)
            {
                return false;
            }

            var integrity = _integrity[structure];
            if (integrity.DestroyedCount == integrity.Layout.PieceCount)
            {
                return false;
            }

            var bitset = new ulong[integrity.BitsetLength];
            for (var i = 0; i < integrity.Layout.PieceCount; i++)
            {
                bitset[i / 64] |= 1UL << (i % 64);
                _collision.SetGroupEnabled(MapColliderGroups.Piece(structure, i), false);
            }

            integrity.ApplyDestroyedSet(bitset);
            log.WriteLine(LogTag + " structure=" + Text(structure) + " removed=all");
            return true;
        }

        // 部品の形を shrink 倍に縮めて、カプセル (人の体) と重なるか。オフラインの HordeFortification.IsBlocked が部品の箱を縮めて重なりを見るのと同じ
        public bool PieceOverlaps(int structure, int piece, Vector3 bottom, Vector3 top, float radius, float shrink)
        {
            if (!_shapesByPiece.TryGetValue((structure, piece), out var shapes))
            {
                return false;
            }

            foreach (var shape in shapes)
            {
                if (shape.Overlaps(bottom, top, radius, shrink))
                {
                    return true;
                }
            }

            return false;
        }

        public bool Enabled { get; set; }

        public bool IsPiece(int colliderId)
        {
            return _shapeByCollider.ContainsKey(colliderId);
        }

        public bool IsDestroyed(int structure, int piece)
        {
            return structure < _integrity.Length && _integrity[structure].IsDestroyed(piece);
        }

        // Unity の StructureDamage.Apply と同じ渡し方にする。
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

        // Unity の StructureDamage.ApplyAround と同じ渡し方にする。
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

        // Unity の StructureDamage.Scale と同じ丸めにする。
        public static int Scale(int damage, float distance, float radius)
        {
            var ratio = 1f - Math.Clamp(distance / radius, 0f, 1f);
            return Math.Clamp((int)MathF.Round(damage * ratio, MidpointRounding.ToEven), 0, damage);
        }

        // 何も壊れていない建物は、クライアントが最初からそう見ているので送らない。
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
            // 交互に寄せる回数の上限。部品も人の体も小さいので、これだけ寄せれば重なりの判定が変わるほどの差は残らない
            private const int _overlapSteps = 16;

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
                return ClosestPoint(point, 1f);
            }

            // 形の中心から shrink 倍に縮めた形の、point にいちばん近い点
            public Vector3 ClosestPoint(Vector3 point, float shrink)
            {
                switch (_collider.Kind)
                {
                    case WorldShapeKind.Box:
                        var inverse = Quaternion.Inverse(_collider.Rotation);
                        var local = Vector3.Transform(point - _collider.Center, inverse);
                        var half = _collider.HalfExtents * shrink;
                        var clamped = Vector3.Clamp(local, -half, half);
                        return _collider.Center + Vector3.Transform(clamped, _collider.Rotation);
                    case WorldShapeKind.Sphere:
                        return OnSphere(point, _collider.Center, _collider.Radius * shrink);
                    case WorldShapeKind.Capsule:
                        var middle = (_collider.Point0 + _collider.Point1) * 0.5f;
                        var point0 = middle + ((_collider.Point0 - middle) * shrink);
                        var axis = (_collider.Point1 - _collider.Point0) * shrink;
                        var lengthSq = axis.LengthSquared();
                        var t = lengthSq > 0f ? Math.Clamp(Vector3.Dot(point - point0, axis) / lengthSq, 0f, 1f) : 0f;
                        return OnSphere(point, point0 + (axis * t), _collider.Radius * shrink);
                    default:
                        var center = (_min + _max) * 0.5f;
                        var extent = (_max - _min) * (0.5f * shrink);
                        return Vector3.Clamp(point, center - extent, center + extent);
                }
            }

            // 縮めた形と、bottom から top までの線分を半径 radius で太らせたカプセルが重なるか。
            // 形もカプセルの芯も凸なので、互いのいちばん近い点へ交互に寄せると、いちばん近い 2 点に寄っていく
            public bool Overlaps(Vector3 bottom, Vector3 top, float radius, float shrink)
            {
                var onSegment = (bottom + top) * 0.5f;
                var onShape = ClosestPoint(onSegment, shrink);
                for (var i = 0; i < _overlapSteps; i++)
                {
                    onSegment = ClosestOnSegment(bottom, top, onShape);
                    var next = ClosestPoint(onSegment, shrink);
                    if (Vector3.DistanceSquared(next, onShape) < 1e-8f)
                    {
                        onShape = next;
                        break;
                    }

                    onShape = next;
                }

                return Vector3.DistanceSquared(onSegment, onShape) < radius * radius;
            }

            private static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 point)
            {
                var segment = b - a;
                var lengthSq = segment.LengthSquared();
                var t = lengthSq > 0f ? Math.Clamp(Vector3.Dot(point - a, segment) / lengthSq, 0f, 1f) : 0f;
                return a + (segment * t);
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
