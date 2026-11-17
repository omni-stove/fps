using System.Runtime.CompilerServices;

// サンプルの言語の一覧 (GameLanguageList) が入れ子の JSON を読むのに、Core の小さな JSON の読み手 (SaveJson) を公開の API にせずに使わせるため
[assembly: InternalsVisibleTo("Shooter.Runtime.UI")]
