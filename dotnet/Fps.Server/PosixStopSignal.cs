using System;
using System.Runtime.InteropServices;

namespace Fps.Server
{
    internal sealed class PosixStopSignal : IStopSignal
    {
        public IDisposable Subscribe(Action onStop)
        {
            ArgumentNullException.ThrowIfNull(onStop);
            // 既定の処理 (すぐ終了) を取り消すのは、1 回目で試合の終わりを待つため。手元の Ctrl+C も docker stop と同じに扱う
            var term = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
            {
                context.Cancel = true;
                onStop();
            });
            var interrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, context =>
            {
                context.Cancel = true;
                onStop();
            });
            return new Registrations(term, interrupt);
        }

        private sealed class Registrations : IDisposable
        {
            private readonly IDisposable _term;
            private readonly IDisposable _interrupt;

            public Registrations(IDisposable term, IDisposable interrupt)
            {
                _term = term;
                _interrupt = interrupt;
            }

            public void Dispose()
            {
                _term.Dispose();
                _interrupt.Dispose();
            }
        }
    }
}
