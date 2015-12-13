using System;
using Gtk;
using Vte;

namespace SSHDebugger
{
	public class windowVTETerminal : Window
	{
		public Vte.Terminal term;

		public windowVTETerminal (String Name, int Columns, int Rows, String FontString)
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
	}
}

