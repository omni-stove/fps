using System;

namespace Shooter.Core
{
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
