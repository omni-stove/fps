using System;

namespace Shooter.Core
{
    public interface IRespawnPolicy
    {
        bool WillReturn(ModeSession session, int playerId);

        bool BeginWait(ModeSession session, int playerId);

        void Cancel(int playerId);

        void CancelAll();

        void Reset();

        // respawn の中で抜けたり倒されたり試合が終わったりするので、実装は呼ぶたびに session.State が Playing のままか、
        // 次に出す人がまだ同じ待ちのままかを確かめる。
        void Tick(ModeSession session, float elapsedSeconds, Action<int> respawn);

        float? GetRemaining(int playerId);

        float? SecondsUntilNextWave { get; }
    }
}
