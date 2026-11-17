using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shooter.Tests.EditMode
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
            Assert.That(GameSignals.Inventory.Value, Is.Null);
            Assert.That(GameSignals.GrenadeCount.Value, Is.Null);
            Assert.That(GameSignals.LocalBlindSeconds.Value, Is.Null);
            Assert.That(GameSignals.Participants.Value, Is.Null);
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
            Assert.That(GameSignals.Streak.Value, Is.Null);
            Assert.That(GameSignals.Minimap.Value, Is.Null);
            Assert.That(GameSignals.Chat.Value, Is.Null);
            Assert.That(GameSignals.Voice.Value, Is.Null);
            Assert.That(GameSignals.MapVote.Value, Is.Null);
            Assert.That(GameSignals.Spectate.Value, Is.Null);
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
            GameSignals.Inventory.Publish(new FakeInventory());
            GameSignals.GrenadeCount.Publish(new GrenadeCountStatus(true, 0, null));
            GameSignals.LocalBlindSeconds.Publish(() => 1f);
            GameSignals.Participants.Publish(new ParticipantTable());
            GameSignals.ModeStatus.Publish(Status());
            GameSignals.Scoreboard.Publish(Array.Empty<ScoreboardEntry>());
            GameSignals.InteractHint.Publish(new InteractHint(this, "detail"));
            GameSignals.Pings.Publish(Array.Empty<PingMarker>());
            GameSignals.SquadOrder.Publish(new SquadOrderStatus(BotErrandKind.Hold, Vector3.one, null));
            GameSignals.HudDensity.Publish(HudDensity.Full);
            GameSignals.PauseTimeMode.Publish(PauseTimeMode.Freeze);
            GameSignals.PaletteViewer.Publish(Team.Bravo);
            GameSignals.Streak.Publish(new StreakStatus(3, new[] { new StreakSlot("uav", "streak.uav", 3, true) }));
            GameSignals.Minimap.Publish(new MinimapStatus(System.Numerics.Vector3.Zero, 0f, null));
            GameSignals.Chat.Publish(new[] { new ChatLine(1, ChatChannel.All, "gg", 1d) });
            GameSignals.Voice.Publish(new VoiceStatus(false, null, true));
            GameSignals.MapVote.Publish(new MapVoteStatus(new[] { new MapVoteCandidate("dust", 0) }, null, 10f, null));
            GameSignals.Spectate.Publish(new SpectateStatus(2, 1, SpectateReason.Dead));
            heard = 0;

            GameSignals.ResetAll();

            EverySignalStartsEmpty();
            GameSignals.ModeStatus.Publish(Status());
            Assert.That(heard, Is.Zero, "subscribers from before the reset are gone");
        }

        [Test]
        public void TheMinimapChatVoiceVoteAndSpectateSignalsHoldWhatWasPublished()
        {
            var minimap = new MinimapStatus(new System.Numerics.Vector3(1f, 0f, 2f), 90f, null);
            var chat = new[] { new ChatLine(4, ChatChannel.Team, "push B", 12.5d) };
            var heardSpectate = new List<SpectateStatus?>();
            GameSignals.Spectate.Changed += heardSpectate.Add;

            GameSignals.Minimap.Publish(minimap);
            GameSignals.Chat.Publish(chat);
            GameSignals.Voice.Publish(new VoiceStatus(false, Array.Empty<int>(), false));
            GameSignals.MapVote.Publish(new MapVoteStatus(Array.Empty<MapVoteCandidate>(), null, 0f, null));
            GameSignals.Spectate.Publish(new SpectateStatus(null, 0, SpectateReason.BetweenRounds));

            Assert.That(GameSignals.Minimap.Value, Is.EqualTo((MinimapStatus?)minimap));
            Assert.That(GameSignals.Chat.Value, Is.SameAs(chat));
            Assert.That(GameSignals.Voice.HasValue, Is.True, "no microphone and nobody speaking is still something to show");
            Assert.That(GameSignals.MapVote.HasValue, Is.True, "an empty vote is still a vote in progress");
            Assert.That(heardSpectate, Is.EqualTo(new SpectateStatus?[] { new SpectateStatus(null, 0, SpectateReason.BetweenRounds) }));
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
        public void ModeStatusDefaultsMissingScoresToEmptyAndRejectsNegativeSeconds()
        {
            var status = new ModeStatus(null, null, null, null, false, null, null);

            Assert.That(status.SideScores, Is.Empty);
            Assert.That(status.RemainingSeconds, Is.Null);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ModeStatus(-1f, null, null, null, false, null, null));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ModeStatus(null, null, null, float.NaN, false, null, null));
        }

        private static ModeStatus Status()
        {
            return new ModeStatus(30f, new[] { new SideScore(Side.Alpha, 1d) }, Side.Alpha, null, false, null, null);
        }

        [Test]
        public void ANullableStructSignalHoldsAZeroCount()
        {
            GameSignals.GrenadeCount.Publish(new GrenadeCountStatus(false, 0, null));

            Assert.That(GameSignals.GrenadeCount.HasValue, Is.True, "a count of zero with grenades off is still something to show");
            Assert.That(GameSignals.GrenadeCount.ClearIfCurrent(new GrenadeCountStatus(false, 0, string.Empty)), Is.True);
            Assert.That(GameSignals.GrenadeCount.HasValue, Is.False);
        }

        private sealed class FakeInventory : IInventoryControl
        {
            public event Action Changed
            {
                add { }
                remove { }
            }

            public Inventory Inventory => null;

            public void Move(int handle, int x, int y, bool rotated)
            {
            }

            public void Use(int handle)
            {
            }

            public void Drop(int handle, int count)
            {
            }

            public void Attach(int handle)
            {
            }

            public void Detach(int weaponHandle, string itemId)
            {
            }
        }
    }
}
