using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public static class GameSignals
    {
        public const string LogTag = "[Signals]";

        public static GameSignal<LocalPlayerSignal> LocalPlayer { get; } = new GameSignal<LocalPlayerSignal>(nameof(LocalPlayer));

        public static GameSignal<HudModel> HudModel { get; } = new GameSignal<HudModel>(nameof(HudModel));

        // オフラインのプレイヤーは持ち物を自分の部品に持つので、出すのは部品に持ち物が無い画面 (ネット対戦) だけ。
        public static GameSignal<IInventoryControl> Inventory { get; } = new GameSignal<IInventoryControl>(nameof(Inventory));

        // オフラインは GrenadeThrower を LocalPlayer から読むので、出すのは GrenadeThrower が無い画面 (ネット対戦) だけ。
        public static GameSignal<GrenadeCountStatus?> GrenadeCount { get; } = new GameSignal<GrenadeCountStatus?>(nameof(GrenadeCount));

        // 値でなく読む関数にするのは、目くらましが延びても知らせは来ないので、出す側が毎フレーム読み直すため。
        // オフラインは StatusEffectReceiver.LocalPlayerBlinded で知らせるので、出すのは受ける役が無い画面 (ネット対戦) だけ。
        public static GameSignal<Func<float>> LocalBlindSeconds { get; } = new GameSignal<Func<float>>(nameof(LocalBlindSeconds));

        // 戦闘の Id (CombatEvents.Killed) の意味がオフラインと違う画面 (ネット対戦) だけが出す。オフラインの Id は UI が部品から引ける。
        public static GameSignal<IParticipants> Participants { get; } = new GameSignal<IParticipants>(nameof(Participants));

        public static GameSignal<ModeStatus> ModeStatus { get; } = new GameSignal<ModeStatus>(nameof(ModeStatus));

        public static GameSignal<DominationStatus> Domination { get; } = new GameSignal<DominationStatus>(nameof(Domination));

        public static GameSignal<IReadOnlyList<ScoreboardEntry>> Scoreboard { get; } =
            new GameSignal<IReadOnlyList<ScoreboardEntry>>(nameof(Scoreboard));

        public static GameSignal<InteractHint?> InteractHint { get; } = new GameSignal<InteractHint?>(nameof(InteractHint));

        public static GameSignal<TutorialStatus?> Tutorial { get; } = new GameSignal<TutorialStatus?>(nameof(Tutorial));

        public static GameSignal<KillCamStatus?> KillCam { get; } = new GameSignal<KillCamStatus?>(nameof(KillCam));

        public static GameSignal<IReadOnlyList<PingMarker>> Pings { get; } = new GameSignal<IReadOnlyList<PingMarker>>(nameof(Pings));

        public static GameSignal<SquadOrderStatus?> SquadOrder { get; } = new GameSignal<SquadOrderStatus?>(nameof(SquadOrder));

        public static GameSignal<CampaignNotice> CampaignNotice { get; } = new GameSignal<CampaignNotice>(nameof(CampaignNotice));

        public static GameSignal<DisconnectNotice> MenuReturnReason { get; } = new GameSignal<DisconnectNotice>(nameof(MenuReturnReason));

        public static GameSignal<HudDensity?> HudDensity { get; } = new GameSignal<HudDensity?>(nameof(HudDensity));

        public static GameSignal<PauseTimeMode?> PauseTimeMode { get; } = new GameSignal<PauseTimeMode?>(nameof(PauseTimeMode));

        public static GameSignal<Team?> PaletteViewer { get; } = new GameSignal<Team?>(nameof(PaletteViewer));

        public static GameSignal<StreakStatus?> Streak { get; } = new GameSignal<StreakStatus?>(nameof(Streak));

        public static GameSignal<MinimapStatus?> Minimap { get; } = new GameSignal<MinimapStatus?>(nameof(Minimap));

        public static GameSignal<IReadOnlyList<ChatLine>> Chat { get; } = new GameSignal<IReadOnlyList<ChatLine>>(nameof(Chat));

        public static GameSignal<VoiceStatus?> Voice { get; } = new GameSignal<VoiceStatus?>(nameof(Voice));

        public static GameSignal<MapVoteStatus?> MapVote { get; } = new GameSignal<MapVoteStatus?>(nameof(MapVote));

        public static GameSignal<SpectateStatus?> Spectate { get; } = new GameSignal<SpectateStatus?>(nameof(Spectate));

        // ドメインの再読み込みを切った Play でも、前の Play の値や消えた UI の購読が残らないよう、シーンを読む前にすべて空に戻す。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetAll()
        {
            LocalPlayer.Reset();
            HudModel.Reset();
            Inventory.Reset();
            GrenadeCount.Reset();
            LocalBlindSeconds.Reset();
            Participants.Reset();
            ModeStatus.Reset();
            Domination.Reset();
            Scoreboard.Reset();
            InteractHint.Reset();
            Tutorial.Reset();
            KillCam.Reset();
            Pings.Reset();
            SquadOrder.Reset();
            CampaignNotice.Reset();
            MenuReturnReason.Reset();
            HudDensity.Reset();
            PauseTimeMode.Reset();
            PaletteViewer.Reset();
            Streak.Reset();
            Minimap.Reset();
            Chat.Reset();
            Voice.Reset();
            MapVote.Reset();
            Spectate.Reset();
        }
    }
}
