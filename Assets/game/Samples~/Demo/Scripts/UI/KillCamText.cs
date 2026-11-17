using Shooter.Core;

namespace Shooter.Runtime
{
    /// <summary>キルカム (<see cref="DeathCamera.KillCam"/>) の再生中に UI が出す文。</summary>
    public static class KillCamText
    {
        /// <summary>再生中に出す見出し (<c>hud.killCam.caption</c>)。killer は倒した相手の名前。</summary>
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
