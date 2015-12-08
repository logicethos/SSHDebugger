# SSHDebugger

Monodevelop Add-in

Execute and debug your .NET apps running on a remote computer, directly from Monodevelop.

![alt tag](https://raw.githubusercontent.com/logicethos/SSHDebugger/master/SSHDebugger.png)

Steps:
 1. Make sure your your target computer has mono installed, and is accessible from ssh.
 2. Add the SSH Debugger template to your project, and change the host address.
 3. Run -> Run With -> SSH Debugger
 
Features:
  
 * Full XTerm console to support MonoCurses apps, and remote keyboard input.
 * Simple pre-debug scripting, to copy your build files to the remote host.
 * Build scripts for different devices.
 * Secure communication for debugging over the internet.
 
Dependencies:

  Gnome VTE terminal libs for gtk

 * libgnome2.0-cil-dev
 * libvte-dev

Future improvments:

* Improve Xterm Terminal (could be a separate standalone project)
* C# pre-debug scripting
* Detach - Reattach debugger?