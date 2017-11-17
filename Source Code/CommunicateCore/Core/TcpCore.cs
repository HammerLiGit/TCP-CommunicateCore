using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using CommunicateCore.Command;
using CommunicateCore.Common;

namespace CommunicateCore.Core
{
	public class TcpCore : IDisposable
	{
		/// <summary>
		/// 获取本地所有可用 IP 地址（IPV4 格式）
		/// </summary>
		/// <returns></returns>
		public static string[ ] GetLocalIPAddresses( )
		{
			List<string> ret = new List<string>( );
			try
			{
				IPAddress[ ] ids = Dns.GetHostAddresses( Dns.GetHostName( ) );
				foreach ( IPAddress id in ids )
				{
					if ( id.AddressFamily == AddressFamily.InterNetwork )
					{
						ret.Add( id.ToString( ) );
					}
				}
			}
			catch
			{
				ret.Clear( );
			}

			return ret.ToArray( );
		}

		#region Declare

		bool disposed = false;

		// TCP socket core
		protected TcpClient _core = null;
		// Socket read & write stream
		protected NetworkStream _stream = null;
		// Flag for socket core created by server listener
		protected bool _connectByClient = false;
		// Receive thread
		protected Thread _receive = null;
		// Wait return command
		protected List<CommandCore> _cmdWait = null;
		protected List<CommandCore> _cmdResult = null;
		// 处理接收命令事件调用的委托, 为了能够在收到命令字时，对设置的接收事件进行异步出发调用，设置此委托进行处理
		protected delegate void DoReceiveCommandEventHandle( CommandCore command );
		// 接收命令事件定义
		public delegate void ReceiveCommandEventHandler( TcpCore sender, CommandCore command, EventArgs e );
		// 连接成功事件定义
		public delegate void ConnectedHandle( TcpCore sender, EventArgs e );
		// 连接断开事件定义
		public delegate void DisconnectedHandle( TCPDisconnectMode disconnectmode, EventArgs e );
		// 触发断开事件定义
		public delegate void DoDisconnectHandle( TCPDisconnectMode disconnectmode );
		// 触发连接成功事件定义
		public delegate void DoConnectHandle( );

		protected List<IAsyncResult> _invokeList = new List<IAsyncResult>( );

		protected object _lockObj = new object( );

		protected int _portReceiveBufferLen = 1024 * 1024;

		protected object _syncObj = new object( );

		#endregion

		#region Constructor

		/// <summary>
		/// 基本构造器
		/// </summary>
		/// <param name="HostIP">服务器 IP 地址</param>
		/// <param name="HostPort">服务器通信端口</param>
		/// <param name="LocalIP">本地 IP 地址</param>
		/// <param name="LocalPort">本地通信端口</param>
		public TcpCore( string HostIP, int HostPort, string LocalIP, int? LocalPort )
		{
			_RemoteIP = HostIP;
			_RemotePort = HostPort;
			if ( LocalIP == null )
			{
				string[ ] ips = TcpCore.GetLocalIPAddresses( );
				if ( ips.Length > 0 )
					_localIP = ips[0];
				else
					throw new Exception( "Havn't can use local IP address." );
			}
			else
			{
				_localIP = LocalIP;
			}
			_localPort = LocalPort == null ? 0 : LocalPort;

			_connectByClient = false;
		}

		/// <summary>>
		/// 简化构造器，本地 IP 地址使用默认地址列表第一个，端口由系统控制
		/// </summary>
		/// <param name="HostIP">>服务器 IP 地址</param>
		/// <param name="HostPort">服务器通信端口</param>
		public TcpCore( string HostIP, int HostPort )
			: this( HostIP, HostPort, null, null )
		{

		}

