namespace Shooter.Core
{
    /// <summary>
    /// <see cref="ControlSettings"/> が持つ数値の設定。値はすべて整数で、範囲は <see cref="ControlSettings.RangeOf"/> が決める。
    /// </summary>
    public enum ControlValue
    {
        /// <summary>マウスの視点感度 (%)。100 で視点操作の基準の速さ。</summary>
        MouseSensitivity,

        /// <summary>ゲームパッドの視点感度 (%)。100 で視点操作の基準の速さ。</summary>
        GamepadSensitivity,

        /// <summary>覗き込み (ADS) 中に視点感度へさらに掛ける倍率 (%)。100 で視野角に合わせた分だけ下げる。</summary>
        AdsSensitivity,

        /// <summary>腰だめのカメラの縦の視野角 (度)。</summary>
        FieldOfView,

        /// <summary>全体の音量 (%)。</summary>
        MasterVolume,

        /// <summary>効果音の音量 (%)。全体の音量に掛ける。</summary>
        EffectsVolume,

        /// <summary>画面の揺れの強さ (%)。0 で揺らさない。</summary>
        CameraShakeIntensity,

        /// <summary>手元の武器 (ビューモデル) の揺れの強さ (%)。0 で揺らさない。</summary>
        ViewmodelMotionIntensity,

        /// <summary>ゲームパッドのスティックのデッドゾーン (‰)。倒した量がこれより小さい間は入力を 0 とみなす。</summary>
        StickDeadzone,

        /// <summary>ゲームパッドの振動の強さ (%)。</summary>
        RumbleStrength,
    }
}
