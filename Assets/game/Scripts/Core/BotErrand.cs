using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// bot が戦いのほかにする用事 (<see cref="BattleRoyaleBotPlanner.Plan"/> が決める) か、人が出した分隊の指示 (<see cref="IsOrder"/>)。
    /// 行き先と、戦っている最中でも優先するかを持つ。指示の扱いは <see cref="BotDriver.FollowOrder"/> が決める。
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

        /// <summary>
        /// 行き先 (足元)。<see cref="BotErrandKind.None"/> では使わない。
        /// <see cref="BotErrandKind.Attack"/> では狙う敵の足元、<see cref="BotErrandKind.Follow"/> では付いていく人の足元で、相手が動いたら呼び出し側が <see cref="WithDestination"/> で更新する。
        /// </summary>
        public Vector3 Destination { get; }

        /// <summary>
        /// 用事の相手の番号。<see cref="BotErrandKind.Collect"/> では拾える物の候補の番号、<see cref="BotErrandKind.Revive"/> ではダウンした味方の候補の番号。
        /// それ以外は -1。
        /// </summary>
        public int TargetIndex { get; }

        /// <summary>true なら、敵を追ったり撃ち合ったりしている最中でもこの行き先へ動く (撃つのはやめない)。</summary>
        public bool OverridesCombat { get; }

        public bool HasErrand => Kind != BotErrandKind.None;

        /// <summary>人が出した分隊の指示 (<see cref="BotErrandKind.Hold"/>・<see cref="BotErrandKind.Attack"/>・<see cref="BotErrandKind.Follow"/>) なら true。</summary>
        public bool IsOrder => IsOrderKind(Kind);

        /// <summary>place へ行って守る指示。</summary>
        public static BotErrand Hold(Vector3 place) => new BotErrand(BotErrandKind.Hold, place, -1, true);

        /// <summary>今 target (足元) にいる敵を狙う指示。</summary>
        public static BotErrand Attack(Vector3 target) => new BotErrand(BotErrandKind.Attack, target, -1, true);

        /// <summary>今 leader (足元) にいる人に付いていく指示。</summary>
        public static BotErrand Follow(Vector3 leader) => new BotErrand(BotErrandKind.Follow, leader, -1, false);

        public static bool IsOrderKind(BotErrandKind kind)
        {
            return kind == BotErrandKind.Hold || kind == BotErrandKind.Attack || kind == BotErrandKind.Follow;
        }

        /// <summary>行き先だけを destination に替えたもの。狙う敵や付いていく人が動いたときに使う。</summary>
        public BotErrand WithDestination(Vector3 destination) => new BotErrand(Kind, destination, TargetIndex, OverridesCombat);
    }
}
