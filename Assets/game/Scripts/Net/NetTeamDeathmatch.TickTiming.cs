using System;
using System.Diagnostics;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetTeamDeathmatch"/> のうち、サーバーの 1 フレームぶんの試合の処理 (Update で Session・Bot・放置・拠点・同期を進め、
    /// LateUpdate でラグ補償の位置を記録する) にかかった時間を計り、<see cref="ServerTickMeasured"/> で知らせる処理。
    /// </summary>
    /// <remarks>
    /// 購読が無い間は時計を読まない。Update と LateUpdate の両方を計れたフレームだけを知らせる
    /// (途中で購読されたフレームの片方だけの時間を 1 tick として渡さないため)。
    /// 各プレイヤーの <see cref="NetworkPlayerMotor"/> が自分の Update で進める移動はここに入らない。Bot の移動は Bot の tick (.Bots.cs) から
    /// 呼ぶので入る。
    /// </remarks>
    public sealed partial class NetTeamDeathmatch
    {
        private const long _notTiming = -1L;

        // このフレームの Update にかかった時間 (Stopwatch の目盛り)。Update を計っていなければ _notTiming
        private long _updateElapsed = _notTiming;

        /// <summary>
        /// サーバーで試合が動いている間、フレームごとに Update と LateUpdate の処理にかかった時間 (ミリ秒) を渡して呼ばれる。
        /// 呼ぶのは LateUpdate の終わり。性能の計測 (<c>Fps.Editor.PerfSmokeRun</c>) が使う。
        /// </summary>
        public event Action<double> ServerTickMeasured;

        private long TickTimingBegin()
        {
            return ServerTickMeasured != null ? Stopwatch.GetTimestamp() : _notTiming;
        }

        private void TickTimingAddUpdate(long startedAt)
        {
            _updateElapsed = startedAt == _notTiming ? _notTiming : Stopwatch.GetTimestamp() - startedAt;
        }

        private void TickTimingEndFrame(long startedAt)
        {
            var updateElapsed = _updateElapsed;
            _updateElapsed = _notTiming;

            var handler = ServerTickMeasured;
            if (handler == null || startedAt == _notTiming || updateElapsed == _notTiming)
            {
                return;
            }

            var elapsed = updateElapsed + (Stopwatch.GetTimestamp() - startedAt);
            handler(elapsed * 1000d / Stopwatch.Frequency);
        }
    }
}
