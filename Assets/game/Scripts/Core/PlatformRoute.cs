using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    // 位置を点の並びと同じ座標で返し、世界へ置くのを呼び手 (MovingPlatform) に任せるのは、Unity 抜きでも道のりを確かめられるようにするため。
    public sealed class PlatformRoute
    {
        private readonly Vector3[] _points;
        private readonly List<int> _calls = new List<int>();

        // 最後の Step で出発に使った呼び出し。Reverse で Step を取り消すときに呼び出しの列へ戻す
        private readonly List<int> _departedCalls = new List<int>();

        private int _from;
        private int _to;
        private int _direction = 1;
        private int _spinSign = 1;
        private float _travelled;
        private float _wait;
        private float _spin;
        private Snapshot _before;
        private bool _canUndo;

        public PlatformRoute(IReadOnlyList<Vector3> points, MovingPlatformSpec spec)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            if (points.Count == 0)
            {
                throw new ArgumentException("points must have at least one point", nameof(points));
            }

            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _points = new Vector3[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (!IsFinite(point.X) || !IsFinite(point.Y) || !IsFinite(point.Z))
                {
                    throw new ArgumentException($"points[{i}] must be finite", nameof(points));
                }

                _points[i] = point;
            }

            Position = _points[0];
        }

        public MovingPlatformSpec Spec { get; }

        public int Count => _points.Length;

        public Vector3 Position { get; private set; }

        public float SpinDegrees => _spin;

        public int From => _from;

        public int To => _to;

        public bool IsMoving => _from != _to;

        public bool IsWaiting => _wait > 0f;

        public bool IsIdle => _from == _to && _wait <= 0f && _calls.Count == 0;

        public IReadOnlyList<int> Calls => _calls;

        public Vector3 Point(int index)
        {
            RequireStop(index);
            return _points[index];
        }

        public bool Call(int stop)
        {
            RequireStop(stop);
            if (Spec.Kind != PlatformRouteKind.OnCall || stop == _to || _calls.Contains(stop))
            {
                return false;
            }

            _calls.Add(stop);
            return true;
        }

        public bool CallNext()
        {
            if (Spec.Kind != PlatformRouteKind.OnCall || Count < 2)
            {
                return false;
            }

            var next = _to + _direction;
            if (next < 0 || next >= Count)
            {
                next = _to - _direction;
            }

            return Call(next);
        }

        public void Step(float seconds)
        {
            if (!(seconds >= 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must be finite and not negative");
            }

            _before = Capture();
            _canUndo = true;
            _departedCalls.Clear();
            _spin = WrapDegrees(_spin + (_spinSign * Spec.SpinDegreesPerSecond * seconds));

            var remaining = seconds;

            // 長さ 0 の区間を待ち 0 で回り続けないよう、1 回の Step で着ける回数に上限を置く
            var guard = (Count * 2) + 4;
            while (remaining > 0f && guard-- > 0)
            {
                if (_wait > 0f)
                {
                    var used = Math.Min(_wait, remaining);
                    _wait -= used;
                    remaining -= used;
                    continue;
                }

                if (_from == _to)
                {
                    if (!TryDepart())
                    {
                        break;
                    }

                    continue;
                }

                if (!(Spec.Speed > 0f))
                {
                    break;
                }

                var left = SegmentLength() - _travelled;
                var reach = Spec.Speed * remaining;
                if (reach < left)
                {
                    _travelled += reach;
                    break;
                }

                remaining = Math.Max(0f, remaining - (left / Spec.Speed));
                Arrive();
            }

            Position = Evaluate();
        }

        // 乗っている体が挟まれそうになったとき。最後の Step を取り消して、来た方へ引き返す。
        // 取り消した後に止まっているなら、挟んだのは回る動きか出発した動きなので、回る向きを逆にして待たせる。
        public void Reverse()
        {
            if (_canUndo)
            {
                Restore(_before);
                _canUndo = false;
            }

            if (_from != _to)
            {
                var length = SegmentLength();
                if (Spec.Kind == PlatformRouteKind.OnCall)
                {
                    // 呼ばれた点は諦めず、戻った点で待ってからもう一度向かう
                    _calls.Remove(_to);
                    _calls.Insert(0, _to);
                }

                var from = _from;
                _from = _to;
                _to = from;
                _travelled = Math.Max(0f, length - _travelled);
                _direction = -_direction;
            }
            else
            {
                // 出発したとたんに挟んだときも、同じ点で待たずに出直して毎フレーム挟み続けないよう、もう一度待たせる
                _spinSign = -_spinSign;
                _wait = Math.Max(_wait, Spec.WaitSeconds);
            }

            Position = Evaluate();
        }

        private void Arrive()
        {
            _from = _to;
            _travelled = 0f;
            _wait = Spec.WaitSeconds;
        }

        private bool TryDepart()
        {
            int next;
            switch (Spec.Kind)
            {
                case PlatformRouteKind.PingPong:
                    if (Count < 2)
                    {
                        return false;
                    }

                    next = _from + _direction;
                    if (next < 0 || next >= Count)
                    {
                        _direction = -_direction;
                        next = _from + _direction;
                    }

                    break;
                case PlatformRouteKind.Loop:
                    if (Count < 2)
                    {
                        return false;
                    }

                    next = (((_from + _direction) % Count) + Count) % Count;
                    break;
                default:
                    next = -1;
                    while (_calls.Count > 0)
                    {
                        var call = _calls[0];
                        _calls.RemoveAt(0);
                        _departedCalls.Add(call);
                        if (call != _from)
                        {
                            next = call;
                            break;
                        }
                    }

                    if (next < 0)
                    {
                        return false;
                    }

                    _direction = next > _from ? 1 : -1;
                    break;
            }

            _to = next;
            _travelled = 0f;
            return true;
        }

        private Vector3 Evaluate()
        {
            if (_from == _to)
            {
                return _points[_from];
            }

            var length = SegmentLength();
            if (!(length > 0f))
            {
                return _points[_to];
            }

            return Vector3.Lerp(_points[_from], _points[_to], Math.Min(1f, _travelled / length));
        }

        private float SegmentLength()
        {
            return Vector3.Distance(_points[_from], _points[_to]);
        }

        private Snapshot Capture()
        {
            return new Snapshot(_from, _to, _direction, _travelled, _wait, _spin);
        }

        private void Restore(Snapshot snapshot)
        {
            _from = snapshot.From;
            _to = snapshot.To;
            _direction = snapshot.Direction;
            _travelled = snapshot.Travelled;
            _wait = snapshot.Wait;
            _spin = snapshot.Spin;
            _calls.InsertRange(0, _departedCalls);
            _departedCalls.Clear();
        }

        private void RequireStop(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"index must be in 0..{Count - 1}");
            }
        }

        private static float WrapDegrees(float degrees)
        {
            var wrapped = degrees % 360f;
            return wrapped < 0f ? wrapped + 360f : wrapped;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private readonly struct Snapshot
        {
            public Snapshot(int from, int to, int direction, float travelled, float wait, float spin)
            {
                From = from;
                To = to;
                Direction = direction;
                Travelled = travelled;
                Wait = wait;
                Spin = spin;
            }

            public int From { get; }

            public int To { get; }

            public int Direction { get; }

            public float Travelled { get; }

            public float Wait { get; }

            public float Spin { get; }
        }
    }
}
