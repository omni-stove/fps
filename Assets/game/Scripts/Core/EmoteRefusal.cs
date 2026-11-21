namespace Shooter.Core
{
    public enum EmoteRefusal
    {
        None,

        RuleOff,

        UnknownId,

        Dead,

        Downed,

        Riding,

        // ネット対戦で短い間に要求を重ねたとき。要求ごとに全員へ配るので、連打で回線を埋めさせない
        TooSoon,
    }
}
