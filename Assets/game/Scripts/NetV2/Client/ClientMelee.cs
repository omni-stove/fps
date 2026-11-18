using System;
using Shooter.Core;

namespace Shooter.NetV2
{
    // 当たりとダメージはサーバー (ServerMelee) が決めるので、ここは押した瞬間に振る見た目を出すかだけを決める。
    // サーバーの答えを待つと往復ぶん遅れて振るので、サーバーと同じ MeleeSwing で振れるかと待ち時間を手元で数える。
    // 当てたかどうかはサーバーの HitConfirmMessage で出す。
    public sealed class ClientMelee
    {
        private readonly MeleeSwing _swing;
        private bool _held;

        public ClientMelee(MeleeSpec spec)
        {
            _swing = new MeleeSwing(spec ?? throw new ArgumentNullException(nameof(spec)));
        }

        // 振る見た目を始めるとき。値は振りの長さ (SwingSeconds) と当たりの瞬間 (ImpactSeconds) に見た目を合わせるため
        public event Action<MeleeSpec> SwingStarted;

        // 振っている途中で倒れた・ダウンしたなどで振りをやめたとき。見た目を途中で止めるため
        public event Action SwingCancelled;

        public MeleeSpec Spec
        {
            get => _swing.Spec;
            set => _swing.Spec = value ?? throw new ArgumentNullException(nameof(value));
        }

        public bool IsSwinging => _swing.IsSwinging;

        public float Elapsed => _swing.Elapsed;

        public float CooldownRemaining => _swing.CooldownRemaining;

        // 毎フレーム、近接のボタン (InputButtons.Melee と同じ入力) を押しているかと、今振れる状態か
        // (ルールで近接が On・生きていてダウンしていない・乗り物に乗っていない・はしごにいない) を渡す。
        // サーバーと同じく押し始めた瞬間だけ振り、押し続けても振り直さない。振り始めたら true
        public bool Update(bool held, bool canSwing, float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            var pressed = held && !_held;
            _held = held;
            if (_swing.IsSwinging && !canSwing)
            {
                _swing.Cancel();
                SwingCancelled?.Invoke();
            }

            _swing.Tick(deltaSeconds);
            if (!pressed || !canSwing || !_swing.TryStart())
            {
                return false;
            }

            SwingStarted?.Invoke(_swing.CurrentSpec);
            return true;
        }

        // 出現し直したとき・試合やマップが替わったときに呼ぶ。サーバーも同じときに振りと待ち時間を捨てる。
        // 押しているかは捨てない。サーバーも捨てないので、押したまま出現すれば離して押し直すまでどちらも振らない
        public void Reset()
        {
            var wasSwinging = _swing.IsSwinging;
            _swing.Reset();
            if (wasSwinging)
            {
                SwingCancelled?.Invoke();
            }
        }
    }
}
