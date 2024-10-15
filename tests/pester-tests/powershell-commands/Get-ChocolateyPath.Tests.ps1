Describe 'Get-ChocolateyPath helper function tests' -Tags Cmdlets, GetChocolateyPath {
    BeforeAll {
        Initialize-ChocolateyTestInstall

        $testLocation = Get-ChocolateyTestLocation
        Import-Module "$testLocation\helpers\chocolateyInstaller.psm1"

        if (-not $env:ChocolateyInstall) {
            $env:ChocolateyInstall = $testLocation
        }
    }

    Context '-PathType InstallPath' {
        BeforeAll {
            $programData = $env:ProgramData
            $systemDrive = $env:SystemDrive
        }

        AfterAll {
            $env:ChocolateyInstall = $testLocation
            $env:ProgramData = $programData
            $env:SystemDrive = $systemDrive
        }

        It 'Returns the value of $env:ChocolateyInstall if set' {
            $env:ChocolateyInstall | Should -Not -BeNullOrEmpty -Because 'it should be set for this test'
            Get-ChocolateyPath -PathType InstallPath | Should -BeExactly $env:ChocolateyInstall
        }

        It 'Falls back to $env:ProgramData\chocolatey if $env:ChocolateyInstall is not set' {
            $env:ChocolateyInstall = ''
            Get-ChocolateyPath -PathType InstallPath | Should -Be "$env:ProgramData\chocolatey"
        }

        It 'Falls back to $env:SystemDrive\ProgramData if $env:ChocolateyInstall and $env:ProgramData are not set' {
            $env:ProgramData = ''
            Get-ChocolateyPath -PathType InstallPath | Should -Be "$env:SystemDrive\ProgramData\chocolatey"
        }

        It 'Falls back to a path relative to the DLL location if none of the above are set' {
            $env:SystemDrive = ''
            Get-ChocolateyPath -PathType InstallPath | Should -Be $testLocation
        }
    }

    Context '-PathType PackagePath' {
        AfterEach {
            $env:ChocolateyPackageFolder = ''
            $env:PackageFolder = ''
            $env:ChocolateyPackageName = ''
        }

        It 'Returns the value of $env:ChocolateyPackageFolder if set' {
            $env:ChocolateyPackageFolder = 'C:\test'
            Get-ChocolateyPath -PathType PackagePath | Should -BeExactly $env:ChocolateyPackageFolder
        }

        It 'Returns the value of $env:PackageFolder if set and $env:ChocolateyPackageFolder is not set' {
            $env:PackageFolder = 'C:\test2'
            Get-ChocolateyPath -PathType PackagePath | Should -BeExactly $env:PackageFolder
        }

        It 'Falls back to "{InstallPath}\lib\$env:ChocolateyPackageName" if neither of the PackageFolder variables are set' {
            $env:ChocolateyPackageName = 'test'
            $installPath = Get-ChocolateyPath -PathType InstallPath

            $expectedPath = Join-Path -Path $installPath -ChildPath "lib\$env:ChocolateyPackageName"
            Get-ChocolateyPath -PathType PackagePath | Should -Be $expectedPath
        }
    }
}