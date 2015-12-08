using System;
using Mono.Addins;
using Mono.Addins.Description;

[assembly:Addin (
	"SSHDebugger", 
	Namespace = "SSHDebugger",
	Version = "0.1"
)]

[assembly:AddinName ("SSHDebugger")]
[assembly:AddinCategory ("IDE extensions")]
[assembly:AddinDescription ("SSHDebugger")]
[assembly:AddinAuthor ("Logic Ethos Ltd")]

[assembly:AddinDependency ("::MonoDevelop.Core", MonoDevelop.BuildInfo.Version)]
[assembly:AddinDependency ("::MonoDevelop.Ide", MonoDevelop.BuildInfo.Version)]
[assembly:AddinDependency ("::MonoDevelop.Debugger", MonoDevelop.BuildInfo.Version)]

[assembly:Mono.Addins.ImportAddinAssembly("Renci.SshNet.dll")]
[assembly:Mono.Addins.ImportAddinAssembly("vte-sharp.dll")]
