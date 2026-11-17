using Shooter.Core;

namespace Shooter.Runtime
{
    public static class KillFeedText
    {
        public static LocalizedText HeadshotMark => new LocalizedText("hud.killFeed.headshot");

        public static LocalizedText FriendlyMark => new LocalizedText("hud.killFeed.friendly");

        public static LocalizedText Suicide => new LocalizedText("hud.killFeed.suicide");

        public static LocalizedText AssistMark => new LocalizedText("assist.killFeed.mark");

        public const string NoWeapon = "-";

        public static string Unknown(int id)
        {
            return "?" + id;
        }

        public static LocalizedText Weapon(LocalizedText weaponName, bool headshot, bool suicide)
        {
            object name = !weaponName.IsEmpty ? weaponName : suicide ? (object)Suicide : NoWeapon;
            return headshot
                ? new LocalizedText("hud.killFeed.weaponHeadshot", name, HeadshotMark)
                : new LocalizedText("hud.killFeed.weapon", name);
        }
    }
}
