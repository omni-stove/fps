namespace Shooter.Core
{
    // 値は通信路に載るので変えない。
    public enum AntiCheatPacketKind : byte
    {
        Invalid = 0,

        Hello = 1,

        Message = 2,
    }
}
