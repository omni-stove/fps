namespace Shooter.Core
{
    public enum DronePilotEnd
    {
        // 操縦する人が自分で降りた。ドローンは残って見回し続ける。
        Interact,

        // ドローンが壊された・落ちた・戻された。
        DroneGone,

        // 操縦する人が倒れた・倒れ込んだ。
        Incapacitated,

        // 操縦の部品が無効になった・消えた。
        Disabled,
    }
}
