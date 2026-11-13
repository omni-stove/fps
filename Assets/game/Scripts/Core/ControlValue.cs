namespace Fps.Core
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
    }
}
