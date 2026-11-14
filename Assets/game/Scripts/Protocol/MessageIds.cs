namespace Fps.Protocol
{
    /// <summary>
    /// 対戦のメッセージの <see cref="IMessage.Id"/>。送る向きと種類ごとに範囲を分け、後から足しても既存の Id を動かさない。
    /// </summary>
    public static class MessageIds
    {
        // 接続 (1-15)
        public const ushort ConnectRequest = 1;
        public const ushort ConnectAccepted = 2;
        public const ushort ConnectRejected = 3;
        public const ushort ServerDisconnecting = 4;
        public const ushort NetStatsProbe = 5;
        public const ushort NetStatsEcho = 6;

        // クライアントからの入力と要求 (16-47)
        public const ushort Input = 16;
        public const ushort FireRequest = 17;
        public const ushort WeaponActionRequest = 18;
        public const ushort SwitchWeaponRequest = 19;
        public const ushort LoadoutRequest = 20;
        public const ushort ThrowGrenadeRequest = 21;
        public const ushort PingRequest = 22;
        public const ushort ReviveRequest = 23;

        // 状態 (48-63)
        public const ushort Snapshot = 48;
        public const ushort StructureState = 49;

        // サーバーからのイベント (64-)
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
    }
}