		/// <summary>
		/// 监听构造器，TCP 通信内核由 TcpListener 建立，用于服务器端相应客户端连接
		/// </summary>
		/// <param name="core"></param>
		public TcpCore( TcpClient core, ReceiveCommandEventHandler listenEvent )
		{
			_core = core;
			_stream = _core.GetStream( );
			_stream.ReadTimeout = 10;
			_stream.WriteTimeout = 100;
			_connectByClient = true;
			_connected = true;

			_RemoteIP = ( ( IPEndPoint )_core.Client.RemoteEndPoint ).Address.ToString( );
			_RemotePort = ( ( IPEndPoint )_core.Client.RemoteEndPoint ).Port;
			_localIP = ( ( IPEndPoint )_core.Client.LocalEndPoint ).Address.ToString( );
			_localPort = ( ( IPEndPoint )_core.Client.LocalEndPoint ).Port;

			this.OnReceiveCommand += listenEvent;

			StartListen( );
		}

		/// <summary>
		/// Release resource
		/// </summary>
		~TcpCore( )
		{
			Dispose( false );
		}

		#endregion

		#region Method

		/// <summary>
		/// 检查指定的命令包是不是同步发送等待的回复数据包
		/// </summary>
		/// <param name="command"></param>
		/// <returns></returns>
		protected virtual bool CheckWaitCommand( CommandCore command )
		{
			if ( _cmdWait != null )
			{
				foreach ( CommandCore item in _cmdWait )
				{
					if ( item.Number == command.Number )
					{
						_cmdResult.Add( command );
						_cmdWait.Remove( item );

						return true;
					}
				}
				return false;
			}

			return true;
		}

		/// <summary>
		/// 处理接收到的命令字
		/// </summary>
		/// <param name="command"></param>
		protected virtual void DoReceiveCommandEvent( CommandCore command )
		{
			if ( !CheckWaitCommand( command ) )
			{
				// Check OnReceiveCommand event define
				if ( this.OnReceiveCommand != null )
				{
					// Get all related method
					Delegate[ ] elist = this.OnReceiveCommand.GetInvocationList( );
					// Trigger every
					foreach ( ReceiveCommandEventHandler ev in elist )
					{
						if ( this.RunWithUI && ( Application.OpenForms.Count > 0 ) )
						{
							try
							{
								Application.OpenForms[0].Invoke( ev, new object[ ] { this, command, new EventArgs( ) } );
							}
							catch
							{
								lock ( _lockObj )
								{
									_invokeList.Add( ev.BeginInvoke( this, command, new EventArgs( ), null, null ) );
								}
							}
						}
						else
							lock ( _lockObj )
							{
								_invokeList.Add( ev.BeginInvoke( this, command, new EventArgs( ), null, null ) );
							}
					}
				}
			}
		}

		protected void CheckInvokeEvents( )
		{
			_invokeList.Clear( );
			//lock ( _lockObj )
			//{
			//    if ( _invokeList != null )
			//    {
			//        while ( _invokeList.Count > 0 )
			//        {
			//            if ( _invokeList[0].IsCompleted )
			//                _invokeList.RemoveAt( 0 );
			//        }
			//    }
			//}
		}

		/// <summary>
		/// 处理连接事件
		/// </summary>
		protected virtual void DoConnectedEvent( )
		{
			// Check OnReceiveCommand event define
			if ( this.OnConnected != null )
			{
				// Get all related method
				Delegate[ ] elist = this.OnConnected.GetInvocationList( );
				// Trigger every
				foreach ( ConnectedHandle ev in elist )
				{
					if ( this.RunWithUI && ( Application.OpenForms.Count > 0 ) )
					{
						try
						{
							Application.OpenForms[0].Invoke( ev, new object[ ] { this, new EventArgs( ) } );
						}
						catch
						{
							ev.BeginInvoke( this, new EventArgs( ), null, null );
						}
					}
					else
						ev.BeginInvoke( this, new EventArgs( ), null, null );
				}
			}
		}

