﻿using CitizenFX.Core;
using System;
using System.Collections.Generic;

namespace CitizenFX.MsgPack
{
	public class MsgPackSerializer
	{
		byte[] m_buffer;
		ulong m_position;

		public MsgPackSerializer()
		{
			m_buffer = new byte[256];
		}

		public byte[] AcquireBuffer()
		{
			return m_buffer;
		}

		public byte[] ToArray()
		{
			byte[] result = new byte[m_position];
			Array.Copy(m_buffer, result, (int)m_position);
			return result;
		}

		public void Reset()
		{
			m_position = 0;
		}

		void EnsureCapacity(uint size)
		{
			ulong requiredCapacity = m_position + size;
			if (requiredCapacity >= (ulong)m_buffer.LongLength)
			{
				byte[] oldBuffer = m_buffer;
				m_buffer = new byte[oldBuffer.Length * 2];
				Array.Copy(oldBuffer, m_buffer, oldBuffer.Length);
			}
		}

		public void Serialize(bool value)
		{
			Write(value ? (byte)MsgPackCode.True : (byte)MsgPackCode.False);
		}

		public void WriteNil()
		{
			Write((byte)MsgPackCode.Nil);
		}

		public void Serialize(object v) => MsgPackRegistry.Serialize(this, v);

		public void Serialize(sbyte v)
		{
			if (v < 0)
			{
				if (v >= unchecked((sbyte)MsgPackCode.FixIntNegativeMin))
					Write(unchecked((byte)v));
				else
					WriteBigEndian(MsgPackCode.Int8, (sbyte)v);
			}
			else
				Write(unchecked((byte)v));
		}

		public void Serialize(byte v)
		{
			if (v <= (byte)MsgPackCode.FixIntPositiveMax)
				Write(v);
			else
				Write(MsgPackCode.UInt8, v);
		}

		public void Serialize(short v)
		{
			if (v < 0)
			{
				if (v >= unchecked((sbyte)MsgPackCode.FixIntNegativeMin))
					Write(unchecked((byte)v));
				else if (v >= sbyte.MinValue)
					WriteBigEndian(MsgPackCode.Int8, (sbyte)v);
				else
					WriteBigEndian(MsgPackCode.Int16, v);
			}
			else
				Serialize(unchecked((ushort)v));
		}

		public void Serialize(ushort v)
		{
			if (v <= (byte)MsgPackCode.FixIntPositiveMax)
				Write(unchecked((byte)v));
			else if (v <= byte.MaxValue)
				Write(MsgPackCode.UInt8, unchecked((byte)v));
			else
				WriteBigEndian(MsgPackCode.UInt16, v);
		}

		public void Serialize(int v)
		{
			if (v < 0)
			{
				if (v >= unchecked((sbyte)MsgPackCode.FixIntNegativeMin))
					Write(unchecked((byte)v));
				else if (v >= sbyte.MinValue)
					Write(MsgPackCode.Int8, unchecked((byte)v));
				else if (v >= short.MinValue)
					WriteBigEndian(MsgPackCode.Int16, (short)v);
				else
					WriteBigEndian(MsgPackCode.Int32, (short)v);
			}
			else
				Serialize(unchecked((uint)v));
		}

		public void Serialize(uint v)
		{
			if (v <= (byte)MsgPackCode.FixIntPositiveMax)
				Write(unchecked((byte)v));
			else if (v <= byte.MaxValue)
				Write(MsgPackCode.UInt8, unchecked((byte)v));
			else if (v <= ushort.MaxValue)
				WriteBigEndian(MsgPackCode.UInt16, unchecked((ushort)v));
			else
				WriteBigEndian(MsgPackCode.UInt32, v);
		}

		public void Serialize(long v)
		{
			if (v < 0)
			{
				if (v >= unchecked((sbyte)MsgPackCode.FixIntNegativeMin))
					Write(unchecked((byte)v));
				else if (v >= sbyte.MinValue)
					WriteBigEndian(MsgPackCode.Int8, (sbyte)v);
				else if (v >= short.MinValue)
					WriteBigEndian(MsgPackCode.Int16, (short)v);
				else if (v >= int.MinValue)
					WriteBigEndian(MsgPackCode.Int32, (int)v);
				else
					WriteBigEndian(MsgPackCode.Int64, v);
			}
			else
				Serialize(unchecked((ulong)v));
		}

		public void Serialize(ulong v)
		{
			if (v <= (byte)MsgPackCode.FixIntPositiveMax)
				Write(unchecked((byte)v));
			else if (v <= byte.MaxValue)
				Write(MsgPackCode.Int8, unchecked((byte)v));
			else if (v <= ushort.MaxValue)
				WriteBigEndian(MsgPackCode.UInt16, unchecked((ushort)v));
			else if (v <= uint.MaxValue)
				WriteBigEndian(MsgPackCode.UInt32, unchecked((uint)v));
			else
				WriteBigEndian(MsgPackCode.UInt64, v);
		}

		public unsafe void Serialize(float v) => WriteBigEndian(MsgPackCode.Float32, *(uint*)&v);
		public unsafe void Serialize(double v) => WriteBigEndian(MsgPackCode.Float64, *(ulong*)&v);

		public void Serialize(IReadOnlyDictionary<string, string> v)
		{
			WriteMapHeader((uint)v.Count);
			foreach (var keyValue in v)
			{
				Serialize(keyValue.Key);
				Serialize(keyValue.Value);
			}
		}

		public void Serialize(Dictionary<string, string> v) => Serialize((IReadOnlyDictionary<string, string>)v);
		public void Serialize(IDictionary<string, string> v) => Serialize((IReadOnlyDictionary<string, string>)v);

