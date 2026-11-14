using System.Runtime.CompilerServices;

// 既定の UI (Fps.Runtime.UI) を入れずに使う場合を、登録済みの既定を外して EditMode テストで再現する (GameUi.ClearDefaults)
[assembly: InternalsVisibleTo("Fps.Tests.EditMode")]