		/// <summary>
		/// 处理连接断开事件
		/// </summary>
		protected virtual void DoDisconnectedEvent( TCPDisconnectMode mode )
		{
			// Check OnReceiveCommand event define
			if ( this.OnDisconnected != null )
			{
				// Get all related method
				Delegate[ ] elist = this.OnDisconnected.GetInvocationList( );
				// Trigger every
				foreach ( DisconnectedHandle ev in elist )
				{
					if ( this.RunWithUI && ( Application.OpenForms.Count > 0 ) )
					{
						try
						{
							Application.OpenForms[0].Invoke( ev, new object[ ] { mode, new EventArgs( ) } );
						}
						catch
						{
							ev.BeginInvoke( mode, new EventArgs( ), null, null );
						}
					}
					else
						ev.BeginInvoke( mode, new EventArgs( ), null, null );
				}
			}
		}

		/// <summary>
		/// 从接收的数据中分析数据包
		/// </summary>
		/// <param name="buff"></param>
		protected virtual void AnalysisReceivedData( List<byte> buff )
		{
			if ( buff.Count < 24 ) return;

			int idx = 0;
			byte[ ] src = buff.ToArray( );
			while ( idx < buff.Count - 5 ) // Dec a packet head len
			{
				try
				{
					// 1 Find packet head
					if ( System.Text.Encoding.Default.GetString( buff.GetRange( idx, 5 ).ToArray( ), 0, 5 ) == "QITPS" )
					{
						// 2 Get packet length
						if ( buff.Count - idx - 5 < 2 ) return;
						int pLen = BitConverter.ToInt32( buff.GetRange( idx + 5, 4 ).ToArray( ), 0 );

						// 3 Get packet content
						if ( buff.Count < idx + pLen ) return;
						byte[ ] content = buff.GetRange( idx + 9, pLen - 18 ).ToArray( );// new byte[pLen - 18];

						// 4 Get CRC32
						byte[ ] crc = buff.GetRange( idx + ( pLen - 9 ), 4 ).ToArray( );

						// 5 Get packet end
						string pEnd = System.Text.Encoding.Default.GetString( buff.GetRange( idx + ( pLen - 5 ), 5 ).ToArray( ), 0, 5 );

						// Verify
						if ( pEnd != "SPTIQ" ) break; // Head
						if ( string.Compare( CRC32.BuildCRC32( content ).ToString( ), BitConverter.ToUInt32( crc, 0 ).ToString( ) ) != 0 ) break; // CRC32

						// Analysis command and trigger event
						CommandCore cmd = CommandCore.Parse( content );
						if ( cmd != null )
						{
							// Record receive time
							_lastReceive = DateTime.Now;

							// Do event
							DoReceiveCommandEventHandle dispRComm = new DoReceiveCommandEventHandle( DoReceiveCommandEvent );
							lock ( _lockObj )
							{
								_invokeList.Add( dispRComm.BeginInvoke( cmd, null, null ) );
							}
						}

						// remove
						buff.RemoveRange( 0, idx + pLen );
					}
					else
						idx++;

				}
				catch ( Exception ex )
				{

				}
				finally
				{

				}
			}
		}

		/// <summary>
		/// 扫描接收线程方法，用于监听端口并接收数据
		/// </summary>
		protected virtual void ListenMethod( )
		{
			if ( !Connected ) return;

			List<byte> buff = new List<byte>( );
			try
			{
				while ( true )
				{
					lock ( _syncObj )
					{
						byte[ ] tmp = new byte[_portReceiveBufferLen];
						try
						{
							// Sleep a receive interval
							Thread.Sleep( _receiveInterval );

							// Check socket core
							if ( !Connected ) throw new Exception( );

							// Receive data
							int len = _stream.Read( tmp, 0, _portReceiveBufferLen );

							if ( len > 0 )
							{
								// Add new receive data to buff
								for ( int i = 0; i < len; i++ )
									buff.Add( tmp[i] );

								// Analysis data
								AnalysisReceivedData( buff );
							}
						}
						catch ( ThreadInterruptedException ex )
						{
							CheckInvokeEvents( );
							return;
						}
						catch ( ThreadAbortException ex )
						{
							CheckInvokeEvents( );

							return;
						}
						catch ( System.IO.IOException ex )
						{

						}
						catch ( Exception ex )
						{
							CheckInvokeEvents( );

							DoDisconnectHandle dist = new DoDisconnectHandle( this.Disconnect );
							dist.BeginInvoke( TCPDisconnectMode.Ppassive, null, null );

							return;
						}
						finally
						{
							tmp = null;
						}
					}
				}
			}
			finally
			{
				buff.Clear( );
				buff = null;
				//增加远程断开连接事件
			}
		}

