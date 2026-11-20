using System.Runtime.CompilerServices;

// 既定の UI (Shooter.Runtime.UI) を入れずに使う場合を、登録済みの既定を外して EditMode テストで再現する (GameUi.ClearDefaults)
[assembly: InternalsVisibleTo("Shooter.Tests.EditMode")]

// 飛行機のテスト (Shooter.Aircraft.Tests) は本体のテストと別のアセンブリにあり、テストの間に GameSignals を空に戻す (GameSignals.ResetAll) ため
[assembly: InternalsVisibleTo("Shooter.Aircraft.Tests")]

// キャンペーンのテスト (Shooter.Campaign.Tests) も本体のテストと別のアセンブリにあり、テストの間に GameSignals を空に戻す (GameSignals.ResetAll) ため
[assembly: InternalsVisibleTo("Shooter.Campaign.Tests")]

// 進行のテスト (Shooter.Progression.Tests) も本体のテストと別のアセンブリにあり、テストの間に GameSignals を空に戻す (GameSignals.ResetAll) ため
[assembly: InternalsVisibleTo("Shooter.Progression.Tests")]

// 既定の UI のテストはサンプル (Shooter.Samples.Tests) にあり、テストの間に GameSignals を空に戻す (GameSignals.ResetAll) ため
[assembly: InternalsVisibleTo("Shooter.Samples.Tests")]

// NetV2 の一人称の武器モデルのテスト (Shooter.Samples.Net.Tests) は、テストの間に ViewmodelPresenter の登録を空に戻す (ViewmodelPresenter.ResetOnPlay) ため
[assembly: InternalsVisibleTo("Shooter.Samples.Net.Tests")]
