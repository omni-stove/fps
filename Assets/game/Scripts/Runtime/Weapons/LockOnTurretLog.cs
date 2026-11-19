namespace Shooter.Runtime
{
    public static class LockOnTurretLog
    {
        public const string Tag = "[LockOnTurret]";

        public static string NoWeapon(string turret)
        {
            return $"{Tag} '{turret}' has no weapon; set one whose round has homing to lock on";
        }

        public static string NoHoming(string turret, string weaponId)
        {
            return $"{Tag} '{turret}' cannot lock on with weapon '{weaponId}': its round has no homing";
        }

        public static string InvalidWeapon(string turret, string message)
        {
            return $"{Tag} '{turret}' cannot read its weapon: {message}";
        }

        public static string InvalidSettings(string turret, string message)
        {
            return $"{Tag} '{turret}' のタレットの設定を使えないので既定の値を使います: {message}";
        }
    }
}
