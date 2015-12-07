using System;
using MonoDevelop.Core.Execution;
using Mono.Debugging.Client;
using Mono.Debugging.Soft;
using Gtk;
using MonoDevelop.Core;
using System.Diagnostics;
using System.Collections.Generic;

namespace SSHDebugger
{
	class clsSSHSoftDebuggerSession : SoftDebuggerSession
	{
		protected override void OnRun (DebuggerStartInfo startInfo)
		{
			try{
				if (startInfo == null) {
					EndSession ();
					return;
				}

				base.OnRun (startInfo);
			}catch (Exception ex) {							
				Gtk.Application.Invoke (delegate {
					using (var md = new MessageDialog (null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, ex.Message)) {
						md.Title = "CustomSoftDebuggerSession";
						md.Run ();
						md.Destroy ();
					}
				});
			}
		}


		protected override void EndSession ()
		{
			base.EndSession ();

		}

	}
}

