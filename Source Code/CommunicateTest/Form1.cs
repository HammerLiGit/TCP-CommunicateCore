using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using CommunicateCore;
using CommunicateCore.Core;
using CommunicateCore.Command;
using CommunicateCore.Common;
using System.IO.Ports;
using System.Xml;

namespace CommunicateTest
{
	public partial class Form1 : Form
	{

		Random r = new Random( );

		/// <summary>
		/// 程序结束时需要释放资源
		/// 分别参考服务器端（中控）和客户端（业务服务器）代码
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Form1_FormClosing( object sender, FormClosingEventArgs e )
		{
			#region Client

			if ( ( tc != null ) && ( tc.Connected ) )
			{
				tc.OnReceiveCommand -= this.OnReceiveCommandEvent;
				tc.OnReceiveCommand -= this.OnClientReceiveCommandEvent;
				tc.Disconnect( );
				while ( tc.Connected )
				{
					Application.DoEvents( );
				}
			}

			#endregion

			#region Server

			if ( ( ts != null ) && ( ts.IsListening ) )
			{
				foreach ( CommunicateClient item in _clients )
				{
					item.OnReceiveCommand -= this.OnReceiveCommandEventS;
					item.Disconnect( );
				}

				ts.OnClientConnected -= this.OnClientConnected;
				ts.OnClientDisconnected -= this.OnClientDisconnected;
				ts.StopListen( );

			}

			#endregion
		}

		private void ShowMessage( TcpCore sender, CommandCore command, ListBox MsgBox )
		{
			MsgBox.BeginUpdate( );
			try
			{
				int idx = 0;
				MsgBox.Items.Insert( 0, "[ " + sender.RemoteIP + ":" + sender.RemotePort.ToString( ) + " ] send a command: " + command.ToString( ) );

				if ( chkShoBin.Checked )
				{
					idx++;
					byte[ ] tmp = command.ToBinary( );
					string ss = "    " + " 00d: "; ;
					for ( int i = 0; i < tmp.Length; i++ )
					{
						ss = ss + ( ss.Trim( ) == "" ? "" : " " ) + string.Format( "0x{0,2:X2}", tmp[i] );
						if ( ( i != 0 ) && ( ( i + 1 ) % 10 == 0 ) )
						{
							MsgBox.Items.Insert( idx, ss );
							idx++;
							ss = "    " + " " + Convert.ToInt32( ( i + 1 ) / 10 * 10 ).ToString( ) + "d: ";
						}
					}
					if ( ss.Trim( ) != "" )
					{
						MsgBox.Items.Insert( idx, ss );
						idx++;
						ss = "    ";
					}
				}
				MsgBox.Items.Insert( idx, "" );
			}
			finally
			{
				MsgBox.EndUpdate( );
			}
		}

		#region Server 端事件

		// 服务器端监听内核
		CommunicateServer ts = null;
		// 服务器端在线客户端列表
		List<CommunicateClient> _clients = null;

		/// <summary>
		/// 监控调试用，当服务器端收到信息时将其显示在监视窗口
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="command"></param>
		/// <param name="e"></param>
		private void OnReceiveCommandEventS( TcpCore sender, CommandCore command, EventArgs e )
		{
			ShowMessage( sender, command, listBox2 );
		}



		/// <summary>
		/// 服务器收到命令事件
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="command"></param>
		/// <param name="e"></param>
		private void OnServerReceiveCommandEvent( TcpCore sender, CommandCore command, EventArgs e )
		{
			if ( command is cHandshake )
			{
				#region Handshake

				cHandshakeAnswer ret = new cHandshakeAnswer( );
				ret.Number = command.Number;
				sender.SendCommand( ret );

				#endregion
			}
			else if ( command is cDebugMessage )
			{
				#region cDebugMessage

				cDebugMessage ret = new cDebugMessage( ) { Message = "Debug Text Answer from Server : " + textBox2.Text };
				sender.SendCommand( ret );

				#endregion
			}

		}

