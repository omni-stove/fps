using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>
    /// 設定画面が、今効いている操作設定を読んで、変えた値を効かせる先。ふだんは <see cref="AppliedControlSettings.Sink"/>。
    /// </summary>
    public interface ISettingsSink
    {
        /// <summary>今効いている設定。</summary>
        ControlSettings Current { get; }

        /// <summary>settings を今効いている設定にする。保存はしない。</summary>
        /// <exception cref="System.ArgumentNullException">settings が null。</exception>
        void Apply(ControlSettings settings);
    }
}
