using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// サーバーへ送った確かめの便 (番号付き) のうち、<see cref="TimeoutSeconds"/> 以内に返事が来なかった割合 (パケットロス) を数える。
    /// 行きと帰りのどちらで欠けても 1 つの欠けとして数える。時刻は呼ぶ側の秒 (増える一方なら何でもよい) で渡す。
    /// </summary>
    /// <remarks>
    /// 待っている便は番号の下位ビットで引く輪の表に置く。番号は 16 ビットで一周するので、表の大きさは 65536 を割り切る 2 のべきにしてある。
    /// 表があふれるほど待ちが溜まったら、いちばん古い便を欠けとして締める。
    /// </remarks>
    public sealed class NetLossMeter
    {
        /// <summary>返事を待つ秒数の既定値。</summary>
        public const double DefaultTimeoutSeconds = 1.0;

        /// <summary>同時に待てる便の数。これを超えて送ると、いちばん古い便を欠けとして締める。</summary>
        public const int Capacity = 256;

        private readonly ushort[] _sequences = new ushort[Capacity];
        private readonly double[] _sentAt = new double[Capacity];
        private readonly bool[] _waiting = new bool[Capacity];
        private readonly bool[] _answered = new bool[Capacity];
        private readonly Queue<ushort> _pending = new Queue<ushort>(Capacity);

        private ushort _next;
        private int _settled;
        private int _lost;

        /// <param name="timeoutSeconds">返事を待つ秒数。0 より大きい有限の値。</param>
        public NetLossMeter(double timeoutSeconds = DefaultTimeoutSeconds)
        {
            if (!(timeoutSeconds > 0d) || double.IsInfinity(timeoutSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), timeoutSeconds, "the timeout must be a positive finite number of seconds");
            }

            TimeoutSeconds = timeoutSeconds;
        }

        /// <summary>返事を待つ秒数。これより遅れた返事は欠けとして数える。</summary>
        public double TimeoutSeconds { get; }

        /// <summary>まだ締めていない (返事を待っているか、返事が来て締めを待っている) 便の数。</summary>
        public int PendingCount => _pending.Count;

        /// <summary>前の <see cref="TakeLossPercent"/> から締めた便の数。</summary>
        public int SettledCount => _settled;

        /// <summary>前の <see cref="TakeLossPercent"/> から締めた便のうち、返事が来なかった数。</summary>
        public int LostCount => _lost;

        /// <summary>時刻 now に送る便の番号を払い出して覚える。返した番号で便を送る。</summary>
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

        /// <summary>
        /// 時刻 now に sequence の返事が届いたことを記す。待っている便で、<see cref="TimeoutSeconds"/> 以内なら true。
        /// 知らない番号・締めた便・2 度目の返事・遅すぎた返事は false (数えない)。
        /// </summary>
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

        /// <summary>
        /// 時刻 now で <see cref="TimeoutSeconds"/> を過ぎた便を締め、前に呼んでから締めた便のうち返事が来なかった割合 (0〜100、四捨五入) を返す。
        /// 締めた便が無ければ null (まだ分からない)。返したら数え直す。
        /// </summary>
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

        /// <summary>待っている便と数えた結果をすべて捨てる (相手が変わったとき・表示を止めたとき)。番号は続きから払い出す。</summary>
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
