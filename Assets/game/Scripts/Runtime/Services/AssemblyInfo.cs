using System.Runtime.CompilerServices;

// 本体のテスト (Shooter.Tests.EditMode) は、テストの間にクラウドセーブの登録と Play 中の同期を戻し・差し替え (CloudSaveBootstrap.ClearRegisteredSlots・UseCurrent・EndPlay)、クラウドから採ったレートを読み直させる (SkillBootstrap.ReloadPulledSkill) ため
[assembly: InternalsVisibleTo("Shooter.Tests.EditMode")]

// キャンペーンのテスト (Shooter.Campaign.Tests) は、テストの間にクラウドセーブの登録を空に戻す (CloudSaveBootstrap.ClearRegisteredSlots) ため
[assembly: InternalsVisibleTo("Shooter.Campaign.Tests")]

// 進行のテスト (Shooter.Progression.Tests) は、テストの間にクラウドセーブの登録と Play 中の同期を空に戻す (CloudSaveBootstrap.ClearRegisteredSlots・EndPlay) ため
[assembly: InternalsVisibleTo("Shooter.Progression.Tests")]
