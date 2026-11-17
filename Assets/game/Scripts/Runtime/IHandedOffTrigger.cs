namespace Shooter.Runtime
{
    // 操作を手放している間でも撃つ・リロードだけは本人に任せたいもの (乗り物の助手席など) が、手放した口とは別に入力を渡すための口。
    // 持ち替えを渡さないのは、座席の移動と同じボタンを使うため。
    public interface IHandedOffTrigger
    {
        bool TryReadTrigger(out bool isPressed, out bool wasPressedThisFrame, out bool reloadPressed);
    }
}
