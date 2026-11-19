using System;

namespace Shooter.Runtime
{
    // 部品そのものでなく型を返すのは、Bot が乗るまで操縦部品を付けず、本体が他のアセンブリの操縦部品の型を知らずに AddComponent できるようにするため。
    public interface IBotPilotedMotor
    {
        Type BotPilotType { get; }
    }
}
