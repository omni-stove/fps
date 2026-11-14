using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// bot が戦いのほかにする用事 (<see cref="BattleRoyaleBotPlanner.Plan"/> が決める)。行き先と、戦っている最中でも優先するかを持つ。
    /// </summary>
    public readonly struct BotErrand
    {
        public BotErrand(BotErrandKind kind, Vector3 destination, int targetIndex, bool overridesCombat)
        {
            Kind = kind;
            Destination = destination;
            TargetIndex = targetIndex;
            OverridesCombat = overridesCombat;
        }

        /// <summary>用事は無い。</summary>
        public static BotErrand None => new BotErrand(BotErrandKind.None, Vector3.Zero, -1, false);

        public BotErrandKind Kind { get; }

        /// <summary>行き先 (足元)。<see cref="BotErrandKind.None"/> では使わない。</summary>
        public Vector3 Destination { get; }

        /// <summary>
        /// 用事の相手の番号。<see cref="BotErrandKind.Collect"/> では拾える物の候補の番号、<see cref="BotErrandKind.Revive"/> ではダウンした味方の候補の番号。
        /// それ以外は -1。
        /// </summary>
        public int TargetIndex { get; }

        /// <summary>true なら、敵を追ったり撃ち合ったりしている最中でもこの行き先へ動く (撃つのはやめない)。</summary>
        public bool OverridesCombat { get; }

        public bool HasErrand => Kind != BotErrandKind.None;
    }
}
