using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommunicateCore.Command
{
	public class cHandshake : CommandCore
	{
		#region Inherited

		/// <summary>
		/// Override ToString method
		/// </summary>
		/// <returns></returns>
		public override string ToString( )
		{
			return base.ToString( ) + " - Handshake < client type: " + Enum.GetName( typeof( HandshakeRequester ), _type ) + " > < remote ip: " + _ip.Trim( ) + " > < remote port: " + _port.ToString( ) + " >";
		}


		/// <summary>
		/// 重载的方法，将 cHandshake 命令转化为二进制数据
		/// </summary>
		/// <returns></returns>
		public override byte[ ] ToBinary( )
		{
			List<byte> ret = new List<byte>( );
			ret.Add( ( byte )_type );
			byte[ ] tmp = System.Text.Encoding.Default.GetBytes( _ip.Substring( 0, _ip.Length < 15 ? _ip.Length : 15 ) );
			for ( int i = 0; i < 15; i++ )
			{
				if ( i < tmp.Length )
					ret.Add( tmp[i] );
				else
					ret.Add( 0x20 );
			}
			ret.AddRange( BitConverter.GetBytes( _port ) );
			return GenerateBinary( ret.ToArray( ) );
		}

		/// <summary>
		/// 从 byte 数组中提取数据生成 cHandshake 对象
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		public new static cHandshake Parse( byte[ ] data )
		{

			HandshakeRequester _r = ( HandshakeRequester )data[6];
			string _ip = System.Text.Encoding.Default.GetString( data, 7, 15 ).Trim( );
			short _port = BitConverter.ToInt16( data, 22 );

			cHandshake ret = new cHandshake( _r, _ip, _port );
			ret._id = BitConverter.ToInt16( data, 0 );
			ret._number = BitConverter.ToInt32( data, 2 );

			return ret;
		}

		/// <summary>
		/// 构造器
		/// </summary>
		/// <param name="requester">请求方类型</param>
		/// <param name="ipaddress">IP 地址</param>
		/// <param name="port">通信端口</param>
		public cHandshake( HandshakeRequester requester, string ipaddress, short port )
		{
			this._id = 10;

			this._type = requester;
			this._ip = ipaddress;
			this._port = port;
		}

		#endregion

		#region Properties

		/// <summary>
		/// 握手发起方类别
		/// </summary>
		private HandshakeRequester _type;
		public HandshakeRequester Type { get { return _type; } }

		/// <summary>
		/// 握手方 IP 地址
		/// </summary>
		private string _ip;
		public string IPAddress { get { return _ip; } }

		/// <summary>
		/// 握手方通讯端口
		/// </summary>
		private short _port;
		public short Port { get { return _port; } }

		#endregion

	}

	public enum HandshakeRequester
	{
		/// <summary>
		/// 未知
		/// </summary>
		Unknown = 0x00,
		/// <summary>
		/// 中控
		/// </summary>
		CenterControll = 0x43,
		/// <summary>
		/// 服务器
		/// </summary>
		Server = 0x52,
		/// <summary>
		/// 监视器
		/// </summary>
		DebugMonitor = 0x68
	}
}
