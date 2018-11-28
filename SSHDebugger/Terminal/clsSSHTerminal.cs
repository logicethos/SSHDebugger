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
using SSHDebugger.Extensions;

namespace SSHDebugger
{
	public class clsSSHTerminal : IDisposable
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

		public delegate void TerminalDataHandler(String text);
		public event TerminalDataHandler TerminalData;

		public Gdk.Key LastKeyPress { get; private set;}

		public bool UserInputMode {get; private set;}
		AutoResetEvent userkeypress = new AutoResetEvent (false);


		const int retryCount = 3;

		public clsSSHTerminal(clsHost host)
		{
			Host = host;
			AddDefaultPrivateKey ();
		}

		public void SetHost(clsHost host)
		{
			Host = host;
		}

		void AddDefaultPrivateKey ()
		{
			string homePath = (Environment.OSVersion.Platform == PlatformID.Unix ||
			                   Environment.OSVersion.Platform == PlatformID.MacOSX) 
				? Environment.GetEnvironmentVariable ("HOME") 
			                 : Environment.ExpandEnvironmentVariables ("%HOMEDRIVE%%HOMEPATH%");
			var keyPath = Path.Combine (homePath, ".ssh/id_rsa");
			if (File.Exists (keyPath))
				AddPrivateKeyFile (keyPath);
			
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
						if (String.IsNullOrEmpty(Host.Password)) Host.Password = RequestUserInput("Enter Host Password: ",'*');				
						sshClient = new SshClient (Host.RemoteHost, Host.RemoteSSHPort, Host.Username,Host.Password);
					}

