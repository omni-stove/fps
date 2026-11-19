using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class PayloadJsonTests
    {
        private const string _source = "payload.json";

        private static readonly string[] _expectedItemNames =
        {
            "cartSpeed", "minPushers", "pusherSpeedMultipliers", "rollbackDelaySeconds", "rollbackSpeed", "checkpointBonusSeconds", "timeLimitSeconds",
        };

        private static readonly float[] _expectedMultipliers = { 1f, 1.25f };
        private static readonly float[] _defaultMultipliers = { 1f, 1.5f, 2f };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = PayloadJson.Parse(
                "{\"cartSpeed\": 2.5, \"minPushers\": 2, \"pusherSpeedMultipliers\": [1, 1.25], \"rollbackDelaySeconds\": 8,"
                + " \"rollbackSpeed\": 0, \"checkpointBonusSeconds\": 45, \"timeLimitSeconds\": 240}",
                _source);

            Assert.That(spec.CartSpeed, Is.EqualTo(2.5f));
            Assert.That(spec.MinPushers, Is.EqualTo(2));
            Assert.That(spec.PusherSpeedMultipliers, Is.EqualTo(_expectedMultipliers));
            Assert.That(spec.RollbackDelaySeconds, Is.EqualTo(8f));
            Assert.That(spec.RollbackSpeed, Is.Zero);
            Assert.That(spec.CheckpointBonusSeconds, Is.EqualTo(45f));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(240f));
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var spec = PayloadJson.Parse("{}", _source);

            Assert.That(spec.CartSpeed, Is.EqualTo(1.5f));
            Assert.That(spec.MinPushers, Is.EqualTo(1));
            Assert.That(spec.PusherSpeedMultipliers, Is.EqualTo(_defaultMultipliers));
            Assert.That(spec.RollbackDelaySeconds, Is.EqualTo(10f));
            Assert.That(spec.RollbackSpeed, Is.EqualTo(0.5f));
            Assert.That(spec.CheckpointBonusSeconds, Is.EqualTo(60f));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(300f));
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(PayloadJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(PayloadJson.ResourcePath, Is.EqualTo("Tuning/payload"));
        }

        [Test]
        public void ABadValueNamesItsItem()
        {
            var speed = Reject("{\"cartSpeed\": 0}");
            var pushers = Reject("{\"minPushers\": 0}");
            var empty = Reject("{\"pusherSpeedMultipliers\": []}");
            var multiplier = Reject("{\"pusherSpeedMultipliers\": [1, -2]}");
            var delay = Reject("{\"rollbackDelaySeconds\": -1}");
            var limit = Reject("{\"timeLimitSeconds\": 0}");
            var unknown = Reject("{\"cartSpeeds\": 2}");

            Assert.That(speed.Item, Is.EqualTo("cartSpeed"));
            Assert.That(speed.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(pushers.Item, Is.EqualTo("minPushers"));
            Assert.That(empty.Item, Is.EqualTo("pusherSpeedMultipliers"));
            Assert.That(multiplier.Item, Is.EqualTo("pusherSpeedMultipliers"));
            Assert.That(delay.Item, Is.EqualTo("rollbackDelaySeconds"));
            Assert.That(limit.Item, Is.EqualTo("timeLimitSeconds"));
            Assert.That(unknown.Message, Does.Contain("cartSpeeds"));
        }

        private static TuningJsonException Reject(string json)
        {
            return Assert.Throws<TuningJsonException>(() => PayloadJson.Parse(json, _source));
        }
    }
}
