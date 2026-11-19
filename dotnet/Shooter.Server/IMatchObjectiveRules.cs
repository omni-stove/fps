using System;
using System.IO;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 試合の目的 (MatchObjective) ごとの勝ち負けの決め方。MatchRulesModule は体力・キル・出現を Session (TeamDeathmatchSession) に任せたまま、
    // 目的ごとの点数と終わり方をこの実装に任せる。実装は ServerObjectiveFactory が MatchObjective から選ぶ
    internal interface IMatchObjectiveRules
    {
        MatchObjective Objective { get; }

        // ラウンドに区切る目的なら、MatchRulesModule はスナップショットにラウンドの段階 (CurrentRoundPhase) を載せる
        bool PlaysInRounds { get; }

        // 試合の点数 (ServerMatch.SetScores) を目的が書くなら、キルの点数で書き換えない
        bool KeepsMatchScore { get; }

        // true の間は誰も撃てず、ダメージも通らない
        bool HoldsFire { get; }

        // 試合の残り時間を目的が数えるなら、その秒数。null なら Session の残り時間を使う
        float? RemainingSeconds { get; }

        // Session の点数・時間の上限と出直し方。目的が勝ち負けを決めるなら、Session が先に終わらないよう上限を届かない値にする
        TeamDeathmatchSpec CreateSessionSpec(GameRules rules, NetMatchSettings settings);

        // 遊べないマップなら InvalidOperationException。次の試合で替わる先のマップを起動のうちに確かめるために呼ぶ
        void CheckMap(ServerMapData map, NetMapPoints points);

        // 今のマップに合わせて作り直す。起動の Session を作る前と、次の試合でマップが替わったときに呼ぶ
        void UseMap(ServerMapData map, NetMapPoints points);

        void Start();

        void Stop();

        // 試合を終えるときに true。MatchRulesModule は続けて DecideWinner の勝者で Session を Finish する
        bool Tick(bool matchPlaying, float deltaSeconds);

        void PlayerJoined(int playerId);

        void PlayerLeft(int playerId);

        void PlayerKilled(int killerId, int victimId);

        void MatchRestarted();

        void MatchEnded();

        RoundPhase? CurrentRoundPhase();

        // killWinner は Session がキル数で決めた勝者
        Team? DecideWinner(Team? killWinner);

        // 勝ちを数えるときに、この人が勝ったか。勝者が 1 人の目的は、チームでなく勝った人で決める
        bool IsWinningPlayer(int playerId, Team playerTeam, Team? winner)
        {
            return winner.HasValue && playerTeam == winner.Value;
        }

        // Bot に巡らせる場所。null なら ServerBots はマップの出現地点を巡らせる。動く目的 (台車) は Tick ごとに今の場所を返す
        ObjectiveRally? BotRally => null;

        // 敵味方を目的が決める (分隊で戦う BR など) ための口。戦闘・Bot に加えて Session も味方撃ち・ダウン・蘇生の味方をこれで決める。
        // 陣営を引数で渡すのは、目的が Session を持たずに既定のチームの判定を返せるようにするため
        bool AreHostile(int a, Team aTeam, int b, Team bTeam)
        {
            return a != b && aTeam != bTeam;
        }

        // 場に置いた物 (箱など) を開ける要求。開ける物を持たない目的は断る
        InteractVerdict Interact(int playerId, int itemId)
        {
            return InteractVerdict.Refused(InteractRefusal.NotInteractable);
        }

        // この Bot を向かわせる先。null なら ServerBots は BotRally か出現地点を巡らせる。
        // 先を少しずつ動かすと Bot の巡る路を毎回作り直すので、同じ先の間は同じ値を返す
        Vector3? BotGoal(int playerId) => null;

        void FillMatch(ref MatchSnapshot snapshot);

        void FillControlPoints(SnapshotList<ControlPointSnapshot> points);

        // 旗・爆弾・タグのように場に置く物。持たない目的は空にする
        void FillObjectiveItems(SnapshotList<ObjectiveItemSnapshot> items);

        // Session が書いた行のうち、目的が数え直すもの (FFA のキル数など) を上書きする。Session に居ない人には呼ばない
        void FillPlayer(int playerId, ref PlayerSnapshot snapshot);
    }

    // ラウンドの区切りでお金を渡せる目的 (今は Search and Destroy)。MatchRulesModule の経済は、この出来事で買える時間を開け閉めして額を渡す。
    // IMatchObjectiveRules に足さないのは、ラウンドの無い目的に空の出来事を持たせないため
    internal interface IEconomyRounds
    {
        // ラウンドの準備が始まり、全員を出直させた後。引数はラウンドの番号
        event Action<int> PreparationStarted;

        event Action<int> RoundStarted;

        // 引き分けなら null
        event Action<Side?> RoundEnded;

        event Action SidesSwapped;

        event Action<Side> Ended;
    }

    // 目的の実装が試合の側に触れる口。Session を直接渡さないのは、Session が作られる前に目的を選んで Session の上限を決めるため
    internal interface IMatchObjectiveHost
    {
        TeamDeathmatchSession Session { get; }

        ServerBodies Bodies { get; }

        TextWriter Log { get; }

        void SetMatchScores(int alpha, int bravo);

        // 倒れている人の蘇生の受け付けを捨てる
        void ClearDowned();
    }
}
