using System.Collections.Generic;
using System.Numerics;
using Fps.Core.Navigation;

namespace Fps.Core.Tests
{
    // 四角ごとに頂点を別に持つのは、Unity のタイルの境目と同じく、隣と頂点を共有しない形で試すため。
    internal sealed class NavMeshBuilder
    {
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<NavTriangle> _triangles = new List<NavTriangle>();
        private readonly List<NavLink> _links = new List<NavLink>();
        private readonly float[] _areaCosts = new float[NavMeshFile.AreaCount];
        private bool _clockwise;

        public NavMeshBuilder()
        {
            for (var i = 0; i < _areaCosts.Length; i++)
            {
                _areaCosts[i] = 1f;
            }
        }

        public NavMeshBuilder Clockwise()
        {
            _clockwise = true;
            return this;
        }

        public NavMeshBuilder Quad(float x0, float z0, float x1, float z1, float y = 0f, int area = 0)
        {
            var i = _vertices.Count;
            _vertices.Add(new Vector3(x0, y, z0));
            _vertices.Add(new Vector3(x1, y, z0));
            _vertices.Add(new Vector3(x1, y, z1));
            _vertices.Add(new Vector3(x0, y, z1));
            Triangle(i, i + 1, i + 2, area);
            Triangle(i, i + 2, i + 3, area);
            return this;
        }

        public NavMeshBuilder Link(Vector3 start, Vector3 end, bool bidirectional = true, NavLinkKind kind = NavLinkKind.Ladder, int area = 0)
        {
            _links.Add(new NavLink(start, end, bidirectional, area, kind));
            return this;
        }

        public NavMeshBuilder AreaCost(int area, float cost)
        {
            _areaCosts[area] = cost;
            return this;
        }

        public NavMeshFile Build()
        {
            return new NavMeshFile(_vertices, _triangles, _areaCosts, _links);
        }

        public MeshNavigation Navigation()
        {
            return new MeshNavigation(Build());
        }

        private void Triangle(int a, int b, int c, int area)
        {
            _triangles.Add(_clockwise ? new NavTriangle(a, c, b, area) : new NavTriangle(a, b, c, area));
        }
    }
}
