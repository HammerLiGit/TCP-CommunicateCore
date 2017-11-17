using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CommunicateCore.Command
{
	public class cDebugMessage : CommandCore
	{
		#region Inherited

		/// <summary>
		/// Override ToString method
		/// </summary>
		/// <returns></returns>
		public override string ToString( )
		{
			return base.ToString( ) + " - cDebugMessage < " + Message + " >";
		}

		/// <summary>
		/// 重载的方法，将 cHandshake 命令转化为二进制数据
		/// </summary>
		/// <returns></returns>
		public override byte[ ] ToBinary( )
		{
			return GenerateBinary( System.Text.Encoding.ASCII.GetBytes( Message ) );
		}

		/// <summary>
		/// 从 byte 数组中提取数据生成 cHandshake 对象
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		public new static cDebugMessage Parse( byte[ ] data )
		{
			cDebugMessage ret = new cDebugMessage( )
			{
				_id = BitConverter.ToInt16( data, 0 ),
				_number = BitConverter.ToInt32( data, 2 )
			};

			ret.Message = System.Text.Encoding.ASCII.GetString( data, 6, data.Length - 6 );

			return ret;
		}

		/// <summary>
		/// 构造器
		/// </summary>
		/// <param name="requester">请求方类型</param>
		/// <param name="ipaddress">IP 地址</param>
		/// <param name="port">通信端口</param>
		public cDebugMessage( )
		{
			this._id = 40;
		}

		#endregion

		#region Properties

		public string Message { get; set; }

		#endregion
	}
}
