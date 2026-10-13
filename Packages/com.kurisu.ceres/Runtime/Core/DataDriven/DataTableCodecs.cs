using System;
using System.Collections.Generic;
using System.ComponentModel;
using Ceres.Resource;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Ceres.DataDriven
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class DataTableCodecs
    {
        public static readonly DataTableCodec<bool> Boolean = new ValueCodec<bool>(DataTableValueKind.Boolean,
            (ref DataTableReader r) => r.ReadBoolean(), (DataTableWriter w, bool v) => w.Write(v));

        public static readonly DataTableCodec<sbyte> SByte = new ValueCodec<sbyte>(DataTableValueKind.Integer,
            (ref DataTableReader r) => r.ReadSByte(), (DataTableWriter w, sbyte v) => w.Write(v));

        public static readonly DataTableCodec<byte> Byte = new ValueCodec<byte>(DataTableValueKind.Integer,
            (ref DataTableReader r) => r.ReadByte(), (DataTableWriter w, byte v) => w.Write(v));

        public static readonly DataTableCodec<short> Int16 = new ValueCodec<short>(DataTableValueKind.Integer,
            (ref DataTableReader r) => r.ReadInt16(), (DataTableWriter w, short v) => w.Write(v));

        public static readonly DataTableCodec<ushort> UInt16 = new ValueCodec<ushort>(DataTableValueKind.Integer,
            (ref DataTableReader r) => r.ReadUInt16(), (DataTableWriter w, ushort v) => w.Write(v));

        public static readonly DataTableCodec<int> Int32 = new ValueCodec<int>(DataTableValueKind.Integer,
            (ref DataTableReader r) => r.ReadInt32(), (DataTableWriter w, int v) => w.Write(v));

        public static readonly DataTableCodec<uint> UInt32 = new ValueCodec<uint>(DataTableValueKind.Integer,
            (ref DataTableReader r) => r.ReadUInt32(), (DataTableWriter w, uint v) => w.Write(v));

        public static readonly DataTableCodec<long> Int64 = new ValueCodec<long>(DataTableValueKind.Integer,
            (ref DataTableReader r) => r.ReadInt64(), (DataTableWriter w, long v) => w.Write(v));

        public static readonly DataTableCodec<ulong> UInt64 = new ValueCodec<ulong>(DataTableValueKind.Integer,
            (ref DataTableReader r) => r.ReadUInt64(), (DataTableWriter w, ulong v) => w.Write(v));

        public static readonly DataTableCodec<char> Char = new ValueCodec<char>(DataTableValueKind.Integer,
            (ref DataTableReader r) => r.ReadChar(), (DataTableWriter w, char v) => w.Write(v));

        public static readonly DataTableCodec<float> Single = new ValueCodec<float>(DataTableValueKind.Float,
            (ref DataTableReader r) => r.ReadSingle(), (DataTableWriter w, float v) => w.Write(v));

        public static readonly DataTableCodec<double> Double = new ValueCodec<double>(DataTableValueKind.Float,
            (ref DataTableReader r) => r.ReadDouble(), (DataTableWriter w, double v) => w.Write(v));

        public static readonly DataTableCodec<string> String = new ValueCodec<string>(DataTableValueKind.String,
            (ref DataTableReader r) => r.ReadString() ?? string.Empty, (DataTableWriter w, string v) => w.Write(v));

        public static readonly DataTableCodec<SoftAssetReference> SoftAssetReference = new ValueCodec<SoftAssetReference>(DataTableValueKind.SoftReference,
            (ref DataTableReader r) => new SoftAssetReference { Address = r.ReadString() ?? string.Empty },
            (DataTableWriter w, SoftAssetReference v) => w.Write(v?.Address));

        public static readonly DataTableCodec<Vector2> Vector2 = new FloatsCodec<Vector2>(2,
            (in Vector2 v, Span<float> f) => { f[0] = v.x; f[1] = v.y; },
            f => new Vector2(f[0], f[1]));

        public static readonly DataTableCodec<Vector3> Vector3 = new FloatsCodec<Vector3>(3,
            (in Vector3 v, Span<float> f) => { f[0] = v.x; f[1] = v.y; f[2] = v.z; },
            f => new Vector3(f[0], f[1], f[2]));

        public static readonly DataTableCodec<Vector4> Vector4 = new FloatsCodec<Vector4>(4,
            (in Vector4 v, Span<float> f) => { f[0] = v.x; f[1] = v.y; f[2] = v.z; f[3] = v.w; },
            f => new Vector4(f[0], f[1], f[2], f[3]));

        public static readonly DataTableCodec<Quaternion> Quaternion = new FloatsCodec<Quaternion>(4,
            (in Quaternion v, Span<float> f) => { f[0] = v.x; f[1] = v.y; f[2] = v.z; f[3] = v.w; },
            f => new Quaternion(f[0], f[1], f[2], f[3]));

        public static readonly DataTableCodec<Color> Color = new FloatsCodec<Color>(4,
            (in Color v, Span<float> f) => { f[0] = v.r; f[1] = v.g; f[2] = v.b; f[3] = v.a; },
            f => new Color(f[0], f[1], f[2], f[3]));

        public static readonly DataTableCodec<Rect> Rect = new FloatsCodec<Rect>(4,
            (in Rect v, Span<float> f) => { f[0] = v.x; f[1] = v.y; f[2] = v.width; f[3] = v.height; },
            f => new Rect(f[0], f[1], f[2], f[3]));

        public static readonly DataTableCodec<Bounds> Bounds = new FloatsCodec<Bounds>(6,
            (in Bounds v, Span<float> f) => { f[0] = v.center.x; f[1] = v.center.y; f[2] = v.center.z; f[3] = v.size.x; f[4] = v.size.y; f[5] = v.size.z; },
            f => new Bounds(new Vector3(f[0], f[1], f[2]), new Vector3(f[3], f[4], f[5])));

        public static readonly DataTableCodec<Color32> Color32 = new IntsCodec<Color32>(4,
            (in Color32 v, Span<int> i) => { i[0] = v.r; i[1] = v.g; i[2] = v.b; i[3] = v.a; },
            i => new Color32((byte)i[0], (byte)i[1], (byte)i[2], (byte)i[3]));

        public static readonly DataTableCodec<Vector2Int> Vector2Int = new IntsCodec<Vector2Int>(2,
            (in Vector2Int v, Span<int> i) => { i[0] = v.x; i[1] = v.y; },
            i => new Vector2Int(i[0], i[1]));

        public static readonly DataTableCodec<Vector3Int> Vector3Int = new IntsCodec<Vector3Int>(3,
            (in Vector3Int v, Span<int> i) => { i[0] = v.x; i[1] = v.y; i[2] = v.z; },
            i => new Vector3Int(i[0], i[1], i[2]));

        private delegate T ReadValue<out T>(ref DataTableReader reader);

        private delegate void WriteValue<in T>(DataTableWriter writer, T value);

        private sealed class ValueCodec<T> : DataTableCodec<T>
        {
            private readonly ReadValue<T> _read;

            private readonly WriteValue<T> _write;

            public ValueCodec(DataTableValueKind kind, ReadValue<T> read, WriteValue<T> write)
            {
                Kind = kind;
                _read = read;
                _write = write;
            }

            internal override DataTableValueKind Kind { get; }

            public override T Read(ref DataTableReader reader) => _read(ref reader);

            public override void Write(DataTableWriter writer, T value) => _write(writer, value);
        }

        private delegate void Split<T, TPart>(in T value, Span<TPart> parts);

        private sealed class FloatsCodec<T> : DataTableCodec<T>
        {
            private readonly int _count;

            private readonly Split<T, float> _split;

            private readonly Func<float[], T> _combine;

            public FloatsCodec(int count, Split<T, float> split, Func<float[], T> combine)
            {
                _count = count;
                _split = split;
                _combine = combine;
            }

            internal override DataTableValueKind Kind => DataTableValueKind.UnityValue;

            public override T Read(ref DataTableReader reader)
            {
                var values = new float[_count];
                reader.ReadArrayHeader();
                for (int i = 0; i < _count; i++) values[i] = reader.ReadSingle();
                return _combine(values);
            }

            public override void Write(DataTableWriter writer, T value)
            {
                Span<float> values = stackalloc float[_count];
                _split(value, values);
                writer.WriteArrayHeader(_count);
                foreach (var v in values) writer.Write(v);
            }
        }

        private sealed class IntsCodec<T> : DataTableCodec<T>
        {
            private readonly int _count;

            private readonly Split<T, int> _split;

            private readonly Func<int[], T> _combine;

            public IntsCodec(int count, Split<T, int> split, Func<int[], T> combine)
            {
                _count = count;
                _split = split;
                _combine = combine;
            }

            internal override DataTableValueKind Kind => DataTableValueKind.UnityValue;

            public override T Read(ref DataTableReader reader)
            {
                var values = new int[_count];
                reader.ReadArrayHeader();
                for (int i = 0; i < _count; i++) values[i] = reader.ReadInt32();
                return _combine(values);
            }

            public override void Write(DataTableWriter writer, T value)
            {
                Span<int> values = stackalloc int[_count];
                _split(value, values);
                writer.WriteArrayHeader(_count);
                foreach (var v in values) writer.Write(v);
            }
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class DataTableEnumCodec<T> : DataTableCodec<T> where T : unmanaged, Enum
    {
        private static readonly TypeCode Underlying = Type.GetTypeCode(Enum.GetUnderlyingType(typeof(T)));

        internal override DataTableValueKind Kind => DataTableValueKind.Enum;

        public override T Read(ref DataTableReader reader)
        {
            switch (Underlying)
            {
                case TypeCode.SByte: { var v = reader.ReadSByte(); return UnsafeUtility.As<sbyte, T>(ref v); }
                case TypeCode.Byte: { var v = reader.ReadByte(); return UnsafeUtility.As<byte, T>(ref v); }
                case TypeCode.Int16: { var v = reader.ReadInt16(); return UnsafeUtility.As<short, T>(ref v); }
                case TypeCode.UInt16: { var v = reader.ReadUInt16(); return UnsafeUtility.As<ushort, T>(ref v); }
                case TypeCode.UInt32: { var v = reader.ReadUInt32(); return UnsafeUtility.As<uint, T>(ref v); }
                case TypeCode.Int64: { var v = reader.ReadInt64(); return UnsafeUtility.As<long, T>(ref v); }
                case TypeCode.UInt64: { var v = reader.ReadUInt64(); return UnsafeUtility.As<ulong, T>(ref v); }
                default: { var v = reader.ReadInt32(); return UnsafeUtility.As<int, T>(ref v); }
            }
        }

        public override void Write(DataTableWriter writer, T value)
        {
            switch (Underlying)
            {
                case TypeCode.SByte: writer.Write(UnsafeUtility.As<T, sbyte>(ref value)); break;
                case TypeCode.Byte: writer.Write(UnsafeUtility.As<T, byte>(ref value)); break;
                case TypeCode.Int16: writer.Write(UnsafeUtility.As<T, short>(ref value)); break;
                case TypeCode.UInt16: writer.Write(UnsafeUtility.As<T, ushort>(ref value)); break;
                case TypeCode.UInt32: writer.Write(UnsafeUtility.As<T, uint>(ref value)); break;
                case TypeCode.Int64: writer.Write(UnsafeUtility.As<T, long>(ref value)); break;
                case TypeCode.UInt64: writer.Write(UnsafeUtility.As<T, ulong>(ref value)); break;
                default: writer.Write(UnsafeUtility.As<T, int>(ref value)); break;
            }
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class DataTableListCodec<T> : DataTableCodec<List<T>>
    {
        private readonly DataTableCodec<T> _element;

        public DataTableListCodec(DataTableCodec<T> element)
        {
            _element = element;
        }

        internal override DataTableValueKind Kind => DataTableValueKind.List;

        internal override DataTableCodec ElementCodec => _element;

        public override List<T> Read(ref DataTableReader reader)
        {
            int count = reader.ReadArrayHeader();
            var list = new List<T>(count);
            for (int i = 0; i < count; i++) list.Add(_element.Read(ref reader));
            return list;
        }

        public override void Write(DataTableWriter writer, List<T> value)
        {
            int count = value?.Count ?? 0;
            writer.WriteArrayHeader(count);
            for (int i = 0; i < count; i++) _element.Write(writer, value[i]);
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class DataTableArrayCodec<T> : DataTableCodec<T[]>
    {
        private readonly DataTableCodec<T> _element;

        public DataTableArrayCodec(DataTableCodec<T> element)
        {
            _element = element;
        }

        internal override DataTableValueKind Kind => DataTableValueKind.List;

        internal override DataTableCodec ElementCodec => _element;

        public override T[] Read(ref DataTableReader reader)
        {
            int count = reader.ReadArrayHeader();
            var array = new T[count];
            for (int i = 0; i < count; i++) array[i] = _element.Read(ref reader);
            return array;
        }

        public override void Write(DataTableWriter writer, T[] value)
        {
            int count = value?.Length ?? 0;
            writer.WriteArrayHeader(count);
            for (int i = 0; i < count; i++) _element.Write(writer, value[i]);
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class DataTableSoftAssetReferenceCodec<T> : DataTableCodec<SoftAssetReference<T>> where T : UnityEngine.Object
    {
        internal override DataTableValueKind Kind => DataTableValueKind.SoftReference;

        public override SoftAssetReference<T> Read(ref DataTableReader reader)
        {
            return new SoftAssetReference<T> { Address = reader.ReadString() ?? string.Empty };
        }

        public override void Write(DataTableWriter writer, SoftAssetReference<T> value)
        {
            writer.Write(value?.Address);
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class DataTableOptionalCodec<T> : DataTableObjectCodec<Optional<T>>
    {
        private readonly DataTableCodec<T> _value;

        public DataTableOptionalCodec(DataTableCodec<T> value)
        {
            _value = value;
        }

        protected override int MemberCount => 2;

        protected override DataTableMember[] CreateMembers() => new[]
        {
            new DataTableMember("enabled", DataTableCodecs.Boolean),
            new DataTableMember("value", _value)
        };

        protected override Optional<T> Create() => default;

        protected override void ReadMembers(ref DataTableReader reader, ref Optional<T> value)
        {
            bool enabled = reader.ReadBoolean();
            value = new Optional<T>(_value.Read(ref reader), enabled);
        }

        protected override void WriteMembers(DataTableWriter writer, Optional<T> value)
        {
            writer.Write(value.Enabled);
            _value.Write(writer, value.Value);
        }
    }
}
