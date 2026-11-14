using System;

namespace Fps.Server.Tests
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
