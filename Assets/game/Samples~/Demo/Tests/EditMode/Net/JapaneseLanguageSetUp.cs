using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;

namespace Fps.Net.Tests
{
    /// <summary>
    /// この名前空間のテストの間、効いている設定の言語を日本語にし、終わったら前の設定に戻す。
    /// HUD の文字 (Core の <see cref="GameText"/> と <see cref="Localizer"/>) を確かめるテストは日本語の文を期待しているので、
    /// 保存した設定や OS の言語 (CI は英語) で結果が変わらないようにする。言語のほかの設定は変えない。
    /// </summary>
    [SetUpFixture]
    public sealed class JapaneseLanguageSetUp
    {
        private ControlSettings _before;

        [OneTimeSetUp]
        public void UseJapanese()
        {
            _before = AppliedControlSettings.Current;
            AppliedControlSettings.Apply(_before.WithLanguage(GameLanguage.Japanese));
        }

        [OneTimeTearDown]
        public void Restore()
        {
            AppliedControlSettings.Apply(_before);
        }
    }
}
