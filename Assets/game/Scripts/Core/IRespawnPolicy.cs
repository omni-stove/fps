using System;

namespace Fps.Core
{
    /// <summary>
    /// 倒された人がいつ戻るか。<see cref="ModeSession"/> が倒された・抜けた・時間が進んだを知らせ、
    /// 待ち終わった人を <see cref="Tick"/> の respawn で返してもらう。待ちの状態は実装が持つ。
    /// 条件付きのリスポーン (味方が拠点を持っている間だけ戻る など) は、<see cref="WillReturn"/> と <see cref="Tick"/> で
    /// session を見て決める実装を足す。
    /// </summary>
    public interface IRespawnPolicy
    {
        /// <summary>
        /// 倒されている playerId が、この試合のうちに戻ってくる見込みがあれば true。
        /// false の人は <see cref="ModeSession.IsEliminated"/> になり、陣営の全員がそうなると陣営ごと脱落する。
        /// </summary>
        bool WillReturn(ModeSession session, int playerId);

        /// <summary>playerId が倒された。待ちを始める。待たずにその場で戻すなら待ちを始めずに true を返す。</summary>
        bool BeginWait(ModeSession session, int playerId);

        /// <summary>playerId の待ちを取り消す (抜けた)。待っていなければ何もしない。</summary>
        void Cancel(int playerId);

        /// <summary>全員の待ちを取り消す (試合が終わった)。試合開始から数えている時刻はそのまま残す。</summary>
        void CancelAll();

        /// <summary>全員の待ちと、試合開始から数えている時刻を初期値に戻す。</summary>
        void Reset();

        /// <summary>
        /// 時間を elapsedSeconds 進め、待ち終わった人ごとに、その人の待ちを消してから respawn(playerId) を呼ぶ。
        /// respawn の中で抜けたり倒されたり試合が終わったりするので、呼ぶたびに session.State が Playing のままか、
        /// 次に出す人がまだ同じ待ちのままかを確かめる。
        /// </summary>
        void Tick(ModeSession session, float elapsedSeconds, Action<int> respawn);

        /// <summary>playerId が戻れるようになるまでの秒数。待っていなければ null。</summary>
        float? GetRemaining(int playerId);

        /// <summary>まとめて戻す次の時刻までの秒数。そういう仕組みがなければ null。</summary>
        float? SecondsUntilNextWave { get; }
    }
}
