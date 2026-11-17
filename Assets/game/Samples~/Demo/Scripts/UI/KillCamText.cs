using Shooter.Core;

namespace Shooter.Runtime
{
    public static class KillCamText
    {
        public static LocalizedText Caption(LocalizedText killer)
        {
            return new LocalizedText("hud.killCam.caption", killer);
        }

        public static LocalizedText Caption(KillCamStatus status)
        {
            return Caption(LocalizedText.Literal(status.KillerName));
        }
    }
}