		/// <summary>
		/// 服务器端处理客户端连接事件
		/// 当服务器端监测到一个客户端成功连接后，进行相应的处理
		/// </summary>
		/// <param name="server"></param>
		/// <param name="client"></param>
		/// <param name="e"></param>
		public void OnClientConnected( CommunicateServer server, CommunicateClient client, EventArgs e )
		{
			client.OnReceiveCommand += OnReceiveCommandEventS;
			client.OnReceiveCommand += OnServerReceiveCommandEvent;


			//
			if ( textBox2.Text.Trim( ) != "" )
			{
				cDebugMessage dm = new cDebugMessage( ) { Message = textBox2.Text.Trim( ) };
				SendCommand_C2S( client, dm, chkSync.Checked );
			}


			_clients.Add( client );
		}

		public void OnClientDisconnected( CommunicateClient client, EventArgs e )
		{
			_clients.Remove( client );
		}

		public void OnStartedListen( CommunicateServer sender )
		{
			button5.Enabled = false;
			button6.Enabled = true;
			button7.Enabled = true;
		}

		public void OnStopedListen( CommunicateServer sender )
		{
			button5.Enabled = true;
			button6.Enabled = false;
			button7.Enabled = false;
		}

		/// <summary>
		/// 读取配置信息
		/// </summary>
		public void LoadSettings( )
		{
			try
			{
				// 建立客户端缓存列表
				_clients = new List<CommunicateClient>( );
				// 建立服务器端监听对象
				ts = new CommunicateServer( cmbServerLocal.Text, Convert.ToInt32( numServerLocal.Value ) );
				// 设置客户端连接响应事件
				ts.OnClientConnected += this.OnClientConnected;
				// 设置客户端断开连接响应事件
				ts.OnClientDisconnected += this.OnClientDisconnected;
				// 设置监听开始响应事件
				ts.OnStartListened += this.OnStartedListen;
				// 设置监听结束响应事件
				ts.OnStopListened += this.OnStopedListen;
				// 设置服务器内核是否需要界面运行
				ts.RunWithUI = true;

			}
			catch ( Exception ex )
			{
				throw new Exception( "配置通讯服务器失败！ 详细信息：" + ex.Message );
			}

		}

		/// <summary>
		/// 初始化设备，打开端口等
		/// </summary>
		public void Initialization( )
		{

		}

		/// <summary>
		/// 关闭并回收资源
		/// </summary>
		public void Finalization( )
		{

		}

		private void ServerListen_Click( object sender, EventArgs e )
		{
			// 检查监听状态
			if ( ts != null )
			{
				MessageBox.Show( "Server alreay open.", "Message", MessageBoxButtons.OK, MessageBoxIcon.Information );
				return;
			}

			Check( );

			try
			{
				// 加载设备信息
				this.LoadSettings( );

				// 初始化设备信息
				this.Initialization( );

				// 打开服务器端监听
				ts.Listen( );
			}
			catch ( Exception ex )
			{
				Finalization( );

				ts = null;

				MessageBox.Show( ex.Message );
			}
		}

		private void ServerStopListen_Click( object sender, EventArgs e )
		{
			try
			{
				if ( ( ts != null ) && ( ts.IsListening ) )
				{
					ts.StopListen( );
				}
				ts = null;

				foreach ( CommunicateClient item in _clients )
				{
					item.Disconnect( );
				}
				_clients.Clear( );
				_clients = null;

				// 关闭设备
				this.Finalization( );
			}
			catch
			{

			}
		}

		private void ClearClients_Click( object sender, EventArgs e )
		{
			int idx = 0;
			while ( idx < _clients.Count )
			{
				if ( !_clients[idx].Connected )
				{
					_clients.RemoveAt( idx );
					continue;
				}

				TimeSpan ts = DateTime.Now - _clients[idx].LastReceive;
				if ( ts.TotalSeconds > 3 )
				{
					_clients[idx].Disconnect( );
					_clients.RemoveAt( idx );
				}
				else
				{
					idx++;
				}
			}

		}

