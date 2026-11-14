using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// 1 つの対戦マップの出現地点と拠点 (<c>server-data/maps/&lt;マップ&gt;.points.json</c>)。Unity 抜きのサーバーはシーンを読めないので、
    /// Unity の Editor がシーンの目印から書き出す (<see cref="Write"/>)。
    /// </summary>
    public sealed class NetMapPoints
    {
        public const string Extension = ".points.json";

        public const string SpawnPointsItem = "spawnPoints";
        public const string ControlPointsItem = "controlPoints";
        public const string PositionItem = "position";
        public const string TeamItem = "team";
        public const string IndexItem = "index";
        public const string CenterItem = "center";
        public const string RadiusItem = "radius";
        public const string HeightItem = "height";

        private const string _kind = "map points JSON";

        private static readonly string[] _topItems = { SpawnPointsItem, ControlPointsItem };
        private static readonly string[] _spawnItems = { PositionItem, TeamItem };
        private static readonly string[] _controlItems = { IndexItem, CenterItem, RadiusItem, HeightItem };

        public NetMapPoints(IReadOnlyList<SpawnPoint> spawnPoints, IReadOnlyList<ControlPointArea> controlPoints)
        {
            SpawnPoints = Copy(spawnPoints, nameof(spawnPoints));
            ControlPoints = new List<ControlPointArea>(controlPoints ?? throw new ArgumentNullException(nameof(controlPoints))).AsReadOnly();
        }

        /// <summary>並びはシーンの階層順。<see cref="SpawnSelector"/> の同点の決め手になるので並べ替えない。</summary>
        public IReadOnlyList<SpawnPoint> SpawnPoints { get; }

        public IReadOnlyList<ControlPointArea> ControlPoints { get; }

        public static NetMapPoints Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            var root = ServerDataJson.ParseDocument(text, source, _kind);
            var top = new ServerDataJson.Fields(source, _kind, root, string.Empty, _topItems);

            var spawns = new List<SpawnPoint>();
            var spawnNodes = top.Array(SpawnPointsItem);
            for (var i = 0; i < spawnNodes.Count; i++)
            {
                var fields = top.Element(spawnNodes[i], SpawnPointsItem, i, _spawnItems);
                var position = fields.ReadVector3(PositionItem);
                var teamNode = fields.Find(TeamItem);
                var team = teamNode == null ? (Team?)null : fields.ToEnum<Team>(fields.ItemPath(TeamItem), teamNode);
                try
                {
                    spawns.Add(new SpawnPoint(position, team));
                }
                catch (ArgumentException e)
                {
                    throw fields.Rejected(fields.ItemPath(PositionItem), e.Message, e);
                }
            }

            var controls = new List<ControlPointArea>();
            var controlNodes = top.Array(ControlPointsItem);
            for (var i = 0; i < controlNodes.Count; i++)
            {
                var fields = top.Element(controlNodes[i], ControlPointsItem, i, _controlItems);
                try
                {
                    controls.Add(new ControlPointArea(
                        fields.ToInt(fields.ItemPath(IndexItem), fields.Require(IndexItem)),
                        fields.ReadVector3(CenterItem),
                        fields.ToFloat(fields.ItemPath(RadiusItem), fields.Require(RadiusItem)),
                        fields.ToFloat(fields.ItemPath(HeightItem), fields.Require(HeightItem))));
                }
                catch (ArgumentException e)
                {
                    throw fields.Rejected(e.ParamName == null ? fields.Path : fields.ItemPath(e.ParamName), e.Message, e);
                }
            }

            return new NetMapPoints(spawns, controls);
        }

        public static string Write(NetMapPoints points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            var writer = new ServerDataJson.Writer();
            writer.BeginArray(SpawnPointsItem);
            foreach (var point in points.SpawnPoints)
            {
                writer.BeginElement();
                writer.Vector3(PositionItem, point.Position);
                if (point.Team.HasValue)
                {
                    writer.Enum(TeamItem, point.Team.Value);
                }

                writer.EndObject();
            }

            writer.EndArray();
            writer.BeginArray(ControlPointsItem);
            foreach (var area in points.ControlPoints)
            {
                writer.BeginElement();
                writer.Int(IndexItem, area.Index);
                writer.Vector3(CenterItem, area.Center);
                writer.Float(RadiusItem, area.Radius);
                writer.Float(HeightItem, area.Height);
                writer.EndObject();
            }

            writer.EndArray();
            return writer.Finish();
        }

        private static System.Collections.ObjectModel.ReadOnlyCollection<SpawnPoint> Copy(IReadOnlyList<SpawnPoint> points, string name)
        {
            if (points == null)
            {
                throw new ArgumentNullException(name);
            }

            var copy = new List<SpawnPoint>(points.Count);
            foreach (var point in points)
            {
                copy.Add(point ?? throw new ArgumentException(name + " must not contain null", name));
            }

            return copy.AsReadOnly();
        }
    }
}
