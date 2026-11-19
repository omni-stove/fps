using System.Runtime.CompilerServices;

// 進行のテスト (Shooter.Progression.Tests) は Edit Mode でシーンの読み込みもクラウドの取り込みも起こせないので、実績の時計を止める口 (AchievementRecorder.StopMatchClock) と取り込んだプロフィールを読み直す口 (ProgressionBootstrap.ReloadPulledProfile) を、公開の API にせずに直接呼ぶため
[assembly: InternalsVisibleTo("Shooter.Progression.Tests")]