		private void RefreshClient_Click( object sender, EventArgs e )
		{

			this.listBox3.BeginUpdate( );
			try
			{
				this.listBox3.Items.Clear( );
				if ( _clients != null )
				{
					foreach ( CommunicateClient item in _clients )
					{
						listBox3.Items.Add( item.RemoteIP.Trim( ) + " : " + item.RemotePort + " --> " + ( item.ClientType == HandshakeRequester.Server ? "Inforamtion server" : "CenterControll" ) );
						listBox3.Items.Add( "    Last Handshake - " + item.LastHandshake.ToLongTimeString( ) );
						listBox3.Items.Add( "    Last Heartbeat - " + item.LastHeartbeat.ToLongTimeString( ) );
					}
				}
			}
			finally
			{
				listBox3.EndUpdate( );
			}
		}

		#endregion

		#region Client 端事件

		// 客户端通讯内核
		CommunicateClient tc = null;

		/// <summary>
		/// 发送一个消息从客户端到服务器端
		/// </summary>
		/// <param name="CientCore">发送命令用的通讯内核</param>
		/// <param name="command">要发送的命令</param>
		/// <param name="Sync">同步发送标志</param>
		private CommandCore SendCommand_C2S( CommunicateClient CientCore, CommandCore command, bool Sync )
		{
			if ( CientCore != null )
			{
				if ( Sync )
				{
					// 同步发送并接收返回结果
					CommandCore ret;
					CientCore.SendCommand( command, out ret );

					// Debug ============
					if ( ret != null ) MessageBox.Show( "Receive answer.  " + " command: " + ret.ToString( ), "Messasge", MessageBoxButtons.OK, MessageBoxIcon.Information );
					// ==================

					return ret;
				}
				else
				{
					// 异步发送，不等待返回结果
					CientCore.SendCommand( command );
					return null;
				}
			}

			return null;
		}

		/// <summary>
		/// 监控调试用，当客户端收到信息时将其显示在监视窗口
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="command"></param>
		/// <param name="e"></param>
		private void OnReceiveCommandEvent( TcpCore sender, CommandCore command, EventArgs e )
		{
			if ( ( command is cDebugMessage ) )
				ShowMessage( sender, command, listBox1 );
		}

		/// <summary>
		/// 客户端端响应消息命令的事件
		/// 当收到消息命令时做出相应的动作
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="command"></param>
		/// <param name="e"></param>
		private void OnClientReceiveCommandEvent( TcpCore sender, CommandCore command, EventArgs e )
		{
			if ( command is cDebugMessage )
			{
				//listBox1.Items.Insert( 0, "Receive debug message : " );
				//listBox1.Items.Insert( 1, "   [" + ( ( command as cDebugMessage ).Message + "]" ) );
				//listBox1.Items.Insert( 0, "" );
			}
			//else if ( command is XXXXXXXXXXXX )
			//{
			//	Do it ...
			//}
			// ....
		}

		/// <summary>
		/// 连接成功事件
		/// 当内核连接成功后悔触发该事件
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		public void OnConnected( TcpCore sender, EventArgs e )
		{
			button1.Enabled = false;
			button2.Enabled = true;

			MessageBox.Show( "Connect to server succeed!" );
		}

		/// <summary>
		/// 断开连接事件
		/// 当内核连接断开后会触发该事件
		/// </summary>
		/// <param name="mode"></param>
		/// <param name="e"></param>
		public void OnDisconnected( TCPDisconnectMode mode, EventArgs e )
		{
			button1.Enabled = true;
			button2.Enabled = false;

			if ( mode == TCPDisconnectMode.Active )
				MessageBox.Show( "Already disconnected from server." );
			else
				MessageBox.Show( "Connection already disconnected by remote server." );
		}

