using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class NetLossMeter
    {
        public const double DefaultTimeoutSeconds = 1.0;

        // 待っている便は番号の下位ビットで引く輪の表に置く。番号は 16 ビットで一周するので、表の大きさは 65536 を割り切る 2 のべきにしてある。
        public const int Capacity = 256;

        private readonly ushort[] _sequences = new ushort[Capacity];
        private readonly double[] _sentAt = new double[Capacity];
        private readonly bool[] _waiting = new bool[Capacity];
        private readonly bool[] _answered = new bool[Capacity];
        private readonly Queue<ushort> _pending = new Queue<ushort>(Capacity);

        private ushort _next;
        private int _settled;
        private int _lost;

        public NetLossMeter(double timeoutSeconds = DefaultTimeoutSeconds)
        {
            if (!(timeoutSeconds > 0d) || double.IsInfinity(timeoutSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), timeoutSeconds, "the timeout must be a positive finite number of seconds");
            }

            TimeoutSeconds = timeoutSeconds;
        }

        public double TimeoutSeconds { get; }

        public int PendingCount => _pending.Count;

        public int SettledCount => _settled;

        public int LostCount => _lost;

        public ushort Send(double now)
        {
            if (_pending.Count >= Capacity)
            {
                Close(_pending.Dequeue());
            }

            var sequence = _next;
            _next = unchecked((ushort)(_next + 1));

            var slot = SlotOf(sequence);
            _sequences[slot] = sequence;
            _sentAt[slot] = now;
            _waiting[slot] = true;
            _answered[slot] = false;
            _pending.Enqueue(sequence);
            return sequence;
        }

        public bool Acknowledge(ushort sequence, double now)
        {
            var slot = SlotOf(sequence);
            if (!_waiting[slot] || _sequences[slot] != sequence || _answered[slot])
            {
                return false;
            }

            if (now - _sentAt[slot] > TimeoutSeconds)
            {
                return false;
            }

            _answered[slot] = true;
            return true;
        }

        public int? TakeLossPercent(double now)
        {
            while (_pending.Count > 0)
            {
                var oldest = _pending.Peek();
                if (now - _sentAt[SlotOf(oldest)] < TimeoutSeconds)
                {
                    break;
                }

                Close(_pending.Dequeue());
            }

            if (_settled == 0)
            {
                return null;
            }

            var percent = (int)Math.Round(_lost * 100.0 / _settled, MidpointRounding.AwayFromZero);
            _settled = 0;
            _lost = 0;
            return percent;
        }

        public void Reset()
        {
            while (_pending.Count > 0)
            {
                var slot = SlotOf(_pending.Dequeue());
                _waiting[slot] = false;
                _answered[slot] = false;
            }

            _settled = 0;
            _lost = 0;
        }

        private void Close(ushort sequence)
        {
            var slot = SlotOf(sequence);
            _settled++;
            if (!_answered[slot])
            {
                _lost++;
            }

            _waiting[slot] = false;
            _answered[slot] = false;
        }

        private static int SlotOf(ushort sequence)
        {
            return sequence & (Capacity - 1);
        }
    }
}
