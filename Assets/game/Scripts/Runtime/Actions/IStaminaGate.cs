using Shooter.Core;

namespace Shooter.Runtime
{
    public interface IStaminaGate
    {
        StaminaStatus Status { get; }

        bool CanSprint { get; }

        float CostOf(StaminaUse use);

        bool CanAfford(StaminaUse use);

        void Spend(StaminaUse use);

        // 動かした側 (PlayerController.Tick) から呼ばせるのは、実際にダッシュの速さで動かした tick だけを減らすため。
        void Step(bool sprinting, float deltaSeconds);
    }
}
