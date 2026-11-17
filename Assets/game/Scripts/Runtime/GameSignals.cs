using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>
    /// ゲーム進行が UI に渡すものの置き場。項目ごとに今の値とそれが変わったときのイベントを持つ (<see cref="GameSignal{T}"/>)。
    /// ゲーム進行は値を出す (<see cref="GameSignal{T}.Publish"/>) だけで、どの UI がどう出すかは知らない。UI は項目を購読し、
    /// 購読し始めたときは今の値 (<see cref="GameSignal{T}.Value"/>) を読んで合わせる。
    /// シーンを読む前 (<see cref="RuntimeInitializeLoadType.SubsystemRegistration"/>) に、値と購読者をすべて空に戻す
    /// (ドメインの再読み込みを切った Play でも、前の Play の値や消えた UI の購読が残らないように)。
    /// </summary>
    public static class GameSignals
    {
        public const string LogTag = "[Signals]";

        /// <summary>この画面で操作しているプレイヤーの部品。いなければ null。</summary>
        public static GameSignal<LocalPlayerSignal> LocalPlayer { get; } = new GameSignal<LocalPlayerSignal>(nameof(LocalPlayer));

        /// <summary>この画面のプレイヤーの体力・弾・試合の残り時間などをまとめたもの。無ければ null。</summary>
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

        /// <summary>オフラインのモード (<see cref="ModeRunnerBase"/>) の今の状態。モードを進めていなければ null。</summary>
        public static GameSignal<ModeStatus> ModeStatus { get; } = new GameSignal<ModeStatus>(nameof(ModeStatus));

        /// <summary>拠点制圧の試合の今の状態。進めていなければ null。</summary>
        public static GameSignal<DominationStatus> Domination { get; } = new GameSignal<DominationStatus>(nameof(Domination));

        /// <summary>スコアボードの行。出すものが無ければ null。</summary>
        public static GameSignal<IReadOnlyList<ScoreboardEntry>> Scoreboard { get; } =
            new GameSignal<IReadOnlyList<ScoreboardEntry>>(nameof(Scoreboard));

        /// <summary>近くで Interact したら何が起きるかの案内。無ければ null。</summary>
        public static GameSignal<InteractHint?> InteractHint { get; } = new GameSignal<InteractHint?>(nameof(InteractHint));

        /// <summary>射撃場のチュートリアルの今の段階。チュートリアルをしていなければ null。</summary>
        public static GameSignal<TutorialStatus?> Tutorial { get; } = new GameSignal<TutorialStatus?>(nameof(Tutorial));

        /// <summary>再生しているキルカム。再生していなければ null。</summary>
        public static GameSignal<KillCamStatus?> KillCam { get; } = new GameSignal<KillCamStatus?>(nameof(KillCam));

        /// <summary>この画面のプレイヤーが付けている印。無ければ null。</summary>
        public static GameSignal<IReadOnlyList<PingMarker>> Pings { get; } = new GameSignal<IReadOnlyList<PingMarker>>(nameof(Pings));

        /// <summary>プレイヤーが味方の bot に出している指示。出していなければ null。</summary>
        public static GameSignal<SquadOrderStatus?> SquadOrder { get; } = new GameSignal<SquadOrderStatus?>(nameof(SquadOrder));

        /// <summary>キャンペーンが一時的に出す案内。キャンペーンをしていなければ null。</summary>
        public static GameSignal<CampaignNotice> CampaignNotice { get; } = new GameSignal<CampaignNotice>(nameof(CampaignNotice));

        /// <summary>メニューに戻った理由 (サーバーとの接続を失った理由)。無ければ null。</summary>
        public static GameSignal<DisconnectNotice> MenuReturnReason { get; } = new GameSignal<DisconnectNotice>(nameof(MenuReturnReason));

        /// <summary>HUD に出す量。ルールが決まるまでは null。</summary>
        public static GameSignal<HudDensity?> HudDensity { get; } = new GameSignal<HudDensity?>(nameof(HudDensity));

        /// <summary>ポーズを開いている間の時間の扱い。決まるまでは null。</summary>
        public static GameSignal<PauseTimeMode?> PauseTimeMode { get; } = new GameSignal<PauseTimeMode?>(nameof(PauseTimeMode));

        /// <summary>敵味方の色の組をどのチームから見て塗るか。自分のチームがまだ無ければ null。</summary>
        public static GameSignal<Team?> PaletteViewer { get; } = new GameSignal<Team?>(nameof(PaletteViewer));

        /// <summary>すべての項目の値と購読者を空に戻す。</summary>
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
        }
    }
}
