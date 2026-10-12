using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using MessagePack;
using UnityEditor;
using UnityEngine;

namespace Ceres.DataDriven.Editor
{
    /// <summary>
    /// Registers each row type's persisted layout as a custom dependency so tables reimport when their schema changes
    /// </summary>
    [InitializeOnLoad]
    internal static class DataTableSchema
    {
        private const string DependencyPrefix = "Ceres.DataTable.Schema/";

        static DataTableSchema()
        {
            bool changed = false;
            foreach (var type in TypeCache.GetTypesDerivedFrom<IDataTableRow>())
            {
                if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters) continue;
                var name = GetDependencyName(DataTableSource.GetRowTypeName(type));
                var hash = ComputeHash(type);
                if (SessionState.GetString(name, null) == hash.ToString()) continue;
                AssetDatabase.RegisterCustomDependency(name, hash);
                SessionState.SetString(name, hash.ToString());
                changed = true;
            }
            if (changed)
            {
                EditorApplication.delayCall += AssetDatabase.Refresh;
            }
        }

        public static string GetDependencyName(string rowTypeName)
        {
            return DependencyPrefix + rowTypeName;
        }

        private static Hash128 ComputeHash(Type type)
        {
            var builder = new StringBuilder();
            Describe(type, builder, new HashSet<Type>());
            return Hash128.Compute(builder.ToString());
        }

        private static void Describe(Type type, StringBuilder builder, HashSet<Type> visited)
        {
            if (DataTableBuilder.IsLeaf(type) || !visited.Add(type))
            {
                builder.Append(type.FullName).Append(';');
                return;
            }
            if (type.IsArray)
            {
                Describe(type.GetElementType(), builder, visited);
                builder.Append("[];");
                return;
            }
            if (type.IsGenericType && typeof(IList).IsAssignableFrom(type))
            {
                Describe(type.GetGenericArguments()[0], builder, visited);
                builder.Append("List;");
                return;
            }
            builder.Append(type.FullName).Append(type.IsDefined(typeof(MessagePackObjectAttribute), false) ? "+mp" : "").Append('{');
            foreach (var field in DataTableBuilder.GetSerializedFields(type))
            {
                builder.Append(field.Name).Append(field.IsDefined(typeof(IgnoreMemberAttribute), true) ? "!" : "").Append(':');
                Describe(field.FieldType, builder, visited);
            }
            builder.Append('}');
        }
    }
}