		private void ClientConnectToServer_Click( object sender, EventArgs e )
		{
			// 建立客户端到服务器端的内核通讯连接

			// 检查条件
			Check( );

			// 检查内核
			if ( tc == null )
			{
				// 建立通讯内核
				tc = new CommunicateClient(
					cmbClientRemote.Text.Trim( ), // Host IP
					Convert.ToInt32( numRemote.Value ), // Host Port
					cmbLocal.Text, // Local IP
					Convert.ToInt32( numLocal.Value ), // Local Port
					chkDebug.Checked ? HandshakeRequester.DebugMonitor : HandshakeRequester.Server // Core type
				);
				// 设置监视事件
				tc.OnReceiveCommand += this.OnReceiveCommandEvent;
				// 设置命令响应处理事件
				tc.OnReceiveCommand += this.OnClientReceiveCommandEvent;
				// 设置连接成功事件
				tc.OnConnected += OnConnected;
				// 设置断开连接事件
				tc.OnDisconnected += OnDisconnected;
				// 设置内核是否需要界面运行标志
				tc.RunWithUI = true;
				// 设置心跳包功能是否打开
				tc.EnabledHeartbeat = chbEnabledHeartbeat.Checked;
			}

			// 检查连接状态
			if ( tc.Connected )
			{
				MessageBox.Show( "Already connected.", "Message", MessageBoxButtons.OK, MessageBoxIcon.Information );
				return;
			}

			try
			{
				// 连接服务器
				tc.Connect( );

				//button1.Enabled = !tc.Connected;
				//button2.Enabled = tc.Connected;
			}
			catch ( Exception ex )
			{
				MessageBox.Show( ex.Message );
				tc.Dispose( );
				tc = null;
			}
		}

		private void ClientDisconnectFromServer_Click( object sender, EventArgs e )
		{
			// 从客户端断开内核通讯连接

			try
			{
				if ( tc != null )
				{
					// 断开连接
					tc.Disconnect( );
					// 销毁对象
					tc.Dispose( );
				}
				tc = null;

				//button1.Enabled = true;
				//button2.Enabled = false;
			}
			catch
			{

			}
		}


		#region Client Send Commands

		/// <summary>
		/// 发送一个心跳信号
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void button3_Click( object sender, EventArgs e )
		{
			SendCommand_C2S( tc, new cHeartbeat( ), chkSync.Checked );
		}

		/// <summary>
		/// 发送一个握手信号
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void button4_Click( object sender, EventArgs e )
		{
			SendCommand_C2S( tc, new cHandshake( HandshakeRequester.Server, tc.LocalIP, Convert.ToInt16( tc.LocalPort ) ), chkSync.Checked );
		}

		/// <summary>
		/// 发送一个 Debug Text
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void button8_Click( object sender, EventArgs e )
		{
			if ( textBox1.Text.Trim( ) == "" )
			{
				textBox1.Focus( );
				MessageBox.Show( "Please input debug text for send.", "Message", MessageBoxButtons.OK, MessageBoxIcon.Information );
				return;
			}
			cDebugMessage c = new cDebugMessage( ) { Message = textBox1.Text };
			SendCommand_C2S( tc, c, chkSync.Checked );
		}


		//private void button8_Click( object sender, EventArgs e )
		//{
		//	SendCommand_C2S( tc, new cLightGetList( ), chkSync.Checked );
		//}

		//private void button9_Click( object sender, EventArgs e )
		//{
		//	SendCommand_C2S( tc, new cLightGetState( new string[ ] { "L1", "L2", "L3", "L4" } ), chkSync.Checked );
		//}

		//private void button10_Click( object sender, EventArgs e )
		//{
		//	List<LightState> ls = new List<LightState>( );

		//	//if ( Convert.ToInt32( button10.Tag ) == 1 )
		//	//{
		//	LightState item = new LightState( ) { LightID = "LT#01" };
		//	item.DirectionState.Add( new LightDirectionState( ) { DirectionID = "2", State = LightStateItem.Red } );
		//	item.DirectionState.Add( new LightDirectionState( ) { DirectionID = "1", State = LightStateItem.Green } );
		//	ls.Add( item );

		//	item = new LightState( ) { LightID = "LT#02" };
		//	item.DirectionState.Add( new LightDirectionState( ) { DirectionID = "2", State = LightStateItem.Red } );
		//	item.DirectionState.Add( new LightDirectionState( ) { DirectionID = "1", State = LightStateItem.Green } );
		//	ls.Add( item );

		//	item = new LightState( ) { LightID = "LT#03" };
		//	item.DirectionState.Add( new LightDirectionState( ) { DirectionID = "1", State = LightStateItem.Green } );
		//	item.DirectionState.Add( new LightDirectionState( ) { DirectionID = "2", State = LightStateItem.Red } );
		//	ls.Add( item );


