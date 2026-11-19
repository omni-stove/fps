namespace Shooter.Core
{
    // ViewMode に三人称の種類として混ぜないのは、デバッグなどで視点だけ切り替えても三人称のときの覗き方が残るようにするため。
    public enum AdsEyeView
    {
        PerWeapon = 0,

        Always = 1,

        Never = 2,
    }
}
