using System;
using Newtonsoft.Json;
using UnityEngine;

namespace Ceres.Serialization
{
    /// <summary>
    /// Prefer to use <see cref="JsonConvert"/> instead of <see cref="JsonUtility"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public class PreferJsonConvertAttribute : Attribute { }
}