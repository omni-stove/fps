using System;

namespace Fps.Server.Tests
{
    internal sealed class NeverStopSignal : IStopSignal
    {
        public IDisposable Subscribe(Action onStop)
        {
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
