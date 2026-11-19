// Copyright © 2017 - 2026 Chocolatey Software, Inc
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

using System;
using System.IO;
using System.Linq;
using Chocolatey.PowerShell.Helpers;
using FluentAssertions;
using Microsoft.Win32;

namespace Chocolatey.PowerShell.Tests.Helpers
{
    // ReSharper disable InconsistentNaming

    public class RegistryHelperFileAssociationSpecs
    {
        public class When_getting_the_extension_to_associate : TinySpec
        {
            public override void Context()
            {
            }

            public override void Because()
            {
            }

            [InlineData("txt", ".txt")]
            [InlineData(".txt", ".txt")]
            [InlineData("  txt  ", ".txt")]
            [InlineData(" .log ", ".log")]
            public void Should_trim_the_extension_and_start_it_with_a_dot(string extension, string expected)
            {
                RegistryHelper.GetFileAssociationExtension(extension).Should().Be(expected);
            }
        }

        public class When_getting_the_file_type_to_associate_with : TinySpec
        {
            public override void Context()
            {
            }

            public override void Because()
            {
            }

            [InlineData(@"C:\tools\tool.exe", "tool.exe")]
            [InlineData(@"C:\Program Files\My App\my app.exe", "my_app.exe")]
            public void Should_use_the_file_name_with_spaces_replaced_by_underscores(string executable, string expected)
            {
                RegistryHelper.GetFileAssociationType(executable).Should().Be(expected);
            }
        }

        /// <summary>
        /// Resolves relative paths from <c>C:\work</c>, and treats only <see cref="ExistingFiles"/> as existing.
        /// </summary>
        public class When_getting_the_executable_to_register : EnvironmentSpecsBase
        {
            private static readonly string[] ExistingFiles =
            {
                @"C:\Tools\tool.exe",
                @"C:\work\tool.exe",
                @"C:\work\tools\tool.exe",
            };

            protected override string[] ManagedVariables => new[]
            {
                "ChocolateyTestsToolRoot",
                "ChocolateyTestsToolFolder",
                "ChocolateyTestsUndefined",
            };

            public override void Context()
            {
                base.Context();

                Environment.SetEnvironmentVariable("ChocolateyTestsToolRoot", @"C:\Tools");
                Environment.SetEnvironmentVariable("ChocolateyTestsToolFolder", "tools");
            }

            public override void Because()
            {
            }

            private static string GetExecutable(string executable)
            {
                return RegistryHelper.GetFileAssociationExecutable(
                    executable,
                    path => Path.GetFullPath(Path.Combine(@"C:\work", path)),
                    path => ExistingFiles.Contains(path, StringComparer.OrdinalIgnoreCase));
            }

            [Fact]
            public void Should_keep_a_path_with_variables_that_expands_to_the_full_path()
            {
                GetExecutable(@"%ChocolateyTestsToolRoot%\tool.exe").Should().Be(@"%ChocolateyTestsToolRoot%\tool.exe");
            }

            [Fact]
            public void Should_compare_the_expanded_and_full_paths_without_regard_to_case()
            {
                RegistryHelper.GetFileAssociationExecutable(@"%ChocolateyTestsToolRoot%\tool.exe", path => path.ToUpperInvariant(), path => true)
                    .Should().Be(@"%ChocolateyTestsToolRoot%\tool.exe");
            }

            [InlineData(@"C:\Tools\tool.exe", @"C:\Tools\tool.exe")]
            [InlineData(@"tool.exe", @"C:\work\tool.exe")]
            [InlineData(@"%ChocolateyTestsToolFolder%\tool.exe", @"C:\work\tools\tool.exe")]
            [InlineData(@"%ChocolateyTestsToolRoot%\..\Tools\tool.exe", @"C:\Tools\tool.exe")]
            public void Should_use_the_full_path_for_anything_else(string executable, string expected)
            {
                GetExecutable(executable).Should().Be(expected);
            }

            [InlineData(@"C:\Tools\missing.exe")]
            [InlineData(@"%ChocolateyTestsUndefined%\tool.exe")]
            [InlineData(@"C:\Tools")]
            public void Should_throw_naming_the_path_as_given_when_there_is_no_file(string executable)
            {
                Action getExecutable = () => GetExecutable(executable);

                getExecutable.Should().Throw<FileNotFoundException>()
                    .WithMessage($"'{executable}' does not exist, cannot create file association");
            }
        }

        /// <summary>
        /// Gives each spec two temporary keys under <c>HKCU\Software\ChocolateyTests</c>, standing in for
        /// <c>HKEY_LOCAL_MACHINE\SOFTWARE\Classes</c> and <c>HKEY_CLASSES_ROOT</c>, removed after the spec. Derived
        /// specs create an association under them in <c>Because()</c> and read the values back.
        /// </summary>
        public abstract class FileAssociationSpecsBase : TinySpec
        {
            private string _rootPath;
            protected RegistryKey MachineClasses;
            protected RegistryKey ClassesRoot;

            public override void Context()
            {
                _rootPath = $@"Software\ChocolateyTests\{Guid.NewGuid():N}";
                MachineClasses = Registry.CurrentUser.CreateSubKey($@"{_rootPath}\machine");
                ClassesRoot = Registry.CurrentUser.CreateSubKey($@"{_rootPath}\classes");
            }

            public override void AfterObservations()
            {
                MachineClasses?.Dispose();
                ClassesRoot?.Dispose();
                Registry.CurrentUser.DeleteSubKeyTree(_rootPath, throwOnMissingSubKey: false);
            }

