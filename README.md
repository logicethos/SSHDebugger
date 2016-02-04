# SSH Debugger for Monodevelop & Xamarin Studio

Deploy, execute and debug your .NET apps running on a remote computer, directly from Monodevelop & Xamarin Studio.
Includes a full XTerm console, on the Linux version. Standard text window on Win & Mac.

![alt tag](https://raw.githubusercontent.com/logicethos/SSHDebugger/master/SSHDebugger.png)

Uses:

 * Develop .NET apps for embedded devices and small computers such as the Raspberry Pi.
 * Deploy and debug apps running in a datacentre, docker container or virtual machine.

Features:
  
 * Requires no port forwarding, or special firewall rules. Just ssh access to the host.
 * Built-in XTerm console to support MonoCurses, Console.Output() & Console.Input().
 * Simple pre-debug scripting, to copy your build files to the remote host.
 * Build scripts for different devices, and store them in your project.
 * Secure communication.  Password, or key-pair security.

Steps for use:
 1. Make sure your remote computer has mono installed, and is accessible from ssh.
 2. Add the SSH Debugger template from this add-in to your project, and change the host address.
 3. Add any dependency files to the script (dll's, data etc) for copying (scp or rsync).
 4. Run -> Run With -> SSH Debugger

 See demo https://www.youtube.com/watch?v=W2sZ56q5C8A

Dependencies:

  Windows & Mac: none

  Linux requires Gnome VTE terminal libs for gtk

 * libgnome2.0-cil-dev
 * libvte-dev

Suggested future improvements:

 * Improve Xterm Terminal, flesh out the UI, copy, paste, go fully managed to remove Gnome VTE dependency?
 * Automate template generation, to fill in known dependencies.
 * Default to rsync where available, or scp only files that have changed.
 * C# pre-debug scripting.
 * Option to Detach & Reattach debugger? Reconnect if connection lost?
 * Wizard to prepare a host, test for stability, setup password-less login using private keys.
 * Add more customisation to script (e.g SOCKS support, default Xterm settings).
 * Windows host support
