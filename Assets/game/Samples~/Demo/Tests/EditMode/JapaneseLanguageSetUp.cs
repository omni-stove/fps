using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    // 画面の文字を確かめるテストは日本語の文を期待しているので、保存した言語や OS の言語 (CI は英語) で結果が変わらないよう、
    // この名前空間のテストの間は今の言語を日本語にする。
    [SetUpFixture]
    public sealed class JapaneseLanguageSetUp
    {
        private GameLanguage _before;

        [OneTimeSetUp]
        public void UseJapanese()
        {
            _before = Localizer.Language;
            Localizer.Apply(GameLanguage.Japanese);
        }

        [OneTimeTearDown]
        public void Restore()
        {
            Localizer.Apply(_before);
        }
    }
}