		//	item = new LightState( ) { LightID = "LT#04" };
		//	item.DirectionState.Add( new LightDirectionState( ) { DirectionID = "1", State = LightStateItem.Green } );
		//	item.DirectionState.Add( new LightDirectionState( ) { DirectionID = "2", State = LightStateItem.Red } );
		//	ls.Add( item );

		//	//	button10.Tag = 2;
		//	//}
		//	//else
		//	//{
		//	//	LightState item = new LightState( ) { LightID = "LT#01" };
		//	//	item.DirectionState.Add( new LightDirectionState( ) { DirectionID = "1", State = LightStateItem.Yellow } );
		//	//	item.DirectionState.Add( new LightDirectionState( ) { DirectionID = "2", State = LightStateItem.Red } );
		//	//	ls.Add( item );

		//	//	//item = new LightState( ) { LightID = "灯 B" };
		//	//	//item.DirectionState.Add( new LightDirectionState( ) { DirectionID = "方向一", State = LightStateItem.Yellow } );
		//	//	//item.DirectionState.Add( new LightDirectionState( ) { DirectionID = "方向二", State = LightStateItem.Red } );
		//	//	//ls.Add( item );

		//	//	button10.Tag = 1;
		//	//}

		//	SendCommand_C2S( tc, new cLightSetState( ls ), chkSync.Checked );
		//}

		///// <summary>
		///// 获取停车位设备信息
		///// </summary>
		///// <param name="sender"></param>
		///// <param name="e"></param>
		//private void button12_Click( object sender, EventArgs e )
		//{
		//	// 发送命令
		//	SendCommand_C2S( tc, new cStopPositionGetList( ), chkSync.Checked );
		//}

		//private void button13_Click( object sender, EventArgs e )
		//{

		//	cStopPositionCommand c = new cStopPositionCommand( );
		//	c.Commands.Add( "#1", StopPositionCommand.PullOut );
		//	c.Commands.Add( "#2", StopPositionCommand.Pullup );
		//	SendCommand_C2S( tc, c, chkSync.Checked );
		//}

		//private void button11_Click( object sender, EventArgs e )
		//{
		//	cLightForceRefresh c = new cLightForceRefresh( new string[ ] { "#2" } );
		//	SendCommand_C2S( tc, c, chkSync.Checked );
		//}

		//private void button14_Click( object sender, EventArgs e )
		//{
		//	cTrackTurnoutGetList c = new cTrackTurnoutGetList( );
		//	SendCommand_C2S( tc, c, chkSync.Checked );
		//}

		//private void button15_Click( object sender, EventArgs e )
		//{
		//	cTrackTurnoutCommand c = new cTrackTurnoutCommand( );
		//	c.Commands.Add( "#1", "1" );
		//	c.Commands.Add( "#2", "3" );
		//	SendCommand_C2S( tc, c, chkSync.Checked );
		//}

		//private void button16_Click( object sender, EventArgs e )
		//{
		//	cTrackTurnoutGetState c = new cTrackTurnoutGetState( new string[ ] { "#2" } );
		//	SendCommand_C2S( tc, c, chkSync.Checked );
		//}

		//private void button17_Click( object sender, EventArgs e )
		//{
		//	cRoadTurnoutGetList c = new cRoadTurnoutGetList( );
		//	SendCommand_C2S( tc, c, chkSync.Checked );
		//}

		//private void button19_Click( object sender, EventArgs e )
		//{
		//	cRoadTurnoutGetState c = new cRoadTurnoutGetState( new string[ ] { "#1", "#2" } );
		//	SendCommand_C2S( tc, c, chkSync.Checked );
		//}

		//private void button20_Click( object sender, EventArgs e )
		//{
		//	cRoadTurnoutCommand c = new cRoadTurnoutCommand( new string[ ] { "#1", "#2" } );
		//	SendCommand_C2S( tc, c, chkSync.Checked );
		//}

