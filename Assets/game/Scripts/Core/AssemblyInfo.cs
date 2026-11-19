using System.Runtime.CompilerServices;

// サンプルの言語の一覧 (GameLanguageList) が入れ子の JSON を読むのに、Core の小さな JSON の読み手 (SaveJson) を公開の API にせずに使わせるため
[assembly: InternalsVisibleTo("Shooter.Runtime.UI")]

// キャンペーンのセーブ (Shooter.Campaign.Core の CampaignSave) が同じ JSON の読み書き (SaveJson) を、公開の API にせずに使うため
[assembly: InternalsVisibleTo("Shooter.Campaign.Core")]
