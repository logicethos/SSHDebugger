using System;
using Gtk;
using MonoDevelop.Ide;

namespace SSHDebugger.Helpers
{
    public static class MessageHelper
    {
        public static void ShowMessage(string title, string message)
        {
            Gtk.Application.Invoke(delegate
            {
                using (var md = new MessageDialog(IdeApp.Workbench.RootWindow, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, ""))
                {
                    md.Title = title;
                    md.Text = message;
                    md.Run();
                    md.Destroy();
                }
            });
        }
    }
}
