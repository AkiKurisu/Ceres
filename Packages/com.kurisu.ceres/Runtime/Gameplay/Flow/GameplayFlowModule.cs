using Ceres.Graph;
using Ceres.Gameplay.Animations;
using Ceres.Modules;
using UnityEngine.Scripting;

namespace Ceres.Gameplay.Flow
{
    [Preserve]
    internal class GameplayFlowModule: RuntimeModule
    {
        public override void Initialize()
        {
            /* Register port implicit conversation */
            // ========================= Animation =========================== //
            CeresPort<LayerHandle>.MakeCompatibleTo<int>(handle => handle.Id);
            CeresPort<int>.MakeCompatibleTo<LayerHandle>(d => new LayerHandle(d));
            CeresPort<string>.MakeCompatibleTo<LayerHandle>(str => new LayerHandle(str));
            // ========================= Animation =========================== //
        }
    }
}
