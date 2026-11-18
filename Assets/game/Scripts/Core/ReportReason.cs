namespace Shooter.Core
{
    // 通報は送る値をそのまま記録に残すので、番号を変えると古い記録の読み方が変わる。足すときは末尾に足す。
    public enum ReportReason : byte
    {
        Cheating = 0,
        Abuse = 1,
        Spam = 2,
        OffensiveName = 3,
        Other = 4,
    }
}