		//private void button23_Click( object sender, EventArgs e )
		//{
		//	cTrackGetList c = new cTrackGetList( );
		//	SendCommand_C2S( tc, c, chkSync.Checked );
		//}

		//private void button24_Click( object sender, EventArgs e )
		//{
		//	cTrackGetState c = new cTrackGetState( new string[ ] { "#1", "#2" } );
		//	SendCommand_C2S( tc, c, chkSync.Checked );
		//}

		//private void button25_Click( object sender, EventArgs e )
		//{
		//	cTrackCommand c = new cTrackCommand( );
		//	c.Commands.Add( "#1", TrackPowerOperation.ForwardPowerOn );
		//	c.Commands.Add( "#2", TrackPowerOperation.ReversePowerOff );
		//	SendCommand_C2S( tc, c, chkSync.Checked );
		//}

		//private void button28_Click( object sender, EventArgs e )
		//{
		//	cSwitchDeviceGetList c = new cSwitchDeviceGetList( );
		//	SendCommand_C2S( tc, c, chkSync.Checked );
		//}

		//private void button27_Click( object sender, EventArgs e )
		//{
		//	cSwitchDeviceGetState c = new cSwitchDeviceGetState( new string[ ] { "#1", "#2" } );
		//	SendCommand_C2S( tc, c, chkSync.Checked );
		//}

		//private void button26_Click( object sender, EventArgs e )
		//{
		//	cSwitchDeviceCommand c = new cSwitchDeviceCommand( );
		//	c.Commands.Add( "VoicePlay#01", SwitchOperate.On );
		//	//c.Commands.Add( "#2", SwitchOperate.Off );
		//	SendCommand_C2S( tc, c, chkSync.Checked );
		//}

		//private void button31_Click( object sender, EventArgs e )
		//{
		//	cNixietubeLEDGetList c = new cNixietubeLEDGetList( );
		//	SendCommand_C2S( tc, c, chkSync.Checked );
		//}

		//private void button30_Click( object sender, EventArgs e )
		//{
		//	cNixietubeLEDGetState c = new cNixietubeLEDGetState( new string[ ] { "#1", "#2" } );
		//	SendCommand_C2S( tc, c, chkSync.Checked );
		//}

		//private void button29_Click( object sender, EventArgs e )
		//{
		//	if ( txtLEDNum.Text.Trim( ) == "" )
		//	{
		//		for ( int i = 30; i >= 0; i-- )
		//		{
		//			cNixietubeLEDCommand c = new cNixietubeLEDCommand( );
		//			c.Commands.Add( "ND#13", i.ToString( ) );
		//			c.Commands.Add( "ND#14", i.ToString( ) );
		//			c.Commands.Add( "ND#01", i.ToString( ) );
		//			c.Commands.Add( "ND#02", i.ToString( ) );
		//			SendCommand_C2S( tc, c, chkSync.Checked );

		//			WaitSecond( 1 );
		//		}
		//	}
		//	else
		//	{
		//		cNixietubeLEDCommand c = new cNixietubeLEDCommand( );
		//		c.Commands.Add( "ND#13", txtLEDNum.Text.Trim( ) );
		//		c.Commands.Add( "ND#14", txtLEDNum.Text.Trim( ) );
		//		c.Commands.Add( "ND#01", txtLEDNum.Text.Trim( ) );
		//		c.Commands.Add( "ND#02", txtLEDNum.Text.Trim( ) );
		//		SendCommand_C2S( tc, c, chkSync.Checked );
		//	}
		//}

		//private void button32_Click( object sender, EventArgs e )
		//{
		//	cTrackForceRefresh c = new cTrackForceRefresh( new string[ ] { "#2" } );
		//	SendCommand_C2S( tc, c, chkSync.Checked );
		//}

		//private void button33_Click( object sender, EventArgs e )
		//{
		//	cSwitchDeviceForceRefresh c = new cSwitchDeviceForceRefresh( new string[ ] { "#2" } );
		//	SendCommand_C2S( tc, c, chkSync.Checked );
		//}

