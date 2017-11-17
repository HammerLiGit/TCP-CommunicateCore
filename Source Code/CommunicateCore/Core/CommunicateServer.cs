using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using CommunicateCore.Command;

namespace CommunicateCore.Core
{
	public class CommunicateServer
	{
		// TCP listener
		protected TcpListener _listener = null;
		// Listen scan thread
		protected Thread _scanThread = null;
		// 处理客户端连接事件调用的委托, 为了能够在客户端连接到服务器时，对设置的连接事件进行异步出发调用，设置此委托进行处理
		protected delegate void DoOnClientConnectedEventHandle( CommunicateClient client );

		protected delegate void DoOnClientDisconnectedEventHandle( CommunicateClient client );

		protected delegate void DoOnStartListenEventHandle( CommunicateServer server );

		protected delegate void DoOnStopListenEventHandle( CommunicateServer server );

		#region Constructor

		/// <summary>
		/// 基本构造器
		/// </summary>
		/// <param name="HostIP"></param>
		/// <param name="HostPort"></param>
		public CommunicateServer( string HostIP, int HostPort )
		{
			_localIP = HostIP;
			_localPort = HostPort;
		}

		#endregion

		#region Properties

		/// <summary>
		/// 监听状态
		/// </summary>
		public bool IsListening
		{
			get { return _listener != null; }
		}

