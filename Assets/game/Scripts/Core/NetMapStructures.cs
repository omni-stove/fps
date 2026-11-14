using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// 1 つの対戦マップの壊せる建物の部品の並び (<c>server-data/maps/&lt;マップ&gt;.structures.json</c>)。
    /// .fpsmap は部品の形しか持たないので、Unity 抜きのサーバーが同じ耐久値と崩れ方で壊せるよう、Unity の Editor が建物ごとの
    /// <see cref="StructureLayout"/> を書き出す (<see cref="Write"/>)。並びは .fpsmap の建物の番号 (<see cref="MapColliderGroups.Piece"/>) の順。
    /// </summary>
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

        private const string _kind = "map structures JSON";

        private static readonly string[] _topItems = { StructuresItem };
        private static readonly string[] _structureItems = { PiecesItem, AdjacencyItem };
        private static readonly string[] _pieceItems = { MaxHealthItem, GroundedItem };
        private static readonly string[] _pairItems = { AItem, BItem };

        public NetMapStructures(IReadOnlyList<StructureLayout> structures)
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
        }

        public IReadOnlyList<StructureLayout> Structures { get; }

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

            return new NetMapStructures(structures);
        }

        public static string Write(NetMapStructures structures)
        {
            if (structures == null)
            {
                throw new ArgumentNullException(nameof(structures));
            }

            var writer = new ServerDataJson.Writer();
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
            return writer.Finish();
        }
    }
}
