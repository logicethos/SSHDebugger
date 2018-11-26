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
using System.IO;
using Mono.Debugging.Soft;
using Gtk;
using MonoDevelop.Ide;
using MonoDevelop.Core;
using System.Net;
using MonoDevelop.Projects;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

namespace SSHDebugger
{
	public class clsHost : IDisposable
	{

		

		public String LocalHost { get; private set;}
		public UInt32 LocalTunnelPort { get; private set;}
		public UInt32 RemoteTunnelPort { get; private set;}

		public String Name { get; private set;}
		public int RemoteSSHPort { get; private set;}
		public String ScriptPath { get; private set;}


		public String Username { get; private set;}
		public String Password { get; set;}
		public String RemoteHost { get; private set;}

		public String WorkingDir { get; private set;}

		public String build_exe_path { get; private set;}

		public String TerminalEmulation { get; private set;}
		public String TerminalFont { get; private set;}
		public int TerminalRows { get; private set;}
		public int TerminalCols { get; private set;}


		ITerminal _terminal = null;
		public ITerminal Terminal 
		{
			get{ return _terminal;}
			set{
				Password = "";  //Reset password
			 	_terminal = value;
			 }
		}


		String _hostString;
		public String HostString
		{
			get { return _hostString;}

			private set
			{
				_hostString = value;

				var pt1 = value.IndexOf ('@');
				var pt2 = value.IndexOf (':');
				if (pt1 > -1) Username = value.Substring (0, pt1);
				if (pt2 > -1 && pt2 < pt1) { //password included in url
					var userSplit = Username.Split (new char[]{ ':' }, 2);
					Username = userSplit[0];
					Password = userSplit[1];
					pt2 = value.IndexOf (':',pt1);
				}

				if (pt2 > -1) {
					RemoteSSHPort = int.Parse (value.Substring (pt2 + 1, value.Length - pt2 - 1));
				} else {
					RemoteSSHPort = 22;
					pt2 = value.Length;
				}
				RemoteHost = value.Substring (pt1+1, pt2 - pt1 -1);
			}
		}



		public clsHost (String filePath)
		{	
			var buildTarget = MonoDevelop.Ide.IdeApp.ProjectOperations.CurrentSelectedBuildTarget;
			var buildConfigs = ((DotNetProject)buildTarget).Configurations;
			build_exe_path = buildConfigs.Cast<DotNetProjectConfiguration> ().First (x => x.DebugType == "full").CompiledOutputName;

			ScriptPath = filePath;
			LocalHost = IPAddress.Loopback.ToString ();
			LocalTunnelPort = 10123;

			TerminalFont = "Monospace 10";
			TerminalCols = 120;
			TerminalRows = 50;
			TerminalEmulation = "vt100";

			try
			{
				ProcessScript (false);
				clsSSHDebuggerEngine.HostsList.Add (this);
			}
			catch (Exception ex)
			{
				Gtk.Application.Invoke (delegate {
						using (var md = new MessageDialog (null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok,ex.Message)) {
							md.Title = "ProcessScript";
							md.Run ();
							md.Destroy ();
						}
					});	
			}

		}


