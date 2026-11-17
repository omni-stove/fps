using System.Runtime.CompilerServices;

// 既定の UI (Shooter.Runtime.UI) を入れずに使う場合を、登録済みの既定を外して EditMode テストで再現する (GameUi.ClearDefaults)
[assembly: InternalsVisibleTo("Shooter.Tests.EditMode")]

// 既定の UI のテストはサンプル (Shooter.Samples.Tests) にあり、テストの間に GameSignals を空に戻す (GameSignals.ResetAll) ため
[assembly: InternalsVisibleTo("Shooter.Samples.Tests")]
