using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    // .fpsmap は部品の形しか持たないので、Unity 抜きのサーバーが同じ耐久値と崩れ方で壊せるよう、Unity の Editor が建物ごとの
    // StructureLayout を書き出したものを読む。並びは .fpsmap の建物の番号 (MapColliderGroups.Piece) の順。
    public sealed class NetMapStructures
    {
        public const string Extension = ".structures.json";

        public const string StructuresItem = "structures";
        public const string PiecesItem = "pieces";
        public const string AdjacencyItem = "adjacency";
        public const string MaxHealthItem = "maxHealth";
        public const string GroundedItem = "grounded";
        public const string AItem = "a";
        public const string BItem = "b";
        public const string FortificationsItem = "fortifications";
        public const string StructureItem = "structure";
        public const string PositionItem = "position";
        public const string StartsBuiltItem = "startsBuilt";
        public const string NameKeyItem = "nameKey";

        private const string _kind = "map structures JSON";

        private static readonly string[] _topItems = { StructuresItem, FortificationsItem };
        private static readonly string[] _structureItems = { PiecesItem, AdjacencyItem };
        private static readonly string[] _pieceItems = { MaxHealthItem, GroundedItem };
        private static readonly string[] _pairItems = { AItem, BItem };
        private static readonly string[] _fortificationItems = { StructureItem, PositionItem, StartsBuiltItem, NameKeyItem };

        public NetMapStructures(IReadOnlyList<StructureLayout> structures)
            : this(structures, null)
        {
        }

        // fortifications は Horde の防衛物 (HordeFortification の付いた建物)。null なら無し
        public NetMapStructures(IReadOnlyList<StructureLayout> structures, IReadOnlyList<MapFortification> fortifications)
        {
            if (structures == null)
            {
                throw new ArgumentNullException(nameof(structures));
            }

            var copy = new List<StructureLayout>(structures.Count);
            foreach (var structure in structures)
            {
                copy.Add(structure ?? throw new ArgumentException("structures must not contain null", nameof(structures)));
            }

            Structures = copy.AsReadOnly();

            var fortificationCopy = new List<MapFortification>();
            var seen = new HashSet<int>();
            if (fortifications != null)
            {
                foreach (var fortification in fortifications)
                {
                    if (fortification == null)
                    {
                        throw new ArgumentException("fortifications must not contain null", nameof(fortifications));
                    }

                    if (fortification.Structure >= copy.Count)
                    {
                        throw new ArgumentException(
                            "the fortification of structure " + Text(fortification.Structure) + " is beyond the " + Text(copy.Count) + " structure(s)",
                            nameof(fortifications));
                    }

                    if (!seen.Add(fortification.Structure))
                    {
                        throw new ArgumentException("structure " + Text(fortification.Structure) + " is a fortification twice", nameof(fortifications));
                    }

                    fortificationCopy.Add(fortification);
                }
            }

            // 近さが同じ防衛物を選ぶ順を書き出しの並びに左右させないよう、建物の番号の順にする
            fortificationCopy.Sort((a, b) => a.Structure.CompareTo(b.Structure));
            Fortifications = fortificationCopy.AsReadOnly();
        }

        public IReadOnlyList<StructureLayout> Structures { get; }

        // 建物の番号の昇順
        public IReadOnlyList<MapFortification> Fortifications { get; }

        public static NetMapStructures Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            var root = ServerDataJson.ParseDocument(text, source, _kind);
            var top = new ServerDataJson.Fields(source, _kind, root, string.Empty, _topItems);

            var structures = new List<StructureLayout>();
            var structureNodes = top.Array(StructuresItem);
            for (var s = 0; s < structureNodes.Count; s++)
            {
                var fields = top.Element(structureNodes[s], StructuresItem, s, _structureItems);
                var maxHealth = new List<int>();
                var grounded = new List<bool>();
                var pieceNodes = fields.Array(PiecesItem);
                for (var p = 0; p < pieceNodes.Count; p++)
                {
                    var piece = fields.Element(pieceNodes[p], PiecesItem, p, _pieceItems);
                    maxHealth.Add(piece.ToInt(piece.ItemPath(MaxHealthItem), piece.Require(MaxHealthItem)));
                    grounded.Add(piece.ReadBool(GroundedItem, false));
                }

                var adjacency = new List<(int A, int B)>();
                var pairNodes = fields.Array(AdjacencyItem);
                for (var i = 0; i < pairNodes.Count; i++)
                {
                    var pair = fields.Element(pairNodes[i], AdjacencyItem, i, _pairItems);
                    adjacency.Add((pair.ToInt(pair.ItemPath(AItem), pair.Require(AItem)), pair.ToInt(pair.ItemPath(BItem), pair.Require(BItem))));
                }

                try
                {
                    structures.Add(new StructureLayout(maxHealth, grounded, adjacency));
                }
                catch (ArgumentException e)
                {
                    throw fields.Rejected(fields.Path, e.Message, e);
                }
            }

            var fortifications = new List<MapFortification>();
            var fortificationNodes = top.Array(FortificationsItem);
            for (var i = 0; i < fortificationNodes.Count; i++)
            {
                var fields = top.Element(fortificationNodes[i], FortificationsItem, i, _fortificationItems);
                var structureItem = fields.ItemPath(StructureItem);
                var structure = fields.ToInt(structureItem, fields.Require(StructureItem));
                if (structure < 0 || structure >= structures.Count)
                {
                    throw fields.Rejected(
                        structureItem, "must be one of the " + Text(structures.Count) + " structure(s) of this map, but is " + Text(structure));
                }

                foreach (var earlier in fortifications)
                {
                    if (earlier.Structure == structure)
                    {
                        throw fields.Rejected(structureItem, "structure " + Text(structure) + " is a fortification twice");
                    }
                }

                var position = fields.ReadVector3(PositionItem);
                try
                {
                    fortifications.Add(new MapFortification(
                        structure, position, fields.ReadBool(StartsBuiltItem, true), fields.ReadString(NameKeyItem, string.Empty)));
                }
                catch (ArgumentException e)
                {
                    // MapFortification の引数名は項目名と同じ
                    throw fields.Rejected(e.ParamName == null ? fields.Path : fields.ItemPath(e.ParamName), e.Message, e);
                }
            }

            return new NetMapStructures(structures, fortifications);
        }

        public static string Write(NetMapStructures structures)
        {
            if (structures == null)
            {
                throw new ArgumentNullException(nameof(structures));
            }

            var writer = new JsonWriter();
            writer.BeginArray(StructuresItem);
            foreach (var layout in structures.Structures)
            {
                writer.BeginElement();
                writer.BeginArray(PiecesItem);
                for (var i = 0; i < layout.PieceCount; i++)
                {
                    writer.BeginElement();
                    writer.Int(MaxHealthItem, layout.MaxHealth(i));
                    writer.Bool(GroundedItem, layout.IsGrounded(i));
                    writer.EndObject();
                }

                writer.EndArray();
                writer.BeginArray(AdjacencyItem);
                foreach (var pair in layout.Adjacency)
                {
                    writer.BeginElement();
                    writer.Int(AItem, pair.A);
                    writer.Int(BItem, pair.B);
                    writer.EndObject();
                }

                writer.EndArray();
                writer.EndObject();
            }

            writer.EndArray();

            // 防衛物の無いマップ (Horde でないマップ) は前と同じ形のまま書く
            if (structures.Fortifications.Count > 0)
            {
                writer.BeginArray(FortificationsItem);
                foreach (var fortification in structures.Fortifications)
                {
                    writer.BeginElement();
                    writer.Int(StructureItem, fortification.Structure);
                    writer.Vector3(PositionItem, fortification.Position);
                    writer.Bool(StartsBuiltItem, fortification.StartsBuilt);
                    writer.String(NameKeyItem, fortification.NameKey);
                    writer.EndObject();
                }

                writer.EndArray();
            }

            return writer.Finish();
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
