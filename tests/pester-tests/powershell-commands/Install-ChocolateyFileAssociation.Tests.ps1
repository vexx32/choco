Describe 'Install-ChocolateyFileAssociation helper function tests' -Tags InstallChocolateyFileAssociation, Cmdlets {
    BeforeAll {
        Initialize-ChocolateyTestInstall

        $testLocation = Get-ChocolateyTestLocation
        Import-Module "$testLocation\helpers\chocolateyInstaller.psm1"

        $machineClasses = 'SOFTWARE\Classes'

        function Get-ClassesValue {
            [CmdletBinding()]
            param([Microsoft.Win32.RegistryKey] $Root, [string] $Path)
            $key = $Root.OpenSubKey($Path)
            if ($null -ne $key) {
                try {
                    $key.GetValue('', $null, 'DoNotExpandEnvironmentNames')
                }
                finally {
                    $key.Dispose()
                }
            }
        }

        function Get-MachineClassesValue {
            [CmdletBinding()]
            param([string] $Path)
            Get-ClassesValue -Root ([Microsoft.Win32.Registry]::LocalMachine) -Path "$machineClasses\$Path"
        }

        function Get-MachineClassesValueKind {
            [CmdletBinding()]
            param([string] $Path)
            $key = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey("$machineClasses\$Path")
            try {
                $key.GetValueKind('')
            }
            finally {
                $key.Dispose()
            }
        }

        function Remove-Association([string] $Extension, [string] $FileType) {
            foreach ($path in $Extension, $FileType) {
                [Microsoft.Win32.Registry]::LocalMachine.DeleteSubKeyTree("$machineClasses\$path", $false)
                [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree("Software\Classes\$path", $false)
            }
        }

        $toolDirectory = Join-Path $TestDrive 'Pester Tools'
        $null = New-Item -Path $toolDirectory -ItemType Directory
        $executable = Join-Path $toolDirectory "pester's tool.exe"
        Set-Content -Path $executable -Value ''
        $fileType = "pester's_tool.exe"
    }

    AfterAll {
        Remove-Module "chocolateyInstaller" -Force
    }

    Context 'Unit tests' -Tags WhatIf {
        BeforeAll {
            $Preamble = [scriptblock]::Create("Import-Module '$testLocation\helpers\chocolateyInstaller.psm1' -Global")
            $Extension = '.chocolatey-pester-whatif'
        }

        It 'Asks to associate the extension, adding the leading dot' {
            $escapedExecutable = $executable.Replace("'", "''")
            $Command = [scriptblock]::Create("Install-ChocolateyFileAssociation -Extension 'chocolatey-pester-whatif' -Executable '$escapedExecutable' -WhatIf")

            $results = Get-WhatIfResult -Preamble $Preamble -Command $Command
            $results.WhatIf | Should -HaveCount 1 -Because $results.Output
            $results.WhatIf[0] | Should -BeExactly "What if: Performing the operation ""Associate with '$executable'"" on target ""$Extension""." -Because $results.Output
        }

        It 'Asks to associate the extension with a path that uses an environment variable, as it was given' {
            $Command = [scriptblock]::Create("`$env:ChocolateyPesterToolDir = '$($toolDirectory.Replace("'", "''"))'; Install-ChocolateyFileAssociation -Extension '$Extension' -Executable '%ChocolateyPesterToolDir%\pester''s tool.exe' -WhatIf")

            $results = Get-WhatIfResult -Preamble $Preamble -Command $Command
            $results.WhatIf | Should -HaveCount 1 -Because $results.Output
            $results.WhatIf[0] | Should -BeExactly "What if: Performing the operation ""Associate with '%ChocolateyPesterToolDir%\pester's tool.exe'"" on target ""$Extension""." -Because $results.Output
        }

        It 'Does not associate the extension' {
            Get-MachineClassesValue $Extension | Should -BeNullOrEmpty
        }
    }

    Context 'Errors' {
        It 'Fails when the executable does not exist' {
            $missing = Join-Path $TestDrive 'missing.exe'

            { Install-ChocolateyFileAssociation -Extension '.chocolatey-pester-error' -Executable $missing } |
                Should -Throw -ExpectedMessage "'$missing' does not exist, cannot create file association"
        }

        It 'Fails when the executable is a directory' {
            { Install-ChocolateyFileAssociation -Extension '.chocolatey-pester-error' -Executable $toolDirectory } |
                Should -Throw -ExpectedMessage "'$toolDirectory' does not exist, cannot create file association"
        }

        It 'Fails when the extension is only whitespace' {
            { Install-ChocolateyFileAssociation -Extension '   ' -Executable $executable } |
                Should -Throw -ExpectedMessage 'The extension cannot be empty or only whitespace.*'
        }

        It 'Does not associate the extension' {
            Get-MachineClassesValue '.chocolatey-pester-error' | Should -BeNullOrEmpty
        }
    }

    Context 'Associating an extension' -Tag VMOnly {
        BeforeAll {
            $Extension = '.chocolatey-pester'
            Remove-Association -Extension $Extension -FileType $fileType

            Install-ChocolateyFileAssociation -Extension ' chocolatey-pester ' -Executable $executable
        }

        AfterAll {
            Remove-Association -Extension $Extension -FileType $fileType
        }

        It 'Points the extension at the file type' {
            Get-MachineClassesValue $Extension | Should -BeExactly $fileType
        }

        It 'Sets the open command to the quoted executable and the default arguments' {
            Get-MachineClassesValue "$fileType\shell\open\command" | Should -BeExactly """$executable"" ""%1"" ""%*"""
        }

        It 'Writes the open command as an expandable string' {
            Get-MachineClassesValueKind "$fileType\shell\open\command" | Should -Be 'ExpandString'
        }

        It 'Labels the file type' {
            Get-ClassesValue -Root ([Microsoft.Win32.Registry]::ClassesRoot) -Path $fileType | Should -BeExactly "$fileType file"
        }

        It 'Is reported by assoc and ftype' {
            cmd /c "assoc $Extension" | Should -BeExactly "$Extension=$fileType"
            cmd /c "ftype $fileType" | Should -BeExactly "$fileType=""$executable"" ""%1"" ""%*"""
        }
    }

    Context 'Associating an extension with a relative path and positional arguments' -Tag VMOnly {
        BeforeAll {
            $Extension = '.chocolatey-pester-relative'
            Remove-Association -Extension $Extension -FileType $fileType

            Push-Location $toolDirectory
            try {
                Install-ChocolateyFileAssociation $Extension ".\pester's tool.exe" '-o "%1"'
            }
            finally {
                Pop-Location
            }
        }

        AfterAll {
            Remove-Association -Extension $Extension -FileType $fileType
        }

        It 'Sets the open command with the full path and the given arguments' {
            Get-MachineClassesValue "$fileType\shell\open\command" | Should -BeExactly """$executable"" -o ""%1"""
        }
    }

    Context 'Associating an extension with a path that uses an environment variable' -Tag VMOnly {
        BeforeAll {
            $Extension = '.chocolatey-pester-variable'
            Remove-Association -Extension $Extension -FileType $fileType

            $env:ChocolateyPesterToolDir = $toolDirectory
            Install-ChocolateyFileAssociation -Extension $Extension -Executable "%ChocolateyPesterToolDir%\pester's tool.exe"
        }

        AfterAll {
            Remove-Item -Path Env:\ChocolateyPesterToolDir -ErrorAction SilentlyContinue
            Remove-Association -Extension $Extension -FileType $fileType
        }

        It 'Sets the open command with the path as it was given' {
            Get-MachineClassesValue "$fileType\shell\open\command" | Should -BeExactly """%ChocolateyPesterToolDir%\pester's tool.exe"" ""%1"" ""%*"""
        }

        It 'Writes the open command as an expandable string' {
            Get-MachineClassesValueKind "$fileType\shell\open\command" | Should -Be 'ExpandString'
        }
    }

    Context 'Failing for a path with an environment variable that is not set' {
        It 'Fails, naming the path as it was given' {
            { Install-ChocolateyFileAssociation -Extension '.chocolatey-pester-error' -Executable '%ChocolateyPesterUndefined%\tool.exe' } |
                Should -Throw -ExpectedMessage "'%ChocolateyPesterUndefined%\tool.exe' does not exist, cannot create file association"
        }
    }
}
