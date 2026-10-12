using System;
using System.Collections.Generic;
using MessagePack;
using MessagePack.Resolvers;
using MessagePack.Unity;

namespace Ceres.DataDriven
{
    /// <summary>
    /// Binary encoding of DataTable rows; one codec per row type
    /// </summary>
    internal abstract class DataTableRowCodec
    {
        public static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard
            .WithResolver(CompositeResolver.Create(UnityResolver.Instance, StandardResolver.Instance))
            .WithSecurity(MessagePackSecurity.TrustedData);

        private static readonly Dictionary<Type, DataTableRowCodec> Codecs = new();

        public static DataTableRowCodec Get(Type rowType)
        {
            lock (Codecs)
            {
                if (!Codecs.TryGetValue(rowType, out var codec))
                {
                    if (!rowType.IsClass || !typeof(IDataTableRow).IsAssignableFrom(rowType))
                    {
                        throw new ArgumentException($"DataTable row type '{rowType.FullName}' must be a class implementing {nameof(IDataTableRow)}.");
                    }
                    codec = (DataTableRowCodec)Activator.CreateInstance(typeof(DataTableRowCodec<>).MakeGenericType(rowType));
                    Codecs.Add(rowType, codec);
                }
                return codec;
            }
        }

        public abstract IDataTableRow Decode(ReadOnlyMemory<byte> bytes);

        public abstract byte[] Encode(IDataTableRow row);
    }

    internal sealed class DataTableRowCodec<T> : DataTableRowCodec where T : class, IDataTableRow
    {
        public override IDataTableRow Decode(ReadOnlyMemory<byte> bytes)
        {
            return MessagePackSerializer.Deserialize<T>(bytes, Options);
        }

        public override byte[] Encode(IDataTableRow row)
        {
            return MessagePackSerializer.Serialize((T)row, Options);
        }
    }
}
