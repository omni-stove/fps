using System.Runtime.CompilerServices;

// HUD の購読と表示の内部の切り替え (NetHud.Initialize・Tick など) を、ネットワークを起動せずに EditMode テストから呼ぶ
[assembly: InternalsVisibleTo("Fps.Net.Tests")]
