using System.Runtime.CompilerServices;

// 乗り物の箱の当たり (Shooter.Core の MapVehicle) が地形と同じ箱と光線の交差 (WorldGeometry) を、公開の API にせずに使うため
[assembly: InternalsVisibleTo("Shooter.Core")]
