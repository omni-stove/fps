using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ServerDataJsonFieldsTests
    {
        private const string _source = "test.json";
        private const string _kind = "test JSON";
        private const string _kindNames = "Object, Array, String, Number, True, False, Null";

        private static readonly string[] _known = { "d", "kinds", "kind", "inner" };
        private static readonly string[] _innerKnown = { "y" };
        private static readonly JsonNodeKind[] _fallbackKinds = { JsonNodeKind.True };

        [Test]
        public void ReadDoubleReadsTheFullPrecision()
        {
            Assert.That(Fields("{\"d\": 0.1}").ReadDouble("d", 2.5), Is.EqualTo(0.1));
            Assert.That(Fields("{\"d\": -3}").ReadDouble("d", 2.5), Is.EqualTo(-3.0));
            Assert.That(Fields("{\"d\": 1e-300}").ReadDouble("d", 2.5), Is.EqualTo(1e-300));
        }

        [Test]
        public void ReadDoubleKeepsTheFallbackWhenTheItemIsLeftOut()
        {
            Assert.That(Fields("{}").ReadDouble("d", 2.5), Is.EqualTo(2.5));
        }

        [Test]
        public void ReadDoubleRejectsWhatIsNotANumber()
        {
            var e = Assert.Throws<ServerDataJsonException>(() => Fields("{\"d\": \"x\"}").ReadDouble("d", 0));

            Assert.That(e.SourceName, Is.EqualTo(_source));
            Assert.That(e.Item, Is.EqualTo("d"));
            Assert.That(e.Message, Is.EqualTo("test JSON 'test.json' item 'd' must be a number, but is the string \"x\" (line 1, column 7)"));
        }

        [Test]
        public void ReadDoubleRejectsNull()
        {
            var e = Assert.Throws<ServerDataJsonException>(() => Fields("{\"d\": null}").ReadDouble("d", 0));

            Assert.That(e.Item, Is.EqualTo("d"));
            Assert.That(e.Message, Does.Contain("must not be null; leave the item out to use the default"));
        }

        [Test]
        public void ReadDoubleNamesTheNestedPath()
        {
            var inner = Fields("{\"inner\": {\"y\": true}}").Object("inner", _innerKnown);

            var e = Assert.Throws<ServerDataJsonException>(() => inner.ReadDouble("y", 0));

            Assert.That(e.Item, Is.EqualTo("inner.y"));
        }

        [Test]
        public void ReadEnumArrayReadsEveryName()
        {
            var read = Fields("{\"kinds\": [\"Array\", \"Null\", \"Array\"]}").ReadEnumArray("kinds", _fallbackKinds);

            Assert.That(read, Is.EqualTo(new[] { JsonNodeKind.Array, JsonNodeKind.Null, JsonNodeKind.Array }));
        }

        [Test]
        public void ReadEnumArrayKeepsTheFallbackWhenTheItemIsLeftOut()
        {
            Assert.That(Fields("{}").ReadEnumArray("kinds", _fallbackKinds), Is.SameAs(_fallbackKinds));
        }

        [Test]
        public void ReadEnumArrayLeavesAnEmptyArrayToTheCaller()
        {
            Assert.That(Fields("{\"kinds\": []}").ReadEnumArray("kinds", _fallbackKinds), Is.Empty);
        }

        [Test]
        public void ReadEnumArrayRejectsWhatIsNotAnArray()
        {
            var e = Assert.Throws<ServerDataJsonException>(() => Fields("{\"kinds\": \"Array\"}").ReadEnumArray("kinds", _fallbackKinds));

            Assert.That(e.Item, Is.EqualTo("kinds"));
            Assert.That(
                e.Message,
                Is.EqualTo("test JSON 'test.json' item 'kinds' must be an array of the names " + _kindNames + ", but is the string \"Array\" (line 1, column 11)"));
        }

        [Test]
        public void ReadEnumArrayNamesTheArrayForABadElement()
        {
            var e = Assert.Throws<ServerDataJsonException>(() => Fields("{\"kinds\": [\"Array\", \"Nope\"]}").ReadEnumArray("kinds", _fallbackKinds));

            Assert.That(e.Item, Is.EqualTo("kinds"));
            Assert.That(e.Message, Is.EqualTo("test JSON 'test.json' item 'kinds' must be one of " + _kindNames + ", but is \"Nope\" (line 1, column 21)"));
        }

        [Test]
        public void ReadEnumArrayTakesOnlyTheExactNames()
        {
            Assert.Throws<ServerDataJsonException>(() => Fields("{\"kinds\": [\"array\"]}").ReadEnumArray("kinds", _fallbackKinds));
            Assert.Throws<ServerDataJsonException>(() => Fields("{\"kinds\": [\"1\"]}").ReadEnumArray("kinds", _fallbackKinds));
            Assert.Throws<ServerDataJsonException>(() => Fields("{\"kinds\": [1]}").ReadEnumArray("kinds", _fallbackKinds));
        }

        [Test]
        public void TheDefaultWordingAsksForAStringAndSaysWhatWasWritten()
        {
            var notText = Assert.Throws<ServerDataJsonException>(() => Fields("{\"kind\": 1}").ReadEnum("kind", JsonNodeKind.True));
            var unknown = Assert.Throws<ServerDataJsonException>(() => Fields("{\"kind\": \"Nope\"}").ReadEnum("kind", JsonNodeKind.True));

            Assert.That(notText.Message, Is.EqualTo("test JSON 'test.json' item 'kind' must be a string, but is the number 1 (line 1, column 10)"));
            Assert.That(unknown.Message, Is.EqualTo("test JSON 'test.json' item 'kind' must be one of " + _kindNames + ", but is \"Nope\" (line 1, column 10)"));
        }

        [Test]
        public void AWordingThatListsNamesSaysSoForEnums()
        {
            var wording = new ServerDataJson.Wording("known items here are", true);

            var notText = Assert.Throws<ServerDataJsonException>(() => Fields("{\"kind\": 1}", wording).ReadEnum("kind", JsonNodeKind.True));
            var unknown = Assert.Throws<ServerDataJsonException>(() => Fields("{\"kind\": \"Nope\"}", wording).ReadEnum("kind", JsonNodeKind.True));
            var element = Assert.Throws<ServerDataJsonException>(
                () => Fields("{\"kinds\": [\"Nope\"]}", wording).ReadEnumArray("kinds", _fallbackKinds));

            Assert.That(notText.Message, Is.EqualTo("test JSON 'test.json' item 'kind' must be one of the names " + _kindNames + ", but is the number 1 (line 1, column 10)"));
            Assert.That(unknown.Message, Is.EqualTo("test JSON 'test.json' item 'kind' \"Nope\" is not one of " + _kindNames + " (line 1, column 10)"));
            Assert.That(element.Message, Is.EqualTo("test JSON 'test.json' item 'kinds' \"Nope\" is not one of " + _kindNames + " (line 1, column 12)"));
        }

        [Test]
        public void TheWordingChoosesHowTheKnownItemsAreIntroduced()
        {
            var byDefault = Assert.Throws<ServerDataJsonException>(() => Fields("{\"x\": 1}"));
            var flat = Assert.Throws<ServerDataJsonException>(() => Fields("{\"x\": 1}", new ServerDataJson.Wording("known items are", false)));

            Assert.That(byDefault.Item, Is.EqualTo("x"));
            Assert.That(byDefault.Message, Is.EqualTo("test JSON 'test.json': unknown item 'x' (line 1, column 7); known items here are d, kinds, kind, inner"));
            Assert.That(flat.Item, Is.EqualTo("x"));
            Assert.That(flat.Message, Is.EqualTo("test JSON 'test.json': unknown item 'x' (line 1, column 7); known items are d, kinds, kind, inner"));
        }

        [Test]
        public void NestedObjectsAndElementsKeepTheWording()
        {
            var wording = new ServerDataJson.Wording("known items are", true);
            var top = Fields("{\"inner\": {\"x\": 1}, \"kinds\": [{\"z\": 2}]}", wording);

            var nested = Assert.Throws<ServerDataJsonException>(() => top.Object("inner", _innerKnown));
            var element = Assert.Throws<ServerDataJsonException>(() => top.Element(top.Array("kinds")[0], "kinds", 0, _innerKnown));

            Assert.That(nested.Item, Is.EqualTo("inner.x"));
            Assert.That(nested.Message, Does.EndWith("; known items are y"));
            Assert.That(element.Item, Is.EqualTo("kinds[0].z"));
            Assert.That(element.Message, Does.EndWith("; known items are y"));
            Assert.That(top.Words, Is.SameAs(wording));
        }

        [Test]
        public void TheFiveArgumentConstructorUsesTheDefaultWording()
        {
            Assert.That(Fields("{}").Words, Is.SameAs(ServerDataJson.Wording.Default));
            Assert.That(ServerDataJson.Wording.Default.KnownItemsLead, Is.EqualTo("known items here are"));
            Assert.That(ServerDataJson.Wording.Default.EnumErrorsListNames, Is.False);
        }

        [Test]
        public void AWordingNeedsALead()
        {
            Assert.Throws<ArgumentException>(() => new ServerDataJson.Wording(string.Empty, false));
            Assert.Throws<ArgumentException>(() => new ServerDataJson.Wording(null, false));
        }

        [Test]
        public void FieldsNeedAWording()
        {
            var root = ServerDataJson.ParseDocument("{}", _source, _kind);

            Assert.Throws<ArgumentNullException>(() => new ServerDataJson.Fields(_source, _kind, root, string.Empty, _known, null));
        }

        [Test]
        public void RethrownGivesBackTheValue()
        {
            Assert.That(ServerDataJson.Rethrown(() => 7, e => new InvalidOperationException(e.Message)), Is.EqualTo(7));
        }

        [Test]
        public void RethrownRewrapsOnlyServerDataErrors()
        {
            var cause = new ServerDataJsonException(_source, "d", "bad d");

            var rewrapped = Assert.Throws<InvalidOperationException>(
                () => ServerDataJson.Rethrown<int>(() => throw cause, e => new InvalidOperationException(e.Message, e)));
            Assert.That(rewrapped.Message, Is.EqualTo("bad d"));
            Assert.That(rewrapped.InnerException, Is.SameAs(cause));

            Assert.Throws<KeyNotFoundException>(
                () => ServerDataJson.Rethrown<int>(() => throw new KeyNotFoundException(), e => new InvalidOperationException(e.Message, e)));
        }

        [Test]
        public void RethrownNeedsBothFunctions()
        {
            Assert.Throws<ArgumentNullException>(() => ServerDataJson.Rethrown<int>(null, e => e));
            Assert.Throws<ArgumentNullException>(() => ServerDataJson.Rethrown(() => 1, null));
        }

        private static ServerDataJson.Fields Fields(string text, ServerDataJson.Wording wording = null)
        {
            var root = ServerDataJson.ParseDocument(text, _source, _kind);
            return wording == null
                ? new ServerDataJson.Fields(_source, _kind, root, string.Empty, _known)
                : new ServerDataJson.Fields(_source, _kind, root, string.Empty, _known, wording);
        }
    }
}
