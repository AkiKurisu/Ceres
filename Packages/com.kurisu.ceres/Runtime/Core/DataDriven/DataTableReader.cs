using System;
using System.Buffers.Binary;
using System.Text;

namespace Ceres.DataDriven
{
    /// <summary>
    /// Reads values encoded by <see cref="DataTableWriter"/>
    /// </summary>
    public ref struct DataTableReader
    {
        private readonly ReadOnlySpan<byte> _data;

        private int _position;

        public DataTableReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _position = 0;
        }

        public bool TryReadNil()
        {
            if (_data[_position] != DataTableFormat.Nil) return false;
            _position++;
            return true;
        }

        public bool ReadBoolean()
        {
            byte code = _data[_position++];
            return code switch
            {
                DataTableFormat.False => false,
                DataTableFormat.True => true,
                _ => throw Unexpected(code)
            };
        }

        public sbyte ReadSByte() => checked((sbyte)ReadInt64());

        public byte ReadByte() => checked((byte)ReadInt64());

        public short ReadInt16() => checked((short)ReadInt64());

        public ushort ReadUInt16() => checked((ushort)ReadInt64());

        public int ReadInt32() => checked((int)ReadInt64());

        public uint ReadUInt32() => checked((uint)ReadInt64());

        public char ReadChar() => (char)ReadUInt16();

        public ulong ReadUInt64()
        {
            if (_data[_position] != DataTableFormat.UInt64) return checked((ulong)ReadInt64());
            _position++;
            return BinaryPrimitives.ReadUInt64BigEndian(Take(8));
        }

        public long ReadInt64()
        {
            byte code = _data[_position++];
            if (code <= DataTableFormat.MaxPositiveFixInt) return code;
            if (code >= DataTableFormat.MinNegativeFixInt) return (sbyte)code;
            return code switch
            {
                DataTableFormat.UInt8 => Take(1)[0],
                DataTableFormat.UInt16 => BinaryPrimitives.ReadUInt16BigEndian(Take(2)),
                DataTableFormat.UInt32 => BinaryPrimitives.ReadUInt32BigEndian(Take(4)),
                DataTableFormat.UInt64 => checked((long)BinaryPrimitives.ReadUInt64BigEndian(Take(8))),
                DataTableFormat.Int8 => (sbyte)Take(1)[0],
                DataTableFormat.Int16 => BinaryPrimitives.ReadInt16BigEndian(Take(2)),
                DataTableFormat.Int32 => BinaryPrimitives.ReadInt32BigEndian(Take(4)),
                DataTableFormat.Int64 => BinaryPrimitives.ReadInt64BigEndian(Take(8)),
                _ => throw Unexpected(code)
            };
        }

        public float ReadSingle()
        {
            byte code = _data[_position];
            if (code != DataTableFormat.Float32) return (float)ReadDouble();
            _position++;
            return BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(Take(4)));
        }

        public double ReadDouble()
        {
            byte code = _data[_position++];
            return code switch
            {
                DataTableFormat.Float32 => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(Take(4))),
                DataTableFormat.Float64 => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(Take(8))),
                _ => throw Unexpected(code)
            };
        }

        public string ReadString()
        {
            if (TryReadNil()) return null;
            byte code = _data[_position++];
            int length = code switch
            {
                >= DataTableFormat.MinFixStr and <= DataTableFormat.MaxFixStr => code & 0x1f,
                DataTableFormat.Str8 => Take(1)[0],
                DataTableFormat.Str16 => BinaryPrimitives.ReadUInt16BigEndian(Take(2)),
                DataTableFormat.Str32 => checked((int)BinaryPrimitives.ReadUInt32BigEndian(Take(4))),
                _ => throw Unexpected(code)
            };
            return Encoding.UTF8.GetString(Take(length));
        }

        public int ReadArrayHeader()
        {
            byte code = _data[_position++];
            return code switch
            {
                >= DataTableFormat.MinFixArray and <= DataTableFormat.MaxFixArray => code & 0x0f,
                DataTableFormat.Array16 => BinaryPrimitives.ReadUInt16BigEndian(Take(2)),
                DataTableFormat.Array32 => checked((int)BinaryPrimitives.ReadUInt32BigEndian(Take(4))),
                _ => throw Unexpected(code)
            };
        }

        /// <summary>
        /// Read the bytes of the next value without decoding it
        /// </summary>
        public ReadOnlySpan<byte> ReadRaw()
        {
            int start = _position;
            Skip();
            return _data.Slice(start, _position - start);
        }

        public void Skip()
        {
            byte code = _data[_position];
            switch (code)
            {
                case DataTableFormat.Nil:
                case DataTableFormat.False:
                case DataTableFormat.True:
                    _position++;
                    return;
                case DataTableFormat.Float32:
                case DataTableFormat.Float64:
                    ReadDouble();
                    return;
                case >= DataTableFormat.MinFixStr and <= DataTableFormat.MaxFixStr:
                case DataTableFormat.Str8:
                case DataTableFormat.Str16:
                case DataTableFormat.Str32:
                    ReadString();
                    return;
                case >= DataTableFormat.MinFixArray and <= DataTableFormat.MaxFixArray:
                case DataTableFormat.Array16:
                case DataTableFormat.Array32:
                    int count = ReadArrayHeader();
                    for (int i = 0; i < count; i++) Skip();
                    return;
                case DataTableFormat.UInt64:
                    ReadUInt64();
                    return;
                default:
                    ReadInt64();
                    return;
            }
        }

        private ReadOnlySpan<byte> Take(int count)
        {
            var span = _data.Slice(_position, count);
            _position += count;
            return span;
        }

        private FormatException Unexpected(byte code)
        {
            return new FormatException($"Unexpected DataTable value code 0x{code:x2} at offset {_position - 1}.");
        }
    }
}
