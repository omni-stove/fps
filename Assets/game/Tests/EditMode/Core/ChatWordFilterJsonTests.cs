using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ChatWordFilterJsonTests
    {
        private const string _source = "chat-words.json";

        private static readonly string[] _expectedItemNames = { "words", "mask", "action", "wholeWordsOnly" };

        private static readonly string[] _expectedWords = { "darn", "heck" };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = ChatWordFilterJson.Parse(
                "{\"words\": [\"Darn\", \" heck \", \"DARN\"], \"mask\": \"#\", \"action\": \"Drop\", \"wholeWordsOnly\": true}",
                _source);

            Assert.That(spec.Words, Is.EqualTo(_expectedWords), "trimmed, lowercased and merged");
            Assert.That(spec.Mask, Is.EqualTo('#'));
            Assert.That(spec.Action, Is.EqualTo(ChatWordFilterAction.Drop));
            Assert.That(spec.WholeWordsOnly, Is.True);
            Assert.That(spec.IsEmpty, Is.False);
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var spec = ChatWordFilterJson.Parse("{}", _source);

            Assert.That(spec.Words, Is.Empty);
            Assert.That(spec.IsEmpty, Is.True);
            Assert.That(spec.Mask, Is.EqualTo('*'));
            Assert.That(spec.Action, Is.EqualTo(ChatWordFilterAction.Mask));
            Assert.That(spec.WholeWordsOnly, Is.False);
            Assert.That(ChatWordFilterSpec.Default.Words, Is.Empty);
            Assert.That(ChatWordFilterSpec.Default.Mask, Is.EqualTo('*'));
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(ChatWordFilterJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(ChatWordFilterJson.ResourcePath, Is.EqualTo("Tuning/chat-words"));
        }

        [Test]
        public void ABadValueNamesItsItem()
        {
            var emptyWord = Reject("{\"words\": [\"ok\", \" \"]}");
            var notText = Reject("{\"words\": [1]}");
            var longMask = Reject("{\"mask\": \"**\"}");
            var blankMask = Reject("{\"mask\": \" \"}");
            var action = Reject("{\"action\": \"Ban\"}");
            var unknown = Reject("{\"list\": []}");
            var broken = Reject("{\"words\": ");

            Assert.That(emptyWord.Item, Is.EqualTo("words"));
            Assert.That(emptyWord.InnerException, Is.InstanceOf<ArgumentException>());
            Assert.That(notText.Item, Is.EqualTo("words[0]"));
            Assert.That(longMask.Item, Is.EqualTo("mask"));
            Assert.That(blankMask.Item, Is.EqualTo("mask"));
            Assert.That(action.Item, Is.EqualTo("action"));
            Assert.That(action.Message, Does.Contain("Mask, Drop"));
            Assert.That(unknown.Item, Is.EqualTo("list"));
            Assert.That(broken.Item, Is.Null);
            Assert.That(broken.SourceName, Is.EqualTo(_source));
            Assert.Throws<ArgumentNullException>(() => ChatWordFilterJson.Parse(null, _source));
        }

        private static TuningJsonException Reject(string json)
        {
            return Assert.Throws<TuningJsonException>(() => ChatWordFilterJson.Parse(json, _source));
        }
    }
}
