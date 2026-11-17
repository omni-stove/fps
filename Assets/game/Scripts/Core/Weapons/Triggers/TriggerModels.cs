using System;

namespace Shooter.Core
{
    public static class TriggerModels
    {
        public static ITriggerModel For(WeaponSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            // ビームと火炎は押している間ずっと当て続けるので、連射間隔でなく照射の間隔で数える撃ち方を土台にする。
            var round = spec.Round;
            switch (round.Delivery)
            {
                case RoundDelivery.Beam:
                    return Wrap(spec.Trigger, new BeamState(round.Beam));
                case RoundDelivery.Spray:
                    // 押した・離したと間隔の数え方はビームと同じなので、火炎の間隔を持つビームとして数える。
                    return Wrap(spec.Trigger, new BeamState(new BeamSpec(round.Spray.TickSeconds)));
                default:
                    return For(spec.Trigger);
            }
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
