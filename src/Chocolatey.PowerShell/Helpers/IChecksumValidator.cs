// Copyright © 2017 - 2024 Chocolatey Software, Inc
// Copyright © 2011 - 2017 RealDimensions Software, LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
//
// You may obtain a copy of the License at
//
// 	http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.


using Chocolatey.PowerShell.Shared;
using System;

namespace Chocolatey.PowerShell.Helpers
{
    public interface IChecksumValidator
    {
        /// <summary>
        /// Validate the checksum of a file against an expected <paramref name="checksum"/> and throw if the checksum does not match.
        /// </summary>
        /// <param name="cmdlet">The cmdlet calling the method.</param>
        /// <param name="path">The path to the file to verify the checksum for.</param>
        /// <param name="checksum">The expected checksum value.</param>
        /// <param name="checksumType">The type of the checksum to look for.</param>
        /// <param name="url">The url the file was downloaded from originally, if any.</param>
        void AssertChecksumValid(string path, string checksum, ChecksumType? checksumType, string url);

        /// <summary>
        /// Tests whether a given <paramref name="checksum"/> matches the checksum of a given file.
        /// </summary>
        /// <param name="Cmdlet">The cmdlet calling the method.</param>
        /// <param name="path">The path to the file to verify the checksum of.</param>
        /// <param name="checksum">The checksum value to validate against.</param>
        /// <param name="checksumType">The type of the checksum.</param>
        /// <param name="url">The original url that the file was downloaded from, if any.</param>
        /// <param name="error">If this method returns false, this will contain an exception that can be raised if needed.</param>
        /// <returns>True if the actual checksum of the file matches the given checksum, otherwise False.</returns>
        bool IsValid(string path, string checksum, ChecksumType? checksumType, string url, out Exception error);
    }
}