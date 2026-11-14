using System;
using System.Collections.Generic;
using System.Numerics;
using Fps.Core;

namespace Fps.Server
{
    internal enum BotRouteStep
    {
        None,
        Walk,
        ApproachLadder,
        Climb,
    }

    /// <remarks>
    /// <para>
    /// 着いた角を自分で数えるのは、<see cref="BotDriver"/> は角を水平の距離でしか見ず、はしごで上の階へ渡ると下の階の角に「まだ着いていない」と
    /// 戻ってしまうため。<see cref="BotDriver"/> には次のはしごの手前までの角だけを渡す。
    /// </para>
    /// <para>
    /// はしごのリンクの区間は、移動を横取りせず人と同じ入力で渡る (クライアントの予測と同じ移動を通すため)。
    /// 区間の途中では経路を引き直さない (はしごの上には NavMesh の面が無いため)。
    /// </para>
    /// </remarks>
    internal sealed class ServerBotRoute
    {
        // NavMesh の面は床から少し浮いているので、足元との高さの差がこれ以内なら同じ階の角とみなす
        private const float _reachHeight = 1f;

        // はしごにつかまるまで向かう点の、はしごの面からの水平の距離。つかまれる距離 (体の半径と GrabDistance) より奥に置き、
        // 着く前に必ずつかまるようにする
        private const float _approachBeyond = 1f;

        private readonly ServerBotNavigation _navigation;
        private readonly NetBotSpec _spec;
        private readonly NavPath _path = new NavPath();
        private bool _hasPath;
        private bool _failed;
        private Vector3 _destination;
        private float _pathTime;
        private int _next;
        private int _ladder = ServerBotNavigation.NoLadder;
        private bool _ascending;
        private bool _climbed;

        public ServerBotRoute(ServerBotNavigation navigation, NetBotSpec spec)
        {
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public bool HasPath => _hasPath;

        public bool IsOnLadderLink => _ladder != ServerBotNavigation.NoLadder;

        public int Ladder => _ladder;

        public bool Ascending => _ascending;

        public IReadOnlyList<NavPathPoint> Points => _path.Points;

        public int NextIndex => _next;

        // 引けなかった先も RepathSeconds の間は引き直さず、届かない先で毎 tick A* を回さない
        public bool Refresh(float clock, Vector3 from, Vector3 destination)
        {
            if (IsOnLadderLink)
            {
                return true;
            }

            var repathDistance = _spec.RepathDistance;
            var fresh = clock - _pathTime < _spec.RepathSeconds
                && Vector3.DistanceSquared(destination, _destination) < repathDistance * repathDistance;
            if ((_hasPath || _failed) && fresh)
            {
                return _hasPath;
            }

            _pathTime = clock;
            _destination = destination;
            _next = 0;
            _hasPath = _navigation.TryPath(from, destination, _path);
            _failed = !_hasPath;
            return _hasPath;
        }

        public void Clear()
        {
            _path.Clear();
            _hasPath = false;
            _failed = false;
            _next = 0;
            LeaveLadder();
        }

        public BotRouteStep Next(Vector3 feet, CharacterLadderState ladder, List<Vector3> corners, out float climbInput)
        {
            ArgumentNullException.ThrowIfNull(corners);
            corners.Clear();
            climbInput = 0f;
            if (!_hasPath)
            {
                return BotRouteStep.None;
            }

            var points = _path.Points;
            if (IsOnLadderLink)
            {
                if (ladder.IsClimbing && ladder.Index == _ladder)
                {
                    _climbed = true;
                    climbInput = ClimbInput(ladder);
                    return BotRouteStep.Climb;
                }

                if (!_climbed)
                {
                    corners.Add(ApproachPoint());
                    return BotRouteStep.ApproachLadder;
                }

                LeaveLadder();
                _next = Math.Min(_next + 1, points.Count);
            }

            while (_next < points.Count)
            {
                var point = points[_next];
                var ladderIndex = _next + 1 < points.Count && point.StartsLink ? _navigation.LadderOf(point.LinkId) : ServerBotNavigation.NoLadder;
                if (ladderIndex != ServerBotNavigation.NoLadder)
                {
                    var grabbed = ladder.IsClimbing && ladder.Index == ladderIndex;
                    if (!grabbed && !Reached(feet, point.Position))
                    {
                        break;
                    }

                    _ladder = ladderIndex;
                    _ascending = points[_next + 1].Position.Y > point.Position.Y;
                    _climbed = false;
                    return Next(feet, ladder, corners, out climbInput);
                }

                if (!Reached(feet, point.Position))
                {
                    break;
                }

                _next++;
            }

            if (_next >= points.Count)
            {
                return BotRouteStep.None;
            }

            for (var i = _next; i < points.Count; i++)
            {
                corners.Add(points[i].Position);
                if (points[i].StartsLink && _navigation.LadderOf(points[i].LinkId) != ServerBotNavigation.NoLadder)
                {
                    break;
                }
            }

            return BotRouteStep.Walk;
        }

        private Vector3 ApproachPoint()
        {
            var span = _navigation.Ladders[_ladder].Span;
            return _ascending ? span.Bottom + (span.Facing * _approachBeyond) : span.Top - (span.Facing * _approachBeyond);
        }

        // 上の端からつかまると、つかまったときに押していた向きが下りに読まれる。前からつかまったなら後ろを押して下りる
        private float ClimbInput(CharacterLadderState ladder)
        {
            if (_ascending)
            {
                return 1f;
            }

            if (ladder.IsInputLatched)
            {
                return ladder.LatchedDownSign != 0 ? ladder.LatchedDownSign : 1f;
            }

            return -1f;
        }

        private bool Reached(Vector3 feet, Vector3 corner)
        {
            var dx = corner.X - feet.X;
            var dz = corner.Z - feet.Z;
            var arrival = _spec.Input.ArrivalDistance;
            return (dx * dx) + (dz * dz) <= arrival * arrival && Math.Abs(corner.Y - feet.Y) <= _reachHeight;
        }

        private void LeaveLadder()
        {
            _ladder = ServerBotNavigation.NoLadder;
            _climbed = false;
        }
    }
}
