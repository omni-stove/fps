using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // Horde の防衛物 (壊れる建物) に触れる口。いくら払うか・払えるかは HordeRules が決め、部品を戻して全員へ送るのはこの実装が持つ
    internal interface IHordeFortifications
    {
        // 水平の距離が radius 以内で、直す・置くものがある防衛物のうち、いちばん近いもの。無ければ null
        HordeFortificationTarget? FindInReach(Vector3 position, float radius);

        // 戻した部品の数を返す。生きている人の体と重なる部品は戻さない
        int Restore(int fortificationId, HordeFortificationAction action);
    }

    internal readonly struct HordeFortificationTarget
    {
        public HordeFortificationTarget(int id, string nameKey, HordeFortificationAction action, int restorablePieces)
        {
            Id = id;
            NameKey = nameKey ?? string.Empty;
            Action = action;
            RestorablePieces = restorablePieces < 0 ? 0 : restorablePieces;
        }

        public int Id { get; }

        // ログと表示の鍵。表示の文はクライアントが鍵から引く
        public string NameKey { get; }

        public HordeFortificationAction Action { get; }

        public int RestorablePieces { get; }
    }
}
