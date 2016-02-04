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
using MonoDevelop.Core;
using System.Linq;

namespace SSHDebugger
{

	public class clsDebuggerOptionsDialog : Gtk.Dialog
	{
	public clsHost SelectedHost;
	Gtk.Button newButton = new Gtk.Button  ("New Host");
	Gtk.Button connectButton = new Gtk.Button ("Run");
	Gtk.ComboBox combo;

	const Gtk.ResponseType connectResponse = Gtk.ResponseType.Ok;
	const Gtk.ResponseType newResponse = Gtk.ResponseType.Accept;


	Properties properties;

	//TODO: dropdown menus for picking string substitutions. also substitutions for port, ip
		public clsDebuggerOptionsDialog () : base (
		"SSH Debug", MonoDevelop.Ide.MessageService.RootWindow,
		Gtk.DialogFlags.DestroyWithParent | Gtk.DialogFlags.Modal)
	{
		properties = PropertyService.Get ("MonoDevelop.Debugger.Soft.SSHDebug", new Properties());

		AddActionWidget (connectButton, connectResponse);
		AddActionWidget (newButton, newResponse);
		AddActionWidget (new Gtk.Button (Gtk.Stock.Cancel), Gtk.ResponseType.Cancel);

		var table = new Gtk.Table (1, 2, false);
		table.BorderWidth = 6;
		VBox.PackStart (table, true, true, 0);

		table.Attach (new Gtk.Label ("Host") { Xalign = 0 }, 	 0, 1, 0, 1);

		var values = clsSSHDebuggerEngine.HostsList.Select (x => String.Format ("{0} ({1})", x.Name, System.IO.Path.GetFileName (x.ScriptPath))).ToArray ();
		combo = new Gtk.ComboBox (values);
		
		int row=0;
		if (clsSSHDebuggerEngine.HostsList.Count == 0) {
				connectButton.Sensitive = false;
		} else {
		
				var lastSelected = clsSSHDebuggerEngine.HostsList.Find (x => x.ScriptPath == properties.Get<string> ("host", ""));
				if (lastSelected != null)
				{
					row = clsSSHDebuggerEngine.HostsList.IndexOf (lastSelected);
					if (row == -1)
						row = 0;
				}
				Gtk.TreeIter iter;
				combo.Model.IterNthChild (out iter, row);
				combo.SetActiveIter (iter);
				SelectedHost = clsSSHDebuggerEngine.HostsList [combo.Active];

				combo.Changed += (object sender, EventArgs e) => 
				{
					SelectedHost = clsSSHDebuggerEngine.HostsList [combo.Active];
				};

		}

		table.Attach (combo, 1, 2, 0, 1);


		VBox.ShowAll ();

	}
	}
}