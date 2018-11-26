using System;
namespace SSHDebugger.Extensions
{
    public static class ExtGdkKey
    {
        public static char GetChar(this Gdk.Key key)
        {
            return (char)Gdk.Keyval.ToUnicode((uint)key);
        }
    }
}
