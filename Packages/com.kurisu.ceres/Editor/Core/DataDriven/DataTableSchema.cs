using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        private const string RegistryPath = "Library/CeresDataTableSchemas.txt";

        private static readonly Hash128 MissingHash = Hash128.Compute("Missing");

        static DataTableSchema()
        {
            bool changed = false;
            var names = new List<string>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<IDataTableRow>())
            {
                if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters) continue;
                var name = GetDependencyName(DataTableSource.GetRowTypeName(type));
                names.Add(name);
                var hash = ComputeHash(type);
                if (SessionState.GetString(name, null) == hash.ToString()) continue;
                AssetDatabase.RegisterCustomDependency(name, hash);
                SessionState.SetString(name, hash.ToString());
                changed = true;
            }

            // Tables of a row type that no longer exists reimport and report it.
            var previous = File.Exists(RegistryPath) ? File.ReadAllLines(RegistryPath) : Array.Empty<string>();
            var current = new HashSet<string>(names);
            foreach (var name in previous)
            {
                if (current.Contains(name)) continue;
                AssetDatabase.RegisterCustomDependency(name, MissingHash);
                SessionState.EraseString(name);
                changed = true;
            }
            names.Sort(StringComparer.Ordinal);
            if (!names.SequenceEqual(previous))
            {
                File.WriteAllLines(RegistryPath, names);
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
