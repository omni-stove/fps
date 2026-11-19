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
            Assert.That(GameSignals.ChatSender.Value, Is.Null);
            Assert.That(GameSignals.MapVoteSender.Value, Is.Null);
            Assert.That(GameSignals.Party.Value, Is.Null);
            Assert.That(GameSignals.PartyControl.Value, Is.Null);
            Assert.That(GameSignals.JoinByCode.Value, Is.Null);
            Assert.That(GameSignals.ReportSender.Value, Is.Null);
            Assert.That(GameSignals.BlockControl.Value, Is.Null);
            Assert.That(GameSignals.Achievements.Value, Is.Null);
            Assert.That(GameSignals.Leaderboard.Value, Is.Null);
            Assert.That(GameSignals.LeaderboardControl.Value, Is.Null);
            Assert.That(GameSignals.Replays.Value, Is.Null);
            Assert.That(GameSignals.Cosmetics.Value, Is.Null);
            Assert.That(GameSignals.Stash.Value, Is.Null);
            Assert.That(GameSignals.Aircraft.Value, Is.Null);
            Assert.That(GameSignals.Stamina.Value, Is.Null);
            Assert.That(GameSignals.LockOnWarning.Value, Is.Null);
            Assert.That(GameSignals.Emotes.Value, Is.Null);
            Assert.That(GameSignals.VoteKick.Value, Is.Null);
            Assert.That(GameSignals.VoteKickSender.Value, Is.Null);
            Assert.That(GameSignals.Wallet.Value, Is.Null);
            Assert.That(GameSignals.BuySender.Value, Is.Null);
            Assert.That(GameSignals.Vision.Value, Is.Null);
            Assert.That(GameSignals.Drone.Value, Is.Null);
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
            GameSignals.ChatSender.Publish(new FakeChatSender());
            GameSignals.MapVoteSender.Publish(new FakeMapVoteSender());
            GameSignals.Party.Publish(new PartyStatus(PartyStage.InParty, new[] { new PartyMember("Ann", true) }, "lobby-1", true));
            GameSignals.PartyControl.Publish(new FakePartyControl());
            GameSignals.JoinByCode.Publish(new FakeJoinByCode());
            GameSignals.ReportSender.Publish(new FakeReportSender());
            GameSignals.BlockControl.Publish(new FakeBlockControl());
            GameSignals.Achievements.Publish(new[] { new AchievementProgress("first-blood", 0, 1, false) });
            GameSignals.Leaderboard.Publish(new LeaderboardStatus("kills", LeaderboardStage.Loading, null));
            GameSignals.LeaderboardControl.Publish(new FakeLeaderboardControl());
            GameSignals.Replays.Publish(new FakeReplayLibrary());
            GameSignals.Cosmetics.Publish(new FakeCosmeticLocker());
            GameSignals.Stash.Publish(new StashStatus(60, null));
            GameSignals.Aircraft.Publish(new AircraftStatus(50f, 120f, 0.5f, 5f, 1f, false, false));
            GameSignals.Stamina.Publish(new StaminaStatus(0f, 100f, true));
            GameSignals.LockOnWarning.Publish(new LockOnWarningStatus(LockOnWarningStage.Locked, 90f));
            GameSignals.Emotes.Publish(new FakeEmoteControl());
            GameSignals.VoteKick.Publish(new VoteKickStatus(3, 1, 1, 4, 30f));
            GameSignals.VoteKickSender.Publish(new FakeVoteKickSender());
            GameSignals.Wallet.Publish(new WalletStatus(800, 16000, true));
            GameSignals.BuySender.Publish(new FakeBuySender());
            GameSignals.Vision.Publish(new VisionStatus(VisionMode.Night, VisionSource.Goggles));
            GameSignals.Drone.Publish(new DroneStatus(1f, 1f, 0, true));
            heard = 0;

            GameSignals.ResetAll();

            EverySignalStartsEmpty();
            GameSignals.ModeStatus.Publish(Status());
            Assert.That(heard, Is.Zero, "subscribers from before the reset are gone");
        }

        [Test]
        public void ACreatedSignalHoldsWhatWasPublishedUntilResetAll()
        {
            var signal = GameSignals.Create<int?>("CreatedForTest");
            var heard = new List<int?>();
            signal.Changed += heard.Add;

            signal.Publish(3);

            Assert.That(signal.Value, Is.EqualTo((int?)3));
            Assert.That(heard, Is.EqualTo(new int?[] { 3 }));

            GameSignals.ResetAll();

            Assert.That(signal.Value, Is.Null);
            Assert.That(signal.HasValue, Is.False);
            signal.Publish(4);
            Assert.That(heard, Is.EqualTo(new int?[] { 3 }), "subscribers from before the reset are gone");
            Assert.That(signal.Value, Is.EqualTo((int?)4), "the created signal still works after the reset");
        }

        [Test]
        public void CreateNamesTheSignalForItsLogs()
        {
            var signal = GameSignals.Create<string>("NamedForTest");
            signal.Changed += _ => throw new InvalidOperationException("broken view");
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(GameSignals.LogTag) + ".*NamedForTest"));
            LogAssert.Expect(LogType.Exception, new Regex("broken view"));

            signal.Publish("x");

            Assert.That(signal.ToString(), Is.EqualTo("NamedForTest=x"));
        }

        [Test]
        public void CreateRejectsAMissingName()
        {
            Assert.Throws<ArgumentException>(() => GameSignals.Create<int?>(null));
            Assert.Throws<ArgumentException>(() => GameSignals.Create<int?>(string.Empty));
        }

        [Test]
        public void ResetAllDropsTheStaminaLockOnAndEmoteSubscribers()
        {
            var heardStamina = 0;
            var heardLockOn = 0;
            var heardEmotes = 0;
            GameSignals.Stamina.Changed += _ => heardStamina++;
            GameSignals.LockOnWarning.Changed += _ => heardLockOn++;
            GameSignals.Emotes.Changed += _ => heardEmotes++;

            GameSignals.ResetAll();
            GameSignals.Stamina.Publish(new StaminaStatus(50f, 100f, false));
            GameSignals.LockOnWarning.Publish(new LockOnWarningStatus(LockOnWarningStage.Tracking, -45f));
            GameSignals.Emotes.Publish(new FakeEmoteControl());

            Assert.That(heardStamina, Is.Zero);
            Assert.That(heardLockOn, Is.Zero);
            Assert.That(heardEmotes, Is.Zero);
        }

        [Test]
        public void TheStaminaLockOnAndEmoteSignalsHoldWhatWasPublished()
        {
            var emotes = new FakeEmoteControl();

            GameSignals.Stamina.Publish(new StaminaStatus(0f, 80f, true));
            GameSignals.LockOnWarning.Publish(new LockOnWarningStatus(LockOnWarningStage.None, 0f));
            GameSignals.Emotes.Publish(emotes);

            Assert.That(GameSignals.Stamina.Value, Is.EqualTo((StaminaStatus?)new StaminaStatus(0f, 80f, true)));
            Assert.That(GameSignals.Stamina.Value.Value.Depleted, Is.True);
            Assert.That(GameSignals.LockOnWarning.HasValue, Is.True, "no threat is still something to show");
            Assert.That(GameSignals.LockOnWarning.Value.Value.IsWarning, Is.False);
            Assert.That(GameSignals.Emotes.Value.EmoteIds, Is.EqualTo(new[] { "wave", "salute" }));
            Assert.That(GameSignals.Emotes.Value.Play("wave"), Is.True);
            Assert.That(GameSignals.Emotes.Value.Play("dance"), Is.False, "an Id the list does not have");
            Assert.That(emotes.Played, Is.EqualTo(new[] { "wave" }));
        }

        [Test]
        public void TheVoteKickWalletVisionAndDroneSignalsHoldWhatWasPublished()
        {
            var voteKick = new FakeVoteKickSender();
            var buy = new FakeBuySender();
            var heardVision = new List<VisionStatus?>();
            GameSignals.Vision.Changed += heardVision.Add;

            GameSignals.VoteKick.Publish(new VoteKickStatus(5, 2, 3, 3, 12.5f));
            GameSignals.VoteKickSender.Publish(voteKick);
            GameSignals.Wallet.Publish(new WalletStatus(0, 9000, false));
            GameSignals.BuySender.Publish(buy);
            GameSignals.Vision.Publish(VisionStatus.None);
            GameSignals.Vision.Publish(new VisionStatus(VisionMode.Thermal, VisionSource.Scope));
            GameSignals.Drone.Publish(new DroneStatus(0f, 0.25f, 2, false));

            GameSignals.VoteKickSender.Value.Call(5);
            GameSignals.VoteKickSender.Value.Vote(false);
            GameSignals.BuySender.Value.Buy("armor");

            Assert.That(GameSignals.VoteKick.Value, Is.EqualTo((VoteKickStatus?)new VoteKickStatus(5, 2, 3, 3, 12.5f)));
            Assert.That(GameSignals.VoteKick.Value.Value.HasEnoughVotes, Is.True);
            Assert.That(GameSignals.VoteKickSender.Value, Is.SameAs(voteKick));
            Assert.That(voteKick.Called, Is.EqualTo(new[] { 5 }));
            Assert.That(voteKick.Votes, Is.EqualTo(new[] { false }));
            Assert.That(GameSignals.Wallet.HasValue, Is.True, "an empty wallet outside the buy time is still something to show");
            Assert.That(GameSignals.Wallet.Value.Value.CanBuy, Is.False);
            Assert.That(GameSignals.Wallet.Value.Value.Max, Is.EqualTo(9000));
            Assert.That(GameSignals.BuySender.Value, Is.SameAs(buy));
            Assert.That(buy.Bought, Is.EqualTo(new[] { "armor" }));
            Assert.That(
                heardVision,
                Is.EqualTo(new VisionStatus?[] { VisionStatus.None, new VisionStatus(VisionMode.Thermal, VisionSource.Scope) }),
                "plain sight is still a published value");
            Assert.That(GameSignals.Drone.Value.Value.IsBatteryEmpty, Is.True);
            Assert.That(GameSignals.Drone.Value.Value.SpottedCount, Is.EqualTo(2));
            Assert.That(GameSignals.Drone.Value.Value.Piloting, Is.False);
        }

        [Test]
        public void ResetAllDropsTheVoteKickWalletVisionAndDroneSubscribers()
        {
            var heard = 0;
            GameSignals.VoteKick.Changed += _ => heard++;
            GameSignals.VoteKickSender.Changed += _ => heard++;
            GameSignals.Wallet.Changed += _ => heard++;
            GameSignals.BuySender.Changed += _ => heard++;
            GameSignals.Vision.Changed += _ => heard++;
            GameSignals.Drone.Changed += _ => heard++;

            GameSignals.ResetAll();
            GameSignals.VoteKick.Publish(new VoteKickStatus(1, 2, 0, 1, 0f));
            GameSignals.VoteKickSender.Publish(new FakeVoteKickSender());
            GameSignals.Wallet.Publish(new WalletStatus(1, 1, true));
            GameSignals.BuySender.Publish(new FakeBuySender());
            GameSignals.Vision.Publish(new VisionStatus(VisionMode.Night, VisionSource.Scope));
            GameSignals.Drone.Publish(new DroneStatus(0.5f, 0.5f, 0, true));

            Assert.That(heard, Is.Zero);
        }

        [Test]
        public void TheNewStatusesRejectValuesOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new VoteKickStatus(-1, 0, 0, 1, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VoteKickStatus(0, -1, 0, 1, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VoteKickStatus(0, 0, -1, 1, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VoteKickStatus(0, 0, 0, 0, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VoteKickStatus(0, 0, 0, 1, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VoteKickStatus(0, 0, 0, 1, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WalletStatus(0, 0, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WalletStatus(-1, 100, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WalletStatus(101, 100, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WalletStatus(10, 100, true).CanAfford(-1));
            Assert.Throws<ArgumentException>(() => new VisionStatus(VisionMode.Night, VisionSource.None));
            Assert.Throws<ArgumentException>(() => new VisionStatus(VisionMode.None, VisionSource.Goggles));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VisionStatus((VisionMode)9, VisionSource.Scope));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VisionStatus(VisionMode.Night, (VisionSource)9));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DroneStatus(1.5f, 0f, 0, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DroneStatus(float.NaN, 0f, 0, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DroneStatus(0f, -0.1f, 0, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DroneStatus(0f, 0f, -1, false));
        }

        [Test]
        public void TheNewStatusesReadTheirDerivedValues()
        {
            Assert.That(new VoteKickStatus(1, 2, 2, 3, 5f).HasEnoughVotes, Is.False);
            Assert.That(default(VoteKickStatus).HasEnoughVotes, Is.False, "a default status has no vote to pass");
            Assert.That(new WalletStatus(300, 300, true).IsFull, Is.True);
            Assert.That(new WalletStatus(300, 1000, true).CanAfford(300), Is.True);
            Assert.That(new WalletStatus(300, 1000, true).CanAfford(301), Is.False);
            Assert.That(default(WalletStatus).IsFull, Is.False);
            Assert.That(VisionStatus.None.IsActive, Is.False);
            Assert.That(new VisionStatus(VisionMode.None, VisionSource.None), Is.EqualTo(VisionStatus.None));
            Assert.That(new VisionStatus(VisionMode.Night, VisionSource.Goggles).IsActive, Is.True);
            Assert.That(
                new VisionStatus(VisionMode.Night, VisionSource.Goggles),
                Is.Not.EqualTo(new VisionStatus(VisionMode.Night, VisionSource.Scope)),
                "the same mode from another source is a change");
            Assert.That(new DroneStatus(0.01f, 1f, 0, true).IsBatteryEmpty, Is.False);
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
        public void TheSendAndPartySignalsHoldWhatWasPublished()
        {
            var chat = new FakeChatSender();
            var vote = new FakeMapVoteSender();
            var control = new FakePartyControl();
            var join = new FakeJoinByCode();
            var heardParty = new List<PartyStatus?>();
            GameSignals.Party.Changed += heardParty.Add;

            GameSignals.ChatSender.Publish(chat);
            GameSignals.MapVoteSender.Publish(vote);
            GameSignals.PartyControl.Publish(control);
            GameSignals.JoinByCode.Publish(join);
            GameSignals.Party.Publish(new PartyStatus(PartyStage.None, null, null, false));

            GameSignals.ChatSender.Value.Send(ChatChannel.Team, "push B");
            GameSignals.MapVoteSender.Value.Vote(2);

            Assert.That(GameSignals.ChatSender.Value, Is.SameAs(chat));
            Assert.That(chat.Sent, Is.EqualTo(new[] { (ChatChannel.Team, "push B") }));
            Assert.That(GameSignals.MapVoteSender.Value, Is.SameAs(vote));
            Assert.That(vote.Votes, Is.EqualTo(new[] { 2 }));
            Assert.That(GameSignals.PartyControl.Value, Is.SameAs(control));
            Assert.That(GameSignals.JoinByCode.Value, Is.SameAs(join));
            Assert.That(GameSignals.Party.HasValue, Is.True, "being outside a party is still something to show");
            Assert.That(heardParty, Is.EqualTo(new PartyStatus?[] { new PartyStatus(PartyStage.None, null, string.Empty, false) }));
        }

        [Test]
        public void TheModerationRecordAndContentSignalsHoldWhatWasPublished()
        {
            var report = new FakeReportSender();
            var block = new FakeBlockControl();
            var achievements = new[] { new AchievementProgress("marksman", 10, 50, false) };
            var replays = new FakeReplayLibrary();
            var cosmetics = new FakeCosmeticLocker();
            var leaderboardControl = new FakeLeaderboardControl();
            var heardLeaderboard = new List<LeaderboardStatus?>();
            GameSignals.Leaderboard.Changed += heardLeaderboard.Add;

            GameSignals.ReportSender.Publish(report);
            GameSignals.BlockControl.Publish(block);
            GameSignals.Achievements.Publish(achievements);
            GameSignals.LeaderboardControl.Publish(leaderboardControl);
            GameSignals.Leaderboard.Publish(new LeaderboardStatus("wins", LeaderboardStage.Unavailable, null));
            GameSignals.LeaderboardControl.Value.Open("kills");
            GameSignals.Replays.Publish(replays);
            GameSignals.Cosmetics.Publish(cosmetics);
            GameSignals.Stash.Publish(new StashStatus(10, Array.Empty<StashEntry>()));

            GameSignals.ReportSender.Value.Report(7, ReportReason.Cheating);
            GameSignals.BlockControl.Value.SetMuted(7, true);

            Assert.That(report.Sent, Is.EqualTo(new[] { (7, ReportReason.Cheating) }));
            Assert.That(GameSignals.BlockControl.Value.IsMuted(7), Is.True);
            Assert.That(GameSignals.BlockControl.Value.IsBlocked(7), Is.False);
            Assert.That(GameSignals.Achievements.Value, Is.SameAs(achievements));
            Assert.That(
                heardLeaderboard,
                Is.EqualTo(new LeaderboardStatus?[] { new LeaderboardStatus("wins", LeaderboardStage.Unavailable, Array.Empty<LeaderboardRow>()) }));
            Assert.That(GameSignals.LeaderboardControl.Value, Is.SameAs(leaderboardControl));
            Assert.That(leaderboardControl.Opened, Is.EqualTo(new[] { "kills" }));
            Assert.That(GameSignals.Replays.Value, Is.SameAs(replays));
            Assert.That(GameSignals.Cosmetics.Value, Is.SameAs(cosmetics));
            Assert.That(GameSignals.Stash.HasValue, Is.True, "an empty stash is still something to show");
            Assert.That(GameSignals.Stash.Value.Value.FreeSlots, Is.EqualTo(10));
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

        private sealed class FakeChatSender : IChatSender
        {
            public List<(ChatChannel, string)> Sent { get; } = new List<(ChatChannel, string)>();

            public void Send(ChatChannel channel, string text)
            {
                Sent.Add((channel, text));
            }
        }

        private sealed class FakeMapVoteSender : IMapVoteSender
        {
            public List<int> Votes { get; } = new List<int>();

            public void Vote(int candidateIndex)
            {
                Votes.Add(candidateIndex);
            }
        }

        private sealed class FakeVoteKickSender : IVoteKickSender
        {
            public List<int> Called { get; } = new List<int>();

            public List<bool> Votes { get; } = new List<bool>();

            public void Call(int targetId)
            {
                Called.Add(targetId);
            }

            public void Vote(bool inFavor)
            {
                Votes.Add(inFavor);
            }
        }

        private sealed class FakeBuySender : IBuySender
        {
            public List<string> Bought { get; } = new List<string>();

            public void Buy(string itemId)
            {
                Bought.Add(itemId);
            }
        }

        private sealed class FakePartyControl : IPartyControl
        {
            public event Action<PartyStatus> StatusChanged
            {
                add { }
                remove { }
            }

            public PartyStatus Status => default;

            public void Create()
            {
            }

            public void Join(string inviteCode)
            {
            }

            public void Leave()
            {
            }
        }

        private sealed class FakeJoinByCode : IJoinByCode
        {
            public void Join(string code, Action<JoinCodeOutcome> done)
            {
                done(JoinCodeOutcome.NotFound);
            }
        }

        private sealed class FakeReportSender : IReportSender
        {
            public List<(int, ReportReason)> Sent { get; } = new List<(int, ReportReason)>();

            public void Report(int participantId, ReportReason reason)
            {
                Sent.Add((participantId, reason));
            }
        }

        private sealed class FakeBlockControl : IBlockControl
        {
            private readonly HashSet<int> _blocked = new HashSet<int>();
            private readonly HashSet<int> _muted = new HashSet<int>();

            public event Action Changed;

            public bool IsBlocked(int participantId)
            {
                return _blocked.Contains(participantId);
            }

            public bool IsMuted(int participantId)
            {
                return _muted.Contains(participantId);
            }

            public void SetBlocked(int participantId, bool blocked)
            {
                Set(_blocked, participantId, blocked);
            }

            public void SetMuted(int participantId, bool muted)
            {
                Set(_muted, participantId, muted);
            }

            private void Set(HashSet<int> set, int participantId, bool on)
            {
                if (on)
                {
                    set.Add(participantId);
                }
                else
                {
                    set.Remove(participantId);
                }

                Changed?.Invoke();
            }
        }

        private sealed class FakeLeaderboardControl : ILeaderboardControl
        {
            public List<string> Opened { get; } = new List<string>();

            public bool Open(string boardId)
            {
                Opened.Add(boardId);
                return true;
            }

            public bool Refresh()
            {
                return false;
            }

            public void Close()
            {
            }
        }

        private sealed class FakeReplayLibrary : IReplayLibrary
        {
            public event Action Changed
            {
                add { }
                remove { }
            }

            public IReadOnlyList<ReplayEntry> Entries => Array.Empty<ReplayEntry>();

            public bool Play(string replayId)
            {
                return false;
            }

            public bool Delete(string replayId)
            {
                return false;
            }
        }

        private sealed class FakeEmoteControl : IEmoteControl
        {
            public IReadOnlyList<string> EmoteIds { get; } = new[] { "wave", "salute" };

            public List<string> Played { get; } = new List<string>();

            public bool Play(string emoteId)
            {
                if (!((IList<string>)EmoteIds).Contains(emoteId))
                {
                    return false;
                }

                Played.Add(emoteId);
                return true;
            }
        }

        private sealed class FakeCosmeticLocker : ICosmeticLocker
        {
            public event Action Changed
            {
                add { }
                remove { }
            }

            public CosmeticCatalog Catalog => CosmeticCatalog.Default;

            public string EquippedId(CosmeticKind kind)
            {
                return null;
            }

            public bool IsUnlocked(string cosmeticId)
            {
                return false;
            }

            public bool Equip(CosmeticKind kind, string cosmeticId)
            {
                return false;
            }
        }
    }
}
