using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine.Scripting;

[assembly: Preserve]
[assembly: AlwaysLinkAssembly]
[assembly: AssemblyProduct("Ceres")]
[assembly: AssemblyDescription("Powerful visual scripting toolkit for Unity")]
[assembly: AssemblyCopyright("Copyright © 2025")]

[assembly: InternalsVisibleTo("Ceres.Graph.Editor")]
[assembly: InternalsVisibleTo("Ceres.Flow")]
[assembly: InternalsVisibleTo("Ceres.Flow.Editor")]
[assembly: InternalsVisibleTo("Unity.Ceres.CodeGen")]
