using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class JsonNodeTests
    {
        [Test]
        public void DescribesEachKind()
        {
            Assert.That(new JsonNode(JsonNodeKind.Object, null, 1, 1).Describe(), Is.EqualTo("an object"));
            Assert.That(new JsonNode(JsonNodeKind.Array, null, 1, 1).Describe(), Is.EqualTo("an array"));
            Assert.That(new JsonNode(JsonNodeKind.String, "hi", 1, 1).Describe(), Is.EqualTo("the string \"hi\""));
            Assert.That(new JsonNode(JsonNodeKind.Number, "-1.5e2", 1, 1).Describe(), Is.EqualTo("the number -1.5e2"));
            Assert.That(new JsonNode(JsonNodeKind.True, null, 1, 1).Describe(), Is.EqualTo("true"));
            Assert.That(new JsonNode(JsonNodeKind.False, null, 1, 1).Describe(), Is.EqualTo("false"));
            Assert.That(new JsonNode(JsonNodeKind.Null, null, 1, 1).Describe(), Is.EqualTo("null"));
        }

        [Test]
        public void KeepsWhatItWasGiven()
        {
            var node = new JsonNode(JsonNodeKind.Number, "007", 12, 34);

            Assert.That(node.Kind, Is.EqualTo(JsonNodeKind.Number));
            Assert.That(node.Text, Is.EqualTo("007"));
            Assert.That(node.Line, Is.EqualTo(12));
            Assert.That(node.Column, Is.EqualTo(34));
            Assert.That(node.Position, Is.EqualTo("line 12, column 34"));
            Assert.That(node.Members, Is.Empty);
            Assert.That(node.Elements, Is.Empty);
        }
    }
}
