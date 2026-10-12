using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Ceres.DataDriven
{
    [Serializable]
    internal sealed class DataTableSchemaType
    {
        public string name;

        public string[] members;

        /// <summary>
        /// Value kind of each member: b, i, f, s, e, r, u:Name, o:TypeIndex or l:ElementKind
        /// </summary>
        public string[] kinds;
    }

    /// <summary>
    /// Schema descriptor of a row type: the row schema first, then every type it contains
    /// </summary>
    internal sealed class DataTableLayout
    {
        private static readonly Dictionary<Type, DataTableLayout> Layouts = new();

        public DataTableCodec Codec { get; }

        public DataTableSchemaType[] Types { get; }

        public Hash128 Hash { get; }

        private DataTableLayout(DataTableCodec codec)
        {
            Codec = codec;
            var codecs = new List<DataTableCodec> { codec };
            var types = new List<DataTableSchemaType>();
            for (int i = 0; i < codecs.Count; i++)
            {
                var members = codecs[i].Members;
                var type = new DataTableSchemaType
                {
                    name = codecs[i].ValueType.FullName,
                    members = new string[members.Count],
                    kinds = new string[members.Count]
                };
                for (int m = 0; m < members.Count; m++)
                {
                    type.members[m] = members[m].Name;
                    type.kinds[m] = GetKind(members[m].Codec, codecs);
                }
                types.Add(type);
            }
            Types = types.ToArray();

            var layout = new StringBuilder();
            foreach (var type in Types)
            {
                for (int m = 0; m < type.members.Length; m++)
                {
                    layout.Append(type.members[m]).Append(':').Append(type.kinds[m]).Append(';');
                }
                layout.Append('|');
            }
            Hash = Hash128.Compute(layout.ToString());
        }

        public static DataTableLayout Get(Type rowType)
        {
            lock (Layouts)
            {
                if (!Layouts.TryGetValue(rowType, out var layout))
                {
                    layout = new DataTableLayout(DataTableCodec.Get(rowType));
                    Layouts.Add(rowType, layout);
                }
                return layout;
            }
        }

        private static string GetKind(DataTableCodec codec, List<DataTableCodec> codecs)
        {
            switch (codec.Kind)
            {
                case DataTableValueKind.Boolean: return "b";
                case DataTableValueKind.Integer: return "i";
                case DataTableValueKind.Float: return "f";
                case DataTableValueKind.String: return "s";
                case DataTableValueKind.Enum: return "e";
                case DataTableValueKind.SoftReference: return "r";
                case DataTableValueKind.UnityValue: return "u:" + codec.ValueType.Name;
                case DataTableValueKind.List: return "l:" + GetKind(codec.ElementCodec, codecs);
                default:
                    int index = codecs.FindIndex(c => c.ValueType == codec.ValueType);
                    if (index < 0)
                    {
                        index = codecs.Count;
                        codecs.Add(codec);
                    }
                    return "o:" + index;
            }
        }
    }
}
