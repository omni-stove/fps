using System;

namespace Shooter.Core
{
    // どのモードも出している ModeStatus だけで決め、モードごとの走らせ役に手を入れずに場面の曲を切り替える。
    public static class MusicCues
    {
        public static MusicCue Pick(bool inMenu, ModeStatus status)
        {
            if (inMenu)
            {
                return MusicCue.Menu;
            }

            if (status == null || !status.IsEnded)
            {
                return MusicCue.Match;
            }

            // 引き分けや観戦 (自分の陣営が無い) は勝ち負けを言えないので、どちらでもない終わりの曲にする。
            if (!status.Winner.HasValue || !status.OwnSide.HasValue)
            {
                return MusicCue.MatchEnd;
            }

            return status.Winner.Value == status.OwnSide.Value ? MusicCue.Victory : MusicCue.Defeat;
        }

        public static bool IsMenuScene(string sceneName, MenuScenes scenes)
        {
            if (scenes == null)
            {
                throw new ArgumentNullException(nameof(scenes));
            }

            return !string.IsNullOrEmpty(sceneName) && string.Equals(sceneName, scenes.MainMenu, StringComparison.Ordinal);
        }
    }
}