					Write("ssh connecting to {0}@{1}:{2}...",Host.Username,Host.RemoteHost,Host.RemoteSSHPort);
					sshClient.Connect ();
					if (sshClient.IsConnected)
					{
						WriteLine("OK");

						WriteLine("MaxSessions:{0} {1} {2}",
											sshClient.ConnectionInfo.MaxSessions,
											sshClient.ConnectionInfo.Encoding,											
											sshClient.ConnectionInfo.CurrentServerEncryption);

						return true;
					}
				}
				catch (Exception ex)
				{
					if (ex.Message.ToLower().Contains("password")) Host.Password=""; //Reset password
					WriteLine("ssh Error: "+ex.Message);
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
					WriteLine("Starting terminal: Emulation = {0}",Host.TerminalEmulation);
	
					shellStream = sshClient.CreateShellStream(Host.TerminalEmulation,(uint)Host.TerminalCols,(uint)Host.TerminalRows,0,0,4096);
					
				
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
						Write("Changing dir: {0}...",Host.WorkingDir);
						shellStream.WriteLine(String.Format("cd {0}\r\n",Host.WorkingDir));
					}
					started.Set();
					keepShellAlive.Reset();
					keepShellAlive.WaitOne();

					WriteLine("\r\n*** Console Stream End");
				}
				catch (Exception ex)
				{
					WriteLine("\r\n*** Console Stream Error: {0}",ex.Message);
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


		public bool ShellExecute(string command, TimeSpan? timespan = null)
		{
			if (!StartShellStream()) return false;		
			shellStream.WriteLine(command);

			return timespan == null || shellStream.Expect(command,timespan.Value) != null;
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
						if (String.IsNullOrEmpty(Host.Password))	Host.Password = RequestUserInput("Enter Host Password: ",'*');				
						sftpClient = new SftpClient (Host.RemoteHost, Host.RemoteSSHPort, Host.Username, Host.Password);
					}

					Write("sftp connecting to {0}@{1}:{2}...",Host.Username,Host.RemoteHost,Host.RemoteSSHPort);
					sftpClient.Connect ();
					if (sftpClient.IsConnected)
					{
						WriteLine("OK");
									
						if (!String.IsNullOrEmpty(Host.WorkingDir))
						{
							var scpPath = Host.WorkingDir.Replace("~", sftpClient.WorkingDirectory);  //this SCP library doesnt like ~
							if (sftpClient.WorkingDirectory != scpPath)
							{
								try
								{
									Write("Changing dir: {0}...",scpPath);
									sftpClient.ChangeDirectory (scpPath);
								}catch(Exception ex)
								{
									WriteLine("FAILED\r\n{0}",ex.Message);
									return false;
								}
							}
							WriteLine("OK");
						}
					}
				}
				catch (Exception ex)
				{
					if (ex.Message.ToLower().Contains("password")) Host.Password=""; //Reset password
					WriteLine("Error: "+ex.Message);
					return false;
				}
			}
			return true;
		}


		public bool Execute(String command)
		{
			if (!ConnectSSH ())	return false;

			var cmd = sshClient.CreateCommand (command);
			Write(cmd.Execute ());
			return true;
		}

		public bool ExecuteAsync(String command)
		{
			if (!ConnectSSH ())	return false;

			var cmd = sshClient.CreateCommand (command);
			cmd.BeginExecute (
					(IAsyncResult r) => {
						var result = (CommandAsyncResult)r;
						WriteLine("{0} {1}",command,result.IsCompleted);
					}
				);
				ReadAsync(cmd.ExtendedOutputStream);
			//	ReadAsync(cmd.OutputStream);
			return true;
		}


		public void ReadAsync(Stream stream)
		{	
			new Task( async () =>
			{
				int numBytesRead;
				byte[] data = new byte[1024];

				WriteLine("stream start");
				try
				{
					while ((numBytesRead = await stream.ReadAsync(data, 0, 1024)) >0)
					{
						Write(Encoding.UTF8.GetString(data,0,numBytesRead));		
					}
				}
				catch (Exception ex)
				{
					WriteLine("\r\nstream error: {0}",ex.Message);
				}
				
			}).Start();
		}

		/// <summary>
		/// Starts an ssh tunnel, where conntection to a local port (which this will listen on), transparently connects it to a remote port.
		/// </summary>
		/// <returns><c>true</c>, if tunnel was started, <c>false</c> otherwise.</returns>
		/// <param name="TunnelPortLocal">Tunnel port local.</param>
		/// <param name="TunnelPortRemote">Tunnel port remote.</param>
		/// <param name="LocalNetwork">Local network.</param>
		public bool StartTunnel(UInt32 TunnelPortLocal, UInt32 TunnelPortRemote, String LocalNetwork = null)
		{

			if (!ConnectSSH ())	return false;

			try
			{

			if (LocalNetwork == null) LocalNetwork = IPAddress.Loopback.ToString ();

			Write ("ssh tunnel: {0}:{1} -> {2}:{3}...",LocalNetwork, TunnelPortLocal, "localhost", TunnelPortRemote);
			if (forwardPort!=null)
			{
				if (forwardPort.BoundPort == TunnelPortLocal && forwardPort.Port == TunnelPortRemote) {
					if (forwardPort.IsStarted) {
						WriteLine ("Tunnel already connencted");
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

			forwardPort.Exception += (object sender, ExceptionEventArgs e) => 
			{
					WriteLine("Tunnel error: {0}",e.Exception.Message);
			};

			forwardPort.RequestReceived += (object sender, PortForwardEventArgs e) => 
			{
				WriteLine("Tunnel connection: {0}->{1}",e.OriginatorHost, e.OriginatorPort);
			};
			forwardPort.Start();
			WriteLine ("OK");

			return forwardPort.IsStarted;

			}
			catch (SocketException ex)
			{
				if (ex.SocketErrorCode == SocketError.AccessDenied)
				{
					WriteLine("FAILED\r\nAccess Denied - Cannot create port redirect. Try running Monodevelop with higher privileges.");
				}
				else
				{
					WriteLine("FAILED\r\nTunnel Error: {0}",ex);
				}
			}
			catch (Exception ex)
			{
				WriteLine("Tunnel Error: {0}",ex);
			}
			return false;
		}


		public bool UploadFile(String LocalPath, String RemoteFileName = null)
		{

			if (!ConnectSFTP ()) return false;

			if (String.IsNullOrEmpty (RemoteFileName)) RemoteFileName = System.IO.Path.GetFileName(LocalPath);

			Write("sftp Uploading: {0}...",LocalPath);

			try
			{	
				using (var fs = File.OpenRead(LocalPath))
				{
					sftpClient.UploadFile (fs, RemoteFileName,true, (bytes) => {Write(".");});
				}
				WriteLine("OK");
				return true;
			} catch (Exception ex)
			{
				WriteLine("Error "+ex.Message);
				return false;
			}
		}

		public bool SynchronizeDir(String LocalDir, String RemoteDir = null, String SearchPattern = "*" )
		{

			if (!ConnectSFTP ()) return false;

			if (RemoteDir==null || RemoteDir == "~") RemoteDir = sftpClient.WorkingDirectory;

			Write("sftp Synchronizing Directories: {0}->{1}...",LocalDir,RemoteDir);

			try
			{	
				sftpClient.SynchronizeDirectories(LocalDir,RemoteDir,SearchPattern);
				WriteLine("OK");
				return true;
			} catch (Exception ex)
			{
				WriteLine("Error "+ex.Message);
				return false;
			}
		}

		public void WriteLine(String output, params object[] args)
		{
			WriteLine (String.Format(output, args));
		}

		public void WriteLine(String output)
		{
				Write (output+"\r\n");
		}

		public void Write(String output, params object[] args)
		{
			Write (String.Format(output, args));
		}

		public void Write(String output)
		{
			if (TerminalData!=null) TerminalData(output);

		}

		public void ShellSend(Gdk.Key key)
		{
			if (UserInputMode)
			{
				LastKeyPress = key;
				userkeypress.Set ();
			}
		    else
			{
				if (LocalEcho) Write(key.ToString());
				if (shellStream!=null && shellStream.CanWrite)
				{
                    byte[] charBytes = null;

                    switch (key)
                    {
                        case Gdk.Key.Shift_L:
                        case Gdk.Key.Shift_R:
                        case Gdk.Key.ISO_Level3_Shift: //Alt Gr
                            //Ignore
                            break;
                        case Gdk.Key.Return:
                        case Gdk.Key.KP_Enter:
                            charBytes = Encoding.UTF8.GetBytes(Environment.NewLine);
                            break;
                        case Gdk.Key.BackSpace:
                            charBytes = new byte[] { (byte)'\b' };
                            break;
                        case Gdk.Key.KP_Tab:
                        case Gdk.Key.Tab:
                            charBytes = new byte[] { (byte)'\t' };
                            break;
                        case Gdk.Key.Escape:
                            charBytes = new byte[] { 0x1B }; //ESC
                            break;
                        default:
                            charBytes = Encoding.UTF8.GetBytes(new char[] { key.GetChar() });
                            break;
                    }

                    if (charBytes != null)
                    {
                        shellStream.Write(charBytes, 0, charBytes.Length);
                        shellStream.Flush();
                    }
				}
			}
		}

		/// <summary>
		/// Requests the user input, without sending it to the ssh connection.
		/// </summary>
		/// <returns>The user input.</returns>
		/// <param name="prompt">Prompt.</param>
		/// <param name="echo">Echo char. If null, the keypress will be displayed</param>
		public String RequestUserInput(String prompt, char? echoChar=null)
		{
			UserInputMode = true;
			Write (prompt);
			String input = "";
			while (UserInputMode && userkeypress.WaitOne ())
			{				
				switch(LastKeyPress)
				{
				    case Gdk.Key.BackSpace:
					{
					    if (input.Length > 0)
					    {
						input = input.Substring(0, input.Length - 1);
						if (echoChar.HasValue) Write("\b \b");
					    }
					}
					break;
				    case Gdk.Key.Return:
				    case Gdk.Key.KP_Enter:
					{
					    Write(Environment.NewLine);
					    UserInputMode = false; //Stop Input
					    break;
					}
				    case Gdk.Key.Shift_L:
				    case Gdk.Key.Shift_R:
				    case Gdk.Key.ISO_Level3_Shift: //Alt Gr
					{
					    //Do nothing
					}
					break;
				    default:
					{
                        var keyValue = LastKeyPress.GetChar();
					    Write(echoChar.HasValue ? echoChar.Value.ToString() : keyValue.ToString());
					    input += keyValue;
					}
					break;
				}
			}
			UserInputMode = false;
			return input;
		}

		public void Dispose()
		{
			userkeypress.Dispose();
			keepShellAlive.Set();
			if (shellStream!=null) shellStream.Dispose ();
			if (sshClient!=null) sshClient.Dispose ();
			if (sftpClient!=null) sftpClient.Dispose ();
		}

	}
}