		//private void button34_Click( object sender, EventArgs e )
		//{
		//	cCardReaderUpload msg = new cCardReaderUpload( ) { Reader = "001", Message = r.Next( 1000000, 1999999 ).ToString( ) };
		//	foreach ( CommunicateClient c in _clients )
		//	{
		//		if ( c.Connected )
		//		{
		//			c.SendCommand( msg );
		//		}
		//	}
		//}

		//private void button35_Click( object sender, EventArgs e )
		//{
		//	cSwitchStateChange msg = new cSwitchStateChange( );
		//	foreach ( SwitchStateDevice c in _switchStatePool )
		//	{
		//		msg.DeviceID = c.ID;
		//		msg.State = ChannelState.On;
		//		foreach ( CommunicateClient client in _clients )
		//		{
		//			client.SendCommand( msg );
		//		}
		//	}
		//}

		//private void button36_Click( object sender, EventArgs e )
		//{
		//	Form2 frm = new Form2( );
		//	frm.Show( );
		//}

		//private void button37_Click( object sender, EventArgs e )
		//{
		//	cDebugMessage msg = new cDebugMessage( );
		//	msg.Message = "Debug message test.";
		//	foreach ( CommunicateClient client in _clients )
		//	{
		//		client.SendCommand( msg );
		//	}
		//}
		//private void button38_Click( object sender, EventArgs e )
		//{
		//	cVirtualDataModuleGetList c = new cVirtualDataModuleGetList( );
		//	SendCommand_C2S( tc, c, chkSync.Checked );
		//}

		//private void button39_Click( object sender, EventArgs e )
		//{
		//	cVirtualDataModuleGetList c = new cVirtualDataModuleGetList( );
		//	cVirtualDataModuleGetListAnswer ret = SendCommand_C2S( tc, c, true ) as cVirtualDataModuleGetListAnswer;

		//	cVirtualDataModuleReadValue cc = new cVirtualDataModuleReadValue( ret.VirtualDataModuleList.Keys.ToArray( ) );
		//	SendCommand_C2S( tc, cc, chkSync.Checked );
		//}

		//private void button40_Click( object sender, EventArgs e )
		//{
		//	Dictionary<string, object> value = new Dictionary<string, object>( );
		//	value.Add( "VDM#01", Convert.ToSingle( txtVDMValue.Text ) );
		//	value.Add( "VDM#02", Convert.ToSingle( txtVDMValue.Text ) );
		//	value.Add( "VDM#03", Convert.ToSingle( txtVDMValue.Text ) );
		//	value.Add( "VDM#04", Convert.ToSingle( txtVDMValue.Text ) );

		//	value.Add( "VDM_I#01", Convert.ToSingle( txtVDMValue.Text ) );
		//	value.Add( "VDM_I#02", Convert.ToSingle( txtVDMValue.Text ) );
		//	value.Add( "VDM_I#03", Convert.ToSingle( txtVDMValue.Text ) );
		//	value.Add( "VDM_I#04", Convert.ToSingle( txtVDMValue.Text ) );
		//	cVirtualDataModuleWriteValue c = new cVirtualDataModuleWriteValue( value );
		//	SendCommand_C2S( tc, c, chkSync.Checked );
		//}

		#endregion

		#endregion


		#region Event

		public Form1( )
		{
			InitializeComponent( );
		}

		private void Form1_Load( object sender, EventArgs e )
		{
			cmbLocal.Items.AddRange( TcpCore.GetLocalIPAddresses( ) );

			if ( cmbLocal.Items.Count > 0 ) cmbLocal.SelectedIndex = 0;

			cmbServerLocal.Items.AddRange( TcpCore.GetLocalIPAddresses( ) );

			if ( cmbServerLocal.Items.Count > 0 ) cmbServerLocal.SelectedIndex = 0;

			cmbClientRemote.Items.AddRange( TcpCore.GetLocalIPAddresses( ) );

			if ( cmbClientRemote.Items.Count > 0 ) cmbClientRemote.SelectedIndex = 0;
		}