		public unsafe void Serialize(string v)
		{
			fixed (char* p_value = v)
			{
				uint size = (uint)CString.UTF8EncodeLength(p_value, v.Length);

				if (size < (MsgPackCode.FixStrMax - MsgPackCode.FixStrMin))
					Write(unchecked((byte)((uint)MsgPackCode.FixStrMin + size)));
				else if (size <= byte.MaxValue)
					Write(MsgPackCode.Str8, unchecked((byte)size));
				else if (size <= ushort.MaxValue)
					Write(MsgPackCode.Str16, unchecked((byte)size));
				else
					Write(MsgPackCode.Str32, unchecked((byte)size));

				EnsureCapacity(size);
				fixed (byte* p_buffer = m_buffer)
				{
					CString.UTF8Encode(p_buffer + m_position, p_value, v.Length);
					m_position += size;
				}
			}
		}

		public unsafe void Serialize(CString v)
		{
			fixed (byte* p_value = v.value)
			{
				uint size = (uint)v.value.LongLength - 1u;

				if (size < (MsgPackCode.FixStrMax - MsgPackCode.FixStrMin))
					Write(unchecked((byte)((uint)MsgPackCode.FixStrMin + size)));
				else if (size <= byte.MaxValue)
					Write(MsgPackCode.Str8, unchecked((byte)size));
				else if (size <= ushort.MaxValue)
					Write(MsgPackCode.Str16, unchecked((byte)size));
				else
					Write(MsgPackCode.Str32, unchecked((byte)size));

				EnsureCapacity(size);
				Array.Copy(v.value, 0, m_buffer, (int)m_position, size);
				m_position += size;
			}
		}

		public void WriteMapHeader(uint v)
		{
			if (v < (MsgPackCode.FixMapMax - MsgPackCode.FixMapMin))
				Write(unchecked((byte)((uint)MsgPackCode.FixMapMin + v)));
			else if (v <= short.MaxValue)
				WriteBigEndian(MsgPackCode.Map16, (short)v);
			else
				WriteBigEndian(MsgPackCode.Map32, v);
		}

		public void WriteArrayHeader(uint v)
		{
			if (v < (MsgPackCode.FixArrayMax - MsgPackCode.FixArrayMin))
				Write(unchecked((byte)((uint)MsgPackCode.FixArrayMin + v)));
			else if (v <= short.MaxValue)
				WriteBigEndian(MsgPackCode.Array16, (short)v);
			else
				WriteBigEndian(MsgPackCode.Array32, v);
		}

		private void Write(byte code)
		{
			EnsureCapacity(1);
			m_buffer[m_position++] = code;
		}

		private void Write(MsgPackCode code, byte value)
		{
			EnsureCapacity(2);
			ulong pos = m_position;
			m_position += 2;
			m_buffer[pos] = (byte)code;
			m_buffer[pos + 1] = value;
		}

		private void WriteBigEndian(MsgPackCode code, ushort value)
		{
			EnsureCapacity(3);
			ulong pos = m_position;
			m_position += 3;
			m_buffer[pos] = (byte)code;
			if (BitConverter.IsLittleEndian)
			{
				m_buffer[pos + 1] = unchecked((byte)(value >> 8));
				m_buffer[pos + 2] = unchecked((byte)(value));
			}
			else
			{
				m_buffer[pos + 1] = unchecked((byte)value);
				m_buffer[pos + 2] = unchecked((byte)(value >> 8));
			}
		}

		private unsafe void WriteBigEndian(MsgPackCode code, uint v)
		{
			EnsureCapacity(5);
			fixed (byte* p_buffer = m_buffer)
			{
				byte* ptr = p_buffer + m_position;
				m_position += 5;

				if (BitConverter.IsLittleEndian)
				{
					v = (v >> 16) | (v << 16); // swap adjacent 16-bit blocks
					v = ((v & 0xFF00FF00u) >> 8) | ((v & 0x00FF00FFu) << 8); // swap adjacent 8-bit blocks
				}

				*ptr = (byte)code;
				*(uint*)(ptr + 1) = v;
			}
		}

		private unsafe void WriteBigEndian(MsgPackCode code, ulong v)
		{
			EnsureCapacity(9);
			fixed (byte* p_buffer = m_buffer)
			{
				byte* ptr = p_buffer + m_position;
				m_position += 9;

				if (BitConverter.IsLittleEndian)
				{
					v = (v >> 32) | (v << 32); // swap adjacent 32-bit blocks
					v = ((v & 0xFFFF0000FFFF0000u) >> 16) | ((v & 0x0000FFFF0000FFFFu) << 16); // swap adjacent 16-bit blocks
					v = ((v & 0xFF00FF00FF00FF00u) >> 8) | ((v & 0x00FF00FF00FF00FFu) << 8); // swap adjacent 8-bit blocks
				}

				*ptr = (byte)code;
				*(ulong*)(ptr + 1) = v;
			}
		}

		private void WriteBigEndian(MsgPackCode code, sbyte value) => WriteBigEndian(code, unchecked((byte)value));
		private void WriteBigEndian(MsgPackCode code, short value) => WriteBigEndian(code, unchecked((ushort)value));
		private void WriteBigEndian(MsgPackCode code, int value) => WriteBigEndian(code, unchecked((uint)value));
		private void WriteBigEndian(MsgPackCode code, long value) => WriteBigEndian(code, unchecked((ulong)value));
		private unsafe void WriteBigEndian(MsgPackCode code, float value) => WriteBigEndian(code, *(uint*)&value);
		private unsafe void WriteBigEndian(MsgPackCode code, double value) => WriteBigEndian(code, *(ulong*)&value);
	}
}