		/// <summary>
		/// 启动接收监听线程
		/// </summary>
		protected virtual void StartListen( )
		{
			if ( _receive != null ) return;

			_cmdWait = new List<CommandCore>( );
			_cmdResult = new List<CommandCore>( );

			_receive = new Thread( ListenMethod );
			_receive.Name = "TcpCore listen thread";
			_receive.IsBackground = true;
			_receive.Start( );
		}

		/// <summary>
		/// 停止接收监听线程
		/// </summary>
		protected virtual void StopListen( )
		{
			try
			{
				if ( ( _receive != null ) && ( _receive.IsAlive ) )
				{
					_receive.Interrupt( );
					_receive.Join( );
				}
				_receive = null;

				_cmdResult.Clear( );
				_cmdWait.Clear( );

				_cmdResult = null;
				_cmdWait = null;
			}
			catch ( Exception ex )
			{

			}
		}

		/// <summary>
		/// 连接到服务器
		/// </summary>
		public virtual void Connect( )
		{
			// Build TCP socket core
			if ( !_connectByClient )
			{
				string ip = "";
				int port = -1;

				// Get local ip
				if ( _localIP == null )
				{
					string[ ] ips = TcpCore.GetLocalIPAddresses( );
					if ( ips.Length <= 0 )
						throw new Exception( "Cann't get communicate ip address." );
					else
						ip = ips[0];
				}
				else
					ip = _localIP;

				// Get local port
				port = ( _localPort == null ? 0 : ( int )_localPort );

				// Connect to server
				IPEndPoint iep = new IPEndPoint( IPAddress.Parse( ip ), port );
				_core = new TcpClient( iep );
				_core.ReceiveTimeout = _receiveInterval;
			}
			else if ( !_core.Connected )
			{
				throw new Exception( "Remote connect already disconnect." );
			}

			// Connect to server
			try
			{
				_core.Connect( new IPEndPoint( IPAddress.Parse( _RemoteIP ), _RemotePort ) );
				_connected = true;
			}
			catch ( Exception ex )
			{
				_connected = false;
				_core.Close( );
				_core = null;
				throw new Exception( ex.Message );
			}

			// Open read & write stream channel
			_stream = _core.GetStream( );
			_stream.ReadTimeout = 10;
			_stream.WriteTimeout = 100;


			Thread.Sleep( 500 );

			// Start recive thread
			StartListen( );

			// Do Event
			DoConnectHandle dd = new DoConnectHandle( DoConnectedEvent );
			dd.BeginInvoke( null, null );
		}

		/// <summary>
		/// 对外公布的断开与服务器连接的方法
		/// </summary>
		public virtual void Disconnect( )
		{
			Disconnect( TCPDisconnectMode.Active );
		}

		/// <summary>
		/// 断开与服务器连接的方法
		/// </summary>
		/// <param name="mode"></param>
		protected virtual void Disconnect( TCPDisconnectMode mode )
		{
			// Check connection is connect by remote client
			if ( !_connectByClient && ( mode == TCPDisconnectMode.Active ) )
			{
				// Before close local connect, send close command to server close TCP port.
				SendCommand( new cCloseConnection( ) );
				Thread.Sleep( 1000 );
			}

			// Stop recieve thread
			StopListen( );

			// Close read & write stream channel
			if ( _stream != null )
			{
				_stream.Close( );
				_stream.Dispose( );
			}

			// Close TCP port
			if ( _core != null )
			{
				_core.Close( );
				_core = null;
			}

			_connected = false;

			// Do event
			DoDisconnectHandle dd = new DoDisconnectHandle( DoDisconnectedEvent );
			dd.BeginInvoke( mode, null, null );
		}

