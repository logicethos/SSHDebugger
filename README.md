# SSH Debugger Monodevelop Add-in

Execute and debug your .NET apps running on a remote computer, directly from Monodevelop.

![alt tag](https://raw.githubusercontent.com/logicethos/SSHDebugger/master/SSHDebugger.png)

Uses:

 * Develop .NET apps for embedded devices.
 * Deploy and debug apps running in a datacentre, or virtual machine.

Features:
  
 * Full XTerm console to support MonoCurses apps, and remote keyboard input.
 * Simple pre-debug scripting, to copy your build files to the remote host.
 * Build scripts for different devices.
 * Secure communication for debugging over the internet.

Steps for use:
 1. Make sure your your computer has mono installed, and is accessible from ssh.
 2. Add the SSH Debugger template from this add-in to your project, and change the host address.
 3. Run -> Run With -> SSH Debugger
  
Dependencies:

  Gnome VTE terminal libs for gtk

 * libgnome2.0-cil-dev
 * libvte-dev

Suggested future improvements:

 * Improve Xterm Terminal, flesh out the UI, copy, paste, go fully managed to remove Gnome VTE dependency?
 * C# pre-debug scripting
 * Option to Detach & Reattach debugger?
 * Wizard to prepare a host, setup password-less login & customise script.