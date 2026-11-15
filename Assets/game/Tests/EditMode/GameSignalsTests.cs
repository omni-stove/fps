using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fps.Tests.EditMode
{
    public sealed class GameSignalsTests
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
        public void EverySignalStartsEmpty()
        {
            Assert.That(GameSignals.LocalPlayer.Value, Is.Null);
            Assert.That(GameSignals.HudModel.Value, Is.Null);
            Assert.That(GameSignals.ModeStatus.Value, Is.Null);
            Assert.That(GameSignals.Domination.Value, Is.Null);
            Assert.That(GameSignals.Scoreboard.Value, Is.Null);
            Assert.That(GameSignals.InteractHint.Value, Is.Null);
            Assert.That(GameSignals.Tutorial.Value, Is.Null);
            Assert.That(GameSignals.KillCam.Value, Is.Null);
            Assert.That(GameSignals.Pings.Value, Is.Null);
            Assert.That(GameSignals.SquadOrder.Value, Is.Null);
            Assert.That(GameSignals.CampaignNotice.Value, Is.Null);
            Assert.That(GameSignals.MenuReturnReason.Value, Is.Null);
            Assert.That(GameSignals.HudDensity.Value, Is.Null);
            Assert.That(GameSignals.PauseTimeMode.Value, Is.Null);
            Assert.That(GameSignals.PaletteViewer.Value, Is.Null);
        }

        [Test]
        public void PublishSetsTheValueAndRaisesTheEventEveryTime()
        {
            var model = new HudModel();
            var heard = new List<HudModel>();
            GameSignals.HudModel.Changed += heard.Add;

            GameSignals.HudModel.Publish(model);
            GameSignals.HudModel.Publish(model);

            Assert.That(GameSignals.HudModel.Value, Is.SameAs(model));
            Assert.That(GameSignals.HudModel.HasValue, Is.True);
            Assert.That(heard, Is.EqualTo(new[] { model, model }), "publishing the same object again still notifies");
        }

        [Test]
        public void ValueTypeSignalsTellAPublishedDefaultFromNothing()
        {
            var heard = new List<Team?>();
            GameSignals.PaletteViewer.Changed += heard.Add;

            GameSignals.PaletteViewer.Publish(Team.Alpha);
            GameSignals.HudDensity.Publish(HudDensity.Full);
            GameSignals.PauseTimeMode.Publish(PauseTimeMode.KeepRunning);

            Assert.That(GameSignals.PaletteViewer.Value, Is.EqualTo((Team?)Team.Alpha));
            Assert.That(GameSignals.PaletteViewer.HasValue, Is.True, "Alpha is the enum's zero but still a value");
            Assert.That(GameSignals.HudDensity.Value, Is.EqualTo((HudDensity?)HudDensity.Full));
            Assert.That(GameSignals.PauseTimeMode.HasValue, Is.True);
            Assert.That(heard, Is.EqualTo(new Team?[] { Team.Alpha }));
        }

        [Test]
        public void ClearEmptiesTheValueAndNotifiesOnlyWhenSomethingWasThere()
        {
            var heard = new List<ModeStatus>();
            GameSignals.ModeStatus.Changed += heard.Add;

            GameSignals.ModeStatus.Clear();
            Assert.That(heard, Is.Empty, "clearing nothing is silent");

            GameSignals.ModeStatus.Publish(Status());
            GameSignals.ModeStatus.Clear();

            Assert.That(GameSignals.ModeStatus.Value, Is.Null);
            Assert.That(GameSignals.ModeStatus.HasValue, Is.False);
            Assert.That(heard, Has.Count.EqualTo(2));
            Assert.That(heard[1], Is.Null);
        }

        [Test]
        public void ClearIfCurrentLeavesAValuePublishedByAnother()
        {
            var mine = Status();
            var theirs = Status();
            GameSignals.ModeStatus.Publish(mine);
            GameSignals.ModeStatus.Publish(theirs);

            Assert.That(GameSignals.ModeStatus.ClearIfCurrent(mine), Is.False);
            Assert.That(GameSignals.ModeStatus.Value, Is.SameAs(theirs));

            Assert.That(GameSignals.ModeStatus.ClearIfCurrent(theirs), Is.True);
            Assert.That(GameSignals.ModeStatus.Value, Is.Null);
            Assert.That(GameSignals.ModeStatus.ClearIfCurrent(theirs), Is.False, "nothing is left to clear");
        }

        [Test]
        public void AThrowingSubscriberIsLoggedAndTheOthersStillHear()
        {
            var heard = new List<ModeStatus>();
            GameSignals.ModeStatus.Changed += _ => throw new InvalidOperationException("broken view");
            GameSignals.ModeStatus.Changed += heard.Add;
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(GameSignals.LogTag) + ".*ModeStatus"));
            LogAssert.Expect(LogType.Exception, new Regex("broken view"));

            var status = Status();
            Assert.DoesNotThrow(() => GameSignals.ModeStatus.Publish(status));

            Assert.That(heard, Is.EqualTo(new[] { status }));
            Assert.That(GameSignals.ModeStatus.Value, Is.SameAs(status));
        }

        [Test]
        public void ResetAllEmptiesEverySignalAndDropsTheSubscribers()
        {
            var heard = 0;
            GameSignals.ModeStatus.Changed += _ => heard++;
            GameSignals.LocalPlayer.Publish(new LocalPlayerSignal(null, null, null, null, null, null, null, null));
            GameSignals.HudModel.Publish(new HudModel());
            GameSignals.ModeStatus.Publish(Status());
            GameSignals.Scoreboard.Publish(Array.Empty<ScoreboardEntry>());
            GameSignals.InteractHint.Publish(new InteractHint(this, "detail"));
            GameSignals.Pings.Publish(Array.Empty<PingMarker>());
            GameSignals.SquadOrder.Publish(new SquadOrderStatus(BotErrandKind.Hold, Vector3.one, null));
            GameSignals.HudDensity.Publish(HudDensity.Full);
            GameSignals.PauseTimeMode.Publish(PauseTimeMode.Freeze);
            GameSignals.PaletteViewer.Publish(Team.Bravo);
            heard = 0;

            GameSignals.ResetAll();

            EverySignalStartsEmpty();
            GameSignals.ModeStatus.Publish(Status());
            Assert.That(heard, Is.Zero, "subscribers from before the reset are gone");
        }

        [Test]
        public void ResetAllRunsBeforeTheSceneLoads()
        {
            var method = typeof(GameSignals).GetMethod(nameof(GameSignals.ResetAll), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            var attribute = method?.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();

            Assert.That(attribute, Is.Not.Null);
            Assert.That(attribute.loadType, Is.EqualTo(RuntimeInitializeLoadType.SubsystemRegistration));
        }

        [Test]
        public void ModeStatusDefaultsMissingListsToEmptyAndRejectsNegativeSeconds()
        {
            var status = new ModeStatus(null, null, null, null, false, null, null, default, null, null, default);

            Assert.That(status.SideScores, Is.Empty);
            Assert.That(status.Scores, Is.Empty);
            Assert.That(status.Lines, Is.Empty);
            Assert.That(status.RemainingSeconds, Is.Null);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ModeStatus(-1f, null, null, null, false, null, null, default, null, null, default));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ModeStatus(null, null, null, float.NaN, false, null, null, default, null, null, default));
        }

        private static ModeStatus Status()
        {
            return new ModeStatus(30f, new[] { new SideScore(Side.Alpha, 1d) }, Side.Alpha, null, false, null, null, default, null, null, default);
        }
    }
}
