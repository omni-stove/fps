using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 地面の拾い物と弾薬箱。中身の詳しいこと (武器の Id・量) はサーバーだけが持ち、クライアントは種類で見た目を選ぶ
    public readonly struct ClientPickupItem
    {
        public ClientPickupItem(int id, ObjectiveItemKind kind, Vector3 position, bool opened, float radius, int? ownerId)
        {
            Id = id;
            Kind = kind;
            Position = position;
            Opened = opened;
            Radius = radius;
            OwnerId = ownerId;
        }

        public int Id { get; }

        public ObjectiveItemKind Kind { get; }

        // 拾い物は足元、弾薬箱は底面の中心
        public Vector3 Position { get; }

        // 拾い物なら拾われて出直しを待っている、弾薬箱なら次に使えるまで待っている
        public bool Opened { get; }

        public float Radius { get; }

        // 倒れた人が落とした物なら落とした人。マップの物は null
        public int? OwnerId { get; }

        public bool IsAmmoCrate => Kind == ObjectiveItemKind.AmmoCrate;

        public bool IsAvailable => !Opened;

        // 弾薬箱なら false
        public bool TryGetPickupKind(out PickupKind kind)
        {
            return PickupItemKinds.TryGetPickupKind(Kind, out kind);
        }

        public float HorizontalDistanceTo(Vector3 position)
        {
            var offset = position - Position;
            return new Vector2(offset.X, offset.Z).Length();
        }

        // サーバー (ServerPickups.IsInReach) と同じく、水平の距離と、足元と物の高さの差で見る
        public bool IsInReach(Vector3 feet)
        {
            return HorizontalDistanceTo(feet) <= Radius && System.Math.Abs(Position.Y - feet.Y) <= ClientPickups.MaxVerticalReach;
        }
    }
}
