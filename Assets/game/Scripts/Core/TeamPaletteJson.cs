using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Shooter.Core
{
    public static class TeamPaletteJson
    {
        public const string ResourcePath = "Tuning/palettes";

        public const string StandardItem = "standard";

        public const string RedGreenItem = "redGreen";

        public const string BlueYellowItem = "blueYellow";

        public const string FriendlyItem = "friendly";

        public const string EnemyItem = "enemy";

        public const string NeutralItem = "neutral";

        public const string HighlightItem = "highlight";

        public const string ObjectiveItem = "objective";

        public const string DangerItem = "danger";

        public const string ContestedItem = "contested";

        public const string SafeItem = "safe";

        public const string UsesMarksItem = "usesMarks";

        private const string _kind = "palettes JSON";

        private const string _colorForms = "\"#RRGGBB\" or an array of 3 numbers (r, g, b) from 0 to 1";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            StandardItem,
            RedGreenItem,
            BlueYellowItem,
        };

        public static IReadOnlyList<string> PaletteItemNames { get; } = new[]
        {
            FriendlyItem,
            EnemyItem,
            NeutralItem,
            HighlightItem,
            ObjectiveItem,
            DangerItem,
            ContestedItem,
            SafeItem,
            UsesMarksItem,
        };

        public static IReadOnlyList<TeamPalette> Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                return new ReadOnlyCollection<TeamPalette>(new[]
                {
                    ReadPalette(fields, StandardItem, TeamPalette.Standard),
                    ReadPalette(fields, RedGreenItem, TeamPalette.RedGreen),
                    ReadPalette(fields, BlueYellowItem, TeamPalette.BlueYellow),
                });
            }
            catch (ServerDataJsonException e)
            {
                // KitTuning は TuningJsonException だけを「読めないので既定値で続ける」と扱う。
                var inner = e.InnerException is ArgumentException rejected ? (Exception)rejected : e;
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, inner);
            }
        }

        private static TeamPalette ReadPalette(ServerDataJson.Fields root, string name, TeamPalette fallback)
        {
            var fields = root.Object(name, PaletteItemNames);
            if (fields == null)
            {
                return fallback;
            }

            var friendly = ReadColor(fields, FriendlyItem, fallback.Friendly);
            var enemy = ReadColor(fields, EnemyItem, fallback.Enemy);
            if (friendly == enemy)
            {
                throw fields.Rejected(
                    fields.ItemPath(EnemyItem), $"{enemy} is the same as '{fields.ItemPath(FriendlyItem)}', so the two teams cannot be told apart");
            }

            return new TeamPalette(
                fallback.Kind,
                friendly,
                enemy,
                ReadColor(fields, NeutralItem, fallback.Neutral),
                ReadColor(fields, HighlightItem, fallback.Highlight),
                ReadColor(fields, ObjectiveItem, fallback.Objective),
                ReadColor(fields, DangerItem, fallback.Danger),
                ReadColor(fields, ContestedItem, fallback.Contested),
                ReadColor(fields, SafeItem, fallback.Safe),
                fields.ReadBool(UsesMarksItem, fallback.UsesMarks));
        }

        private static PaletteColor ReadColor(ServerDataJson.Fields fields, string name, PaletteColor fallback)
        {
            var node = fields.Find(name);
            if (node == null)
            {
                return fallback;
            }

            var item = fields.ItemPath(name);
            switch (node.Kind)
            {
                case GameRulesJson.NodeKind.String:
                    return FromHex(fields, item, node);
                case GameRulesJson.NodeKind.Array:
                    return FromComponents(fields, item, node);
                default:
                    throw fields.Invalid(item, node, $"must be {_colorForms}, but is {node.Describe()}");
            }
        }

        // FromBytes を通すのは、既定の組の FromBytes で決めた色と同じ値に読めるようにするため。
        private static PaletteColor FromHex(ServerDataJson.Fields fields, string item, GameRulesJson.Node node)
        {
            var text = node.Text;
            if (text.Length != 7 || text[0] != '#')
            {
                throw fields.Invalid(item, node, $"must be {_colorForms}, but is \"{text}\"");
            }

            for (var i = 1; i < text.Length; i++)
            {
                if (!IsHexDigit(text[i]))
                {
                    throw fields.Invalid(item, node, $"must be {_colorForms}, but \"{text}\" has a character that is not a hex digit");
                }
            }

            return PaletteColor.FromBytes(HexByte(text, 1), HexByte(text, 3), HexByte(text, 5));
        }

        private static PaletteColor FromComponents(ServerDataJson.Fields fields, string item, GameRulesJson.Node node)
        {
            if (node.Elements.Count != 3)
            {
                throw fields.Invalid(
                    item, node, $"must have 3 numbers (r, g, b), but has {node.Elements.Count.ToString(CultureInfo.InvariantCulture)}");
            }

            return new PaletteColor(
                Component(fields, item, node, 0),
                Component(fields, item, node, 1),
                Component(fields, item, node, 2));
        }

        private static float Component(ServerDataJson.Fields fields, string item, GameRulesJson.Node array, int index)
        {
            var element = array.Elements[index];
            var path = item + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
            var value = fields.ToFloat(path, element);
            if (!(value >= 0f && value <= 1f))
            {
                throw fields.Invalid(path, element, $"must be from 0 to 1, but is {element.Text}");
            }

            return value;
        }

        private static bool IsHexDigit(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        private static byte HexByte(string text, int start)
        {
            return byte.Parse(text.AsSpan(start, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        }
    }
}
