using System;

namespace Fps.Core
{
    /// <summary>倒された人は試合中は戻らない。倒された人はそのまま <see cref="ModeSession.IsEliminated"/> になる。</summary>
    public sealed class NoRespawn : IRespawnPolicy
    {
        public float? SecondsUntilNextWave => null;

        public bool WillReturn(ModeSession session, int playerId) => false;

        public bool BeginWait(ModeSession session, int playerId) => false;

        public void Cancel(int playerId)
        {
        }

        public void CancelAll()
        {
        }

        public void Reset()
        {
        }

        public void Tick(ModeSession session, float elapsedSeconds, Action<int> respawn)
        {
        }

        public float? GetRemaining(int playerId) => null;
    }
}
