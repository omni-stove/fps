using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class GrappleJsonTests
    {
        private static readonly string[] _expectedItemNames =
        {
            "id", "minRange", "maxRange", "pullSpeed", "pullAcceleration", "arriveDistance", "maxPullSeconds",
            "hangHeight", "edgeSnapDistance", "hangSeconds", "grabbableLayers", "maxCharges", "rechargeSeconds", "cooldownSeconds",
        };

        [Test]
        public void TheItemNamesFollowTheConstructors()
        {
            Assert.That(GrappleJson.ItemNames, Is.EqualTo(_expectedItemNames));
        }

        [Test]
        public void OnlyAnIdGivesTheKitDefaults()
        {
            var grapple = GrappleJson.Parse("{\"id\": \"grapple\"}", "grapple.json");

            Assert.That(grapple.Id, Is.EqualTo("grapple"));
            Assert.That(grapple.Spec.MinRange, Is.EqualTo(2f));
            Assert.That(grapple.Spec.MaxRange, Is.EqualTo(30f));
            Assert.That(grapple.Spec.PullSpeed, Is.EqualTo(18f));
            Assert.That(grapple.Spec.PullAcceleration, Is.EqualTo(60f));
            Assert.That(grapple.Spec.ArriveDistance, Is.EqualTo(0.3f));
            Assert.That(grapple.Spec.MaxPullSeconds, Is.EqualTo(3f));
            Assert.That(grapple.Spec.HangHeight, Is.EqualTo(0.8f));
            Assert.That(grapple.Spec.EdgeSnapDistance, Is.EqualTo(1.5f));
            Assert.That(grapple.Spec.HangSeconds, Is.EqualTo(0.5f));
            Assert.That(grapple.Ability.MaxCharges, Is.EqualTo(1));
            Assert.That(grapple.Ability.RechargeSeconds, Is.EqualTo(10f));
            Assert.That(grapple.Ability.CooldownSeconds, Is.EqualTo(1f));
            Assert.That(grapple.Ability.WindupSeconds, Is.Zero);
            Assert.That(grapple.GrabbableLayers, Is.EqualTo(~(1 << 2)));
            Assert.That(GrappleType.DefaultGrabbableLayers, Is.EqualTo(-5));
        }

        [Test]
        public void ReadsEveryItem()
        {
            var grapple = GrappleJson.Parse(
                "{\"id\": \"hook\", \"minRange\": 1, \"maxRange\": 20, \"pullSpeed\": 12, \"pullAcceleration\": 40,"
                + " \"arriveDistance\": 0.5, \"maxPullSeconds\": 2, \"hangHeight\": 0.6, \"edgeSnapDistance\": 1,"
                + " \"hangSeconds\": 0.75, \"grabbableLayers\": 1, \"maxCharges\": 2, \"rechargeSeconds\": 8, \"cooldownSeconds\": 0.5}",
                "grapple.json");

            Assert.That(grapple.Id, Is.EqualTo("hook"));
            Assert.That(grapple.Spec.MinRange, Is.EqualTo(1f));
            Assert.That(grapple.Spec.MaxRange, Is.EqualTo(20f));
            Assert.That(grapple.Spec.PullSpeed, Is.EqualTo(12f));
            Assert.That(grapple.Spec.PullAcceleration, Is.EqualTo(40f));
            Assert.That(grapple.Spec.ArriveDistance, Is.EqualTo(0.5f));
            Assert.That(grapple.Spec.MaxPullSeconds, Is.EqualTo(2f));
            Assert.That(grapple.Spec.HangHeight, Is.EqualTo(0.6f));
            Assert.That(grapple.Spec.EdgeSnapDistance, Is.EqualTo(1f));
            Assert.That(grapple.Spec.HangSeconds, Is.EqualTo(0.75f));
            Assert.That(grapple.GrabbableLayers, Is.EqualTo(1));
            Assert.That(grapple.Grabs(0), Is.True);
            Assert.That(grapple.Grabs(1), Is.False);
            Assert.That(grapple.Ability.MaxCharges, Is.EqualTo(2));
            Assert.That(grapple.Ability.RechargeSeconds, Is.EqualTo(8f));
            Assert.That(grapple.Ability.CooldownSeconds, Is.EqualTo(0.5f));
        }

        [Test]
        public void TheIdIsRequired()
        {
            var e = Assert.Throws<TuningJsonException>(() => GrappleJson.Parse("{\"maxRange\": 20}", "grapple.json"));

            Assert.That(e.Item, Is.EqualTo("id"));
        }

        [Test]
        public void ABlankIdIsRejectedByName()
        {
            var e = Assert.Throws<TuningJsonException>(() => GrappleJson.Parse("{\"id\": \" \"}", "grapple.json"));

            Assert.That(e.Item, Is.EqualTo("id"));
        }

        [Test]
        public void AnUnknownItemIsRejected()
        {
            var e = Assert.Throws<TuningJsonException>(() => GrappleJson.Parse("{\"id\": \"grapple\", \"grabbableTags\": []}", "grapple.json"));

            Assert.That(e.Message, Does.Contain("grabbableTags"));
        }

        [Test]
        public void AnInvalidValueIsRejectedByItem()
        {
            var range = Assert.Throws<TuningJsonException>(
                () => GrappleJson.Parse("{\"id\": \"grapple\", \"minRange\": 40}", "grapple.json"));
            var charges = Assert.Throws<TuningJsonException>(
                () => GrappleJson.Parse("{\"id\": \"grapple\", \"maxCharges\": 0}", "grapple.json"));
            var speed = Assert.Throws<TuningJsonException>(
                () => GrappleJson.Parse("{\"id\": \"grapple\", \"pullSpeed\": -1}", "grapple.json"));

            Assert.That(range.Item, Is.EqualTo("minRange"));
            Assert.That(charges.Item, Is.EqualTo("maxCharges"));
            Assert.That(speed.Item, Is.EqualTo("pullSpeed"));
            Assert.That(range.SourceName, Is.EqualTo("grapple.json"));
        }

        [Test]
        public void GrabsOnlyLayersInTheMask()
        {
            var grapple = new GrappleType("grapple", GrappleSpec.Default, AbilitySpec.Default, (1 << 0) | (1 << 5));

            Assert.That(grapple.Grabs(0), Is.True);
            Assert.That(grapple.Grabs(5), Is.True);
            Assert.That(grapple.Grabs(2), Is.False);
            Assert.That(grapple.Grabs(-1), Is.False);
            Assert.That(grapple.Grabs(32), Is.False);
        }
    }
}