		/// <summary>
		/// 发送一个指定的命令
		/// </summary>
		/// <param name="command"></param>
		public virtual void SendCommand( CommandCore command )
		{
			if ( !Connected || !CanSend ) return;

			// Send specify communicate command to remote

			// Get ready
			byte[ ] _body = command.ToBinary( );
			byte[ ] _packetHeader = System.Text.Encoding.Default.GetBytes( "QITPS" );
			byte[ ] _packetEnd = System.Text.Encoding.Default.GetBytes( "SPTIQ" );
			byte[ ] _proof = BitConverter.GetBytes( CRC32.BuildCRC32( _body ) );
			byte[ ] _packetLen = BitConverter.GetBytes( _body.Length + 18 );

			// Build packet
			List<byte> tmp = new List<byte>( );
			tmp.AddRange( _packetHeader );
			tmp.AddRange( _packetLen );
			tmp.AddRange( _body );
			tmp.AddRange( _proof );
			tmp.AddRange( _packetEnd );

			try
			{
				// Send command
				_stream.Write( tmp.ToArray( ), 0, tmp.Count );
			}
			catch ( Exception ex )
			{
				_connected = false;
				//this.Disconnect( TCPDisconnectMode.Ppassive );
			}

			// Record last send time
			_lastSend = DateTime.Now;
		}

		/// <summary>
		/// 同步发送命令，等待返回信息
		/// </summary>
		/// <param name="command"></param>
		/// <param name="result"></param>
		public virtual void SendCommand( CommandCore command, out CommandCore result )
		{
			if ( !Connected || !CanSend )
			{
				result = null;
				return;
			}

			if ( _cmdWait == null )
			{
				// Send command
				result = null;
				this.SendCommand( command );
				return;
			}

			// Wait result
			result = null;
			_cmdWait.Add( command );
			DateTime st = DateTime.Now;

			// Send command
			this.SendCommand( command );

			while ( true )
			{
				Application.DoEvents( );// Thread.Sleep( 1 );

				if ( ( _cmdWait != null ) && ( _cmdWait.IndexOf( command ) < 0 ) )
				{
					foreach ( CommandCore item in _cmdResult )
					{
						if ( item.Number == command.Number )
						{
							result = item;
							_cmdResult.Remove( item );
							break;
						}
					}

					if ( result != null )
						break;
				}

				TimeSpan tmp = DateTime.Now - st;
				if ( tmp.TotalMilliseconds > _syncSendCommandTimeOut )
				{
					result = null;
					break;
				}
			}
		}

		/// <summary>
		/// 释放资源
		/// </summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose( bool disposing )
		{
			if ( disposed )
				return;

			if ( disposing )
			{
				if ( ( _receive != null ) && ( _receive.ThreadState == ThreadState.Running ) )
				{
					this.StopListen( );

				}

				if ( _stream != null )
				{
					_stream.Close( );
					_stream.Dispose( );
					_stream = null;
				}

				if ( _core != null )
				{
					_core.Close( );
					_core = null;
				}
			}

			// Free any unmanaged objects here. 
			//
			disposed = true;
		}

		public void Dispose( )
		{
			Dispose( true );
			GC.SuppressFinalize( this );
		}

		#endregion

		#region Event

		/// <summary>
		/// 接收到命令字时触发的事件
		/// 注意，此事件中如果需要对界面操作，必须将 RunWithUI 设置为 True
		/// </summary>
		public event ReceiveCommandEventHandler OnReceiveCommand;

		/// <summary>
		/// 连接成功事件
		/// </summary>
		public event ConnectedHandle OnConnected;

		/// <summary>
		/// 连接断开事件
		/// </summary>
		public event DisconnectedHandle OnDisconnected;

