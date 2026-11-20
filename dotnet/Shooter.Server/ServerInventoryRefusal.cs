namespace Shooter.Server
{
    // 持ち物の要求を、中身を見る前に送った人の今の状態で断る理由
    internal enum ServerInventoryRefusal
    {
        None,
        NotInMatch,
        NotPlaying,
        Dead,
        Downed,
        BetweenRounds,
    }
}
