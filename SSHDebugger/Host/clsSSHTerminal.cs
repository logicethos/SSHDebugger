// 
// clsHost.cs
//  
// Author:
//		 Stuart Johnson <stuart@logicethos.com>
// 
// Copyright (c) 2015 Stuart Johnson, Logic Ethos Ltd.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using Renci.SshNet;
using Renci.SshNet.Common;
using System.Threading;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SSHDebugger
{
	public class clsSSHTerminal : clsTerminal, IDisposable
	{
		List<PrivateKeyFile> PrivateKeyFileList = new List<PrivateKeyFile>();


		clsHost Host;

		SshClient sshClient;
		SftpClient sftpClient;
		ForwardedPortLocal forwardPort = null;
		ShellStream shellStream = null;

		public bool LocalEcho { get; set; }
		Task ShellStreamTask;
		
		ManualResetEvent keepShellAlive = new ManualResetEvent(false);

		const int retryCount = 3;

		public clsSSHTerminal(clsHost host) : base (host.Name, host.TerminalCols, host.TerminalRows, host.TerminalFont)
		{
			Host = host;
			KeyPress += (Gdk.Key key) => 
			{
				if (LocalEcho) Write(key.ToString());
				if (shellStream!=null && shellStream.CanWrite)
					{
						 shellStream.WriteByte((byte)key);
						 shellStream.Flush();
					}
			};
		}

		public void SetHost(clsHost host)
		{
			Host = host;

			Gtk.Application.Invoke (delegate {
				Name = host.Name;
				base.term.SetSize(host.TerminalCols,host.TerminalRows);
			});
		}

		public void AddPrivateKeyFile(String path)
		{
			PrivateKeyFileList.Add(new PrivateKeyFile(path));
		}

		public void AddPrivateKeyFile(String path, String password)
		{
			PrivateKeyFileList.Add(new PrivateKeyFile(path,password));
		}

		public bool ConnectSSH()
		{
			int retry = 0;
			if (sftpClient!=null && sftpClient.IsConnected) sftpClient.Disconnect();  //Seems if sftp client is open, it blocks the shell/tunnel.

			while (sshClient == null || !sshClient.IsConnected)
			{
				if (retry++ > retryCount) return false;
				try
				{

					if (PrivateKeyFileList.Count>0 && retry == 1)
					{
						sshClient = new SshClient (Host.RemoteHost, Host.RemoteSSHPort,  Host.Username, PrivateKeyFileList.ToArray());
					}
					else
					{
						if (String.IsNullOrEmpty(Host.Password)) Host.Password = RequestUserInput("Enter Host Password: ","*");				
						sshClient = new SshClient (Host.RemoteHost, Host.RemoteSSHPort, Host.Username,Host.Password);
					}

					this.Write("ssh connecting to {0}@{1}:{2}...",Host.Username,Host.RemoteHost,Host.RemoteSSHPort);
					sshClient.Connect ();
					if (sshClient.IsConnected)
					{
						this.WriteLine("OK");

						this.WriteLine("MaxSessions:{0} {1} {2}",
											sshClient.ConnectionInfo.MaxSessions,
											sshClient.ConnectionInfo.Encoding,											
											sshClient.ConnectionInfo.CurrentServerEncryption);
						return true;
					}
				}
				catch (Exception ex)
				{
					this.WriteLine("ssh Error: "+ex.Message);
					return false;
				}
			}
			return true;
		}

		public bool StartShellStream()
		{
			if (!ConnectSSH ())	return false;

			ManualResetEvent started = new ManualResetEvent(false);
			ShellStreamTask = new Task( () =>
			{
				try
				{	
					this.WriteLine("*** Console Stream Start");
	
					shellStream = sshClient.CreateShellStream("xterm",(uint)Host.TerminalCols,(uint)Host.TerminalRows,0,0,4096);
					
				
					shellStream.DataReceived += (object sender, ShellDataEventArgs e) => 
					{
						Write(Encoding.UTF8.GetString(e.Data,0,e.Data.Length));
					};

					shellStream.ErrorOccurred+= (object sender, ExceptionEventArgs e) => 
					{
						WriteLine(e.Exception.Message);
						keepShellAlive.Set();
					};

					if (!String.IsNullOrEmpty(Host.WorkingDir))
					{
						this.Write("Changing dir: {0}...",Host.WorkingDir);
						shellStream.WriteLine(String.Format("cd {0}\r\n",Host.WorkingDir));
					}
					started.Set();
					keepShellAlive.Reset();
					keepShellAlive.WaitOne();

					this.WriteLine("\r\n*** Console Stream End");
				}
				catch (Exception ex)
				{
					this.WriteLine("\r\n*** Console Stream Error: {0}",ex.Message);
				}
				finally
				{
					shellStream.Dispose();
					shellStream = null;
				}
				
			});
			ShellStreamTask.Start();
			return started.WaitOne(5000);
		}

		string Convert(byte[] data, int len)
		{
			char[] characters = new char[len];
			for (int f=0;f<len;f++)
			{
				characters[f] = (char)data[f];
			}
    		return new string(characters);
		}

		public void ShellExecute(string command)
		{
			if (!StartShellStream()) return;		
			shellStream.WriteLine(command);

		}


		public bool ConnectSFTP()
		{
			int retry = 0;

			while (sftpClient == null || !sftpClient.IsConnected)
			{
				if (retry++ > retryCount) return false;
				try
				{
					
					if (PrivateKeyFileList.Count>0 && retry == 1)
					{
						sftpClient = new SftpClient (Host.RemoteHost, Host.RemoteSSHPort, Host.Username, PrivateKeyFileList.ToArray());
					}
					else
					{
						if (String.IsNullOrEmpty(Host.Password))	Host.Password = RequestUserInput("Enter Host Password: ","*");				
						sftpClient = new SftpClient (Host.RemoteHost, Host.RemoteSSHPort, Host.Username, Host.Password);
					}

					this.Write("sftp connecting to {0}@{1}:{2}...",Host.Username,Host.RemoteHost,Host.RemoteSSHPort);
					sftpClient.Connect ();
					if (sftpClient.IsConnected)
					{
						this.WriteLine("OK");
									
						if (!String.IsNullOrEmpty(Host.WorkingDir))
						{
							var scpPath = Host.WorkingDir.Replace("~", sftpClient.WorkingDirectory);  //this SCP library doesnt like ~
							if (sftpClient.WorkingDirectory != scpPath)
							{
								try
								{
									this.Write("Changing dir: {0}...",scpPath);
									sftpClient.ChangeDirectory (scpPath);
								}catch(Exception ex)
								{
									this.WriteLine("FAILED\r\n{0}",ex.Message);
									return false;
								}
							}
							this.WriteLine("OK");
						}
					}
				}
				catch (Exception ex)
				{
					this.WriteLine("Error: "+ex.Message);
					return false;
				}
			}
			return true;
		}


		public void Execute(String command)
		{
			if (!ConnectSSH ())	return;

			var cmd = sshClient.CreateCommand (command);
			cmd.Execute ();
		}

		public void ExecuteAsync(String command)
		{
			if (!ConnectSSH ())	return;

			var cmd = sshClient.CreateCommand (command);
			cmd.BeginExecute (
					(IAsyncResult r) => {
						var result = (CommandAsyncResult)r;
						this.WriteLine("{0} {1}",command,result.IsCompleted);
					}
				);
				ReadAsync(cmd.ExtendedOutputStream);
				ReadAsync(cmd.OutputStream);
		}


		public void ReadAsync(Stream stream)
		{	
			new Task( async () =>
			{
				int numBytesRead;
				byte[] data = new byte[1024];

				this.WriteLine("*** Console Stream Start");
				try
				{
					while ((numBytesRead = await stream.ReadAsync(data, 0, 1024)) >0)
					{
						this.Write(	Encoding.UTF8.GetString(data,0,numBytesRead));		
					}
					this.WriteLine("\r\n*** Console Stream End");
				}
				catch (Exception ex)
				{
					this.WriteLine("\r\n*** Console Stream Error: {0}",ex.Message);
				}
				
			}).Start();
		}


		public bool StartTunnel(UInt32 TunnelPortLocal, UInt32 TunnelPortRemote, String LocalNetwork = null)
		{

			if (!ConnectSSH ())	return false;

			try
			{

			if (LocalNetwork == null) LocalNetwork = IPAddress.Loopback.ToString ();

			this.Write ("ssh tunnel: {0}:{1} -> {2}:{3}...",LocalNetwork, TunnelPortLocal, "localhost", TunnelPortRemote);
			if (forwardPort!=null)
			{
				if (forwardPort.BoundPort == TunnelPortLocal && forwardPort.Port == TunnelPortRemote) {
					if (forwardPort.IsStarted) {
						this.WriteLine ("Tunnel already connencted");
						return true;
					} else {
						forwardPort.Dispose ();
					}
				} else {
					forwardPort.Dispose ();
				}
			}


			forwardPort = new ForwardedPortLocal(LocalNetwork, TunnelPortLocal, "localhost", TunnelPortRemote);

			sshClient.AddForwardedPort(forwardPort);


			forwardPort.RequestReceived += (object sender, PortForwardEventArgs e) => 
			{
				this.WriteLine("Tunnel connection: {0}->{1}",e.OriginatorHost, e.OriginatorPort);
			};

			forwardPort.Start();
			this.WriteLine ("OK");

			return forwardPort.IsStarted;

			}
			catch (SocketException ex)
			{
				if (ex.SocketErrorCode == SocketError.AccessDenied)
				{
					this.WriteLine("FAILED\r\nAccess Denied - Cannot create port redirect. Try running monodevelop with higher privileges.");
				}
				else
				{
					this.WriteLine("FAILED\r\nTunnel Error: {0}",ex);
				}
			}
			catch (Exception ex)
			{
				this.WriteLine("Tunnel Error: {0}",ex);
			}
			return false;
		}


		public bool UploadFile(String LocalPath, String RemoteFileName = null)
		{

			if (!ConnectSFTP ()) return false;

			if (String.IsNullOrEmpty (RemoteFileName)) RemoteFileName = System.IO.Path.GetFileName(LocalPath);

			this.Write("sftp Uploading: {0}...",LocalPath);

			try
			{	
				using (var fs = File.OpenRead(LocalPath))
				{
					sftpClient.UploadFile (fs, RemoteFileName,true, (bytes) => {Write(".");});
	
				}
				this.WriteLine("OK");
				return true;
			} catch (Exception ex)
			{
				return false;
			}
		}

		public override void Dispose()
		{
			keepShellAlive.Set();
			if (shellStream!=null) shellStream.Dispose ();
			if (sshClient!=null) sshClient.Dispose ();
			if (sftpClient!=null) sftpClient.Dispose ();
			base.Dispose();
		}

	}
}
