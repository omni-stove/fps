namespace Shooter.Protocol
{
    // IMessage に足さず別にするのは、前の版を読む必要のない種類まで書き換えずに、違う版は断るままにするため。
    public interface IReadsOlderVersions
    {
        byte OldestReadableVersion { get; }

        void ReadOlder(MessageReader reader, byte version);
    }
}
