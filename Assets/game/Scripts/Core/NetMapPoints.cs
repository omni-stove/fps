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

        private const string _kind = "map points JSON";

        private static readonly string[] _topItems = { SpawnPointsItem, ControlPointsItem, FlagBasesItem };
        private static readonly string[] _spawnItems = { PositionItem, TeamItem };
        private static readonly string[] _controlItems = { IndexItem, CenterItem, RadiusItem, HeightItem };
        private static readonly string[] _flagBaseItems = { TeamItem, PositionItem };

        public NetMapPoints(
            IReadOnlyList<SpawnPoint> spawnPoints,
            IReadOnlyList<ControlPointArea> controlPoints,
            IReadOnlyList<FlagBasePoint> flagBases = null)
        {
            SpawnPoints = Copy(spawnPoints, nameof(spawnPoints));
            ControlPoints = new List<ControlPointArea>(controlPoints ?? throw new ArgumentNullException(nameof(controlPoints))).AsReadOnly();
            FlagBases = flagBases == null
                ? (IReadOnlyList<FlagBasePoint>)Array.Empty<FlagBasePoint>()
                : new List<FlagBasePoint>(flagBases).AsReadOnly();
        }

        // 並びはシーンの階層順。SpawnSelector の同点の決め手になるので並べ替えない。
        public IReadOnlyList<SpawnPoint> SpawnPoints { get; }

        public IReadOnlyList<ControlPointArea> ControlPoints { get; }

        // 陣営ごとに 1 つかは、旗を使う目的が始めるときに確かめる。旗を使わないマップでも同じファイルを読むため
        public IReadOnlyList<FlagBasePoint> FlagBases { get; }

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

            return new NetMapPoints(spawns, controls, flagBases);
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
