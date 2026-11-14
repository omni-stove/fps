using System.Runtime.CompilerServices;

// ネットワークを起動せずに確かめたい内部の切り替え (NetPlayer.SetLocalView など) を EditMode テストから呼ぶ
[assembly: InternalsVisibleTo("Fps.Net.Tests")]

// サンプルの NetPlayer のプレハブを使うテストは、サンプルのテスト (Fps.Samples.Net.Tests) に置いてある
[assembly: InternalsVisibleTo("Fps.Samples.Net.Tests")]

// 画面 (Fps.Net.UI) は同期値 (NetPlayer.TeamVariable など) を購読して出すので、ゲーム進行の外へ公開せずに読ませる
[assembly: InternalsVisibleTo("Fps.Net.UI")]
