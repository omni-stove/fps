using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Server
{
    // ルールを試合の途中で替えず次の試合の始めに替えるのは、点数・時間の上限と目的が試合の途中で入れ替わると、
    // それまでに取った点数と残り時間が新しい目的では意味を持たなくなるため。
    internal sealed partial class ServerMatch
    {
        // ChooseTeam と同じく、目的を作るルールのモジュールが差し込む。受け付けられないなら理由を返す。null なら確かめずに受け付ける。
        // 2 つ目はそのルールで遊びうるマップで、null なら試合のすべてのマップ
        public Func<GameRules, IReadOnlyList<ServerMapData>, string> CheckNextRules { get; set; }

        // MapChanged の後、Restarted より前に呼ぶ。モジュールが目的・上限を新しいルールのものにしてから、ルールが全員を出現させるため
        public event Action<GameRules> RulesChanged;

        public GameRules NextRules { get; private set; }

        // 受け付けられなかったときは、前に受け付けた次の試合のルールを残す
        public bool RequestNextRules(GameRules rules, out string error)
        {
            return RequestNextRules(rules, null, out error);
        }

        // maps は次の試合で行きうるマップ。null なら試合のすべてのマップで遊べることを求める
        public bool RequestNextRules(GameRules rules, IReadOnlyList<ServerMapData> maps, out string error)
        {
            ArgumentNullException.ThrowIfNull(rules);
            var problem = CheckNextRules?.Invoke(rules, maps);
            if (problem != null)
            {
                error = problem;
                return false;
            }

            NextRules = rules;
            error = null;
            return true;
        }

        partial void ChangeRulesForNextMatch()
        {
            var next = NextRules;
            NextRules = null;
            if (next == null)
            {
                return;
            }

            RulesChanged?.Invoke(next);
        }
    }
}
