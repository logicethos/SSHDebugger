// 
// windowTerminalVTE.cs
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

#if VTE

using System;
using Gtk;
using Vte;
using System.Collections;
using System.Threading;

namespace SSHDebugger
{
	public class windowTerminalVTE :  Window, ITerminal
	{
		public Vte.Terminal term; 

		public clsSSHTerminal SSH {get; private set;}

		clsHost	Host;


		public windowTerminalVTE(clsHost host) : base(host.Name)
		{

			SSH = new clsSSHTerminal(host);

			Host = host;
			HBox hbox = new HBox ();
			term = new Terminal ();
			term.CursorBlinks = true;
			term.MouseAutohide = false;
			term.ScrollOnKeystroke = true;
			term.DeleteBinding = TerminalEraseBinding.Auto;
			term.BackspaceBinding = TerminalEraseBinding.Auto;			
			term.FontFromString = host.TerminalFont;
			term.Emulation = "xterm";
			term.Encoding = "UTF-8";

			term.SetSize(host.TerminalCols,host.TerminalRows);

			VScrollbar vscroll = new VScrollbar (term.Adjustment);
			hbox.PackStart (term);
			hbox.PackStart (vscroll);

			this.CanFocus = true;

			this.Add (hbox);
			ShowAll ();

			SSH.TerminalData += (string text) => 
			{
				Gtk.Application.Invoke (delegate {
					term.Feed(text);
				});
			};

		}


		[GLib.ConnectBefore]
		protected override bool OnKeyPressEvent (Gdk.EventKey evnt) {
			SSH.ShellSend(evnt.Key);
			return base.OnKeyPressEvent (evnt);
 		}

		public String RequestUserInput(String prompt, String echo=null)
		{
			return SSH.RequestUserInput(prompt,echo);
		}

		protected override void OnDestroyed ()
		{
			Host.Terminal = null;
			base.OnDestroyed ();
		}

		public void Front()
		{
			Gtk.Application.Invoke (delegate {
				base.Present();
				term.CanFocus = true;
			});
		}

		public override void Dispose()
		{
			term.Dispose();
			SSH.Dispose();
			base.Dispose();
		}

	}
}
#endif