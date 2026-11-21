using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public static class MusicPlayerLog
    {
        public const string Tag = "[Music]";

        public static string CueChanged(MusicCue from, MusicCue to, AudioClip clip)
        {
            return Tag + " " + from + " -> " + to + ": " + (clip != null ? "playing '" + clip.name + "'" : "no clip in the music catalog; silent");
        }

        public static string NoCatalog()
        {
            return Tag + " no music catalog in GameContent or at Resources/" + MusicCatalog.ResourcePath + "; no music plays";
        }

        public static string UsingCatalog(MusicCatalog catalog)
        {
            return Tag + " using the music catalog '" + catalog.name + "'";
        }
    }
}
