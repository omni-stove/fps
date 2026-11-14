using System;

namespace Fps.Core
{
    /// <summary>
    /// 能力 1 枠の回数と時間。使うと回数を 1 つ減らして待ち時間を始め、効果が出るまでの秒数 (<see cref="AbilitySpec.WindupSeconds"/>) の後に効果を出す。
    /// 回数は上限より少ない間、使っている最中も含めて <see cref="AbilitySpec.RechargeSeconds"/> ごとに 1 つずつ戻る。
    /// 能力をいつ終えるかは持ち主が決め、<see cref="End"/> で知らせる。時間は <see cref="Tick"/> で進める。
    /// </summary>
    public sealed class AbilitySlot
    {
        /// <exception cref="ArgumentNullException">spec が null。</exception>
        public AbilitySlot(AbilitySpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Charges = spec.MaxCharges;
        }

        public AbilitySpec Spec { get; }

        /// <summary>残りの回数。0 以上 <see cref="AbilitySpec.MaxCharges"/> 以下。</summary>
        public int Charges { get; private set; }

        /// <summary>今どこにいるか。</summary>
        public AbilityPhase Phase { get; private set; }

        /// <summary>次に使えるまでの待ち時間の残り (秒)。</summary>
        public float CooldownRemaining { get; private set; }

        /// <summary>効果が出るまでの残り (秒)。<see cref="AbilityPhase.WindingUp"/> でなければ 0。</summary>
        public float WindupRemaining { get; private set; }

        /// <summary>次の 1 回が戻るまでの経過 (秒)。回数が上限にあるか、戻らない設定なら 0。</summary>
        public float RechargeElapsed { get; private set; }

        /// <summary>次の 1 回が戻るまでの残り (秒)。回数が上限にあるなら 0、戻らない設定で上限より少ないなら無限大。</summary>
        public float RechargeRemaining
        {
            get
            {
                if (Charges >= Spec.MaxCharges)
                {
                    return 0f;
                }

                return Spec.Recharges ? Math.Max(0f, Spec.RechargeSeconds - RechargeElapsed) : float.PositiveInfinity;
            }
        }

        /// <summary>使っている最中 (効果を待っているか、効果が出ている) か。</summary>
        public bool IsRunning => Phase != AbilityPhase.Idle;

        /// <summary>
        /// 枠だけで見て使えない理由 (<see cref="AbilityUseBlock.Running"/>・<see cref="AbilityUseBlock.Cooldown"/>・<see cref="AbilityUseBlock.NoCharges"/> のどれか)。
        /// 使えるなら <see cref="AbilityUseBlock.None"/>。
        /// </summary>
        public AbilityUseBlock Block
        {
            get
            {
                if (IsRunning)
                {
                    return AbilityUseBlock.Running;
                }

                if (CooldownRemaining > 0f)
                {
                    return AbilityUseBlock.Cooldown;
                }

                return Charges <= 0 ? AbilityUseBlock.NoCharges : AbilityUseBlock.None;
            }
        }

        /// <summary>枠だけで見て今使えるか。</summary>
        public bool CanStart => Block == AbilityUseBlock.None;

        /// <summary>
        /// 使っていないとして、次に使えるようになるまでの秒数。今使えるなら 0。回数が無く戻らない設定なら無限大。
        /// 使っている最中は、使い終えた後に待つ分 (待ち時間と回数の戻り) だけを数える。
        /// </summary>
        public float SecondsUntilReady
        {
            get
            {
                var wait = CooldownRemaining;
                if (Charges <= 0)
                {
                    wait = Math.Max(wait, RechargeRemaining);
                }

                return wait;
            }
        }

        /// <summary>使い始める。使えなければ何もせず false。効果が出るまでの秒数が 0 なら、その場で <see cref="AbilityPhase.Active"/> になる。</summary>
        public bool TryStart()
        {
            if (!CanStart)
            {
                return false;
            }

            Charges--;
            CooldownRemaining = Spec.CooldownSeconds;
            if (Spec.WindupSeconds > 0f)
            {
                Phase = AbilityPhase.WindingUp;
                WindupRemaining = Spec.WindupSeconds;
            }
            else
            {
                Phase = AbilityPhase.Active;
                WindupRemaining = 0f;
            }

            return true;
        }

        /// <summary>時間を deltaSeconds 進め、待ち時間・効果が出るまで・回数の戻りを進める。</summary>
        /// <exception cref="ArgumentOutOfRangeException">deltaSeconds が負か有限でない。</exception>
        public AbilitySlotStep Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative and must be finite");
            }

            var step = AbilitySlotStep.None;
            CooldownRemaining = Math.Max(0f, CooldownRemaining - deltaSeconds);

            if (Phase == AbilityPhase.WindingUp)
            {
                WindupRemaining -= deltaSeconds;
                if (WindupRemaining <= 0f)
                {
                    WindupRemaining = 0f;
                    Phase = AbilityPhase.Active;
                    step |= AbilitySlotStep.EffectStarted;
                }
            }

            if (Charges < Spec.MaxCharges && Spec.Recharges)
            {
                RechargeElapsed += deltaSeconds;
                while (RechargeElapsed >= Spec.RechargeSeconds && Charges < Spec.MaxCharges)
                {
                    RechargeElapsed -= Spec.RechargeSeconds;
                    Charges++;
                    step |= AbilitySlotStep.Recharged;
                }
            }

            if (Charges >= Spec.MaxCharges)
            {
                RechargeElapsed = 0f;
            }

            return step;
        }

        /// <summary>使い終える (効果を待っている途中で止めるときも)。使っていなければ何もせず false。回数と待ち時間はそのまま。</summary>
        public bool End()
        {
            if (!IsRunning)
            {
                return false;
            }

            Phase = AbilityPhase.Idle;
            WindupRemaining = 0f;
            return true;
        }

        /// <summary>回数を上限に戻し、待ち時間を消し、使っていない状態にする (出撃し直したときなど)。</summary>
        public void Reset()
        {
            Charges = Spec.MaxCharges;
            Phase = AbilityPhase.Idle;
            CooldownRemaining = 0f;
            WindupRemaining = 0f;
            RechargeElapsed = 0f;
        }
    }
}
