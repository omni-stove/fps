using System.Runtime.CompilerServices;

// ネットワークを起動せずに確かめたい内部の切り替え (NetPlayer.SetLocalView など) を EditMode テストから呼ぶ
[assembly: InternalsVisibleTo("Fps.Net.Tests")]
