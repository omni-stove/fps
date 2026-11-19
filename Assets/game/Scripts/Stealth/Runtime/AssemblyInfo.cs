using System.Runtime.CompilerServices;

// 隠密のテスト (Shooter.Stealth.Tests) は Edit Mode で Update が走らないので、足音の歩幅を StealthTarget.AdvanceFootsteps で手で進める。公開の API にしないため
[assembly: InternalsVisibleTo("Shooter.Stealth.Tests")]