            protected void SetFileAssociation(string extension, string executable, string arguments)
            {
                RegistryHelper.SetFileAssociation(MachineClasses, ClassesRoot, extension, executable, arguments);
            }

            protected static bool KeyExists(RegistryKey root, string path)
            {
                using (var key = root.OpenSubKey(path))
                {
                    return key != null;
                }
            }

            protected static object GetValue(RegistryKey root, string path, string name = "")
            {
                using (var key = root.OpenSubKey(path))
                {
                    return key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                }
            }

            protected static RegistryValueKind GetValueKind(RegistryKey root, string path)
            {
                using (var key = root.OpenSubKey(path))
                {
                    return key.GetValueKind(string.Empty);
                }
            }
        }

        public class When_associating_an_extension : FileAssociationSpecsBase
        {
            public override void Because()
            {
                SetFileAssociation(".txt", @"C:\Program Files\My App\my app.exe", @"""%1"" ""%*""");
            }

            [Fact]
            public void Should_set_the_file_type_as_the_default_value_of_the_extension_key()
            {
                GetValue(MachineClasses, ".txt").Should().Be("my_app.exe");
                GetValueKind(MachineClasses, ".txt").Should().Be(RegistryValueKind.String);
            }

            [Fact]
            public void Should_set_the_quoted_executable_and_arguments_as_the_open_command_of_the_file_type()
            {
                GetValue(MachineClasses, @"my_app.exe\shell\open\command").Should().Be(@"""C:\Program Files\My App\my app.exe"" ""%1"" ""%*""");
            }

            [Fact]
            public void Should_write_the_open_command_as_an_expandable_string()
            {
                GetValueKind(MachineClasses, @"my_app.exe\shell\open\command").Should().Be(RegistryValueKind.ExpandString);
            }

            [Fact]
            public void Should_set_the_label_of_the_file_type_under_the_classes_root()
            {
                GetValue(ClassesRoot, "my_app.exe").Should().Be("my_app.exe file");
                GetValueKind(ClassesRoot, "my_app.exe").Should().Be(RegistryValueKind.String);
            }

            [Fact]
            public void Should_create_the_file_type_key_under_the_machine_classes_without_a_label()
            {
                KeyExists(MachineClasses, "my_app.exe").Should().BeTrue();
                GetValue(MachineClasses, "my_app.exe").Should().BeNull();
            }

            [Fact]
            public void Should_write_nothing_else_under_the_classes_root()
            {
                ClassesRoot.GetSubKeyNames().Should().BeEquivalentTo("my_app.exe");
                KeyExists(ClassesRoot, @"my_app.exe\shell").Should().BeFalse();
            }
        }

        public class When_associating_an_extension_with_other_arguments : FileAssociationSpecsBase
        {
            public override void Because()
            {
                SetFileAssociation(".md", @"C:\tools\tool.exe", @"-o ""%1""");
            }

            [Fact]
            public void Should_put_the_arguments_after_the_quoted_executable()
            {
                GetValue(MachineClasses, @"tool.exe\shell\open\command").Should().Be(@"""C:\tools\tool.exe"" -o ""%1""");
            }
        }

        public class When_associating_an_extension_without_arguments : FileAssociationSpecsBase
        {
            public override void Because()
            {
                SetFileAssociation(".md", @"C:\tools\tool.exe", string.Empty);
            }

            [Fact]
            public void Should_set_only_the_quoted_executable_as_the_open_command()
            {
                GetValue(MachineClasses, @"tool.exe\shell\open\command").Should().Be(@"""C:\tools\tool.exe""");
            }
        }

        public class When_associating_an_extension_with_quotes_and_variables : FileAssociationSpecsBase
        {
            public override void Because()
            {
                SetFileAssociation(".log", @"C:\Tools\O'Brien\$tool.exe", @"--name='x' ""%1""");
            }

            [Fact]
            public void Should_keep_the_executable_and_arguments_as_they_were_given()
            {
                GetValue(MachineClasses, @"$tool.exe\shell\open\command").Should().Be(@"""C:\Tools\O'Brien\$tool.exe"" --name='x' ""%1""");
            }

            [Fact]
            public void Should_point_the_extension_at_the_file_type()
            {
                GetValue(MachineClasses, ".log").Should().Be("$tool.exe");
            }
        }

        public class When_replacing_an_existing_association : FileAssociationSpecsBase
        {
            public override void Context()
            {
                base.Context();

                using (var extensionKey = MachineClasses.CreateSubKey(".txt"))
                {
                    extensionKey.SetValue(string.Empty, "txtfile");
                    extensionKey.SetValue("Content Type", "text/plain");
                }

                using (var labelKey = ClassesRoot.CreateSubKey("tool.exe"))
                {
                    labelKey.SetValue(string.Empty, "Old label");
                }
            }

            public override void Because()
            {
                SetFileAssociation(".txt", @"C:\tools\tool.exe", @"""%1""");
            }

            [Fact]
            public void Should_replace_the_file_type_of_the_extension()
            {
                GetValue(MachineClasses, ".txt").Should().Be("tool.exe");
            }

            [Fact]
            public void Should_keep_the_other_values_of_the_extension_key()
            {
                GetValue(MachineClasses, ".txt", "Content Type").Should().Be("text/plain");
            }

            [Fact]
            public void Should_replace_the_label_of_the_file_type()
            {
                GetValue(ClassesRoot, "tool.exe").Should().Be("tool.exe file");
            }
        }
    }
}
