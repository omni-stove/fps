using System;

namespace Fps.Core
{
    public static class TriggerModels
    {
        public static ITriggerModel For(WeaponSpec spec)
        {
            return For((spec ?? throw new ArgumentNullException(nameof(spec))).Trigger);
        }

        // 部品ごとに内側の撃ち方を包むのは、バーストのミニガンや装填動作のあるチャージのように部品を重ねても組めるようにするため。
        public static ITriggerModel For(TriggerSpec trigger)
        {
            if (trigger == null)
            {
                throw new ArgumentNullException(nameof(trigger));
            }

            var model = For(trigger.FireMode, trigger.FireInterval);
            if (trigger.Burst != null)
            {
                model = new BurstTrigger(trigger.Burst, model);
            }

            if (trigger.SpinUp != null)
            {
                model = new SpinUpTrigger(trigger.SpinUp, model);
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
