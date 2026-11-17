using System.Collections.Generic;

namespace Shooter.Protocol
{
    /// <summary>対戦で送り合うメッセージを、受け取る側ごとにそろえる。</summary>
    public static class MatchMessages
    {
        /// <summary>クライアントが送り、サーバーが受け取るメッセージを 1 つずつ新しく作る。</summary>
        public static IReadOnlyList<IMessage> CreateClientToServer()
        {
            return new IMessage[]
            {
                new ConnectRequestMessage(),
                new NetStatsProbeMessage(),
                new AntiCheatToServerMessage(),
                new InputMessage(),
                new FireRequestMessage(),
                new WeaponActionRequestMessage(),
                new SwitchWeaponRequestMessage(),
                new LoadoutRequestMessage(),
                new ThrowGrenadeRequestMessage(),
                new PingRequestMessage(),
                new ReviveRequestMessage(),
                new InventoryRequestMessage(),
                new VehicleRequestMessage(),
                new VehicleMoveMessage(),
            };
        }

        /// <summary>サーバーが送り、クライアントが受け取るメッセージを 1 つずつ新しく作る。</summary>
        public static IReadOnlyList<IMessage> CreateServerToClient()
        {
            return new IMessage[]
            {
                new ConnectAcceptedMessage(),
                new ConnectRejectedMessage(),
                new ServerDisconnectingMessage(),
                new NetStatsEchoMessage(),
                new AntiCheatToClientMessage(),
                new SnapshotMessage(),
                new StructureStateMessage(),
                new ShotMessage(),
                new HitConfirmMessage(),
                new DamageTakenMessage(),
                new KilledMessage(),
                new ExplosionMessage(),
                new PingPlacedMessage(),
                new PingMovedMessage(),
                new PingLostMessage(),
                new PingRemovedMessage(),
                new TeleportedMessage(),
                new ShotIndexCorrectedMessage(),
                new IdleWarningMessage(),
                new LoadoutAnswerMessage(),
                new ProjectileSpawnedMessage(),
                new ProjectileEndedMessage(),
                new BeamStateMessage(),
                new StatusEffectMessage(),
                new AreaEffectMessage(),
                new InventoryStateMessage(),
                new VehicleCorrectedMessage(),
            };
        }

        /// <summary>サーバーが受け取るメッセージの表。</summary>
        public static MessageTable CreateServerTable()
        {
            return new MessageTable(CreateClientToServer());
        }

        /// <summary>クライアントが受け取るメッセージの表。</summary>
        public static MessageTable CreateClientTable()
        {
            return new MessageTable(CreateServerToClient());
        }
    }
}
