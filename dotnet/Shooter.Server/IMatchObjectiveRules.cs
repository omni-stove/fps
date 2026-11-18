using System.IO;
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

        void FillMatch(ref MatchSnapshot snapshot);

        void FillControlPoints(SnapshotList<ControlPointSnapshot> points);

        // 旗・爆弾のように運べる物。受け取る並びは空にしてから書く
        void FillObjectiveItems(SnapshotList<ObjectiveItemSnapshot> items);
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
