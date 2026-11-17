namespace Shooter.Protocol
{
    // 後から足しても既存の Id を動かさないよう、送る向きと種類ごとに範囲を分ける
    // (接続 1-15、クライアントからの入力と要求 16-47、状態 48-63、サーバーからのイベント 64-)。
    public static class MessageIds
    {
        public const ushort ConnectRequest = 1;
        public const ushort ConnectAccepted = 2;
        public const ushort ConnectRejected = 3;
        public const ushort ServerDisconnecting = 4;
        public const ushort NetStatsProbe = 5;
        public const ushort NetStatsEcho = 6;
        public const ushort AntiCheatToServer = 7;
        public const ushort AntiCheatToClient = 8;

        public const ushort Input = 16;
        public const ushort FireRequest = 17;
        public const ushort WeaponActionRequest = 18;
        public const ushort SwitchWeaponRequest = 19;
        public const ushort LoadoutRequest = 20;
        public const ushort ThrowGrenadeRequest = 21;
        public const ushort PingRequest = 22;
        public const ushort ReviveRequest = 23;
        public const ushort InventoryRequest = 24;
        public const ushort VehicleRequest = 25;
        public const ushort VehicleMove = 26;
        public const ushort ChatRequest = 27;
        public const ushort MapVoteRequest = 28;
        public const ushort SpectateRequest = 29;

        public const ushort Snapshot = 48;
        public const ushort StructureState = 49;
        public const ushort MapVoteState = 50;
        public const ushort VoiceRoster = 51;

        public const ushort Shot = 64;
        public const ushort HitConfirm = 65;
        public const ushort DamageTaken = 66;
        public const ushort Killed = 67;
        public const ushort Explosion = 68;
        public const ushort PingPlaced = 69;
        public const ushort PingMoved = 70;
        public const ushort PingLost = 71;
        public const ushort PingRemoved = 72;
        public const ushort Teleported = 73;
        public const ushort ShotIndexCorrected = 74;
        public const ushort IdleWarning = 75;
        public const ushort LoadoutAnswer = 76;
        public const ushort ProjectileSpawned = 77;
        public const ushort ProjectileEnded = 78;
        public const ushort BeamState = 79;
        public const ushort StatusEffect = 80;
        public const ushort AreaEffect = 81;
        public const ushort InventoryState = 82;
        public const ushort VehicleCorrected = 83;
        public const ushort Chat = 84;
    }
}
