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
using Gtk;
using Vte;
using System.Collections;
using System.Threading;

namespace SSHDebugger
{
	public class clsTerminal :  Window 
	{
		Vte.Terminal term; 

		public delegate void KeyPressHandler(Gdk.Key key);
		public event KeyPressHandler KeyPress;

		public Gdk.Key LastKeyPress { get; private set;}
		public bool UserInputMode {get; private set;}

		AutoResetEvent userkeypress = new AutoResetEvent (false);

		public clsTerminal(String Name, int Columns, int Rows, String FontString) : base(Name)
		{
			HBox hbox = new HBox ();
			term = new Terminal ();
			term.CursorBlinks = true;
			term.MouseAutohide = false;
			term.ScrollOnKeystroke = true;
			term.DeleteBinding = TerminalEraseBinding.Auto;
			term.BackspaceBinding = TerminalEraseBinding.Auto;			
			term.FontFromString = FontString;
			term.Emulation = "xterm";
			term.Encoding = "UTF-8";

			term.SetSize(Columns,Rows);

			VScrollbar vscroll = new VScrollbar (term.Adjustment);
			hbox.PackStart (term);
			hbox.PackStart (vscroll);

//			Gdk.Color white = new Gdk.Color ();
//			Gdk.Color.Parse ("white", ref white);
//
//			Gdk.Color black = new Gdk.Color ();
//			Gdk.Color.Parse ("black", ref black);
//			term.SetColors (black, white, new Gdk.Color[]{}, 16);


			term.ButtonPressEvent += (o, args) => 
			{
				Write(args.Event.Button.ToString());
			};

			this.CanFocus = true;

			term.Show ();
			hbox.Show ();
			vscroll.Show ();
			this.Add (hbox);
			ShowAll ();

		}


		[GLib.ConnectBefore]
		protected override bool OnKeyPressEvent (Gdk.EventKey evnt) {
			LastKeyPress = evnt.Key;
			userkeypress.Set ();
			if (!UserInputMode && KeyPress!=null) KeyPress.Invoke(evnt.Key);
			return base.OnKeyPressEvent (evnt);
 		}


		public String RequestUserInput(String prompt, String echo=null)
		{
			UserInputMode = true;
			Write (prompt);
			String input = "";
			while (userkeypress.WaitOne ())
			{				
				if (LastKeyPress == Gdk.Key.BackSpace) {
					if (input.Length > 0) {
						input = input.Substring (0, input.Length - 1);
						if (echo != "") Write ("\b \b");
					}
				} else if (LastKeyPress == Gdk.Key.Return) {
					Write ("\r\n");
					break;
				} else {
					Write (echo ?? LastKeyPress.ToString());
					input += LastKeyPress;
				}
			}
			UserInputMode = false;
			return input;

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
			Gtk.Application.Invoke (delegate {
				term.Feed (output);
			});

		}

		public override void Dispose()
		{
			userkeypress.Dispose();
			base.Dispose();
		}

	}
}