		#endregion

		#region Properties

		/// <summary>
		/// 检查是否处于连接状态
		/// </summary>
		private bool _connected = false;
		public bool Connected
		{
			get { return ( _core != null ) && ( _connected ); } // && ( _core.Connected )
		}

		/// <summary>
		/// 检查是否可以发送数据
		/// </summary>
		public bool CanSend
		{
			get { return Connected && ( _stream != null ) && _stream.CanWrite; }
		}

		/// <summary>
		/// 服务器端通信 IP 地址，只能在未连接状态下修改
		/// </summary>
		protected string _RemoteIP = String.Empty;
		public string RemoteIP
		{
			get
			{
				if ( Connected )
					return ( ( IPEndPoint )_core.Client.RemoteEndPoint ).Address.ToString( );
				else
					return _RemoteIP;
			}
			set
			{
				if ( !Connected && !_connectByClient ) _RemoteIP = value;
			}
		}

		/// <summary>
		/// 服务器端通信端口，只能在未连接状态下修改
		/// </summary>
		protected int _RemotePort = -1;
		public int RemotePort
		{
			get
			{
				if ( Connected )
					return ( ( IPEndPoint )_core.Client.RemoteEndPoint ).Port;
				else
					return _RemotePort;
			}
			set
			{
				if ( !Connected && !_connectByClient ) _RemotePort = value;
			}
		}

		/// <summary>
		/// 本地通信 IP 地址，只能在未连接状态下修改
		/// </summary>
		protected string _localIP = null;
		public string LocalIP
		{
			get
			{
				if ( Connected )
					return ( ( IPEndPoint )_core.Client.LocalEndPoint ).Address.ToString( );
				else
					return _localIP;
			}
			set
			{
				if ( !Connected && !_connectByClient ) _localIP = value;
			}
		}

		/// <summary>
		/// 本地通信端口，只能在未连接状态下修改
		/// </summary>
		protected int? _localPort = null;
		public int? LocalPort
		{
			get
			{
				if ( Connected )
					return ( ( IPEndPoint )_core.Client.LocalEndPoint ).Port;
				else
					return _localPort;
			}
			set
			{
				if ( !Connected && !_connectByClient ) _localPort = value;
			}
		}

		/// <summary>
		/// 接收数据的扫描间隔
		/// </summary>
		protected Int32 _receiveInterval = 300;
		public Int32 ReceiveInterval
		{
			get { return _receiveInterval; }
			set { _receiveInterval = value; }
		}

		/// <summary>
		/// 同步发送命令的等待超时
		/// </summary>
		protected Int32 _syncSendCommandTimeOut = 30000;
		public Int32 SyncSendCommandTimeOut
		{
			get { return _syncSendCommandTimeOut; }
			set { _syncSendCommandTimeOut = value; }
		}

		/// <summary>
		/// 是否从 UI 程序运行，如果此参数设置为 False，则在 OnReceiveCommand 事件中不能操作 UI 上的控件等信息
		/// </summary>
		protected bool _runWithUI = false;
		public bool RunWithUI
		{
			get { return _runWithUI; }
			set { _runWithUI = value; }
		}

		/// <summary>
		/// 最后一次发送命令的时间
		/// </summary>
		protected DateTime _lastSend;
		public DateTime LastSend
		{
			get { return _lastSend; }
		}

		/// <summary>
		/// 接收最后一个命令的时间
		/// </summary>
		protected DateTime _lastReceive;
		public DateTime LastReceive
		{
			get { return _lastReceive; }
		}

		public int PortReceiveBufferLen
		{
			get { return _portReceiveBufferLen; }
			set
			{
				lock ( _syncObj )
				{
					_portReceiveBufferLen = value;
				}
			}
		}

		#endregion

	}



	public enum TCPDisconnectMode
	{
		/// <summary>
		/// 主动断开
		/// </summary>
		Active,
		/// <summary>
		/// 被动断开
		/// </summary>
		Ppassive
	}

}
