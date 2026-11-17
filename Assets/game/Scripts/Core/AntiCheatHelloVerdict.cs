namespace Shooter.Core
{
    public enum AntiCheatHelloVerdict
    {
        Unknown = 0,

        // 前にも受け取った Hello でも、相手がこちらの Hello を待っているかもしれないので、こちらの Hello を送り直す。
        Duplicate = 1,

        Accepted = 2,

        Registered = 3,

        Rejected = 4,
    }
}
