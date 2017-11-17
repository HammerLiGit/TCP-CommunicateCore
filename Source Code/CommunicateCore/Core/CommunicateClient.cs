using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Threading;
using CommunicateCore.Command;

namespace CommunicateCore.Core
{
	public class CommunicateClient : TcpCore
	{

		protected Thread _clientHeartbeat = null;
		//protected bool _handshakeSucceed = false;

		#region Constructor

		/// <summary>
		/// 基本构造器
		/// </summary>
		/// <param name="HostIP">服务器 IP 地址</param>
		/// <param name="HostPort">服务器通信端口</param>
		/// <param name="LocalIP">本地 IP 地址</param>
		/// <param name="LocalPort">本地通信端口</param>
		public CommunicateClient( string HostIP, int HostPort, string LocalIP, int? LocalPort, HandshakeRequester ClientType )
			: base( HostIP, HostPort, LocalIP, LocalPort )
		{
			_clientType = ClientType;
			this.OnReceiveCommand += OnReceiveCommandEvent;
		}

		/// <summary>>
		/// 简化构造器，本地 IP 地址使用默认地址列表第一个，端口由系统控制
		/// </summary>
		/// <param name="HostIP">>服务器 IP 地址</param>
		/// <param name="HostPort">服务器通信端口</param>
		public CommunicateClient( string HostIP, int HostPort, HandshakeRequester ClientType )
			: base( HostIP, HostPort )
		{
			_clientType = ClientType;
			this.OnReceiveCommand += OnReceiveCommandEvent;
		}

		/// <summary>
		/// 监听构造器，TCP 通信内核由 TcpListener 建立，用于服务器端相应客户端连接
		/// </summary>
		/// <param name="core"></param>
		public CommunicateClient( TcpClient core, ReceiveCommandEventHandler listenEvent )
			: base( core, listenEvent )
		{
			_clientType = HandshakeRequester.Unknown;
			_enabledHeartbeat = false;
			this.OnReceiveCommand += OnReceiveCommandEvent;
		}

		#endregion

		#region Method
		public override string ToString( )
		{
			return "C: " + this.LocalIP.ToString( ) + ":" + this.LocalPort.ToString( ) + " - S: " + this.RemoteIP.ToString( ) + ":" + this.RemotePort.ToString( );
		}

		/// <summary>
		/// 连接到服务器
		/// </summary>
		public new cHandshakeAnswer Connect( )
		{
			//_handshakeSucceed = false;
			base.Connect( );

			if ( this.Connected )
			{
				// -- Handshake
				//CommandCore ret;
				//this.SendCommand( new cHandshake( _clientType, _localIP, Convert.ToInt16( _localPort ) ), out ret );

				//if ( ret == null )
				//{
				//	Disconnect( TCPDisconnectMode.Active );
				//	base.Disconnect( );
				//	throw new Exception( "Handshake to server failed." );
				//}

				//_handshakeSucceed = true;
				//_lastHandshake = DateTime.Now;

				// -- Heartbeat
				if ( _enabledHeartbeat )
					StartHeartbeat( );

				//DoConnectedEvent( );

				return null; // ret as cHandshakeAnswer;
			}
			else
				return null;
		}

		protected override void DoConnectedEvent( )
		{
			//if ( _handshakeSucceed )
			//{
			base.DoConnectedEvent( );
			//}
		}

		protected override void DoDisconnectedEvent( TCPDisconnectMode mode )
		{
			//if ( _handshakeSucceed )
			//{
			base.DoDisconnectedEvent( mode );
			//}

		}

		/// <summary>
		/// 从服务器断开连接
		/// </summary>
		public override void Disconnect( )
		{
			if ( _clientHeartbeat != null )
				StopHeartbeat( );

			base.Disconnect( );

		}

		protected override void Disconnect( TCPDisconnectMode mode )
		{
			if ( _clientHeartbeat != null )
				StopHeartbeat( );

			base.Disconnect( mode );
		}

