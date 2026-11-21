using System;
using System.Numerics;

namespace Shooter.Core
{
    // Unity 抜きのサーバーがシーンの HordeFortification と同じ防衛物を直す・置くよう、Unity の Editor が書き出したものを読む。
    // 番号は NetMapStructures の建物の番号 (MapColliderGroups.Piece の建物) と同じ。
    public sealed class MapFortification
    {
        // 部品の形を縮めて重なりを見るのは、隣に立っているだけの人を重なりとみなさないため。オフラインの HordeFortification と同じ値
        public const float BlockCheckShrink = 0.9f;

        public MapFortification(int structure, Vector3 position, bool startsBuilt, string nameKey)
        {
            if (structure < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(structure), structure, "structure must not be negative");
            }

            if (!IsFinite(position.X) || !IsFinite(position.Y) || !IsFinite(position.Z))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            Structure = structure;
            Position = position;
            StartsBuilt = startsBuilt;
            NameKey = nameKey ?? string.Empty;
        }

        public int Structure { get; }

        // 届くかを測る基準の点。オフラインの HordeFortification と同じく防衛物の Transform の位置
        public Vector3 Position { get; }

        // false なら試合の始まりに部品を全部外し、通貨を払って置く場所にする
        public bool StartsBuilt { get; }

        // ログと表示の鍵。表示の文はクライアントが鍵から引く
        public string NameKey { get; }

        // オフラインの HordeFortification.HorizontalDistanceTo と同じく高さを除いて測る
        public float HorizontalDistanceTo(Vector3 point)
        {
            var dx = point.X - Position.X;
            var dz = point.Z - Position.Z;
            return (float)Math.Sqrt((dx * dx) + (dz * dz));
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
