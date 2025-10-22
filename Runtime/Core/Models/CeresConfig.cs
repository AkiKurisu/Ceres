using System;
using Chris.Configs;
using UnityEngine;

namespace Ceres
{
    [Serializable]
    [ConfigPath("Ceres.Ceres")]
    public class CeresConfig : Config<CeresConfig>
    {
        [SerializeField]
        internal LogType logLevel = LogType.Log;
    }
}
