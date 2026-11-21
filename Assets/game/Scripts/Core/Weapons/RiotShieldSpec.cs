using System;

namespace Shooter.Core
{
    // 盾を武器として持つときの部品。持っている間は前に盾を出して弾を止め、撃つ代わりに盾で殴る。
    public sealed class RiotShieldSpec
    {
        public const float DefaultBlockArcDegrees = 120f;

        // 体の前を肩から膝まで隠す大きさ。
        public const float DefaultWidth = 0.6f;
        public const float DefaultHeight = 1.2f;

        public const float DefaultMoveSpeedMultiplier = 0.8f;

        public const int DefaultBashDamage = 40;
        public const float DefaultBashReach = 1.6f;
        public const float DefaultBashArcDegrees = 90f;
        public const float DefaultBashSwingSeconds = 0.6f;
        public const float DefaultBashCooldownSeconds = 0.5f;
        public const float DefaultBashImpactSeconds = 0.25f;
        public const float DefaultBashLungeDistance = 0.5f;

        public const float MaxBlockArcDegrees = 360f;

        // 持ち主の原点 (体の中心) から盾の板の中心までの前への距離と、板の厚さ (m)。体の半径 (0.5m) より前に出し、体と重ねない。
        // オフラインの仮の板 (HeldRiotShield) とサーバーの当たり (ServerHitboxes) が同じ所に板を置くよう、ここ 1 か所に持つ。
        public const float HeldForward = 0.6f;
        public const float HeldThickness = 0.05f;

        public static readonly MeleeSpec DefaultBash = new MeleeSpec(
            DefaultBashDamage,
            DefaultBashReach,
            DefaultBashArcDegrees,
            DefaultBashSwingSeconds,
            DefaultBashCooldownSeconds,
            DefaultBashImpactSeconds,
            DefaultBashLungeDistance);

        public static readonly RiotShieldSpec Default = new RiotShieldSpec();

        public RiotShieldSpec(
            float blockArcDegrees = DefaultBlockArcDegrees,
            float width = DefaultWidth,
            float height = DefaultHeight,
            float moveSpeedMultiplier = DefaultMoveSpeedMultiplier,
            MeleeSpec bash = null)
        {
            if (!(blockArcDegrees > 0f && blockArcDegrees <= MaxBlockArcDegrees))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(blockArcDegrees), blockArcDegrees, nameof(blockArcDegrees) + " must be greater than 0 and at most 360");
            }

            RequirePositiveFinite(width, nameof(width));
            RequirePositiveFinite(height, nameof(height));

            // 1 を超える値は受け付けない。盾を持つほうが速く動けると、撃てない代わりの重さが無くなる。
            if (!(moveSpeedMultiplier > 0f && moveSpeedMultiplier <= 1f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(moveSpeedMultiplier), moveSpeedMultiplier, nameof(moveSpeedMultiplier) + " must be greater than 0 and at most 1");
            }

            BlockArcDegrees = blockArcDegrees;
            Width = width;
            Height = height;
            MoveSpeedMultiplier = moveSpeedMultiplier;
            Bash = bash ?? DefaultBash;
        }

        // 盾の正面を中心にした扇の全体の角度。撃った者がこの扇の外にいれば、盾の板に当たっても止めずに持ち主へ通す。
        public float BlockArcDegrees { get; }

        // 差し替えの見た目が無いときに作る板の幅と高さ (m)。
        public float Width { get; }

        public float Height { get; }

        // 武器の MoveSpeedMultiplier とは別に、盾を構えている間だけ掛ける。走って盾を下げている間は掛けない。
        public float MoveSpeedMultiplier { get; }

        // プレイヤーの近接攻撃と同じ形にし、当たりの探し方 (MeleeStrike) と振りの進め方 (MeleeSwing) を共有する。
        public MeleeSpec Bash { get; }

        private static void RequirePositiveFinite(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be positive and finite");
            }
        }
    }
}
