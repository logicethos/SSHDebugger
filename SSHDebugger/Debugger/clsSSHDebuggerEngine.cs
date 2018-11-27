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
using MonoDevelop.Ide;
using System.Linq;
using Gtk;
using System.IO;
using MonoDevelop.Projects;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Debugger;
using Newtonsoft.Json;
using System.Text;
using SSHDebugger.Helpers;

namespace SSHDebugger
{
    public class clsSSHDebuggerEngine : DebuggerEngineBackend
    {
        clsSSHSoftDebuggerSession DebuggerSession = null;
        public static List<clsHost> HostsList = new List<clsHost>();

        clsHost selectedHost = null;
        AutoResetEvent termWait = new AutoResetEvent(false);

        public override bool IsDefaultDebugger(ExecutionCommand cmd)
        {
            return base.IsDefaultDebugger(cmd);
        }

        public override bool CanDebugCommand(ExecutionCommand cmd)
        {
            return true;
        }

        private bool BuildList()
        {
            bool addedNew = false;


            Project project = IdeApp.ProjectOperations.CurrentSelectedProject;

            //Find Startup-Project
            var solution = (IdeApp.ProjectOperations.CurrentSelectedWorkspaceItem as Solution);
            if (solution.StartupItem != null)
                project = solution.StartupItem as Project;

            if(project == null)
            {
                MessageHelper.ShowMessage("SSH Debugger - No Project found", "Cannot start SSH Debugger, because no project found!");
                return addedNew;
            }

            foreach (var file in project.Files.Where(x => x.Name.EndsWith(".ssh.txt")))
            {
                if (!HostsList.Exists(x => x.ScriptPath == file.FilePath))
                {
                    new clsHost(project, file.FilePath);
                    addedNew = true;
                }
            }
            return addedNew;
        }

        public override DebuggerSession CreateSession()
        {
            DebuggerSession = new clsSSHSoftDebuggerSession();
            return DebuggerSession;
        }

        public override DebuggerStartInfo CreateDebuggerStartInfo(ExecutionCommand c)
        {

            SoftDebuggerStartInfo dsi = null;
            try
            {

                //If new host, no host is selected, or ther terminal window is closed
                if (BuildList() || selectedHost == null || selectedHost.Terminal == null)
                {
                    //Load any new templates
                    selectedHost = InvokeSynch<clsHost>(GetDebuggerInfo);  //Query user for selected host
                }

                if (selectedHost != null)
                {

                    if (selectedHost.Terminal == null)
                    {
#if VTE
                        selectedHost.Terminal = new windowTerminalVTE(selectedHost);
#else
							selectedHost.Terminal = new windowTerminalGTK(selectedHost);
#endif
                    }
                    else
                    {
                        selectedHost.Terminal.Front();
                    }

                    var done = new ManualResetEvent(false);
                    Task.Run(() =>
                    {
                        dsi = selectedHost.ProcessScript(true);
                    }).ContinueWith((t) =>
                    {
                        done.Set();
                    });

                    while (true)
                    {
                        Gtk.Application.RunIteration();
                        if (done.WaitOne(0))
                            break;
                    }

                }

                if (dsi != null) selectedHost.Terminal.SSH.WriteLine("Starting debugger");

                return dsi;
            }
            catch (ThreadAbortException)  //User closed terminal (probably)
            {
                return null;
            }
            catch (Exception ex)
            {
                Gtk.Application.Invoke(delegate
                   {
                       using (var md = new MessageDialog(null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, "Terminal error: " + ex.Message))
                       {
                           md.Run();
                           md.Destroy();
                       }
                   });
                return null;
            }

        }

        void OpenTerminal()
        {

        }

        clsHost GetDebuggerInfo()
        {
            ResponseType response;
            String filepath = null;
            clsHost selectedHost = null;

            try
            {

                using (var dlg = new clsDebuggerOptionsDialog())
                {
                    response = (Gtk.ResponseType)dlg.Run();
                    if (dlg.SelectedHost != null)
                    {
                        filepath = dlg.SelectedHost.ScriptPath;
                        selectedHost = dlg.SelectedHost;
                    }
                    dlg.Destroy();
                }

                while (GLib.MainContext.Iteration()) ;

                if (response == Gtk.ResponseType.Accept)
                {

                    Gtk.Application.Invoke(delegate
                   {
                       using (var md = new MessageDialog(null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, "Please add a ssh template file manually to your project"))
                       {
                           md.Run();
                           md.Destroy();
                       }
                   });
                    return null;
                }
                else if (response != Gtk.ResponseType.Ok)
                    return null;

                var properties = PropertyService.Get("MonoDevelop.Debugger.Soft.SSHDebug", new Properties());
                properties.Set("host", filepath);

                return selectedHost;
            }
            catch (Exception ex)
            {
                Gtk.Application.Invoke(delegate
                {
                    using (var md = new MessageDialog(null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, ex.Message))
                    {
                        md.Title = "SoftDebuggerStartInfo";
                        md.Run();
                        md.Destroy();
                    }
                });
                return null;
            }
        }

        static T InvokeSynch<T>(Func<T> func)
        {

            if (MonoDevelop.Core.Runtime.IsMainThread)
                return func();

            var ev = new System.Threading.ManualResetEvent(false);
            T val = default(T);
            Exception caught = null;
            Gtk.Application.Invoke(delegate
            {
                try
                {
                    val = func();
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
                finally
                {
                    ev.Set();
                }
            });
            ev.WaitOne();
            if (caught != null)
                throw caught;
            return val;
        }
    }
}
