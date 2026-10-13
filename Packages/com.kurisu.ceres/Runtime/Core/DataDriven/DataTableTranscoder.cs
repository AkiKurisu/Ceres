using System;
using System.Collections.Generic;

namespace Ceres.DataDriven
{
    /// <summary>
    /// Rewrites rows encoded under a table's schema descriptor into the running schema's layout.
    /// Members are matched by name; missing members take the value of a newly created instance,
    /// and removed members or members whose value kind changed are dropped.
    /// </summary>
    internal sealed class DataTableTranscoder
    {
        private readonly DataTableSchemaType[] _types;

        private readonly Dictionary<(int, Type), ObjectPlan> _objects = new();

        private readonly Plan _root;

        public DataTableTranscoder(DataTableSchemaType[] types, DataTableCodec rowCodec)
        {
            _types = types;
            _root = GetObject(0, rowCodec);
        }

        public byte[] Transcode(ReadOnlySpan<byte> row)
        {
            var reader = new DataTableReader(row);
            var writer = new DataTableWriter();
            _root.Copy(ref reader, writer);
            return writer.ToArray();
        }

        private ObjectPlan GetObject(int typeIndex, DataTableCodec codec)
        {
            var key = (typeIndex, codec.ValueType);
            if (!_objects.TryGetValue(key, out var plan))
            {
                plan = new ObjectPlan();
                _objects.Add(key, plan);
                plan.Initialize(this, _types[typeIndex], codec);
            }
            return plan;
        }

        private Plan Match(string kind, DataTableCodec codec)
        {
            switch (kind[0])
            {
                case 'b': return codec.Kind == DataTableValueKind.Boolean ? RawPlan.Instance : null;
                case 'i': return codec.Kind == DataTableValueKind.Integer ? RawPlan.Instance : null;
                case 'f': return codec.Kind == DataTableValueKind.Float ? RawPlan.Instance : null;
                case 's': return codec.Kind == DataTableValueKind.String ? RawPlan.Instance : null;
                case 'e': return codec.Kind == DataTableValueKind.Enum ? RawPlan.Instance : null;
                case 'r': return codec.Kind == DataTableValueKind.SoftReference ? RawPlan.Instance : null;
                case 'u': return codec.Kind == DataTableValueKind.UnityValue && kind.Substring(2) == codec.ValueType.Name ? RawPlan.Instance : null;
                case 'l':
                    if (codec.Kind != DataTableValueKind.List) return null;
                    var element = Match(kind.Substring(2), codec.ElementCodec);
                    return element == null ? null : element == RawPlan.Instance ? RawPlan.Instance : new ListPlan(element);
                case 'o':
                    if (codec.Kind != DataTableValueKind.Object) return null;
                    return GetObject(int.Parse(kind.Substring(2)), codec);
                default: return null;
            }
        }

        private abstract class Plan
        {
            public abstract void Copy(ref DataTableReader reader, DataTableWriter writer);
        }

        private sealed class RawPlan : Plan
        {
            public static readonly RawPlan Instance = new();

            public override void Copy(ref DataTableReader reader, DataTableWriter writer)
            {
                writer.WriteRaw(reader.ReadRaw());
            }
        }

        private sealed class ListPlan : Plan
        {
            private readonly Plan _element;

            public ListPlan(Plan element)
            {
                _element = element;
            }

            public override void Copy(ref DataTableReader reader, DataTableWriter writer)
            {
                if (reader.TryReadNil())
                {
                    writer.WriteNil();
                    return;
                }
                int count = reader.ReadArrayHeader();
                writer.WriteArrayHeader(count);
                for (int i = 0; i < count; i++) _element.Copy(ref reader, writer);
            }
        }

        private sealed class ObjectPlan : Plan
        {
            private int[] _sources;

            private Plan[] _plans;

            private byte[][] _defaults;

            public void Initialize(DataTableTranscoder transcoder, DataTableSchemaType type, DataTableCodec codec)
            {
                var members = codec.Members;
                _sources = new int[members.Count];
                _plans = new Plan[members.Count];
                _defaults = new byte[members.Count][];

                var defaults = DataTableCodec.Encode(codec, codec.CreateDefault());
                var reader = new DataTableReader(defaults);
                reader.ReadArrayHeader();
                for (int m = 0; m < members.Count; m++)
                {
                    _defaults[m] = reader.ReadRaw().ToArray();
                    _sources[m] = -1;
                    for (int s = 0; s < type.members.Length; s++)
                    {
                        if (type.members[s] != members[m].Name) continue;
                        var plan = transcoder.Match(type.kinds[s], members[m].Codec);
                        if (plan == null) break;
                        _sources[m] = s;
                        _plans[m] = plan;
                        break;
                    }
                }
            }

            public override void Copy(ref DataTableReader reader, DataTableWriter writer)
            {
                if (reader.TryReadNil())
                {
                    writer.WriteNil();
                    return;
                }
                int count = reader.ReadArrayHeader();
                var values = new byte[count][];
                for (int i = 0; i < count; i++) values[i] = reader.ReadRaw().ToArray();

                writer.WriteArrayHeader(_sources.Length);
                for (int m = 0; m < _sources.Length; m++)
                {
                    int source = _sources[m];
                    if (source >= 0)
                    {
                        var value = new DataTableReader(values[source]);
                        _plans[m].Copy(ref value, writer);
                    }
                    else
                    {
                        writer.WriteRaw(_defaults[m]);
                    }
                }
            }
        }
    }
}
