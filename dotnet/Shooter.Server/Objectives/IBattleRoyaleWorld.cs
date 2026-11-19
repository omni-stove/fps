using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // Battle Royale の目的が試合の場 (移動・武器・ダメージ・箱) に触れる口。目的の工場は設定と IMatchObjectiveHost しか渡さないので、
    // つなぐ側が ServerBattleRoyaleWorld (ServerMovement・ServerCombat・ServerInventories・ServerLoot) で作って渡す
    internal interface IBattleRoyaleWorld
    {
        // 武器をすべて手放させる (始まりと復帰。拾うまで撃てない)
        void Disarm(int playerId);

        bool IsArmed(int playerId);

        // その位置からパラシュートで降ろす。置けた人がいれば true
        bool Drop(int playerId, Vector3 position);

        // エリアの外のダメージ。撃った人のいないダメージとして与える
        DamageOutcome HurtByZone(int playerId, int amount);

        // 返すのはスナップショットの物の Id
        int SpawnCrate(Vector3 position, string tableId, float reach);

        // 体の無い人なら置かずに null
        int? SpawnDeathBox(int playerId);

        // 箱へ着いた Bot に開けさせる。人の開ける要求 (InteractRequestMessage) と同じ判断を通す
        InteractVerdict Open(int playerId, int itemId);

        void CollectUnopenedBoxes(List<ServerLootBox> boxes);
    }
}
