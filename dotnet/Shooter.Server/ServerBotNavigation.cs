using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Core.Navigation;

namespace Shooter.Server
{
    // 書き出した NavMesh は壊せる建物をくり抜いていないので、経路は壊れていない壁を通り抜けることがある。
    // 歩く区間を世界の当たり (壊れた部品は外れている) に線で問い、建物の部品に当たる経路は使わない。
    internal sealed class ServerBotNavigation
    {
        public const int AllAreas = ~0;

        public const int NoLadder = -1;

        // リンクの端ははしごの面から体の半径と余白だけ離れ、NavMesh の面へ寄せてあるので、はしごの下端・上端からこの距離までは同じはしごとみなす
        private const float _ladderLinkTolerance = 2f;

        private readonly INavigation _navigation;
        private readonly IWorldCollision _world;
        private readonly Func<int, bool> _isStructurePiece;
        private readonly int[] _ladderOfLink;
        private readonly float _sampleRadius;
        private readonly float _checkHeight;

        public ServerBotNavigation(
            INavigation navigation,
            IReadOnlyList<NavLink> links,
            IReadOnlyList<MapLadder> ladders,
            IWorldCollision world,
            Func<int, bool> isStructurePiece,
            float sampleRadius,
            float checkHeight,
            bool laddersAllowed = true)
        {
            LaddersAllowed = laddersAllowed;
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            ArgumentNullException.ThrowIfNull(links);
            Ladders = ladders ?? throw new ArgumentNullException(nameof(ladders));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _isStructurePiece = isStructurePiece ?? throw new ArgumentNullException(nameof(isStructurePiece));
            if (!(sampleRadius > 0f) || float.IsInfinity(sampleRadius))
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRadius), sampleRadius, "sampleRadius must be positive and finite");
            }

            if (!(checkHeight >= 0f) || float.IsInfinity(checkHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(checkHeight), checkHeight, "checkHeight must be finite and not negative");
            }

            _sampleRadius = sampleRadius;
            _checkHeight = checkHeight;
            _ladderOfLink = new int[links.Count];
            for (var i = 0; i < links.Count; i++)
            {
                _ladderOfLink[i] = links[i].Kind == NavLinkKind.Ladder ? FindLadder(links[i], ladders) : NoLadder;
                if (links[i].Kind == NavLinkKind.Ladder && _ladderOfLink[i] == NoLadder)
                {
                    UnmatchedLadderLinks++;
                }
            }
        }

        public IReadOnlyList<MapLadder> Ladders { get; }

        // はしごにつかまれないルールで はしごのリンクを通る経路を使うと、はしごの下で進めなくなる
        public bool LaddersAllowed { get; }

        public int UnmatchedLadderLinks { get; }

        public bool CanSample(Vector3 position)
        {
            return _navigation.SamplePosition(position, _sampleRadius, AllAreas, out _);
        }

        public bool TryPath(Vector3 from, Vector3 destination, NavPath path)
        {
            ArgumentNullException.ThrowIfNull(path);
            path.Clear();
            if (!_navigation.SamplePosition(from, _sampleRadius, AllAreas, out var start)
                || !_navigation.SamplePosition(destination, _sampleRadius, AllAreas, out var end)
                || !_navigation.CalculatePath(start, end, AllAreas, path))
            {
                path.Clear();
                return false;
            }

            var points = path.Points;
            for (var i = 0; i + 1 < points.Count; i++)
            {
                var crossesLadder = points[i].StartsLink && LadderOf(points[i].LinkId) != NoLadder;
                if ((crossesLadder && !LaddersAllowed) || (!points[i].StartsLink && IsBlocked(points[i].Position, points[i + 1].Position)))
                {
                    path.Clear();
                    return false;
                }
            }

            return true;
        }

        public int LadderOf(int linkId)
        {
            return linkId >= 0 && linkId < _ladderOfLink.Length ? _ladderOfLink[linkId] : NoLadder;
        }

        public bool IsBlocked(Vector3 a, Vector3 b)
        {
            var from = a + new Vector3(0f, _checkHeight, 0f);
            var to = b + new Vector3(0f, _checkHeight, 0f);
            var offset = to - from;
            var distance = offset.Length();
            return distance > 0f
                && _world.Raycast(from, offset / distance, distance, ServerHitboxes.RaycastLayers, out var hit)
                && _isStructurePiece(hit.ColliderId);
        }

        private static int FindLadder(NavLink link, IReadOnlyList<MapLadder> ladders)
        {
            var lower = link.Start.Y <= link.End.Y ? link.Start : link.End;
            var upper = link.Start.Y <= link.End.Y ? link.End : link.Start;
            var best = NoLadder;
            var bestDistance = float.PositiveInfinity;
            for (var i = 0; i < ladders.Count; i++)
            {
                var span = ladders[i].Span;
                var bottom = Vector3.Distance(lower, span.Bottom);
                var top = Vector3.Distance(upper, span.Top);
                if (bottom <= _ladderLinkTolerance && top <= _ladderLinkTolerance && bottom + top < bestDistance)
                {
                    best = i;
                    bestDistance = bottom + top;
                }
            }

            return best;
        }
    }
}
