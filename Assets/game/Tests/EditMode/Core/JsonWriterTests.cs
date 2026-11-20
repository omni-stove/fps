using System.Globalization;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class JsonWriterTests
    {
        private static readonly string[] _ids = { "a", "b" };
        private static readonly float[] _floats = { 0.5f, 2f };
        private static readonly JsonNodeKind[] _kinds = { JsonNodeKind.Array, JsonNodeKind.Null };

        [Test]
        public void EmptyWriterIsAnEmptyObject()
        {
            Assert.That(new JsonWriter().Finish(), Is.EqualTo("{\n}\n"));
        }

        [Test]
        public void IndentsNestedObjectsAndArrays()
        {
            var writer = new JsonWriter();
            writer.Int("count", 3);
            writer.BeginObject("inner");
            writer.Bool("on", true);
            writer.Bool("off", false);
            writer.EndObject();
            writer.BeginArray("list");
            writer.BeginElement();
            writer.String("id", "a");
            writer.EndObject();
            writer.Element("{\"id\": \"b\"}");
            writer.EndArray();

            Assert.That(
                writer.Finish(),
                Is.EqualTo(
                    "{\n"
                    + "  \"count\": 3,\n"
                    + "  \"inner\": {\n"
                    + "    \"on\": true,\n"
                    + "    \"off\": false\n"
                    + "  },\n"
                    + "  \"list\": [\n"
                    + "    {\n"
                    + "      \"id\": \"a\"\n"
                    + "    },\n"
                    + "    {\"id\": \"b\"}\n"
                    + "  ]\n"
                    + "}\n"));
        }

        [Test]
        public void WritesEveryValueShape()
        {
            var writer = new JsonWriter();
            writer.Float("f", -2.5f);
            writer.Int("i", -7);
            writer.Enum("kind", JsonNodeKind.Array);
            writer.Vector3("p", new Vector3(1f, 2.5f, -3f));
            writer.StringArray("ids", _ids);
            writer.StringArray("none", System.Array.Empty<string>());
            writer.FloatArray("fs", _floats);
            writer.Embed("rules", "{\n  \"x\": 1\n}\n");

            Assert.That(
                writer.Finish(),
                Is.EqualTo(
                    "{\n"
                    + "  \"f\": -2.5,\n"
                    + "  \"i\": -7,\n"
                    + "  \"kind\": \"Array\",\n"
                    + "  \"p\": [1, 2.5, -3],\n"
                    + "  \"ids\": [\"a\", \"b\"],\n"
                    + "  \"none\": [],\n"
                    + "  \"fs\": [0.5, 2],\n"
                    + "  \"rules\": {\n"
                    + "    \"x\": 1\n"
                    + "  }\n"
                    + "}\n"));
        }

        [Test]
        public void QuoteEscapesWhatJsonRequires()
        {
            Assert.That(JsonWriter.Quote("a\"b\\c\nd\re\tf\u0001g/hé"), Is.EqualTo("\"a\\\"b\\\\c\\nd\\re\\tf\\u0001g/hé\""));
            Assert.That(JsonWriter.Quote(string.Empty), Is.EqualTo("\"\""));
            Assert.That(JsonWriter.Quote(null), Is.EqualTo("\"\""));
        }

        [Test]
        public void QuotesNamesToo()
        {
            var writer = new JsonWriter();
            writer.Int("a\"b", 1);

            Assert.That(writer.Finish(), Is.EqualTo("{\n  \"a\\\"b\": 1\n}\n"));
        }

        [TestCase(0f, "0")]
        [TestCase(1f, "1")]
        [TestCase(-2.5f, "-2.5")]
        [TestCase(0.1f, "0.1")]
        public void FloatTextIsTheShortForm(float value, string expected)
        {
            Assert.That(JsonWriter.FloatText(value), Is.EqualTo(expected));
        }

        [TestCase(1f / 3f)]
        [TestCase(0.1f + 0.2f)]
        [TestCase(float.MaxValue)]
        [TestCase(float.Epsilon)]
        [TestCase(-123456.79f)]
        public void FloatTextReadsBackToTheSameFloat(float value)
        {
            var text = JsonWriter.FloatText(value);

            Assert.That(float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture), Is.EqualTo(value));
        }

        [Test]
        public void WritesDoublesAndEnumArrays()
        {
            var writer = new JsonWriter();
            writer.Double("d", 0.1);
            writer.Double("whole", 2.0);
            writer.EnumArray("kinds", _kinds);
            writer.EnumArray("none", System.Array.Empty<JsonNodeKind>());

            Assert.That(
                writer.Finish(),
                Is.EqualTo(
                    "{\n"
                    + "  \"d\": 0.1,\n"
                    + "  \"whole\": 2,\n"
                    + "  \"kinds\": [\"Array\", \"Null\"],\n"
                    + "  \"none\": []\n"
                    + "}\n"));
        }

        [TestCase(1.0 / 3.0)]
        [TestCase(0.1 + 0.2)]
        [TestCase(double.MaxValue)]
        [TestCase(double.Epsilon)]
        [TestCase(-1.5e-300)]
        public void DoubleTextReadsBackToTheSameDouble(double value)
        {
            var text = JsonWriter.DoubleText(value);

            Assert.That(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture), Is.EqualTo(value));
        }

        [Test]
        public void NodeTextWritesTheTreeOnOneLineKeepingNumbersAsWritten()
        {
            var root = new JsonParser("{\n  \"a\": [1.50, -2e3, true, false, null, \"q\\\"x\\n\"],\n  \"b\": {},\n  \"c\": []\n}").ParseDocument();

            Assert.That(
                JsonWriter.NodeText(root),
                Is.EqualTo("{\"a\": [1.50, -2e3, true, false, null, \"q\\\"x\\n\"], \"b\": {}, \"c\": []}"));
        }

        [Test]
        public void NodeTextReadsBackToTheSameTree()
        {
            var text = JsonWriter.NodeText(new JsonParser("{\"rules\": {\"actions\": {\"cover\": \"On\"}, \"playerMaxHealth\": 150}}").ParseDocument());

            var root = new JsonParser(text).ParseDocument();

            Assert.That(root.Members[0].Key, Is.EqualTo("rules"));
            Assert.That(root.Members[0].Value.Members[0].Value.Members[0].Value.Text, Is.EqualTo("On"));
            Assert.That(root.Members[0].Value.Members[1].Value.Text, Is.EqualTo("150"));
        }

        [Test]
        public void NodeTextNeedsANode()
        {
            Assert.Throws<System.ArgumentNullException>(() => JsonWriter.NodeText(null));
        }

        [Test]
        public void ParserReadsBackWhatTheWriterWrote()
        {
            var writer = new JsonWriter();
            writer.String("text", "line\n\"quoted\"\t\u0002");
            writer.Float("f", 1f / 3f);
            writer.Vector3("p", new Vector3(0.1f, -0f, 1e-7f));

            var root = new JsonParser(writer.Finish()).ParseDocument();

            Assert.That(root.Members[0].Value.Text, Is.EqualTo("line\n\"quoted\"\t\u0002"));
            Assert.That(float.Parse(root.Members[1].Value.Text, NumberStyles.Float, CultureInfo.InvariantCulture), Is.EqualTo(1f / 3f));
            Assert.That(root.Members[2].Value.Kind, Is.EqualTo(JsonNodeKind.Array));
            Assert.That(root.Members[2].Value.Elements.Count, Is.EqualTo(3));
            Assert.That(float.Parse(root.Members[2].Value.Elements[2].Text, NumberStyles.Float, CultureInfo.InvariantCulture), Is.EqualTo(1e-7f));
        }
    }
}
