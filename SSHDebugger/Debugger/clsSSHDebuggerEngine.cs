// 
// clsSSHDebuggerEngine.cs
//  
// Author:
//       Michael Hutchinson <mhutchinson@novell.com>
//		 Stuart Johnson <stuart@logicethos.com>
// 
// Copyright (c) 2010 Novell, Inc.
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
using System.Diagnostics;
using Mono.Debugging.Client;
using MonoDevelop.Core;
using MonoDevelop.Core.Execution;
using System.Net;
using System.Collections.Generic;
using Mono.Debugging.Soft;
using MonoDevelop.Debugger;
using MonoDevelop.Ide;
using System.Linq;
using Gtk;
using System.IO;
using MonoDevelop.Projects;
using System.Threading;
using System.Threading.Tasks;


namespace SSHDebugger
{
	public class clsSSHDebuggerEngine: DebuggerEngineBackend
	{
		clsSSHSoftDebuggerSession DebuggerSession = null;
		public static List<clsHost> HostsList = new List<clsHost>();
		public clsSSHTerminal sshTerminal = null;
		clsHost selectedHost = null;
		ManualResetEvent termWait = new ManualResetEvent (false);

		public override bool CanDebugCommand (ExecutionCommand cmd)
		{			
			return true;
		}

		public void BuildList()
		{
			HostsList.Clear ();
			foreach (var file in IdeApp.ProjectOperations.CurrentSelectedProject.Files.Where(x => x.Name.EndsWith(".ssh.txt")))
			{
				new clsHost(file.FilePath);
			}
		}

		public override DebuggerSession CreateSession ()
		{
			DebuggerSession = new clsSSHSoftDebuggerSession ();
			return DebuggerSession;
		}

		public override DebuggerStartInfo CreateDebuggerStartInfo (ExecutionCommand c)
		{

			SoftDebuggerStartInfo dsi = null;
			try{
	
				BuildList ();
				selectedHost = InvokeSynch<clsHost> (GetDebuggerInfo);
	
				if (selectedHost != null) {

					var threadstart = new ThreadStart (OpenTerminal);
					Thread windowThread = new Thread(threadstart);					 
					windowThread.IsBackground = false;
					windowThread.Start();
				

					if (!termWait.WaitOne(1000) || sshTerminal==null)
					{
						Gtk.Application.Invoke (delegate
							{
								using (var md = new MessageDialog(null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, "Unable to start VTE terminal"))
								{
									md.Run ();
									md.Destroy();
								}
							});
						return null;
					}
	
					sshTerminal.DeleteEvent += (o, args) => 
					{
						DebuggerSession.Exit();
					};
	
					dsi = selectedHost.StartScript(sshTerminal);
				
				}
				sshTerminal.WriteLine("Starting debugger");
				return dsi;
			}
			catch (Exception ex)
			{
				sshTerminal.WriteLine("CreateDebuggerStartInfo: Error {0}",ex.Message);
				return null;
			}
			
		}

		void OpenTerminal()
		{
			Gtk.Application.Invoke (delegate {
				sshTerminal = new clsSSHTerminal (selectedHost);
				sshTerminal.Show();
				while (GLib.MainContext.Iteration ());
				termWait.Set();
			});
		}

		clsHost GetDebuggerInfo ()
		{
			ResponseType response;
			String filepath = null;
			clsHost selectedHost = null;

			try {
				
				using (var dlg = new clsDebuggerOptionsDialog ())
				{
					response = (Gtk.ResponseType) dlg.Run();
					if (dlg.SelectedHost!=null)
					{
						filepath = dlg.SelectedHost.ScriptPath;
						selectedHost = dlg.SelectedHost;
					}
					dlg.Destroy();
				}


				while (GLib.MainContext.Iteration ());

					
				if (response == Gtk.ResponseType.Accept) {

					Gtk.Application.Invoke (delegate
					{
						using (var md = new MessageDialog(null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, "Please add a ssh template file manually to your project"))
						{
						md.Run ();
						md.Destroy();
						}
					});
					return null;
				} else if (response != Gtk.ResponseType.Ok)
					return null;

				var properties = PropertyService.Get ("MonoDevelop.Debugger.Soft.SSHDebug", new Properties ());
				properties.Set ("host", filepath);

				return selectedHost;
			}
			catch(Exception ex) {
				Gtk.Application.Invoke (delegate {
					using (var md = new MessageDialog (null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, ex.Message)) {
						md.Title = "SoftDebuggerStartInfo";
						md.Run ();
						md.Destroy();
					}
				});
				return null;
			}
		}

		static T InvokeSynch<T> (Func<T> func)
		{
			if (MonoDevelop.Ide.DispatchService.IsGuiThread)
				return func ();

			var ev = new System.Threading.ManualResetEvent (false);
			T val = default (T);
			Exception caught = null;
			Gtk.Application.Invoke (delegate {
				try {
					val = func ();
				} catch (Exception ex) {
					caught = ex;
				} finally {
					ev.Set ();
				}
			});
			ev.WaitOne ();
			if (caught != null)
				throw caught;
			return val;
		}
	}
}
