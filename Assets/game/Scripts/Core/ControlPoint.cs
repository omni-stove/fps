using System;

namespace Fps.Core
{
    /// <summary>
    /// 拠点制圧の拠点 1 つの占領の判定。半径の中にいる各チームの人数を <see cref="Update"/> で受け取り、占領の進みを動かす。
    /// 進みは <see cref="Control"/> の 1 本の値で持ち、+1 が Alpha の占領、-1 が Bravo の占領、0 が中立。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>片方のチームだけがいると、そのチームの側へ 1 秒あたり min(人数, MaxCapturers) / CaptureSeconds だけ進む。
    /// 相手の拠点なら、まず 0 まで押し戻して中立に戻し (<see cref="Neutralized"/>)、そのまま自分の側の端まで進めて占領する (<see cref="Captured"/>)。</item>
    /// <item>両チームがいると押し合いになり、進みは止まる (<see cref="IsContested"/>)。</item>
    /// <item>誰もいないと、持ち主がいれば持ち主の端へ、いなければ 0 へ、1 人分の速さで戻る。占領しかけで離れた分は中立に戻る。</item>
    /// </list>
    /// </remarks>
    public sealed class ControlPoint
    {
        // 小刻みな Tick を足し合わせると端や 0 にわずかに届かないことがあるので、この差までは届いたものとして扱う。
        private const double _snap = 1e-6;

        /// <summary>チームがこの拠点を占領した瞬間に呼ばれる。引数は占領したチーム。呼ばれた時点で <see cref="Owner"/> は更新済み。</summary>
        public event Action<Team> Captured;

        /// <summary>持ち主のいた拠点が中立に戻った瞬間に呼ばれる。引数はそれまでの持ち主。呼ばれた時点で <see cref="Owner"/> は null。</summary>
        public event Action<Team> Neutralized;

        private double _control;

        /// <summary>1 人で中立から占領し終えるまでの秒数。0 より大きい有限値。</summary>
        public float CaptureSeconds { get; }

        /// <summary>占領の速さに数える人数の上限。1 以上。これより多くいても速くならない。</summary>
        public int MaxCapturers { get; }

        /// <summary>占領しているチーム。中立なら null。</summary>
        public Team? Owner { get; private set; }

        /// <summary>占領の進み。-1 (Bravo) から +1 (Alpha) で、0 が中立。</summary>
        public float Control => (float)_control;

        /// <summary>進みが寄っている側のチーム。ちょうど 0 なら null。</summary>
        public Team? LeaningTeam => _control > 0d ? Team.Alpha : _control < 0d ? Team.Bravo : (Team?)null;

        /// <summary>最後の <see cref="Update"/> で両チームがいて、進みが止まっていたら true。</summary>
        public bool IsContested { get; private set; }

        /// <param name="captureSeconds">1 人で中立から占領し終えるまでの秒数。0 より大きい有限値。</param>
        /// <param name="maxCapturers">占領の速さに数える人数の上限。1 以上。</param>
        public ControlPoint(float captureSeconds, int maxCapturers = 1)
        {
            if (!(captureSeconds > 0f) || float.IsInfinity(captureSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(captureSeconds), captureSeconds, "captureSeconds must be positive and finite");
            }

            if (maxCapturers < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCapturers), maxCapturers, "maxCapturers must be at least 1");
            }

            CaptureSeconds = captureSeconds;
            MaxCapturers = maxCapturers;
        }

        /// <summary>
        /// 半径の中に alphaCount 人と bravoCount 人がいる状態で deltaSeconds 進める。
        /// 1 回の呼び出しで相手の拠点を中立に戻してさらに占領し終えたときは、<see cref="Neutralized"/>、<see cref="Captured"/> の順に呼ぶ。
        /// </summary>
        public void Update(int alphaCount, int bravoCount, float deltaSeconds)
        {
            if (alphaCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(alphaCount), alphaCount, "alphaCount must not be negative");
            }

            if (bravoCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bravoCount), bravoCount, "bravoCount must not be negative");
            }

            if (!(deltaSeconds >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative");
            }

            IsContested = alphaCount > 0 && bravoCount > 0;
            if (IsContested || deltaSeconds == 0f)
            {
                return;
            }

            double target;
            int capturers;
            if (alphaCount > 0)
            {
                target = 1d;
                capturers = alphaCount;
            }
            else if (bravoCount > 0)
            {
                target = -1d;
                capturers = bravoCount;
            }
            else
            {
                target = EndOf(Owner);
                capturers = 1;
            }

            var step = Math.Min(capturers, MaxCapturers) * (double)deltaSeconds / CaptureSeconds;
            _control = MoveToward(_control, target, step);
            ApplyOwnership();
        }

        /// <summary>中立で誰もいない状態に戻す。イベントは出さない。</summary>
        public void Reset()
        {
            _control = 0d;
            Owner = null;
            IsContested = false;
        }

        private void ApplyOwnership()
        {
            if ((Owner == Team.Alpha && _control <= 0d) || (Owner == Team.Bravo && _control >= 0d))
            {
                var previous = Owner.Value;
                Owner = null;
                Neutralized?.Invoke(previous);
            }

            // ハンドラの中で Reset されていたら、ここで改めて占領させない。
            if (_control >= 1d && Owner != Team.Alpha)
            {
                Owner = Team.Alpha;
                Captured?.Invoke(Team.Alpha);
            }
            else if (_control <= -1d && Owner != Team.Bravo)
            {
                Owner = Team.Bravo;
                Captured?.Invoke(Team.Bravo);
            }
        }

        private static double EndOf(Team? team)
        {
            switch (team)
            {
                case Team.Alpha:
                    return 1d;
                case Team.Bravo:
                    return -1d;
                default:
                    return 0d;
            }
        }

        private static double MoveToward(double current, double target, double step)
        {
            var next = current < target ? Math.Min(target, current + step) : Math.Max(target, current - step);
            if (Math.Abs(target - next) < _snap)
            {
                return target;
            }

            return Math.Abs(next) < _snap ? 0d : next;
        }
    }
}
