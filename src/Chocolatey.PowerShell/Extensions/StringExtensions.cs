using System;
using System.Collections.Generic;
using System.Security;
using System.Text;

namespace Chocolatey.PowerShell.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Takes a string and returns a secure string
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        public static SecureString ToSecureStringSafe(this string input)
        {
            var secureString = new SecureString();

            if (string.IsNullOrWhiteSpace(input)) return secureString;

            foreach (char character in input)
            {
                secureString.AppendChar(character);
            }

            return secureString;
        }
    }
}
