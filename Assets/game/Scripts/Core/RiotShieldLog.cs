namespace Shooter.Core
{
    // 盾の武器を持つ側の出来事。止めた弾の行は敵の盾と同じ ShieldLog.Blocked に出す。
    public static class RiotShieldLog
    {
        public const string LogTag = "[Combat]";

        // 下げた理由。オフライン (WeaponController) とサーバー (ServerCombat) で同じ語を出し、同じ grep で拾えるようにする。
        public const string SprintCause = "sprint";

        public static string Out(string holderId, string weaponId)
        {
            return $"{LogTag} riot-shield out id={holderId} weapon={weaponId}";
        }

        public static string Away(string holderId, string weaponId)
        {
            return $"{LogTag} riot-shield away id={holderId} weapon={weaponId}";
        }

        public static string Lowered(string holderId, string cause)
        {
            return $"{LogTag} riot-shield lowered id={holderId} cause={cause}";
        }

        public static string Raised(string holderId)
        {
            return $"{LogTag} riot-shield raised id={holderId}";
        }
    }
}