		/// <summary>
		/// 心跳线程函数
		/// </summary>
		protected virtual void HeartbeatProc( )
		{
			while ( true )
			{
				try
				{
					// Sleep half of min heartbeat interval
					Thread.Sleep( 500 );

					TimeSpan ts = DateTime.Now - this._lastSend;
					if ( ts.TotalSeconds > this._heartbeatInterval )
					{
						Console.WriteLine( DateTime.Now.ToString( ) + " send heartbeat." );
						SendCommand( new cHeartbeat( ) );
					}

				}
				catch ( ThreadInterruptedException ex )
				{
					return;
				}
				catch ( ThreadAbortException ex )
				{
					return;
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
		/// 开始发送心跳
		/// </summary>
		protected virtual void StartHeartbeat( )
		{
			if ( Connected )
			{
				_clientHeartbeat = new Thread( HeartbeatProc );
				_clientHeartbeat.IsBackground = true;
				_clientHeartbeat.Start( );
			}
		}

		/// <summary>
		/// 结束发送心跳
		/// </summary>
		protected virtual void StopHeartbeat( )
		{
			if ( _clientHeartbeat != null )
			{
				_clientHeartbeat.Interrupt( );
				_clientHeartbeat.Join( );
				_clientHeartbeat = null;
			}
		}

		#endregion

		#region Properties

		/// <summary>
		/// 是否打开心跳功能，默认是打开的
		/// </summary>
		protected bool _enabledHeartbeat = true;
		public bool EnabledHeartbeat
		{
			get { return _enabledHeartbeat; }
			set
			{
				_enabledHeartbeat = value;

				if ( value && base.Connected )
					StartHeartbeat( );

				if ( !value && ( _clientHeartbeat != null ) )
					StopHeartbeat( );
			}
		}

		/// <summary>
		/// 心跳的间隔
		/// </summary>
		protected Int32 _heartbeatInterval = 5;
		public Int32 HeartbeatInterval
		{
			get { return _heartbeatInterval; }
			set { _heartbeatInterval = value; }
		}

		/// <summary>
		/// 客户端类型
		/// </summary>
		protected HandshakeRequester _clientType = HandshakeRequester.Unknown;
		public HandshakeRequester ClientType
		{
			get { return _clientType; }
		}

		/// <summary>
		/// 最近一次握手时间
		/// </summary>
		protected DateTime _lastHandshake;
		public DateTime LastHandshake
		{
			get { return _lastHandshake; }
		}

		/// <summary>
		/// 最近一次的心跳时间
		/// </summary>
		protected DateTime _lastHeartbeat;
		public DateTime LastHeartbeat
		{
			get { return _lastHeartbeat; }
		}
		///// <summary>
		///// 客户端状态
		///// </summary>
		//protected ClientState _state;
		//public ClientState State
		//{
		//    get { return _state; }
		//    set { _state = value; }
		//}
		#endregion

		#region Event

		protected void OnReceiveCommandEvent( TcpCore sender, CommandCore command, EventArgs e )
		{
			if ( ( command is cHandshake ) && ( _connectByClient ) )
			{
				this._clientType = ( command as cHandshake ).Type;
				_lastHandshake = DateTime.Now;
			}
			else if ( ( command is cHeartbeat ) && ( _connectByClient ) )
			{
				_lastHeartbeat = DateTime.Now;
			}
			else if ( ( command is cDebugMessage ) && ( _connectByClient ) )
			{
				_lastHeartbeat = DateTime.Now;
			}

		}

		#endregion

		#region Commands

		public bool GetConnectAddressInfo( out string DBConnect, out string WebServerConnect, out string CloudServerConnect )
		{
			try
			{
				CommandCore ret = null;
				this.SendCommand( new cHandshake( this._clientType, this.LocalIP, Convert.ToInt16( this.LocalPort ) ), out ret );
				DBConnect = "";
				WebServerConnect = "";
				CloudServerConnect = "";

				return true;
			}
			catch
			{
				DBConnect = string.Empty;
				WebServerConnect = string.Empty;
				CloudServerConnect = string.Empty;
				return false;
			}
		}

		#endregion

	}
}
