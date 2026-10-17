using System;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace Ceres.Gameplay
{
    internal static class GameWorldLateTick
    {
        private struct CeresWorldLateTick
        {
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Install()
        {
            var playerLoop = PlayerLoop.GetCurrentPlayerLoop();
            var phases = playerLoop.subSystemList;
            for (int i = 0; i < phases.Length; ++i)
            {
                if (phases[i].type != typeof(PreLateUpdate)) continue;
                var systems = phases[i].subSystemList;
                int lateUpdate = Array.FindIndex(systems, system => system.type == typeof(PreLateUpdate.ScriptRunBehaviourLateUpdate));
                if (lateUpdate < 0) return;
                int installed = Array.FindIndex(systems, system => system.type == typeof(CeresWorldLateTick));
                var list = new System.Collections.Generic.List<PlayerLoopSystem>(systems);
                if (installed >= 0)
                {
                    list.RemoveAt(installed);
                    if (installed < lateUpdate) --lateUpdate;
                }
                list.Insert(lateUpdate + 1, new PlayerLoopSystem
                {
                    type = typeof(CeresWorldLateTick),
                    updateDelegate = GameWorld.LateTickCurrent
                });
                phases[i].subSystemList = list.ToArray();
                playerLoop.subSystemList = phases;
                PlayerLoop.SetPlayerLoop(playerLoop);
                return;
            }
        }
    }
}
