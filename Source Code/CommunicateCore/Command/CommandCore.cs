using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommunicateCore.Command
{
	public class CommandCore
	{
		/// <summary>
		/// 从 byte 数组中提取数据生成对象
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		public static CommandCore Parse( byte[ ] data )
		{
			if ( data.Length < 7 ) return null;

			switch ( BitConverter.ToInt16( data, 0 ) )
			{
				case 10: return cHandshake.Parse( data );
				case 11: return cHandshakeAnswer.Parse( data );
				case 20: return cHeartbeat.Parse( data );
				case 30: return cCloseConnection.Parse( data );
				case 40: return cDebugMessage.Parse( data );

				//case 200: return cCardReaderUpload.Parse( data );

				//case 100: return cLightGetList.Parse( data );
				//case 101: return cLightGetListAnswer.Parse( data );
				//case 102: return cLightGetState.Parse( data );
				//case 103: return cLightSetState.Parse( data );
				//case 104: return cLightStateAnswer.Parse( data );
				//case 109: return cLightForceRefresh.Parse( data );

				//case 105: return cStopPositionGetList.Parse( data );
				//case 106: return cStopPositionGetListAnswer.Parse( data );
				//case 107: return cStopPositionCommand.Parse( data );
				//case 108: return cStopPositionCommandAnswer.Parse( data );

				//case 110: return cTrackTurnoutGetList.Parse( data );
				//case 111: return cTrackTurnoutGetListAnswer.Parse( data );
				//case 112: return cTrackTurnoutCommand.Parse( data );
				//case 113: return cTrackTurnoutCommandAnswer.Parse( data );
				//case 114: return cTrackTurnoutGetState.Parse( data );
				//case 115: return cTrackTurnoutGetStateAnswer.Parse( data );

				//case 116: return cRoadTurnoutGetList.Parse( data );
				//case 117: return cRoadTurnoutGetListAnswer.Parse( data );
				//case 118: return cRoadTurnoutCommand.Parse( data );
				//case 119: return cRoadTurnoutCommandAnswer.Parse( data );
				//case 120: return cRoadTurnoutGetState.Parse( data );
				//case 121: return cRoadTurnoutGetStateAnswer.Parse( data );

				//case 122: return cTrackGetList.Parse( data );
				//case 123: return cTrackGetListAnswer.Parse( data );
				//case 124: return cTrackCommand.Parse( data );
				//case 125: return cTrackCommandAnswer.Parse( data );
				//case 126: return cTrackGetState.Parse( data );
				//case 127: return cTrackGetStateAnswer.Parse( data );
				//case 140: return cTrackForceRefresh.Parse( data );

				//case 128: return cSwitchDeviceGetList.Parse( data );
				//case 129: return cSwitchDeviceGetListAnswer.Parse( data );
				//case 130: return cSwitchDeviceCommand.Parse( data );
				//case 131: return cSwitchDeviceCommandAnswer.Parse( data );
				//case 132: return cSwitchDeviceGetState.Parse( data );
				//case 133: return cSwitchDeviceGetStateAnswer.Parse( data );
				//case 141: return cSwitchDeviceForceRefresh.Parse( data );

				//case 134: return cNixietubeLEDGetList.Parse( data );
				//case 135: return cNixietubeLEDGetListAnswer.Parse( data );
				//case 136: return cNixietubeLEDCommand.Parse( data );
				//case 137: return cNixietubeLEDCommandAnswer.Parse( data );
				//case 138: return cNixietubeLEDGetState.Parse( data );
				//case 139: return cNixietubeLEDGetStateAnswer.Parse( data );

				//case 142: return cSwitchStateChange.Parse( data );

				//case 150: return cVirtualDataModuleGetList.Parse( data );
				//case 151: return cVirtualDataModuleGetListAnswer.Parse( data );
				//case 152: return cVirtualDataModuleWriteValue.Parse( data );
				//case 153: return cVirtualDataModuleWriteValueAnswer.Parse( data );
				//case 154: return cVirtualDataModuleReadValue.Parse( data );
				//case 155: return cVirtualDataModuleReadValueAnswer.Parse( data );

				default: return null;
			}
		}

		/// <summary>
		/// 将一个字符串转换为指定长度的 byte 数组
		/// </summary>
		/// <param name="s">要转化字符串</param>
		/// <param name="length">转化后的数组长度</param>
		/// <param name="def">当字符串长度不够时，补充的值</param>
		/// <returns></returns>
		public static byte[ ] GetStringBytes( string s, int length, byte def )
		{
			byte[ ] ret = new byte[length];
			for ( int i = 0; i < ret.Length; i++ ) ret[i] = def;
			if ( ( s != null ) && ( s.Trim( ) != "" ) )
			{
				byte[ ] tmp = System.Text.Encoding.Default.GetBytes( s.Substring( 0, s.Length < length ? s.Length : length ) );
				Array.Copy( tmp, ret, tmp.Length );
			}
			return ret;
		}

		/// <summary>
		/// 产生一个命令号
		/// </summary>
		protected static Int32 GenerateNumber( )
		{
			return Convert.ToInt32( DateTime.Now.ToString( "HHmmssfff" ) );
		}

		/// <summary>
		/// 将 DataTable 转化为二进制数组
		/// </summary>
		/// <param name="s"></param>
		/// <returns></returns>
		protected static byte[ ] GetDataTableBytes( DataTable s )
		{
			if ( s == null ) return new byte[0];

			byte[ ] buff = null;
			MemoryStream ms = new MemoryStream( );
			try
			{
				s.WriteXml( ms, XmlWriteMode.WriteSchema );
				ms.Seek( 0, SeekOrigin.Begin );
				buff = new byte[ms.Length];
				ms.Read( buff, 0, Convert.ToInt32( ms.Length ) );

				return buff;
			}
			catch ( Exception ex )
			{
				return new byte[0];
			}
			finally
			{
				buff = null;
				ms.Close( );
			}
		}

		/// <summary>
		/// 将 DataTable 转化为二进制数组
		/// </summary>
		/// <param name="s"></param>
		/// <returns></returns>
		protected static byte[ ] GetDataTableBytes( DataSet s )
		{
			if ( s == null ) return new byte[0];

			byte[ ] buff = null;
			MemoryStream ms = new MemoryStream( );
			try
			{
				s.WriteXml( ms, XmlWriteMode.WriteSchema );
				ms.Seek( 0, SeekOrigin.Begin );
				buff = new byte[ms.Length];
				ms.Read( buff, 0, Convert.ToInt32( ms.Length ) );

				return buff;
			}
			catch
			{
				return new byte[0];
			}
			finally
			{
				buff = null;
				ms.Close( );
			}
		}


		protected static DataTable GetBytesDataTable( byte[ ] buff, int startIdx, int length )
		{
			MemoryStream ms = new MemoryStream( );
			try
			{
				ms.Write( buff, startIdx, length );
				ms.Seek( 0, SeekOrigin.Begin );

				DataTable ret = new DataTable( );
				ret.ReadXml( ms );

				return ret;
			}
			catch
			{
				return null;
			}
			finally
			{
				ms.Close( );
			}

		}
		protected static DataSet GetBytesDataSet( byte[ ] buff, int startIdx, int length )
		{
			MemoryStream ms = new MemoryStream( );
			try
			{
				ms.Write( buff, startIdx, length );
				ms.Seek( 0, SeekOrigin.Begin );

				DataSet ret = new DataSet( );
				ret.ReadXml( ms );

				return ret;
			}
			catch
			{
				return null;
			}
			finally
			{
				ms.Close( );
			}

		}

		public static string ByteArray2Hex( byte[] buff )
		{
			string ret = "";
			int idx = 0;
			idx++;
			for ( int i = 0; i < buff.Length; i++ )
			{
				ret = ret + string.Format( "{0,2:X2}", buff[i] );
			}
			return ret;
		}


		/// <summary>
		/// 命令字
		/// </summary>
		protected short _id;
		public short ID { get { return _id; } }

		/// <summary>
		/// 命令号
		/// </summary>
		protected int _number = -1;
		public int Number
		{
			get
			{
				if ( _number < 0 ) _number = CommandCore.GenerateNumber( );
				return _number;
			}
			set { _number = value; }
		}

		/// <summary>
		/// 将命令信息转化为二进制数组
		/// </summary>
		/// <returns></returns>
		public virtual byte[ ] ToBinary( )
		{
			throw new Exception( "Undefined convert method !" );
		}

		/// <summary>
		/// Override ToString method
		/// </summary>
		/// <returns></returns>
		public override string ToString( )
		{
			return "ID = " + ID.ToString( ) + " : Number = " + Number.ToString( );
		}

		/// <summary>
		/// Generate packet send data
		/// </summary>
		/// <param name="content"></param>
		/// <returns></returns>
		protected byte[ ] GenerateBinary( byte[ ] content )
		{
			byte[ ] ret = new byte[content.Length + 6];
			Array.Copy( BitConverter.GetBytes( ID ), 0, ret, 0, 2 );
			Array.Copy( BitConverter.GetBytes( Number ), 0, ret, 2, 4 );
			Array.Copy( content, 0, ret, 6, content.Length );

			return ret;
		}


	}
}
