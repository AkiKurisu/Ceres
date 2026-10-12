using System;
using System.Buffers.Binary;
using System.Text;

namespace Ceres.DataDriven
{
    /// <summary>
    /// Writes DataTable values in a compact binary encoding: nil, booleans, integers, floating point, strings and arrays
    /// </summary>
    public sealed class DataTableWriter
    {
        private byte[] _buffer = new byte[256];

        private int _length;

        public void WriteNil() => Put(DataTableFormat.Nil);

        public void Write(bool value) => Put(value ? DataTableFormat.True : DataTableFormat.False);

        public void Write(sbyte value) => Write((long)value);

        public void Write(byte value) => Write((ulong)value);

        public void Write(short value) => Write((long)value);

        public void Write(ushort value) => Write((ulong)value);

        public void Write(int value) => Write((long)value);

        public void Write(uint value) => Write((ulong)value);

        public void Write(char value) => Write((ulong)value);

        public void Write(long value)
        {
            if (value >= 0)
            {
                Write((ulong)value);
                return;
            }
            if (value >= -32)
            {
                Put((byte)value);
            }
            else if (value >= sbyte.MinValue)
            {
                Put(DataTableFormat.Int8);
                Put((byte)value);
            }
            else if (value >= short.MinValue)
            {
                Put(DataTableFormat.Int16);
                BinaryPrimitives.WriteInt16BigEndian(Reserve(2), (short)value);
            }
            else if (value >= int.MinValue)
            {
                Put(DataTableFormat.Int32);
                BinaryPrimitives.WriteInt32BigEndian(Reserve(4), (int)value);
            }
            else
            {
                Put(DataTableFormat.Int64);
                BinaryPrimitives.WriteInt64BigEndian(Reserve(8), value);
            }
        }

        public void Write(ulong value)
        {
            if (value <= DataTableFormat.MaxPositiveFixInt)
            {
                Put((byte)value);
            }
            else if (value <= byte.MaxValue)
            {
                Put(DataTableFormat.UInt8);
                Put((byte)value);
            }
            else if (value <= ushort.MaxValue)
            {
                Put(DataTableFormat.UInt16);
                BinaryPrimitives.WriteUInt16BigEndian(Reserve(2), (ushort)value);
            }
            else if (value <= uint.MaxValue)
            {
                Put(DataTableFormat.UInt32);
                BinaryPrimitives.WriteUInt32BigEndian(Reserve(4), (uint)value);
            }
            else
            {
                Put(DataTableFormat.UInt64);
                BinaryPrimitives.WriteUInt64BigEndian(Reserve(8), value);
            }
        }

        public void Write(float value)
        {
            Put(DataTableFormat.Float32);
            BinaryPrimitives.WriteInt32BigEndian(Reserve(4), BitConverter.SingleToInt32Bits(value));
        }

        public void Write(double value)
        {
            Put(DataTableFormat.Float64);
            BinaryPrimitives.WriteInt64BigEndian(Reserve(8), BitConverter.DoubleToInt64Bits(value));
        }

        public void Write(string value)
        {
            if (value == null)
            {
                WriteNil();
                return;
            }
            int length = Encoding.UTF8.GetByteCount(value);
            if (length <= 31)
            {
                Put((byte)(DataTableFormat.MinFixStr | length));
            }
            else if (length <= byte.MaxValue)
            {
                Put(DataTableFormat.Str8);
                Put((byte)length);
            }
            else if (length <= ushort.MaxValue)
            {
                Put(DataTableFormat.Str16);
                BinaryPrimitives.WriteUInt16BigEndian(Reserve(2), (ushort)length);
            }
            else
            {
                Put(DataTableFormat.Str32);
                BinaryPrimitives.WriteUInt32BigEndian(Reserve(4), (uint)length);
            }
            Encoding.UTF8.GetBytes(value, Reserve(length));
        }

        public void WriteArrayHeader(int count)
        {
            if (count <= 15)
            {
                Put((byte)(DataTableFormat.MinFixArray | count));
            }
            else if (count <= ushort.MaxValue)
            {
                Put(DataTableFormat.Array16);
                BinaryPrimitives.WriteUInt16BigEndian(Reserve(2), (ushort)count);
            }
            else
            {
                Put(DataTableFormat.Array32);
                BinaryPrimitives.WriteUInt32BigEndian(Reserve(4), (uint)count);
            }
        }

        public void WriteRaw(ReadOnlySpan<byte> value)
        {
            value.CopyTo(Reserve(value.Length));
        }

        public byte[] ToArray() => _buffer.AsSpan(0, _length).ToArray();

        private void Put(byte value)
        {
            Reserve(1)[0] = value;
        }

        private Span<byte> Reserve(int count)
        {
            if (_length + count > _buffer.Length) Array.Resize(ref _buffer, Math.Max(_length + count, _buffer.Length * 2));
            var span = _buffer.AsSpan(_length, count);
            _length += count;
            return span;
        }
    }

    internal static class DataTableFormat
    {
        public const byte MaxPositiveFixInt = 0x7f;
        public const byte MinFixArray = 0x90;
        public const byte MaxFixArray = 0x9f;
        public const byte MinFixStr = 0xa0;
        public const byte MaxFixStr = 0xbf;
        public const byte Nil = 0xc0;
        public const byte False = 0xc2;
        public const byte True = 0xc3;
        public const byte Float32 = 0xca;
        public const byte Float64 = 0xcb;
        public const byte UInt8 = 0xcc;
        public const byte UInt16 = 0xcd;
        public const byte UInt32 = 0xce;
        public const byte UInt64 = 0xcf;
        public const byte Int8 = 0xd0;
        public const byte Int16 = 0xd1;
        public const byte Int32 = 0xd2;
        public const byte Int64 = 0xd3;
        public const byte Str8 = 0xd9;
        public const byte Str16 = 0xda;
        public const byte Str32 = 0xdb;
        public const byte Array16 = 0xdc;
        public const byte Array32 = 0xdd;
        public const byte MinNegativeFixInt = 0xe0;
    }
}
