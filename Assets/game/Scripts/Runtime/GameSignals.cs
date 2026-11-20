using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public static class GameSignals
    {
        public const string LogTag = "[Signals]";

        // ResetAll で一覧ごと空にしないのは、作った側が静的な欄に持ち続け、ドメインの再読み込みを切った Play では作り直されないため。
        private static readonly List<Action> _createdResets = new List<Action>();

        public static GameSignal<LocalPlayerSignal> LocalPlayer { get; } = new GameSignal<LocalPlayerSignal>(nameof(LocalPlayer));

        public static GameSignal<HudModel> HudModel { get; } = new GameSignal<HudModel>(nameof(HudModel));

        // オフラインのプレイヤーは持ち物を自分の部品に持つので、出すのは部品に持ち物が無い画面 (ネット対戦) だけ。
        public static GameSignal<IInventoryControl> Inventory { get; } = new GameSignal<IInventoryControl>(nameof(Inventory));

        // オフラインは GrenadeThrower を LocalPlayer から読むので、出すのは GrenadeThrower が無い画面 (ネット対戦) だけ。
        public static GameSignal<GrenadeCountStatus?> GrenadeCount { get; } = new GameSignal<GrenadeCountStatus?>(nameof(GrenadeCount));

        // 値でなく読む関数にするのは、目くらましが延びても知らせは来ないので、出す側が毎フレーム読み直すため。
        // オフラインは StatusEffectReceiver.LocalPlayerBlinded で知らせるので、出すのは受ける役が無い画面 (ネット対戦) だけ。
        public static GameSignal<Func<float>> LocalBlindSeconds { get; } = new GameSignal<Func<float>>(nameof(LocalBlindSeconds));

        // 値でなく読む関数にするのは、地図の知らせ (Minimap) は間隔ごとにしか来ず、コンパスが毎フレーム今の向き (北から時計回りの度) を読み直すため。
        // オフラインは LocalPlayer の向きから読めるので、出すのは LocalPlayer の無い画面 (ネット対戦) だけ。
        public static GameSignal<Func<float>> LocalHeadingDegrees { get; } = new GameSignal<Func<float>>(nameof(LocalHeadingDegrees));

        // 戦闘の Id (CombatEvents.Killed) の意味がオフラインと違う画面 (ネット対戦) だけが出す。オフラインの Id は UI が部品から引ける。
        public static GameSignal<IParticipants> Participants { get; } = new GameSignal<IParticipants>(nameof(Participants));

        public static GameSignal<ModeStatus> ModeStatus { get; } = new GameSignal<ModeStatus>(nameof(ModeStatus));

        public static GameSignal<IReadOnlyList<ScoreboardEntry>> Scoreboard { get; } =
            new GameSignal<IReadOnlyList<ScoreboardEntry>>(nameof(Scoreboard));

        public static GameSignal<InteractHint?> InteractHint { get; } = new GameSignal<InteractHint?>(nameof(InteractHint));

        // 手元のプレイヤーが座っている席で何を撃てるか。座っていなければ値なし。照準を出すかを UI が決めるのに使う。
        public static GameSignal<VehicleSeatFiring?> SeatFiring { get; } = new GameSignal<VehicleSeatFiring?>(nameof(SeatFiring));

        public static GameSignal<KillCamStatus?> KillCam { get; } = new GameSignal<KillCamStatus?>(nameof(KillCam));

        public static GameSignal<IReadOnlyList<PingMarker>> Pings { get; } = new GameSignal<IReadOnlyList<PingMarker>>(nameof(Pings));

        public static GameSignal<SquadOrderStatus?> SquadOrder { get; } = new GameSignal<SquadOrderStatus?>(nameof(SquadOrder));

        public static GameSignal<DisconnectNotice> MenuReturnReason { get; } = new GameSignal<DisconnectNotice>(nameof(MenuReturnReason));

        public static GameSignal<HudDensity?> HudDensity { get; } = new GameSignal<HudDensity?>(nameof(HudDensity));

        public static GameSignal<PauseTimeMode?> PauseTimeMode { get; } = new GameSignal<PauseTimeMode?>(nameof(PauseTimeMode));

        public static GameSignal<Team?> PaletteViewer { get; } = new GameSignal<Team?>(nameof(PaletteViewer));

        public static GameSignal<StreakStatus?> Streak { get; } = new GameSignal<StreakStatus?>(nameof(Streak));

        public static GameSignal<StreakTargetingStatus?> StreakTargeting { get; } = new GameSignal<StreakTargetingStatus?>(nameof(StreakTargeting));

        public static GameSignal<MinimapStatus?> Minimap { get; } = new GameSignal<MinimapStatus?>(nameof(Minimap));

        public static GameSignal<IReadOnlyList<ChatLine>> Chat { get; } = new GameSignal<IReadOnlyList<ChatLine>>(nameof(Chat));

        public static GameSignal<VoiceStatus?> Voice { get; } = new GameSignal<VoiceStatus?>(nameof(Voice));

        public static GameSignal<MapVoteStatus?> MapVote { get; } = new GameSignal<MapVoteStatus?>(nameof(MapVote));

        public static GameSignal<SpectateStatus?> Spectate { get; } = new GameSignal<SpectateStatus?>(nameof(Spectate));

        // 以下の送る・操る口は、UI (Shooter.Runtime.UI) が NetV2 を参照しないので NetV2 の部品がここに出す。
        public static GameSignal<IChatSender> ChatSender { get; } = new GameSignal<IChatSender>(nameof(ChatSender));

        public static GameSignal<IMapVoteSender> MapVoteSender { get; } = new GameSignal<IMapVoteSender>(nameof(MapVoteSender));

        public static GameSignal<PartyStatus?> Party { get; } = new GameSignal<PartyStatus?>(nameof(Party));

        public static GameSignal<IPartyControl> PartyControl { get; } = new GameSignal<IPartyControl>(nameof(PartyControl));

        public static GameSignal<IJoinByCode> JoinByCode { get; } = new GameSignal<IJoinByCode>(nameof(JoinByCode));

        public static GameSignal<IReportSender> ReportSender { get; } = new GameSignal<IReportSender>(nameof(ReportSender));

        public static GameSignal<IBlockControl> BlockControl { get; } = new GameSignal<IBlockControl>(nameof(BlockControl));

        public static GameSignal<LeaderboardStatus?> Leaderboard { get; } = new GameSignal<LeaderboardStatus?>(nameof(Leaderboard));

        public static GameSignal<ILeaderboardControl> LeaderboardControl { get; } = new GameSignal<ILeaderboardControl>(nameof(LeaderboardControl));

        public static GameSignal<IReplayLibrary> Replays { get; } = new GameSignal<IReplayLibrary>(nameof(Replays));

        public static GameSignal<ICosmeticLocker> Cosmetics { get; } = new GameSignal<ICosmeticLocker>(nameof(Cosmetics));

        public static GameSignal<StashStatus?> Stash { get; } = new GameSignal<StashStatus?>(nameof(Stash));

        public static GameSignal<AccountStatus?> Account { get; } = new GameSignal<AccountStatus?>(nameof(Account));

        // UI は NetEos を参照しないので、サインインの部品がここに出す。サインインを使えないビルドや設定では出さない。
        public static GameSignal<IAccountControl> AccountControl { get; } = new GameSignal<IAccountControl>(nameof(AccountControl));

        public static GameSignal<StaminaStatus?> Stamina { get; } = new GameSignal<StaminaStatus?>(nameof(Stamina));

        public static GameSignal<LockOnWarningStatus?> LockOnWarning { get; } = new GameSignal<LockOnWarningStatus?>(nameof(LockOnWarning));

        public static GameSignal<IEmoteControl> Emotes { get; } = new GameSignal<IEmoteControl>(nameof(Emotes));

        public static GameSignal<VoteKickStatus?> VoteKick { get; } = new GameSignal<VoteKickStatus?>(nameof(VoteKick));

        public static GameSignal<IVoteKickSender> VoteKickSender { get; } = new GameSignal<IVoteKickSender>(nameof(VoteKickSender));

        public static GameSignal<WalletStatus?> Wallet { get; } = new GameSignal<WalletStatus?>(nameof(Wallet));

        public static GameSignal<IBuySender> BuySender { get; } = new GameSignal<IBuySender>(nameof(BuySender));

        public static GameSignal<VisionStatus?> Vision { get; } = new GameSignal<VisionStatus?>(nameof(Vision));

        public static GameSignal<DroneStatus?> Drone { get; } = new GameSignal<DroneStatus?>(nameof(Drone));

        // 測る相手 (サーバー) がいるネット対戦の接続中だけ出し、切れたら空に戻す。UI は空を「回線の表示なし」として隠す。
        public static GameSignal<NetStatsSignal?> NetStats { get; } = new GameSignal<NetStatsSignal?>(nameof(NetStats));

        // 値で毎回出さず集める口で出すのは、相手が補間で毎フレーム動き、見通しも目の位置で変わるため。空ならオフラインの集め方を使う。
        public static GameSignal<INameplateContacts> Nameplates { get; } = new GameSignal<INameplateContacts>(nameof(Nameplates));

        // 本体のプロパティにしないのは、本体が型を知らない他のアセンブリ (追加の乗り物など) の signal も、ここの signal と一緒に ResetAll で空へ戻すため。
        public static GameSignal<T> Create<T>(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("a signal needs a name for its logs", nameof(name));
            }

            var signal = new GameSignal<T>(name);
            _createdResets.Add(signal.Reset);
            return signal;
        }

        // ドメインの再読み込みを切った Play でも、前の Play の値や消えた UI の購読が残らないよう、シーンを読む前にすべて空に戻す。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetAll()
        {
            LocalPlayer.Reset();
            HudModel.Reset();
            Inventory.Reset();
            GrenadeCount.Reset();
            LocalBlindSeconds.Reset();
            LocalHeadingDegrees.Reset();
            Participants.Reset();
            ModeStatus.Reset();
            Scoreboard.Reset();
            InteractHint.Reset();
            SeatFiring.Reset();
            KillCam.Reset();
            Pings.Reset();
            SquadOrder.Reset();
            MenuReturnReason.Reset();
            HudDensity.Reset();
            PauseTimeMode.Reset();
            PaletteViewer.Reset();
            Streak.Reset();
            StreakTargeting.Reset();
            Minimap.Reset();
            Chat.Reset();
            Voice.Reset();
            MapVote.Reset();
            Spectate.Reset();
            ChatSender.Reset();
            MapVoteSender.Reset();
            Party.Reset();
            PartyControl.Reset();
            JoinByCode.Reset();
            ReportSender.Reset();
            BlockControl.Reset();
            Leaderboard.Reset();
            LeaderboardControl.Reset();
            Replays.Reset();
            Cosmetics.Reset();
            Stash.Reset();
            Account.Reset();
            AccountControl.Reset();
            Stamina.Reset();
            LockOnWarning.Reset();
            Emotes.Reset();
            VoteKick.Reset();
            VoteKickSender.Reset();
            Wallet.Reset();
            BuySender.Reset();
            Vision.Reset();
            Drone.Reset();
            NetStats.Reset();
            Nameplates.Reset();
            foreach (var reset in _createdResets)
            {
                reset();
            }
        }
    }
}
