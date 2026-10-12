using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MessagePack;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace Ceres.DataDriven.Editor
{
    public static class DataTableBuilder
    {
        /// <summary>
        /// Encode a source into a runtime table; throws <see cref="FormatException"/> for invalid sources
        /// </summary>
        public static DataTable Build(DataTableSource source, Action<string> warnings = null)
        {
            var rowType = DataTableSource.ResolveRowType(source.RowTypeName);
            if (rowType == null && !string.IsNullOrEmpty(source.RowTypeName))
            {
                throw new FormatException($"Row type '{source.RowTypeName}' was not found.");
            }
            if (rowType == null && source.Rows.Count > 0)
            {
                throw new FormatException("A table with rows needs a row type.");
            }
            if (rowType != null && (!rowType.IsClass || rowType.IsAbstract || !typeof(IDataTableRow).IsAssignableFrom(rowType)))
            {
                throw new FormatException($"Row type '{rowType.FullName}' must be a concrete class implementing {nameof(IDataTableRow)}.");
            }

            var ids = new string[source.Rows.Count];
            var offsets = new int[source.Rows.Count + 1];
            var known = new HashSet<string>();
            using var data = new MemoryStream();
            var codec = rowType != null ? DataTableRowCodec.Get(rowType) : null;
            for (int i = 0; i < source.Rows.Count; i++)
            {
                var row = source.Rows[i];
                if (string.IsNullOrEmpty(row.Id))
                {
                    throw new FormatException($"Row {i} has no id.");
                }
                if (!known.Add(row.Id))
                {
                    throw new FormatException($"Row id '{row.Id}' is used more than once.");
                }

                var value = (IDataTableRow)JsonUtility.FromJson(row.Json, rowType);
                if (value is IValidateRow validate && !validate.ValidateRow(row.Id, out var reason))
                {
                    warnings?.Invoke($"Row '{row.Id}': {reason}");
                }

                var bytes = Encode(codec, rowType, row.Id, value);
                ids[i] = row.Id;
                offsets[i] = (int)data.Length;
                data.Write(bytes, 0, bytes.Length);
            }
            offsets[source.Rows.Count] = (int)data.Length;

            var table = ScriptableObject.CreateInstance<DataTable>();
            table.Initialize(rowType, ids, offsets, data.ToArray());
            return table;
        }

        private static byte[] Encode(DataTableRowCodec codec, Type rowType, string rowId, IDataTableRow value)
        {
            byte[] bytes;
            IDataTableRow decoded;
            try
            {
                bytes = codec.Encode(value);
                decoded = codec.Decode(bytes);
            }
            catch (MessagePackSerializationException e)
            {
                throw new FormatException($"Row '{rowId}' of {rowType.FullName} cannot be encoded: {GetInnermostMessage(e)} " +
                                          "Mark the row type and every serializable type it contains with [MessagePackObject(true)].", e);
            }

            var expected = JObject.Parse(JsonUtility.ToJson(WithoutIgnoredMembers(value)));
            var actual = JObject.Parse(JsonUtility.ToJson(decoded));
            var path = FindDifference(expected, actual);
            if (path != null)
            {
                throw new FormatException($"Row '{rowId}' of {rowType.FullName} loses member '{path}' in binary encoding. " +
                                          "Serialize the member or mark it [IgnoreMember] if it is Editor-only.");
            }
            return bytes;
        }

        private static string GetInnermostMessage(Exception exception)
        {
            while (exception.InnerException != null) exception = exception.InnerException;
            return exception.Message;
        }

        /// <summary>
        /// Copy of a row with every [IgnoreMember] field restored to its initial value
        /// </summary>
        private static object WithoutIgnoredMembers(object row)
        {
            var copy = JsonUtility.FromJson(JsonUtility.ToJson(row), row.GetType());
            ResetIgnoredMembers(copy, 0);
            return copy;
        }

        private static void ResetIgnoredMembers(object target, int depth)
        {
            if (target == null || depth > 16) return;
            var type = target.GetType();
            object initial = null;
            foreach (var field in GetSerializedFields(type))
            {
                if (field.IsDefined(typeof(IgnoreMemberAttribute), true))
                {
                    initial ??= CreateInitial(type);
                    field.SetValue(target, initial != null ? field.GetValue(initial) : null);
                    continue;
                }
                var value = field.GetValue(target);
                if (value == null || IsLeaf(value.GetType())) continue;
                if (value is IList list)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var element = list[i];
                        if (element == null || IsLeaf(element.GetType())) continue;
                        ResetIgnoredMembers(element, depth + 1);
                        list[i] = element;
                    }
                    continue;
                }
                ResetIgnoredMembers(value, depth + 1);
                if (value.GetType().IsValueType) field.SetValue(target, value);
            }
        }

        private static object CreateInitial(Type type)
        {
            try
            {
                return Activator.CreateInstance(type, true);
            }
            catch (MissingMethodException)
            {
                return null;
            }
        }

        internal static bool IsLeaf(Type type)
        {
            return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)
                   || typeof(UObject).IsAssignableFrom(type) || type.Namespace == "UnityEngine";
        }

        /// <summary>
        /// Instance fields Unity serializes, from base type to derived type
        /// </summary>
        internal static IEnumerable<FieldInfo> GetSerializedFields(Type type)
        {
            var chain = new Stack<Type>();
            for (var t = type; t != null && t != typeof(object) && t != typeof(ValueType); t = t.BaseType)
            {
                chain.Push(t);
            }
            foreach (var t in chain)
            {
                foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.IsInitOnly || field.IsNotSerialized) continue;
                    if (!field.IsPublic && !field.IsDefined(typeof(SerializeField), true)) continue;
                    yield return field;
                }
            }
        }

        private static string FindDifference(JToken expected, JToken actual, string path = "")
        {
            if (expected.Type != actual.Type)
            {
                return path.Length > 0 ? path : "(row)";
            }
            switch (expected)
            {
                case JObject expectedObject:
                    var actualObject = (JObject)actual;
                    foreach (var property in expectedObject.Properties())
                    {
                        var childPath = path.Length > 0 ? $"{path}.{property.Name}" : property.Name;
                        var other = actualObject[property.Name];
                        if (other == null) return childPath;
                        var difference = FindDifference(property.Value, other, childPath);
                        if (difference != null) return difference;
                    }
                    return null;
                case JArray expectedArray:
                    var actualArray = (JArray)actual;
                    if (expectedArray.Count != actualArray.Count) return path;
                    for (int i = 0; i < expectedArray.Count; i++)
                    {
                        var difference = FindDifference(expectedArray[i], actualArray[i], $"{path}[{i}]");
                        if (difference != null) return difference;
                    }
                    return null;
                default:
                    return JToken.DeepEquals(expected, actual) ? null : path;
            }
        }
    }
}
