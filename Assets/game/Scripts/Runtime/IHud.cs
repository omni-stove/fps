using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// ゲーム進行 (<see cref="GameBootstrap"/>・<see cref="MatchRunner"/>・モードの Runner・<see cref="CampaignRunner"/> など) から見た HUD。
    /// 作るのは <see cref="GameUi.HudFactory"/> に登録された生成役で、ゲーム進行は具体的な UI のクラスを知らない。
    /// 部分ごとの表示 (<see cref="Scoreboard"/> など) は、その HUD が持っていなければ null を返す。
    /// </summary>
    public interface IHud
    {
        // Component.gameObject がそのまま実装になるよう、Unity の名前に合わせる。
        /// <summary>HUD を載せている GameObject。作ったものを片付けるときに使う。</summary>
        GameObject gameObject { get; }

        /// <summary>この HUD 用に作り、HUD を消すときに一緒に消す PanelSettings。無ければ null。</summary>
        PanelSettings OwnedPanelSettings { get; }

        /// <summary>試合や対戦の点数を一覧にする表示。無ければ null。</summary>
        IScoreboard Scoreboard { get; }

        /// <summary>弾が当たったことを出す印。無ければ null。</summary>
        IHitMarker HitMarker { get; }

        /// <summary>オフラインのモード (<see cref="ModeRunnerBase"/>) の表示。無ければ null。</summary>
        IModeHud ModeHud { get; }

        /// <summary>射撃場のチュートリアルの案内。無ければ null。</summary>
        ITutorialPrompt TutorialPrompt { get; }

        /// <summary>HUD の量 (<see cref="GameRules.HudDensity"/>) に合わせて出す項目を切り替える。</summary>
        /// <exception cref="System.ArgumentOutOfRangeException">density が定義済みの値でない。</exception>
        void ApplyDensity(HudDensity density);

        /// <summary>残り時間を出す試合を差し替える。null なら仮の表示に戻す。</summary>
        void BindMatch(Match match);

        /// <summary>一時的な案内の源を差し替える。null なら案内を消す。</summary>
        void BindNotice(CampaignNotice notice);

        /// <summary>
        /// viewer のチームから見た敵味方の色で塗り、設定の色の組が変わったら塗り直す。viewer が null なら Alpha を味方の色にする。
        /// </summary>
        void UseTeamPalette(Team? viewer);

        /// <summary>ポーズを開いている間に時間をどう扱うか。</summary>
        void SetPauseTimeMode(PauseTimeMode mode);

        /// <summary>
        /// 残りのグレネードの数の表示を (無ければ) 付けて返す。画面に出す場所が見つからなければエラーをログに出し、出せないまま返す。
        /// </summary>
        IGrenadeCount AttachGrenadeCount();

        /// <summary>
        /// 能力の枠ごとの回数と待ち時間の表示を (無ければ) 付けて返す。画面に出す場所が見つからなければエラーをログに出し、出せないまま返す。
        /// </summary>
        IAbilitySlots AttachAbilitySlots();

        /// <summary>
        /// 拠点制圧 (<see cref="Domination"/>) の表示を (無ければ) 付けて返す。画面に出す場所が見つからなければエラーをログに出し、出せないまま返す。
        /// </summary>
        IDominationHud AttachDomination();
    }
}
