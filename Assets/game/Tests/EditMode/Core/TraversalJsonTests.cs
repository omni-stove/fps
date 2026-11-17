using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class TraversalJsonTests
    {
        [Test]
        public void TheDefaultsAreTheMotionDefaults()
        {
            var spec = TraversalSpec.Default;

            Assert.That(spec.Slide.StartSpeed, Is.EqualTo(11f));
            Assert.That(spec.Slide.Deceleration, Is.EqualTo(8f));
            Assert.That(spec.Slide.EndSpeed, Is.EqualTo(3f));
            Assert.That(spec.Slide.MinSeconds, Is.EqualTo(0.4f));
            Assert.That(spec.Slide.MaxSeconds, Is.EqualTo(3f));
            Assert.That(spec.Slide.SlopeAcceleration, Is.EqualTo(20f));
            Assert.That(spec.Parachute.MinOpenHeight, Is.EqualTo(6f));
            Assert.That(spec.Parachute.DescentSpeed, Is.EqualTo(4f));
            Assert.That(spec.Parachute.HorizontalSpeed, Is.EqualTo(6f));
            Assert.That(spec.Parachute.SteerAcceleration, Is.EqualTo(8f));
            Assert.That(spec.Zipline.GrabRadius, Is.EqualTo(1.5f));
            Assert.That(spec.Zipline.HangOffset, Is.EqualTo(1.3f));
            Assert.That(spec.Zipline.RegrabSeconds, Is.EqualTo(0.5f));
            Assert.That(spec.Zipline.SpreadMoveFraction, Is.EqualTo(1f));
        }

        [Test]
        public void ReadsEveryItem()
        {
            var spec = TraversalJson.Parse(
                "{\"slide\": {\"startSpeed\": 22, \"deceleration\": 6, \"endSpeed\": 2, \"minSeconds\": 0.5, \"maxSeconds\": 4, \"slopeAcceleration\": 15},"
                + " \"parachute\": {\"minOpenHeight\": 10, \"descentSpeed\": 3, \"horizontalSpeed\": 7, \"steerAcceleration\": 5},"
                + " \"zipline\": {\"grabRadius\": 2, \"hangOffset\": 1.1, \"regrabSeconds\": 0.8, \"spreadMoveFraction\": 0.5}}",
                "traversal.json");

            Assert.That(spec.Slide.StartSpeed, Is.EqualTo(22f));
            Assert.That(spec.Slide.Deceleration, Is.EqualTo(6f));
            Assert.That(spec.Slide.EndSpeed, Is.EqualTo(2f));
            Assert.That(spec.Slide.MinSeconds, Is.EqualTo(0.5f));
            Assert.That(spec.Slide.MaxSeconds, Is.EqualTo(4f));
            Assert.That(spec.Slide.SlopeAcceleration, Is.EqualTo(15f));
            Assert.That(spec.Parachute.MinOpenHeight, Is.EqualTo(10f));
            Assert.That(spec.Parachute.DescentSpeed, Is.EqualTo(3f));
            Assert.That(spec.Parachute.HorizontalSpeed, Is.EqualTo(7f));
            Assert.That(spec.Parachute.SteerAcceleration, Is.EqualTo(5f));
            Assert.That(spec.Zipline.GrabRadius, Is.EqualTo(2f));
            Assert.That(spec.Zipline.HangOffset, Is.EqualTo(1.1f));
            Assert.That(spec.Zipline.RegrabSeconds, Is.EqualTo(0.8f));
            Assert.That(spec.Zipline.SpreadMoveFraction, Is.EqualTo(0.5f));
        }

        [Test]
        public void OmittedItemsAndGroupsUseTheDefaults()
        {
            var spec = TraversalJson.Parse("{\"slide\": {\"startSpeed\": 22}}", "traversal.json");

            Assert.That(spec.Slide.StartSpeed, Is.EqualTo(22f));
            Assert.That(spec.Slide.Deceleration, Is.EqualTo(SlideMotion.DefaultDeceleration));
            Assert.That(spec.Slide.MaxSeconds, Is.EqualTo(SlideMotion.DefaultMaxSeconds));
            Assert.That(spec.Parachute, Is.SameAs(ParachuteTuning.Default));
            Assert.That(spec.Zipline, Is.SameAs(ZiplineTuning.Default));

            var empty = TraversalJson.Parse("{}", "traversal.json");
            Assert.That(empty.Slide, Is.SameAs(SlideTuning.Default));
            Assert.That(empty.Parachute.DescentSpeed, Is.EqualTo(ParachuteMotion.DefaultDescentSpeed));
            Assert.That(empty.Zipline.GrabRadius, Is.EqualTo(ZiplineTuning.DefaultGrabRadius));
        }

        [Test]
        public void AnInvalidValueNamesItsGroupAndItem()
        {
            var slide = Assert.Throws<TuningJsonException>(() => TraversalJson.Parse("{\"slide\": {\"startSpeed\": 1}}", "traversal.json"));
            var parachute = Assert.Throws<TuningJsonException>(() => TraversalJson.Parse("{\"parachute\": {\"descentSpeed\": 0}}", "traversal.json"));
            var zipline = Assert.Throws<TuningJsonException>(() => TraversalJson.Parse("{\"zipline\": {\"spreadMoveFraction\": 1.5}}", "traversal.json"));

            Assert.That(slide.Item, Is.EqualTo("slide.startSpeed"), "1 is not above the default endSpeed 3");
            Assert.That(slide.SourceName, Is.EqualTo("traversal.json"));
            Assert.That(slide.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(parachute.Item, Is.EqualTo("parachute.descentSpeed"));
            Assert.That(zipline.Item, Is.EqualTo("zipline.spreadMoveFraction"));
            Assert.That(zipline.Message, Does.Contain("traversal JSON 'traversal.json' item 'zipline.spreadMoveFraction' is invalid"));
        }

        [Test]
        public void UnknownItemsWrongTypesAndBrokenJsonAreRejected()
        {
            var unknownGroup = Assert.Throws<TuningJsonException>(() => TraversalJson.Parse("{\"vault\": {}}", "traversal.json"));
            var unknownItem = Assert.Throws<TuningJsonException>(() => TraversalJson.Parse("{\"zipline\": {\"speed\": 3}}", "traversal.json"));
            var notAnObject = Assert.Throws<TuningJsonException>(() => TraversalJson.Parse("{\"slide\": 3}", "traversal.json"));
            var notANumber = Assert.Throws<TuningJsonException>(() => TraversalJson.Parse("{\"slide\": {\"endSpeed\": \"fast\"}}", "traversal.json"));
            var broken = Assert.Throws<TuningJsonException>(() => TraversalJson.Parse("{\"slide\": ", "traversal.json"));

            Assert.That(unknownGroup.Item, Is.EqualTo("vault"));
            Assert.That(unknownItem.Item, Is.EqualTo("zipline.speed"));
            Assert.That(notAnObject.Item, Is.EqualTo("slide"));
            Assert.That(notANumber.Item, Is.EqualTo("slide.endSpeed"));
            Assert.That(broken.Item, Is.Null);
            Assert.That(broken.Message, Does.Contain("is not valid JSON"));
        }

        [Test]
        public void TheTuningTypesRejectBadValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SlideTuning(maxSeconds: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ParachuteTuning(minOpenHeight: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZiplineTuning(grabRadius: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZiplineTuning(regrabSeconds: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZiplineTuning(spreadMoveFraction: -0.1f));
        }
    }
}
