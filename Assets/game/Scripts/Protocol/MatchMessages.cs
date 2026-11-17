using System.Collections.Generic;

namespace Shooter.Protocol
{
    public static class MatchMessages
    {
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

        public static MessageTable CreateServerTable()
        {
            return new MessageTable(CreateClientToServer());
        }

        public static MessageTable CreateClientTable()
        {
            return new MessageTable(CreateServerToClient());
        }
    }
}