		public SoftDebuggerStartInfo ProcessScript(bool Execute)
		{			
			
			int ConsolePort = -1;
			int LineCount = 0;

			try {

				if (Terminal != null)
				{
				 	Terminal.SSH.WriteLine("Running script: {0}",Path.GetFileName(ScriptPath));
				 	Terminal.DebuggerThread = Thread.CurrentThread;
				}

				using (var fs = File.OpenText (ScriptPath)) {
					String linein;
					while ((linein = fs.ReadLine ()) != null) {
						LineCount++;
						linein = ReplaceVarsInString(linein.Trim ());
						if (linein == "" || linein.StartsWith ("#") || linein.StartsWith ("//"))
							continue;
						if (linein.StartsWith ("<")) {
							if (Execute) 
							{
								var proc_command = linein.Substring(1).Split(new char[]{' '},2);
								ProcessStartInfo startInfo = new ProcessStartInfo();        
								startInfo.FileName = proc_command[0];
								if (proc_command.Length>1) startInfo.Arguments = proc_command[1];
								Process.Start(startInfo);
							}							
						} else if (linein.StartsWith (">")) {
							if (Execute)
								if (!Terminal.SSH.Execute(linein.Substring(1))) return null;
						} else if (linein.StartsWith ("&>")) {
							if (Execute)
							 if (!Terminal.SSH.ExecuteAsync(linein.Substring(2))) return null;
						} else if (linein.StartsWith ("s>") || linein.StartsWith ("S>")) {
							if (Execute)
  							  if (!Terminal.SSH.ShellExecute(linein.Substring(2), TimeSpan.FromSeconds(5))) return null;
						} else {
							var commandLine = linein.Split (new char[]{ ' ', '=' }, 2);
							var command = commandLine [0].Trim ();
							String commandArgs = "";
							if (commandLine.Length > 1) {
								commandArgs = commandLine [1].Trim ();
								if (commandArgs.StartsWith ("="))
									commandArgs = commandArgs.Substring (1).TrimStart ();
							}

							switch (command.ToLower ()) {
								case "host":
									HostString = commandArgs;
									break;
								case "name":
									Name = commandArgs;
									break;
								case "consoleport":
									ConsolePort = int.Parse(commandArgs);
									break;
								case "localhost":
									LocalHost = commandArgs;
									break;
								case "localtunnelport":
									LocalTunnelPort = UInt32.Parse(commandArgs);
									break;
								case "remotetunnelport":
									RemoteTunnelPort = UInt32.Parse(commandArgs);
									break;
								case "workingdir":
								case "workingdirectory":
									WorkingDir = commandArgs;
									break;
								case "terminalfont":
									TerminalFont = commandArgs;
									break;
								case "terminalrows":
									TerminalRows = int.Parse(commandArgs);
									break;
								case "terminalcols":
									TerminalCols = int.Parse(commandArgs);
									break;
								case "terminalemulation":
									TerminalEmulation = commandArgs;
									break;
								case "privatekeyfile":
									if (!String.IsNullOrEmpty(commandArgs)) Terminal.SSH.AddPrivateKeyFile(commandArgs);								
									break;
								default:
								{
									if (Execute)
									{
										switch (command.ToLower ())
										{
											case "scp-copy": // $exe-file $mdb-file
												foreach (var file in commandArgs.Split(new char[]{' '}))
												{
													if (!Terminal.SSH.UploadFile(file)) return null;
												}
												break;
											case "scp-sync":
											if (!Terminal.SSH.SynchronizeDir(Path.GetDirectoryName(build_exe_path))) return null;
												break;
											case "starttunnel": 
												if (!Terminal.SSH.StartTunnel(LocalTunnelPort,RemoteTunnelPort)) return null;
												break;
											case "sleep":
												Thread.Sleep(int.Parse(commandArgs));
												break;
											default:
											if (Terminal != null) Terminal.SSH.WriteLine ("Script Error (Line {0}): {1} Unkown command", LineCount, linein);
												break;
										}
									}
								}
								break;
							}
						}
					}
				}
				if (Execute) return DebuggerInfo(ConsolePort);
			} catch (Exception ex) {
				String errorMsg = String.Format("SSH Script ended (Line {0}:{1})", LineCount, ex.Message);
				if (Terminal != null) {
					Terminal.SSH.WriteLine (errorMsg);
				} else {
					throw new Exception(errorMsg);
				}
			}
			finally {

			}
			return null;
		}

		String ReplaceVarsInString (String input)
		{
			var sb = new StringBuilder ();
			int pt0 = 0;
			int pt1,pt2;

			while ((pt1 = input.IndexOf ("$[",pt0)) != -1)
			{
				pt2 = input.IndexOf ("]", pt1);
				sb.Append (input.Substring (pt0, pt1-pt0));
				pt0 = pt2 + 1;
				sb.Append (GetVar(input.Substring(pt1 + 2, pt2 - pt1 - 2)));
			}

			if (pt0 == 0) return input;
			if (pt0 < input.Length-1) sb.Append (input.Substring (pt0));
			return sb.ToString ();
		}

		String GetVar(String input)
        {
            switch (input)
            {
                case "exe-path":
                    return build_exe_path;
                case "mdb-path":
                    return build_exe_path + ".mdb";
                case "pdb-path":
                    //Replace Test.exe => Test.pdb
                    return build_exe_path.Substring(0, build_exe_path.Length - ".exe".Length) + ".pdb";
                case "build-path":
                    return Path.GetDirectoryName(build_exe_path);
                case "work-dir":
                    return WorkingDir;
                case "RemoteTunnelPort":
                    return RemoteTunnelPort.ToString();
                case "exe-file":
                    return Path.GetFileName(build_exe_path);
                default:
                    return "?";

            }
        }


		public SoftDebuggerStartInfo DebuggerInfo (int consolePort = -1)
		{
			try
			{

				IPAddress[] addresslist = Dns.GetHostAddresses(LocalHost);

				var	startArgs = new SoftDebuggerConnectArgs ("", addresslist[0], (int)LocalTunnelPort, consolePort) {
						//infinite connection retries (user can cancel), 800ms between them
						TimeBetweenConnectionAttempts = 800,
						MaxConnectionAttempts = -1,			
				};

				var dsi = new SoftDebuggerStartInfo (startArgs) {
						Command = "",
						Arguments = ""
				};

				if (Terminal != null) Terminal.SSH.WriteLine ("Configuring debugger {0}:{1}",addresslist[0], (int)LocalTunnelPort);

				return dsi;

			}
			catch (Exception ex)
			{

				if (Terminal != null) {
					Terminal.SSH.WriteLine ("SoftDebuggerStartInfo Error {0}", ex.Message);
				} else {
					Gtk.Application.Invoke (delegate {
						using (var md = new MessageDialog (null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, String.Format("SoftDebuggerStartInfo Error {0}", ex.Message))) {
							md.Title = "ProcessScript";
							md.Run ();
							md.Destroy ();
						}
					});	
				}
				return null;
			}
		}

		public void Dispose()
		{	
			if (Terminal!=null) Terminal.Dispose();
		}
			
	}
}

