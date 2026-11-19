using System.Runtime.CompilerServices;

// 既定の UI (Shooter.Runtime.UI) を入れずに使う場合を、登録済みの既定を外して EditMode テストで再現する (GameUi.ClearDefaults)
[assembly: InternalsVisibleTo("Shooter.Tests.EditMode")]

// 飛行機のテスト (Shooter.Aircraft.Tests) は本体のテストと別のアセンブリにあり、テストの間に GameSignals を空に戻す (GameSignals.ResetAll) ため
[assembly: InternalsVisibleTo("Shooter.Aircraft.Tests")]

// キャンペーンのテスト (Shooter.Campaign.Tests) も本体のテストと別のアセンブリにあり、テストの間に GameSignals とクラウドセーブの登録を空に戻す (GameSignals.ResetAll・CloudSaveBootstrap.ClearRegisteredSlots) ため
[assembly: InternalsVisibleTo("Shooter.Campaign.Tests")]

// 進行 (Shooter.Progression) はレートを渡す先が先に入っていることを Play の開始時の順に頼らずに保つため、SkillBootstrap.EnsureInstalledOnPlay を公開の API にせずに呼ぶ
[assembly: InternalsVisibleTo("Shooter.Progression")]

// 進行のテスト (Shooter.Progression.Tests) も本体のテストと別のアセンブリにあり、テストの間に GameSignals とクラウドセーブの登録を空に戻す (GameSignals.ResetAll・CloudSaveBootstrap.ClearRegisteredSlots) ため
[assembly: InternalsVisibleTo("Shooter.Progression.Tests")]

// 既定の UI のテストはサンプル (Shooter.Samples.Tests) にあり、テストの間に GameSignals を空に戻す (GameSignals.ResetAll) ため
[assembly: InternalsVisibleTo("Shooter.Samples.Tests")]

// NetV2 の一人称の武器モデルのテスト (Shooter.Samples.Net.Tests) は、テストの間に ViewmodelPresenter の登録を空に戻す (ViewmodelPresenter.ResetOnPlay) ため
[assembly: InternalsVisibleTo("Shooter.Samples.Net.Tests")]
