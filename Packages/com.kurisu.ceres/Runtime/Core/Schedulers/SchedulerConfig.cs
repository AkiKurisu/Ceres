using System;
using Ceres.Configs;
using UnityEngine;

namespace Ceres.Schedulers
{
    [Serializable]
    [ConfigPath("Ceres.Schedulers")]
    public class SchedulerConfig : Config<SchedulerConfig>
    {
        [SerializeField]
        [ConfigVariable("r.scheduler.stackTrace", IsEditor = true)]
        internal bool enableStackTrace = true;

        public static bool EnableStackTrace => Get().enableStackTrace;
    }
}