		public bool Check( )
		{
			if ( cmbLocal.SelectedIndex < 0 )
			{
				MessageBox.Show( "Please set local ip address.", "Message", MessageBoxButtons.OK, MessageBoxIcon.Information );
				cmbLocal.Focus( );
				return false;
			}

			if ( numLocal.Text.Trim( ) == "" )
			{
				MessageBox.Show( "Please set local Port.", "Message", MessageBoxButtons.OK, MessageBoxIcon.Information );
				numLocal.Focus( );
				return false;
			}

			if ( cmbClientRemote.Text.Trim( ) == "" )
			{
				MessageBox.Show( "Please set remote ip address.", "Message", MessageBoxButtons.OK, MessageBoxIcon.Information );
				cmbClientRemote.Focus( );
				return false;
			}

			if ( numRemote.Text.Trim( ) == "" )
			{
				MessageBox.Show( "Please set remote Port.", "Message", MessageBoxButtons.OK, MessageBoxIcon.Information );
				numRemote.Focus( );
				return false;
			}

			if ( cmbServerLocal.Text.Trim( ) == "" )
			{
				MessageBox.Show( "Please set server listen address.", "Message", MessageBoxButtons.OK, MessageBoxIcon.Information );
				cmbServerLocal.Focus( );
				return false;
			}

			if ( numServerLocal.Text.Trim( ) == "" )
			{
				MessageBox.Show( "Please set server listen Port.", "Message", MessageBoxButtons.OK, MessageBoxIcon.Information );
				numServerLocal.Focus( );
				return false;
			}


			return true;

		}

		private void button21_Click( object sender, EventArgs e )
		{
			listBox1.Items.Clear( );
		}

		private void button22_Click( object sender, EventArgs e )
		{
			listBox2.Items.Clear( );
		}

		private void timer1_Tick( object sender, EventArgs e )
		{
			RefreshClient_Click( button18, new EventArgs( ) );
		}

		private void WaitSecond( uint second )
		{
			DateTime dt = DateTime.Now;
			while ( true )
			{
				TimeSpan ts = DateTime.Now - dt;
				if ( ts.TotalSeconds > second )
					break;
				else
					Application.DoEvents( );
			}
		}


		#endregion

		private void button9_Click( object sender, EventArgs e )
		{
			if ( textBox3.Text.Trim( ) == "" )
			{
				textBox3.Focus( );
				MessageBox.Show( "Please input debug text for send.", "Message", MessageBoxButtons.OK, MessageBoxIcon.Information );
				return;
			}
			cDebugMessage c = new cDebugMessage( ) { Message = textBox3.Text };
			foreach ( CommunicateClient cc in _clients )
			{
				SendCommand_C2S( cc, c, chkSync.Checked );
			}

		}

		private void button10_Click( object sender, EventArgs e )
		{
			if ( textBox1.Text.Trim( ) == "" )
			{
				textBox1.Focus( );
				MessageBox.Show( "Please input debug text for send.", "Message", MessageBoxButtons.OK, MessageBoxIcon.Information );
				return;
			}
			cDebugMessage c = new cDebugMessage( ) { Message = textBox1.Text };

			// Get ready
			byte[ ] _body = c.ToBinary( );
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

			listBox1.BeginUpdate( );
			try
			{
				int idx = 0;
				listBox1.Items.Insert( 0, "Full package contant: " );

				idx++;
				string ss = "    " + " 00d: ";
				textBox4.Text = "";
				for ( int i = 0; i < tmp.Count; i++ )
				{
					textBox4.Text = textBox4.Text + string.Format( "{0,2:X2}", tmp[i] );

					ss = ss + ( ss.Trim( ) == "" ? "" : " " ) + string.Format( "0x{0,2:X2}", tmp[i] );
					if ( ( i != 0 ) && ( ( i + 1 ) % 10 == 0 ) )
					{
						listBox1.Items.Insert( idx, ss );
						idx++;
						ss = "    " + " " + Convert.ToInt32( (i + 1) / 10 *10 ).ToString() + "d: ";
					}
				}
				if ( ss.Trim( ) != "" )
				{
					listBox1.Items.Insert( idx, ss );
					idx++;
					ss = "    ";
				}

				listBox1.Items.Insert( idx, "" );
			}
			finally
			{
				listBox1.EndUpdate( );
			}

		}
	}
}
