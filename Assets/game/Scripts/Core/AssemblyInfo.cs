using System.Runtime.CompilerServices;

// サンプルの言語の一覧 (GameLanguageList) が入れ子の JSON を読むのに、Core の小さな JSON の読み手 (SaveJson) を公開の API にせずに使わせるため
[assembly: InternalsVisibleTo("Shooter.Runtime.UI")]

// キャンペーンのセーブ (Shooter.Campaign.Core の CampaignSave) が同じ JSON の読み書き (SaveJson) を、公開の API にせずに使うため
[assembly: InternalsVisibleTo("Shooter.Campaign.Core")]

// 隠密の足音の JSON (Shooter.Stealth.Core の StealthNoiseJson) がサーバーデータと同じ JSON の読み手 (ServerDataJson) を、公開の API にせずに使うため
[assembly: InternalsVisibleTo("Shooter.Stealth.Core")]

// 進行の JSON (Shooter.Progression.Core の ProgressionJson・AchievementJson・PlayerProfile) がサーバーデータとセーブと同じ JSON の読み書き (ServerDataJson・SpecJsonReader・SaveJson) と値の確かめ (SpecLists・SpecGuard) を、公開の API にせずに使うため
[assembly: InternalsVisibleTo("Shooter.Progression.Core")]