		/// <summary>
		/// 本地通信 IP 地址，只能在未连接状态下修改
		/// </summary>
		protected string _localIP = null;
		public string LocalIP
		{
			get
			{
				if ( IsListening )
					return ( ( IPEndPoint )_listener.Server.LocalEndPoint ).Address.ToString( );
				else
					return _localIP;
			}
			set
			{
				if ( !IsListening ) _localIP = value;
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
				if ( IsListening )
					return ( ( IPEndPoint )_listener.Server.LocalEndPoint ).Port;
				else
					return _localPort;
			}
			set
			{
				if ( !IsListening ) _localPort = value;
			}
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

		#endregion

		#region Method



		/// <summary>
		/// 接收到命令，此处只处理 关闭 命令，业务命令由使用者处理
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="command"></param>
		/// <param name="e"></param>
		protected void OnReceiveCommand( TcpCore sender, CommandCore command, EventArgs e )
		{
			if ( command is cCloseConnection )
			{
				sender.Disconnect( );

				DoOnClientDisconnectedEventHandle dispRComm = new DoOnClientDisconnectedEventHandle( DoOnClientDisconnectedEvent );
				dispRComm.BeginInvoke( sender as CommunicateClient, null, null );

			}
		}

		/// <summary>
		/// 监听客户端连接的线程函数
		/// </summary>
		protected virtual void ListenProc( )
		{
			_listener = new TcpListener( IPAddress.Parse( _localIP ), Convert.ToInt32( _localPort ) );
			_listener.Start( );
			try
			{
				while ( true )
				{
					try
					{
						Thread.Sleep( 500 );

						if ( _listener.Pending( ) )
						{
							TcpClient tc = _listener.AcceptTcpClient( );
							CommunicateClient cc = new CommunicateClient( tc, OnReceiveCommand );
							cc.RunWithUI = this.RunWithUI;

							DoOnClientConnectedEventHandle dispRComm = new DoOnClientConnectedEventHandle( DoOnClientConnectedEvent );
							dispRComm.BeginInvoke( cc, null, null );
						}

					}
					catch ( ThreadInterruptedException ex )
					{
						break;
					}
					catch ( ThreadAbortException ex )
					{
						break;
					}
					catch ( Exception ex )
					{

					}
				}
			}
			finally
			{
				_listener.Stop( );
				_listener = null;
			}
		}

		/// <summary>
		/// 处理客户端连接事件函数
		/// </summary>
		/// <param name="client"></param>
		protected virtual void DoOnClientConnectedEvent( CommunicateClient client )
		{
			// Check OnReceiveCommand event define
			if ( this.OnClientConnected != null )
			{
				// Get all related method
				Delegate[ ] elist = this.OnClientConnected.GetInvocationList( );
				// Trigger every
				foreach ( ClientConnectedEventHandler ev in elist )
				{
					if ( this.RunWithUI && ( Application.OpenForms.Count > 0 ) && Application.OpenForms.Count > 0 )
						Application.OpenForms[0].Invoke( ev, new object[ ] { this, client, new EventArgs( ) } );
					else
						ev.BeginInvoke( this, client, new EventArgs( ), null, null );
				}
			}
		}
		protected virtual void DoOnClientDisconnectedEvent( CommunicateClient client )
		{
			// Check OnReceiveCommand event define
			if ( this.OnClientDisconnected != null )
			{
				// Get all related method
				Delegate[ ] elist = this.OnClientDisconnected.GetInvocationList( );
				// Trigger every
				foreach ( ClientDisconnectEventHandler ev in elist )
				{
					if ( this.RunWithUI && ( Application.OpenForms.Count > 0 ) && Application.OpenForms.Count > 0 )
						Application.OpenForms[0].Invoke( ev, new object[ ] { client, new EventArgs( ) } );
					else
						ev.BeginInvoke( client, new EventArgs( ), null, null );
				}
			}
		}

		protected virtual void DoOnStartListenedEvent( CommunicateServer sender )
		{
			// Check OnReceiveCommand event define
			if ( this.OnStartListened != null )
			{
				// Get all related method
				Delegate[ ] elist = this.OnStartListened.GetInvocationList( );
				// Trigger every
				foreach ( StartListenEventHandler ev in elist )
				{
					if ( this.RunWithUI && ( Application.OpenForms.Count > 0 ) && Application.OpenForms.Count > 0 )
						Application.OpenForms[0].Invoke( ev, new object[ ] { this } );
					else
						ev.BeginInvoke( this, null, null );
				}
			}
		}

		protected virtual void DoOnStopListened( CommunicateServer sender )
		{
			// Check OnReceiveCommand event define
			if ( this.OnStopListened != null )
			{
				// Get all related method
				Delegate[ ] elist = this.OnStopListened.GetInvocationList( );
				// Trigger every
				foreach ( StopListenEventHandler ev in elist )
				{
					if ( this.RunWithUI && ( Application.OpenForms.Count > 0 ) && Application.OpenForms.Count > 0 )
						Application.OpenForms[0].Invoke( ev, new object[ ] { this } );
					else
						ev.BeginInvoke( this, null, null );
				}
			}
		}

		/// <summary>
		/// 开始监听客户端连接
		/// </summary>
		public void Listen( )
		{
			_scanThread = new Thread( ListenProc );
			_scanThread.Start( );

			DoOnStartListenEventHandle dispRComm = new DoOnStartListenEventHandle( DoOnStartListenedEvent );
			dispRComm.BeginInvoke( this, null, null );
		}

		/// <summary>
		/// 结束监听客户端连接
		/// </summary>
		public void StopListen( )
		{
			if ( ( _scanThread != null ) && ( _scanThread.IsAlive ) )
			{
				_scanThread.Interrupt( );
				_scanThread.Join( );
			}
			_scanThread = null;

			DoOnStopListenEventHandle dispRComm = new DoOnStopListenEventHandle( DoOnStopListened );
			IAsyncResult ret = dispRComm.BeginInvoke( this, null, null );

		}

		#endregion

		#region Event

		// 客户端连接事件定义
		public delegate void ClientConnectedEventHandler( CommunicateServer server, CommunicateClient client, EventArgs e );
		// 客户端断开事件定义
		public delegate void ClientDisconnectEventHandler( CommunicateClient client, EventArgs e );
		// 服务器监听开始事件定义
		public delegate void StartListenEventHandler( CommunicateServer sender );
		// 服务器监听结束事件定义
		public delegate void StopListenEventHandler( CommunicateServer sender );


		public event ClientConnectedEventHandler OnClientConnected;

		public event ClientDisconnectEventHandler OnClientDisconnected;

		public event StartListenEventHandler OnStartListened;

		public event StopListenEventHandler OnStopListened;

		#endregion


	}
}
