using System;
using System.Collections.Generic;
using System.Text;

namespace Chocolatey.PowerShell.Helpers
{
    internal static class ArchitectureWidth
    {
        internal static int Get()
        {
            return Environment.Is64BitProcess ? 64 : 32;
        }

        internal static bool Matches(int compareTo)
        {
            return Get() == compareTo;
        }
    }
}
