using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // Unity 抜きのサーバーはシーンを読めないので、Unity の Editor がシーンの目印から書き出したものを読む。
    public sealed class NetMapPoints
    {
        public const string Extension = ".points.json";

        public const string SpawnPointsItem = "spawnPoints";
        public const string ControlPointsItem = "controlPoints";
        public const string FlagBasesItem = "flagBases";
        public const string PositionItem = "position";
        public const string TeamItem = "team";
        public const string IndexItem = "index";
        public const string CenterItem = "center";
        public const string RadiusItem = "radius";
        public const string HeightItem = "height";
        public const string PayloadItem = "payload";
        public const string AttackersItem = "attackers";
        public const string RouteItem = "route";
        public const string CheckpointItem = "checkpoint";

        private const string _kind = "map points JSON";

        private static readonly string[] _topItems = { SpawnPointsItem, ControlPointsItem, FlagBasesItem, PayloadItem };
        private static readonly string[] _spawnItems = { PositionItem, TeamItem };
        private static readonly string[] _controlItems = { IndexItem, CenterItem, RadiusItem, HeightItem };
        private static readonly string[] _flagBaseItems = { TeamItem, PositionItem };
        private static readonly string[] _payloadItems = { AttackersItem, RadiusItem, HeightItem, RouteItem };
        private static readonly string[] _routePointItems = { PositionItem, CheckpointItem };

        public NetMapPoints(
            IReadOnlyList<SpawnPoint> spawnPoints,
            IReadOnlyList<ControlPointArea> controlPoints,
            IReadOnlyList<FlagBasePoint> flagBases = null,
            NetMapPayload payload = null)
        {
            SpawnPoints = Copy(spawnPoints, nameof(spawnPoints));
            ControlPoints = new List<ControlPointArea>(controlPoints ?? throw new ArgumentNullException(nameof(controlPoints))).AsReadOnly();
            FlagBases = flagBases == null
                ? (IReadOnlyList<FlagBasePoint>)Array.Empty<FlagBasePoint>()
                : new List<FlagBasePoint>(flagBases).AsReadOnly();
            Payload = payload;
        }

        // 並びはシーンの階層順。SpawnSelector の同点の決め手になるので並べ替えない。
        public IReadOnlyList<SpawnPoint> SpawnPoints { get; }

        public IReadOnlyList<ControlPointArea> ControlPoints { get; }

        // 陣営ごとに 1 つかは、旗を使う目的が始めるときに確かめる。旗を使わないマップでも同じファイルを読むため
        public IReadOnlyList<FlagBasePoint> FlagBases { get; }

        // 台車を置いていないマップでは null。道筋があるかは Payload の目的が始めるときに確かめる
        public NetMapPayload Payload { get; }

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

            var flagBases = new List<FlagBasePoint>();
            var flagNodes = top.Array(FlagBasesItem);
            for (var i = 0; i < flagNodes.Count; i++)
            {
                var fields = top.Element(flagNodes[i], FlagBasesItem, i, _flagBaseItems);
                var team = fields.ToEnum<Team>(fields.ItemPath(TeamItem), fields.Require(TeamItem));
                var position = fields.ReadVector3(PositionItem);
                try
                {
                    flagBases.Add(new FlagBasePoint(team, position));
                }
                catch (ArgumentException e)
                {
                    throw fields.Rejected(fields.ItemPath(PositionItem), e.Message, e);
                }
            }

            return new NetMapPoints(spawns, controls, flagBases, ReadPayload(top));
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

            // 旗の台の無いマップの書き出しを、旗の台を知らなかったころと同じ中身に保つため、空なら項目ごと書かない
            if (points.FlagBases.Count > 0)
            {
                writer.BeginArray(FlagBasesItem);
                foreach (var flagBase in points.FlagBases)
                {
                    writer.BeginElement();
                    writer.Enum(TeamItem, flagBase.Team);
                    writer.Vector3(PositionItem, flagBase.Position);
                    writer.EndObject();
                }

                writer.EndArray();
            }

            // 旗の台と同じく、台車の無いマップは台車を知らなかったころと同じ中身に保つ
            if (points.Payload != null)
            {
                WritePayload(writer, points.Payload);
            }

            return writer.Finish();
        }

        // 中継点を点の番号でなく点ごとの印で書くのは、手で点を足し引きしたときに番号を数え直さずに済むようにするため
        private static void WritePayload(ServerDataJson.Writer writer, NetMapPayload payload)
        {
            writer.BeginObject(PayloadItem);
            writer.Enum(AttackersItem, payload.Attackers);
            writer.Float(RadiusItem, payload.Radius);
            writer.Float(HeightItem, payload.Height);
            writer.BeginArray(RouteItem);
            var route = payload.Route;
            var next = 0;
            for (var i = 0; i < route.PointCount; i++)
            {
                writer.BeginElement();
                writer.Vector3(PositionItem, route.Point(i));
                if (next < route.CheckpointCount && route.CheckpointPoint(next) == i)
                {
                    writer.Bool(CheckpointItem, true);
                    next++;
                }

                writer.EndObject();
            }

            writer.EndArray();
            writer.EndObject();
        }

        private static NetMapPayload ReadPayload(ServerDataJson.Fields top)
        {
            var fields = top.Object(PayloadItem, _payloadItems);
            if (fields == null)
            {
                return null;
            }

            var attackers = fields.ToEnum<Team>(fields.ItemPath(AttackersItem), fields.Require(AttackersItem));
            var radius = fields.ToFloat(fields.ItemPath(RadiusItem), fields.Require(RadiusItem));
            var height = fields.ToFloat(fields.ItemPath(HeightItem), fields.Require(HeightItem));
            fields.Require(RouteItem);
            var nodes = fields.Array(RouteItem);
            var positions = new List<System.Numerics.Vector3>(nodes.Count);
            var checkpoints = new List<int>();
            for (var i = 0; i < nodes.Count; i++)
            {
                var point = fields.Element(nodes[i], RouteItem, i, _routePointItems);
                positions.Add(point.ReadVector3(PositionItem));
                if (point.ReadBool(CheckpointItem, false))
                {
                    checkpoints.Add(i);
                }
            }

            try
            {
                return new NetMapPayload(attackers, positions, checkpoints, radius, height);
            }
            catch (ArgumentException e)
            {
                throw fields.Rejected(PayloadItemOf(fields, e.ParamName), e.Message, e);
            }
        }

        // 道筋の点と中継点の誤りは route、それ以外は引数の名前と同じ項目を指す
        private static string PayloadItemOf(ServerDataJson.Fields fields, string paramName)
        {
            switch (paramName)
            {
                case "radius":
                    return fields.ItemPath(RadiusItem);
                case "height":
                    return fields.ItemPath(HeightItem);
                case "attackers":
                    return fields.ItemPath(AttackersItem);
                default:
                    return fields.ItemPath(RouteItem);
            }
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
