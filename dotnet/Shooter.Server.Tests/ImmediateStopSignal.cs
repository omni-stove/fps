using System;

namespace Shooter.Server.Tests
{
    internal sealed class ImmediateStopSignal : IStopSignal
    {
        public IDisposable Subscribe(Action onStop)
        {
            onStop();
            return new Subscription();
        }

        private sealed class Subscription : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
