using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CommunicateCore.Common
{
	public static class CRC32
	{
		static ulong[ ] _crc32Table;

		/// <summary>
		/// Build CRC32 table
		/// </summary>
		private static void GetCRC32Table( )
		{
			ulong Crc;
			_crc32Table = new ulong[256]; int i, j;
			for ( i = 0; i < 256; i++ )
			{
				Crc = ( ulong )i;
				for ( j = 8; j > 0; j-- )
				{
					if ( ( Crc & 1 ) == 1 )
						Crc = ( Crc >> 1 ) ^ 0xEDB88320;
					else
						Crc >>= 1;
				}
				_crc32Table[i] = Crc;
			}
		}

		/// <summary>
		/// Build CRC32 proof
		/// </summary>
		/// <param name="content"></param>
		/// <returns></returns>
		public static UInt32 BuildCRC32( byte[ ] content )
		{
			//生成码表
			if ( _crc32Table == null )
				GetCRC32Table( );

			//byte[ ] buffer = System.Text.ASCIIEncoding.ASCII.GetBytes( content );
			ulong value = 0xffffffff;
			int len = content.Length;
			for ( int i = 0; i < len; i++ )
			{
				value = ( value >> 8 ) ^ _crc32Table[( value & 0xFF ) ^ content[i]];
			}
			return Convert.ToUInt32( value ^ 0xffffffff );
		}
	}


	public static class CRC8
	{
		/// <summary>
		/// Socket 通讯 CRC 计算
		/// </summary>
		/// <param name="buffer"></param>
		/// <returns></returns>
		public static byte BuildCRC( byte[ ] buffer )
		{
			byte crc = 0;
			for ( int j = 0; j < buffer.Length; j++ )
			{
				crc ^= buffer[j];
				for ( int i = 0; i < 8; i++ )
				{
					if ( ( crc & 0x01 ) != 0 )
					{
						crc >>= 1;
						crc ^= 0x8c;
					}
					else
					{
						crc >>= 1;
					}
				}
			}
			return crc;
		}

		/// <summary>
		/// 控制板通讯协议 CRC 计算，累加
		/// </summary>
		/// <param name="buffer"></param>
		/// <returns></returns>
		public static byte BuildDeviceCRC( byte[ ] buffer, int index, int length )
		{
			byte ret = 0x00;
			for ( int i = 0; i < length; i++ )
			{
				ret = ( byte )( ret + buffer[i + index] );
			}
			return ret;
		}

	}

}
