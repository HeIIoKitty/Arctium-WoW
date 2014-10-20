/*
 * Copyright (C) 2012-2014 Arctium Emulation <http://arctium.org>
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.IO;
using System.Text;
using Framework.Misc;
using Framework.Objects;

namespace Framework.Network.Packets
{
    public class Packet
    {
        public PacketHeader Header { get; private set; }
        public byte[] Data { get; set; }

        dynamic stream;

        #region Bit Variables
        byte bitPosition = 8;
        byte bitValue;
        #endregion

        public Packet()
        {
            stream = new BinaryWriter(new MemoryStream());

            if (stream == null)
                throw new InvalidOperationException("");
        }

        public Packet(byte[] data, bool readHeader = true)
        {
            stream = new BinaryReader(new MemoryStream(data));

            if (stream == null)
                throw new InvalidOperationException("");

            if (readHeader)
            {
                Header = new PacketHeader
                {
                    Size    = Read<ushort>(),
                    Message = Read<ushort>()
                };

                // Copy packet buffer for logging, etc.
                Data = new byte[Header.Size];
                Buffer.BlockCopy(data, 4, Data, 0, Header.Size);
            }
        }

        public Packet(object message)
        {
            stream = new BinaryWriter(new MemoryStream());

            if (stream == null)
                throw new InvalidOperationException("");

            Header = new PacketHeader
            {
                Size    = 4,
                Message = Convert.ToUInt16(message)
            };

            Write(Header.Size);
            Write(Header.Message);
        }

        #region Reader
        public T Read<T>(int count = 0, bool isCString = false)
        {
            switch (typeof(T).Name)
            {
                case "String":
                    if (isCString)
                    {
                        var tmpString = new StringBuilder();
                        var tmpChar = stream.ReadChar();
                        var tmpEndChar = Convert.ToChar(Encoding.UTF8.GetString(new byte[] { 0 }));

                        while (tmpChar != tmpEndChar)
                        {
                            tmpString.Append(tmpChar);
                            tmpChar = stream.ReadChar();
                        }

                        return (T)Convert.ChangeType(tmpString.ToString(), typeof(T));
                    }
                    else
                    {
                        var stringArray = stream.ReadBytes(count);

                        return (T)Convert.ChangeType(Encoding.UTF8.GetString(stringArray), typeof(T));
                    }
                case "SmartGuid":
                    var loLength = Read<byte>();
                    var hiLength = Read<byte>();

                    return new SmartGuid { Low = GetSmartGuid(loLength), High = GetSmartGuid(hiLength) }.ChangeType<T>();
                default:
                    return (T)Extensions.Read<T>(stream);
            }
        }

        ulong GetSmartGuid(byte length)
        {
            var guid = 0ul;

            for (var i = 0; i < 8; i++)
                if ((1 << i & length) != 0)
                    guid |= (ulong)Read<byte>() << (i * 8);

            return guid;
        }

        public T[] Read<T>(T[] data, params int[] indices)
        {
            for (int i = 0; i < indices.Length; i++)
                data[indices[i]] = Read<T>();

            return data;
        }

        public byte[] ReadBytes(int count)
        {
            return stream.ReadBytes(count);
        }

        public string ReadString(byte bits)
        {
            var length = GetBits<int>(bits);

            bitPosition = 8;
            bitValue = 0;

            return Read<string>(length);
        }

        public void Skip(int count)
        {
            stream.BaseStream.Position += count;
        }
        #endregion
        #region Writers
        public void Write(bool value)
        {
        	stream.Write(value);
        }
        
        public void Write(sbyte value)
        {
        	stream.Write(value);
        }
        
        public void Write(byte value)
        {
        	stream.Write(value);
        }
        
        public void Write(Int16 value)
        {
        	stream.Write(value);
        }
        
        public void Write(UInt16 value)
        {
        	stream.Write(value);
        }
        
        public void Write(Int64 value)
        {
        	stream.Write(value);
        }
        
        public void Write(UInt64 value)
        {
        	stream.Write(value);
        }
        
        public void Write(Single value)
        {
        	stream.Write(value);
        }
        
        public void Write(byte[] value)
        {
        	if(value != null)
        		stream.Write(value);
        }
        
        public void Write(string value, bool isCString = false)
        {
        	byte[] bytes = Encoding.UTF8.GetBytes(value);
        	bytes = isCString ? bytes.Combine(new byte[1]) : bytes;
        	this.Write(bytes);
        }
        
        public void Write(SmartGuid value)
        {
        	var loGuid = GetPackedGuid(value.Low, out byte loLength, out byte wLoLength);
        	var hiGuid = GetPackedGuid(value.High, out byte hiLength, out byte wHiLength);
        	
        	if(value.Low == 0 || value.High == 0)
        	{
        		Write((byte)0);
        		write((byte)0);
        	}
        	else
        	{
        		Write(loLength);
        		Write(hiLength);
        		WriteBytes(loGuid, wLoLength);
        		WriteBytes(hiGuid, wHiLength);
        	}
        }
        
        public void Write(Int32 value)
        {
        	stream.Write(value);
        }
        
        public void Write(uInt32 value)
        {
        	stream.Write(value);
        }

        public void WriteBytes(byte[] data, int count = 0)
        {
            if (count == 0)
                stream.Write(data);
            else
                stream.Write(data, 0, count);
        }

        byte[] GetPackedGuid(ulong guid, out byte gLength, out byte written)
        {
            var packedGuid = new byte[8];
            byte gLen = 0;
            byte length = 0;

            for (byte i = 0; guid != 0; i++)
            {
                if ((guid & 0xFF) != 0)
                {
                    gLen |= (byte)(1 << i);
                    packedGuid[length] = (byte)(guid & 0xFF);
                    ++length;
                }

                guid >>= 8;
            }

            gLength = gLen;
            written = length;

            return packedGuid;
        }
        public void Finish()
        {
            Data = new byte[stream.BaseStream.Length];

            stream.BaseStream.Seek(0, SeekOrigin.Begin);

            for (int i = 0; i < Data.Length; i++)
                Data[i] = (byte)stream.BaseStream.ReadByte();

            if (Header != null)
            {
                Header.Size = (ushort)(Data.Length - 2);

                Data[0] = (byte)(0xFF & Header.Size);
                Data[1] = (byte)(0xFF & (Header.Size >> 8));

                if (Header.Size > 0x7FFF)
                    Data[0] = (byte)(0x80 | (0xFF & (Header.Size >> 16)));
            }

            stream.Dispose();
        }
        #endregion
        #region BitReader
        public bool GetBit()
        {
            if (bitPosition == 8)
            {
                bitValue = Read<byte>();
                bitPosition = 0;
            }

            int returnValue = bitValue;
            bitValue = (byte)(2 * returnValue);
            ++bitPosition;

            return Convert.ToBoolean(returnValue >> 7);
        }

        public T GetBits<T>(byte bitCount)
        {
            int returnValue = 0;

            checked
            {
                for (var i = bitCount - 1; i >= 0; --i)
                    returnValue = GetBit() ? (1 << i) | returnValue : returnValue;
            }

            return returnValue.ChangeType<T>();
        }
        #endregion
        #region BitWriter
        public void PutBit<T>(T bit)
        {
            --bitPosition;

            if (Convert.ToBoolean(bit))
                bitValue |= (byte)(1 << (bitPosition));

            if (bitPosition == 0)
            {
                Write(bitValue);

                bitPosition = 8;
                bitValue = 0;
            }
        }

        public void PutBits<T>(T bit, int count)
        {
            checked
            {
                for (int i = count - 1; i >= 0; --i)
                    PutBit((T)Convert.ChangeType(((Convert.ToInt32(bit) >> i) & 1), typeof(T)));
            }
        }

        public void Flush()
        {
            Write(bitValue);

            bitValue = 0;
            bitPosition = 8;
        }
        #endregion
    }
}
