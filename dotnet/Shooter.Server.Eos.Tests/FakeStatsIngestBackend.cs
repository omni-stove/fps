using System;
using System.Collections.Generic;

namespace Shooter.Server.Eos.Tests
{
    internal sealed class FakeStatsIngestBackend : IStatsIngestBackend
    {
        public List<Call> Calls { get; } = new List<Call>();

        public int Ticks { get; private set; }

        public bool Disposed { get; private set; }

        // 送ったその場で結果を返す EOS をまねるときに入れる。null なら CompleteNext を待つ
        public Func<Call, (string Error, bool CanRetry)?> Respond { get; set; }

        // SDK が Tick の中で完了を知らせるのをまねるため、Tick のたびに呼ぶ。
        public Action OnTick { get; set; }

        public int Waiting
        {
            get
            {
                var count = 0;
                foreach (var call in Calls)
                {
                    if (call.Done != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public void Tick()
        {
            Ticks++;
            OnTick?.Invoke();
        }

        public void Ingest(string productUserId, IReadOnlyList<StatIngest> stats, Action<string, bool> done)
        {
            var copy = new List<StatIngest>(stats);
            var call = new Call(productUserId, copy, done);
            Calls.Add(call);
            var response = Respond?.Invoke(call);
            if (response.HasValue)
            {
                call.Complete(response.Value.Error, response.Value.CanRetry);
            }
        }

        public void SucceedAtOnce()
        {
            Respond = _ => ((string)null, false);
        }

        public void CompleteNext(string error = null, bool canRetry = false)
        {
            foreach (var call in Calls)
            {
                if (call.Done != null)
                {
                    call.Complete(error, canRetry);
                    return;
                }
            }

            throw new InvalidOperationException("no pending ingest");
        }

        public void Dispose()
        {
            Disposed = true;
        }

        internal sealed class Call
        {
            public Call(string userId, IReadOnlyList<StatIngest> stats, Action<string, bool> done)
            {
                UserId = userId;
                Stats = stats;
                Done = done;
            }

            public string UserId { get; }

            public IReadOnlyList<StatIngest> Stats { get; }

            public Action<string, bool> Done { get; private set; }

            public void Complete(string error, bool canRetry)
            {
                var done = Done ?? throw new InvalidOperationException("ingest to " + UserId + " is already complete");
                Done = null;
                done(error, canRetry);
            }
        }
    }
}
