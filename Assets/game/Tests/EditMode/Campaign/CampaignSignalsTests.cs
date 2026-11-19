using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class CampaignSignalsTests
    {
        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            GameSignals.ResetAll();
        }

        [Test]
        public void TheCampaignSignalsStartEmpty()
        {
            Assert.That(CampaignSignals.Tutorial.Value, Is.Null);
            Assert.That(CampaignSignals.Tutorial.HasValue, Is.False);
            Assert.That(CampaignSignals.Notice.Value, Is.Null);
            Assert.That(CampaignSignals.Notice.HasValue, Is.False);
        }

        [Test]
        public void TheCampaignSignalsHoldWhatWasPublished()
        {
            var status = new TutorialStatus(new TutorialSteps(), InputActionPaths.Move, InputDeviceKind.KeyboardMouse);
            var notice = new CampaignNotice();

            CampaignSignals.Tutorial.Publish(status);
            CampaignSignals.Notice.Publish(notice);

            Assert.That(CampaignSignals.Tutorial.Value, Is.EqualTo((TutorialStatus?)status));
            Assert.That(CampaignSignals.Notice.Value, Is.SameAs(notice));
        }

        [Test]
        public void ResetAllEmptiesTheCampaignSignalsAndDropsTheSubscribers()
        {
            var heardTutorial = new List<TutorialStatus?>();
            var heardNotice = new List<CampaignNotice>();
            CampaignSignals.Tutorial.Changed += heardTutorial.Add;
            CampaignSignals.Notice.Changed += heardNotice.Add;
            CampaignSignals.Tutorial.Publish(new TutorialStatus(new TutorialSteps(), InputActionPaths.Move, InputDeviceKind.KeyboardMouse));
            CampaignSignals.Notice.Publish(new CampaignNotice());
            heardTutorial.Clear();
            heardNotice.Clear();

            GameSignals.ResetAll();

            TheCampaignSignalsStartEmpty();
            CampaignSignals.Tutorial.Publish(new TutorialStatus(new TutorialSteps(), InputActionPaths.Reload, InputDeviceKind.KeyboardMouse));
            CampaignSignals.Notice.Publish(new CampaignNotice());
            Assert.That(heardTutorial, Is.Empty, "tutorial subscribers from before the reset are gone");
            Assert.That(heardNotice, Is.Empty, "notice subscribers from before the reset are gone");
        }
    }
}
