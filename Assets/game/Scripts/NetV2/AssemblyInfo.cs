using System.Runtime.CompilerServices;

// 乗り物の同期 (NetV2Vehicles) は内部の型 (NetV2Weapons) を受けるので公開せず、サンプルの NetV2 のテスト (Shooter.Samples.Net.Tests) から直に叩けるようにする
[assembly: InternalsVisibleTo("Shooter.Samples.Net.Tests")]
