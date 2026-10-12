using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace Ceres.DataDriven
{
    /// <summary>
    /// How a row member is encoded; members of the same kind can be read from each other
    /// </summary>
    internal enum DataTableValueKind
    {
        Boolean,
        Integer,
        Float,
        String,
        Enum,
        List,
        Object,
        UnityValue,
        SoftReference
    }

    public readonly struct DataTableMember
    {
        public readonly string Name;

        public readonly DataTableCodec Codec;

        public DataTableMember(string name, DataTableCodec codec)
        {
            Name = name;
            Codec = codec;
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class DataTableCodecAttribute : Attribute
    {
        public Type Type { get; }

        public Type CodecType { get; }

        public DataTableCodecAttribute(Type type, Type codecType)
        {
            Type = type;
            CodecType = codecType;
        }
    }

    /// <summary>
    /// Binary encoding of one type in DataTable rows
    /// </summary>
    public abstract class DataTableCodec
    {
        private static readonly Dictionary<Type, DataTableCodec> Codecs = new();

        private static readonly Dictionary<Type, Type> Registrations = new();

        private static readonly HashSet<Assembly> ScannedAssemblies = new();

        internal abstract Type ValueType { get; }

        internal abstract DataTableValueKind Kind { get; }

        internal virtual DataTableCodec ElementCodec => null;

        /// <summary>
        /// Members of an object, in encoding order
        /// </summary>
        internal virtual IReadOnlyList<DataTableMember> Members => Array.Empty<DataTableMember>();

        internal abstract object ReadBoxed(ref DataTableReader reader);

        internal abstract void WriteBoxed(DataTableWriter writer, object value);

        internal virtual object CreateDefault() => null;

        internal static DataTableCodec Get(Type type)
        {
            lock (Codecs)
            {
                if (Codecs.TryGetValue(type, out var codec)) return codec;
                if (!Registrations.ContainsKey(type))
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) Scan(assembly);
                }
                if (!Registrations.TryGetValue(type, out var codecType))
                {
                    throw new InvalidOperationException($"'{type.FullName}' has no DataTable codec. Codecs are generated at compile time " +
                                                        "for row schemas and the serializable types they contain in assemblies that reference Ceres.");
                }
                codec = (DataTableCodec)Activator.CreateInstance(codecType);
                Codecs.Add(type, codec);
                return codec;
            }
        }

        private static void Scan(Assembly assembly)
        {
            if (!ScannedAssemblies.Add(assembly)) return;
            foreach (var attribute in assembly.GetCustomAttributes<DataTableCodecAttribute>())
            {
                Registrations.TryAdd(attribute.Type, attribute.CodecType);
            }
        }

        internal static byte[] Encode(DataTableCodec codec, object value)
        {
            var writer = new DataTableWriter();
            codec.WriteBoxed(writer, value);
            return writer.ToArray();
        }

        internal static object Decode(DataTableCodec codec, ReadOnlySpan<byte> bytes)
        {
            var reader = new DataTableReader(bytes);
            return codec.ReadBoxed(ref reader);
        }
    }

    public abstract class DataTableCodec<T> : DataTableCodec
    {
        private static DataTableCodec<T> _default;

        public static DataTableCodec<T> Default => _default ??= (DataTableCodec<T>)Get(typeof(T));

        internal sealed override Type ValueType => typeof(T);

        public abstract T Read(ref DataTableReader reader);

        public abstract void Write(DataTableWriter writer, T value);

        internal sealed override object ReadBoxed(ref DataTableReader reader) => Read(ref reader);

        internal sealed override void WriteBoxed(DataTableWriter writer, object value) => Write(writer, (T)value);
    }

    /// <summary>
    /// Codec of a type encoded as an array of its members; derived by generated code
    /// </summary>
    public abstract class DataTableObjectCodec<T> : DataTableCodec<T>
    {
        private DataTableMember[] _members;

        internal sealed override DataTableValueKind Kind => DataTableValueKind.Object;

        internal sealed override IReadOnlyList<DataTableMember> Members => _members ??= CreateMembers();

        protected abstract int MemberCount { get; }

        protected abstract DataTableMember[] CreateMembers();

        protected abstract T Create();

        internal sealed override object CreateDefault() => Create();

        protected abstract void ReadMembers(ref DataTableReader reader, ref T value);

        protected abstract void WriteMembers(DataTableWriter writer, T value);

        public sealed override T Read(ref DataTableReader reader)
        {
            var value = Create();
            if (reader.TryReadNil()) return value;
            reader.ReadArrayHeader();
            ReadMembers(ref reader, ref value);
            return value;
        }

        public sealed override void Write(DataTableWriter writer, T value)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }
            writer.WriteArrayHeader(MemberCount);
            WriteMembers(writer, value);
        }
    }
}
