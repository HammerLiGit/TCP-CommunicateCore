using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommunicateCore.Command
{
	public class cHandshakeAnswer : CommandCore
	{
		#region Inherited

		/// <summary>
		/// Override ToString method
		/// </summary>
		/// <returns></returns>
		public override string ToString( )
		{
			return base.ToString( ) + " - Handshake Answer < time: " + Convert.ToDateTime( CommandTime ).ToString( "HH:mm:ss.fff" ) + " >";
		}


		/// <summary>
		/// 重载的方法，将 cHandshake 命令转化为二进制数据
		/// </summary>
		/// <returns></returns>
		public override byte[ ] ToBinary( )
		{
			return GenerateBinary( System.Text.Encoding.Default.GetBytes( DateTime.Now.ToString( "yyyy-MM-dd HH:mm:ss.fff" ) ) );
		}

		/// <summary>
		/// 从 byte 数组中提取数据生成 cHandshakeAnswer 对象
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		public new static cHandshakeAnswer Parse( byte[ ] data )
		{
			return new cHandshakeAnswer( )
			{
				_id = BitConverter.ToInt16( data, 0 ),
				_number = BitConverter.ToInt32( data, 2 ),
				_commandTime = Convert.ToDateTime( System.Text.Encoding.Default.GetString( data, 6, 23 ).Trim( ) )
			};
		}

		/// <summary>
		/// 构造器
		/// </summary>
		public cHandshakeAnswer( )
		{
			this._id = 11;

		}

		#endregion

		#region Properties

		protected DateTime? _commandTime = null;
		public DateTime? CommandTime
		{
			get { return _commandTime; }
		}
		#endregion

	}
	
}
