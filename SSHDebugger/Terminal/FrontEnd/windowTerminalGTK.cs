// 
// windowTerminalGTK.cs
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
using System.Text;
using Pango;

#if !VTE

using System;
using Gtk;
using System.Collections;
using System.Threading;

namespace SSHDebugger
{
	public class windowTerminalGTK :  Window, ITerminal
	{
		public TextView textview1; 

		public clsSSHTerminal SSH {get; private set;}

		clsHost	Host;


		public windowTerminalGTK(clsHost host) : base(host.Name)
		{

			SSH = new clsSSHTerminal(host);
			Host = host;
			
			ScrolledWindow scrolledWindow = new ScrolledWindow(); 
			textview1 = new TextView();

			this.SetSizeRequest(800,600);

			scrolledWindow.Add(textview1);
			textview1.ModifyFont(FontDescription.FromString(host.TerminalFont));


			this.Add(scrolledWindow);

			this.CanFocus = true;

			ShowAll ();

			SSH.TerminalData += (string text) => 
			{
				Gtk.Application.Invoke (delegate {
					TextIter mIter = textview1.Buffer.EndIter;
					textview1.Buffer.Insert(ref mIter, text);
					textview1.ScrollToIter(textview1.Buffer.EndIter, 0, false, 0, 0);
				});
			};
		}



		[GLib.ConnectBefore]
		protected override bool OnKeyPressEvent (Gdk.EventKey evnt) {
			SSH.ShellSend(evnt.Key);
			if (SSH.LocalEcho)
				return base.OnKeyPressEvent (evnt);
			 else
				return false;

 		}


		public void Front()
		{
			Gtk.Application.Invoke (delegate {
				base.Present();
				textview1.CanFocus = true;
			});
		}

		protected override void OnDestroyed ()
		{
			Host.Terminal = null;
			base.OnDestroyed ();
		}

		public override void Dispose()
		{
			textview1.Dispose();
			SSH.Dispose();
			base.Dispose();
		}

	}
}
#endif