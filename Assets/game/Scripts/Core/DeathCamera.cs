namespace Shooter.Core
{
    public enum DeathCamera
    {
        None,

        Killer,

        Ally,

        Free,

        // 値を保存・同期に使うので、後から足した KillCam と Fallen は最後に置く。
        KillCam,

        Fallen,
    }
}
