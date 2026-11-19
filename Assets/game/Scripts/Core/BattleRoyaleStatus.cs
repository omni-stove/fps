using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class BattleRoyaleStatus
    {
        public BattleRoyaleStatus(
            BattleRoyaleSession session,
            int? localPlayerId,
            int? reviveTargetId,
            ZoneState? zone,
            bool isLocalPlayerUnarmed,
            IReadOnlyList<SupplyDropMark> supplyDrops = null,
            bool isLocalPlayerAtSupplyDrop = false,
            string interactKeyLabel = null)
            // BattleRoyaleSession のまま渡すとこのコンストラクタ自身が選ばれるので、口の型にして共通の方へ渡す
            : this(
                (IBattleRoyaleView)(session ?? throw new ArgumentNullException(nameof(session))),
                localPlayerId,
                reviveTargetId,
                zone,
                isLocalPlayerUnarmed,
                supplyDrops,
                isLocalPlayerAtSupplyDrop,
                interactKeyLabel)
        {
            Session = session;
            if (localPlayerId.HasValue && session.Mode.HasPlayer(localPlayerId.Value))
            {
                LocalRedeploySeconds = session.GetRedeployRemaining(localPlayerId.Value);
                var squadOut = session.GetPlacement(session.Mode.GetSide(localPlayerId.Value)).HasValue;
                var downedWithoutReturn = session.Mode.IsDown(localPlayerId.Value) && !LocalRedeploySeconds.HasValue;
                LocalRedeploysLeft = (session.IsRedeployOpen && !squadOut && !downedWithoutReturn)
                    || LocalRedeploySeconds.HasValue
                    ? session.GetRedeploysLeft(localPlayerId.Value)
                    : (int?)null;
            }
        }

        // 対戦では戻る待ちと残りの回数を数えるのがサーバーなので、決めた値をそのまま受け取る。
        public BattleRoyaleStatus(
            IBattleRoyaleView game,
            int? localPlayerId,
            int? reviveTargetId,
            ZoneState? zone,
            bool isLocalPlayerUnarmed,
            float? localRedeploySeconds,
            int? localRedeploysLeft,
            IReadOnlyList<SupplyDropMark> supplyDrops = null,
            bool isLocalPlayerAtSupplyDrop = false,
            string interactKeyLabel = null)
            : this(
                game ?? throw new ArgumentNullException(nameof(game)),
                localPlayerId,
                reviveTargetId,
                zone,
                isLocalPlayerUnarmed,
                supplyDrops,
                isLocalPlayerAtSupplyDrop,
                interactKeyLabel)
        {
            if (localRedeploySeconds < 0f || float.IsNaN(localRedeploySeconds ?? 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(localRedeploySeconds), localRedeploySeconds, "localRedeploySeconds must not be negative");
            }

            if (localRedeploysLeft < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(localRedeploysLeft), localRedeploysLeft, "localRedeploysLeft must not be negative");
            }

            LocalRedeploySeconds = localPlayerId.HasValue ? localRedeploySeconds : null;
            LocalRedeploysLeft = localPlayerId.HasValue ? localRedeploysLeft : null;
        }

        private BattleRoyaleStatus(
            IBattleRoyaleView game,
            int? localPlayerId,
            int? reviveTargetId,
            ZoneState? zone,
            bool isLocalPlayerUnarmed,
            IReadOnlyList<SupplyDropMark> supplyDrops,
            bool isLocalPlayerAtSupplyDrop,
            string interactKeyLabel)
        {
            Game = game;
            LocalPlayerId = localPlayerId;
            ReviveTargetId = localPlayerId.HasValue ? reviveTargetId : null;
            Zone = zone;
            IsLocalPlayerUnarmed = localPlayerId.HasValue && isLocalPlayerUnarmed;
            SupplyDrops = supplyDrops == null ? Array.Empty<SupplyDropMark>() : Copy(supplyDrops);
            IsLocalPlayerAtSupplyDrop = localPlayerId.HasValue && isLocalPlayerAtSupplyDrop;
            InteractKeyLabel = interactKeyLabel;

            foreach (var drop in SupplyDrops)
            {
                if (drop.HasLanded)
                {
                    LandedSupplyDrops++;
                }
                else
                {
                    FallingSupplyDrops++;
                }
            }
        }

        // オフラインの試合のときだけある。HUD は Game を読む
        public BattleRoyaleSession Session { get; }

        // Session と同じく写しでなく進んでいる試合 (対戦なら届くたびに書き換わる写し) なので、受け取った側は出された時に読む。
        public IBattleRoyaleView Game { get; }

        public int? LocalPlayerId { get; }

        public int? ReviveTargetId { get; }

        public ZoneState? Zone { get; }

        public bool IsLocalPlayerUnarmed { get; }

        // まだ開けられていない箱 (落ちている途中の物も含む)。開けた箱は載せない。
        public IReadOnlyList<SupplyDropMark> SupplyDrops { get; }

        public int FallingSupplyDrops { get; }

        public int LandedSupplyDrops { get; }

        public bool IsLocalPlayerAtSupplyDrop { get; }

        public string InteractKeyLabel { get; }

        public float? LocalRedeploySeconds { get; }

        public int? LocalRedeploysLeft { get; }

        private static SupplyDropMark[] Copy(IReadOnlyList<SupplyDropMark> drops)
        {
            var copy = new SupplyDropMark[drops.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = drops[i];
            }

            return copy;
        }
    }
}
