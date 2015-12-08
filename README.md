# SSHDebugger

Monodevelop Add-in

Execute and debug your .NET apps running on a remote computer, directly from Monodevelop.

Steps:
1. Make sure your your target computer has mono installed, and is accessible from ssh.
2. Add the SSH Debugger template to your project, and change the host address.
3. Run -> Run With -> SSH Debugger

Dependencies:
Gnome VTE terminal libs for gtk

  libgnome2.0-cil-dev
  libvte-dev
