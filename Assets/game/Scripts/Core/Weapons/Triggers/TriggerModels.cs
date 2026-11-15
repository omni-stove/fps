using System;

namespace Fps.Core
{
    public static class TriggerModels
    {
        public static ITriggerModel For(WeaponSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            // ビームは押している間ずっと当て続けるので、連射間隔でなくビームの間隔で数える撃ち方を土台にする。
            var round = spec.Round;
            return round.Delivery == RoundDelivery.Beam ? Wrap(spec.Trigger, new BeamState(round.Beam)) : For(spec.Trigger);
        }

        public static ITriggerModel For(TriggerSpec trigger)
        {
            if (trigger == null)
            {
                throw new ArgumentNullException(nameof(trigger));
            }

            return Wrap(trigger, For(trigger.FireMode, trigger.FireInterval));
        }

        // 部品ごとに内側の撃ち方を包むのは、バーストのミニガンや装填動作のあるチャージのように部品を重ねても組めるようにするため。
        private static ITriggerModel Wrap(TriggerSpec trigger, ITriggerModel model)
        {
            if (trigger.Burst != null)
            {
                model = new BurstTrigger(trigger.Burst, model);
            }

            if (trigger.SpinUp != null)
            {
                model = new SpinUpTrigger(trigger.SpinUp, model, trigger.FireMode == FireMode.Auto);
            }

            if (trigger.Charge != null)
            {
                model = new ChargeTrigger(trigger.Charge, model);
            }

            if (trigger.Cycle != null)
            {
                model = new CycleTrigger(trigger.Cycle, model);
            }

            return model;
        }

        public static ITriggerModel For(FireMode mode, float fireInterval)
        {
            switch (mode)
            {
                case FireMode.Auto:
                    return new AutoTrigger(fireInterval);
                case FireMode.Semi:
                    return new SemiTrigger(fireInterval);
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "mode must be Auto or Semi");
            }
        }
    }
}
