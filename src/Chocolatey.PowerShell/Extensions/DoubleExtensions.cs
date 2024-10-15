using System;
using System.Collections.Generic;
using System.Text;

namespace Chocolatey.PowerShell.Extensions
{
    internal static class DoubleExtensions
    {
        internal static string AsFileSizeString(this double size)
        {
            var units = new[] { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB" };
            foreach (var unit in units)
            {
                if (size < 1024)
                {
                    return string.Format("{0:0.##} {1}", size, unit);
                }

                size /= 1024;
            }

            return string.Format("{0:0.##} YB", size);
        }
    }
}
