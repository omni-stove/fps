using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class KeySharingJsonTests
    {
        private static readonly string[] _expectedItemNames = { "shared" };
        private static readonly string[] _expectedRuleItemNames = { "actions", "prefer" };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = KeySharingJson.Parse(
                "{\"shared\": [{\"actions\": [\"Melee\", \"Interact\"], \"prefer\": \"Interact\"}, {\"actions\": [\"Crouch\", \"Prone\"]}]}",
                "key-sharing.json");

            Assert.That(spec.Shared.Count, Is.EqualTo(2));
            Assert.That(spec.Shared[0].Actions, Is.EqualTo(Names("Melee", "Interact")));
            Assert.That(spec.Shared[0].Prefer, Is.EqualTo("Interact"));
            Assert.That(spec.Shared[1].Actions, Is.EqualTo(Names("Crouch", "Prone")));
            Assert.That(spec.Shared[1].Prefer, Is.Null, "the game tells a tap from a hold itself");
        }

        [Test]
        public void LeftOutOrEmptySharingAllowsNothing()
        {
            var leftOut = KeySharingJson.Parse("{}", "key-sharing.json");
            var empty = KeySharingJson.Parse("{\"shared\": []}", "key-sharing.json");

            Assert.That(leftOut, Is.SameAs(KeySharingSpec.Default));
            Assert.That(KeySharingSpec.Default.Shared, Is.Empty);
            Assert.That(empty.Shared, Is.Empty);
            Assert.That(empty.Allows("Melee", "Interact"), Is.False);
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(KeySharingJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(KeySharingJson.RuleItemNames, Is.EqualTo(_expectedRuleItemNames));
            Assert.That(KeySharingJson.ResourcePath, Is.EqualTo("Tuning/key-sharing"));
        }

        [Test]
        public void ABadValueNamesItsItem()
        {
            var noActions = Reject("{\"shared\": [{\"prefer\": \"Interact\"}]}");
            var one = Reject("{\"shared\": [{\"actions\": [\"Melee\"]}]}");
            var twice = Reject("{\"shared\": [{\"actions\": [\"Melee\", \"melee\"]}]}");
            var blank = Reject("{\"shared\": [{\"actions\": [\"Melee\", \" \"]}]}");
            var stranger = Reject("{\"shared\": [{\"actions\": [\"Melee\", \"Interact\"], \"prefer\": \"Reload\"}]}");
            var unknown = Reject("{\"shared\": [{\"actions\": [\"Melee\", \"Interact\"], \"tap\": true}]}");
            var wrongType = Reject("{\"shared\": [{\"actions\": [\"Melee\", 3]}]}");
            var broken = Reject("{\"shared\": [");

            Assert.That(noActions.Item, Is.EqualTo("shared[0].actions"));
            Assert.That(noActions.InnerException, Is.InstanceOf<ArgumentNullException>());
            Assert.That(one.Item, Is.EqualTo("shared[0].actions"));
            Assert.That(twice.Item, Is.EqualTo("shared[0].actions"));
            Assert.That(blank.Item, Is.EqualTo("shared[0].actions"));
            Assert.That(stranger.Item, Is.EqualTo("shared[0].prefer"));
            Assert.That(stranger.Message, Does.Contain("'Reload'"));
            Assert.That(unknown.Item, Is.EqualTo("shared[0].tap"));
            Assert.That(wrongType.Item, Is.EqualTo("shared[0].actions[1]"));
            Assert.That(broken.Item, Is.Null);
            Assert.That(broken.SourceName, Is.EqualTo("key-sharing.json"));
        }

        [Test]
        public void AllowsOnlyActionsInTheSameRule()
        {
            var spec = new KeySharingSpec(Rules(
                new KeySharingRule(Names("Melee", "Interact"), "Interact"),
                new KeySharingRule(Names("Crouch", "Prone", "Dodge"))));

            Assert.That(spec.Allows("Melee", "Interact"), Is.True);
            Assert.That(spec.Allows("interact", " MELEE "), Is.True, "action names are looked up regardless of case, as Input System does");
            Assert.That(spec.Allows("Prone", "Dodge"), Is.True);
            Assert.That(spec.Allows("Melee", "Prone"), Is.False, "the two are in different rules");
            Assert.That(spec.Allows("Melee", "Reload"), Is.False);
            Assert.That(spec.Allows("Melee", "Melee"), Is.False, "two bindings of one action on one key are still a conflict");
            Assert.That(spec.Allows(null, "Interact"), Is.False);
            Assert.That(spec.Allows("Melee", " "), Is.False);
        }

        [Test]
        public void YieldsToNamesOnlyThePreferredActionOfAnotherAction()
        {
            var spec = new KeySharingSpec(Rules(
                new KeySharingRule(Names("Melee", "Interact"), "interact"),
                new KeySharingRule(Names("Reload", "Interact"), "Interact"),
                new KeySharingRule(Names("Melee", "Ping"), "Ping"),
                new KeySharingRule(Names("Crouch", "Prone"))));

            Assert.That(spec.Shared[0].Prefer, Is.EqualTo("Interact"), "prefer takes the spelling from actions");
            Assert.That(spec.YieldsTo("Melee"), Is.EqualTo(Names("Interact", "Ping")));
            Assert.That(spec.YieldsTo("Reload"), Is.EqualTo(Names("Interact")));
            Assert.That(spec.YieldsTo("Interact"), Is.Empty, "the preferred action never yields");
            Assert.That(spec.YieldsTo("Crouch"), Is.Empty, "a rule without prefer leaves both to the game");
            Assert.That(spec.YieldsTo("Jump"), Is.Empty);
            Assert.That(spec.YieldsTo(null), Is.Empty);
        }

        [Test]
        public void TheSpecRefusesBadValues()
        {
            Assert.Throws<ArgumentNullException>(() => new KeySharingSpec(null));
            Assert.Throws<ArgumentException>(() => new KeySharingSpec(Rules((KeySharingRule)null)));
            Assert.Throws<ArgumentNullException>(() => new KeySharingRule(null));
            Assert.Throws<ArgumentException>(() => new KeySharingRule(Names("Melee")));
            Assert.Throws<ArgumentException>(() => new KeySharingRule(Names("Melee", "Melee")));
            Assert.Throws<ArgumentException>(() => new KeySharingRule(Names("Melee", null)));
            Assert.Throws<ArgumentException>(() => new KeySharingRule(Names("Melee", "Interact"), "Reload"));
            Assert.That(new KeySharingRule(Names(" Melee ", "Interact"), " ").Actions, Is.EqualTo(Names("Melee", "Interact")));
            Assert.That(new KeySharingRule(Names("Melee", "Interact"), " ").Prefer, Is.Null);
        }

        private static string[] Names(params string[] names)
        {
            return names;
        }

        private static KeySharingRule[] Rules(params KeySharingRule[] rules)
        {
            return rules;
        }

        private static TuningJsonException Reject(string text)
        {
            return Assert.Throws<TuningJsonException>(() => KeySharingJson.Parse(text, "key-sharing.json"));
        }
    }
}
