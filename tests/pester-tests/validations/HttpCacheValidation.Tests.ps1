Describe "CacheFolderLockdownValidation" -Tag Chocolatey, Validations, HttpCache -Skip:(-not $env:TEST_KITCHEN) {
    BeforeDiscovery {
        $installedPackages = Get-ChocolateyInstalledPackages
        $agentInstalled = $installedPackages.Name -contains 'chocolatey-agent'
    }

    BeforeAll {
        Initialize-ChocolateyTestInstall

        function Assert-HttpCacheLockdown {
            $errors = @(
                $cacheFolder = "$env:ProgramData\ChocolateyHttpCache"
                if (-not (Test-Path $cacheFolder)) {
                    "The cache folder does not exist"
                }

                $acl = Get-Acl -Path $cacheFolder
                if ($acl.Owner -cne "BUILTIN\Administrators") {
                    "The cache folder is owned by $($acl.Owner), not Administrators"
                }

                $rules = $acl.Access
                if ($rules.Count -ne 3) {
                    "Unexpected access rules are present on the cache folder:`n$($rules | Format-List | Out-String)"
                }

                $expectedRules = @(
                    @{
                        Identifier = "BUILTIN\Administrators"
                        AclType = "Allow"
                        Rights = "FullControl"
                    }
                    @{
                        Identifier = "NT AUTHORITY\SYSTEM"
                        AclType = "Allow"
                        Rights = "FullControl"
                    }
                    @{
                        Identifier = "BUILTIN\Users"
                        AclType = "Allow"
                        Rights = "ReadAndExecute, Synchronize"
                    }
                )

                foreach ($rule in $expectedRules) {
                    $actual = $rules.Where{$_.IdentityReference.Value -eq $rule.Identifier}
                    if ($actual) {
                        if ($actual.AccessControlType -ne $rule.AclType) {
                            "Access control type for $($rule.Identifier) rule should have '$($rule.AclType)', but has '$($actual.AccessControlType)'"
                        }
                        
                        if ($actual.FileSystemRights -ne $rule.Rights) {
                            "File system rights for $($rule.Identifier) rule should have '$($rule.Rights)', but has '$($actual.FileSystemRights)'"
                        }
                    }
                    else {
                        "There is no access rule for $($rule.Identifier)"
                    }
                }
            ) -join "`n"

            $errors = $errors.Trim()
            if ($errors) {
                throw $errors
            }
        }
    }

    AfterAll {
        Remove-ChocolateyTestInstall
    }

    Context 'Normal Admin Context' {
        Describe 'With a suspicious HttpCache directory' {
            BeforeAll {
                # Remove any original cache folder, if there is one.
                $CacheFolder = "$env:ProgramData/ChocolateyHttpCache"
                Remove-Item -Path $cacheFolder -Force -Recurse

                # Create normal folder and file with no special ACLs in its place.
                New-Item -Path $cacheFolder -ItemType Directory
                New-Item -Path "$cacheFolder/test-file.dat"

                $result = Invoke-Choco search chocolatey
            }

            AfterAll {
                # Ensure no files within the cache folder are set read-only so it can be removed
                Get-ChildItem -Path $CacheFolder -Recurse -File |
                    Where-Object { $_.IsReadOnly } |
                    ForEach-Object { $_.IsReadOnly = $false }
            }

            It 'exits with 0' {
                $result.ExitCode | Should -Be 0
            }

            It 'warns that the pre-existing cache folder has been purged' {
                $result.String | Should -Match 'Validation Warnings:'
                $result.String | Should -Match 'The HTTP Cache was not correctly locked down, purging the cache'
            }

            It 'creates a new cache directory with the correct permissions' {
                Assert-HttpCacheLockdown
            }
        }

        Describe 'With Read-Only files in the suspicious HttpCache directory' {
            BeforeAll {
                # Remove any original cache folder, if there is one.
                $CacheFolder = "$env:ProgramData/ChocolateyHttpCache"
                Remove-Item -Path $cacheFolder -Force -Recurse

                # Create normal folder and file with no special ACLs in its place.
                New-Item -Path $cacheFolder -ItemType Directory
                $File = New-Item -Path "$cacheFolder/test-file.dat"

                # Make the file read only so the removal can't work
                $File.IsReadOnly = $true

                $result = Invoke-Choco search chocolatey
            }

            AfterAll {
                # Ensure no files within the cache folder are set read-only so it can be removed
                Get-ChildItem -Path $CacheFolder -Recurse -File |
                    Where-Object { $_.IsReadOnly } |
                    ForEach-Object { $_.IsReadOnly = $false }
            }

            It 'exits with 1' {
                $result.ExitCode | Should -Be 1
            }

            It 'fails with an error message indicating that the cache folder could not be purged' {
                $result.String | Should -Match 'Validation Errors:'
                $result.String | Should -Match 'System Cache directory exists, but could not be locked down'
            }

            It 'fails to purge the cache directory' {
                $File | Should -Exist -Because 'Chocolatey should not have been able to purge the cache folder'
            }
        }

        Describe 'With Read-Only files in the suspicious HttpCache directory and --ignore-http-cache' {
            BeforeAll {
                # Remove any original cache folder, if there is one.
                $CacheFolder = "$env:ProgramData/ChocolateyHttpCache"
                Remove-Item -Path $cacheFolder -Force -Recurse

                # Create normal folder and file with no special ACLs in its place.
                New-Item -Path $cacheFolder -ItemType Directory
                $File = New-Item -Path "$cacheFolder/test-file.dat"

                # Make the file read only so the removal can't work
                $File.IsReadOnly = $true

                $result = Invoke-Choco search chocolatey --ignore-http-cache
            }

            AfterAll {
                # Ensure no files within the cache folder are set read-only so it can be removed
                Get-ChildItem -Path $CacheFolder -Recurse -File |
                    Where-Object { $_.IsReadOnly } |
                    ForEach-Object { $_.IsReadOnly = $false }
            }

            It 'exits with 0' {
                $result.ExitCode | Should -Be 0
            }

            It 'warns that the cache folder could not be purged' {
                $result.String | Should -Match 'Validation Warnings:'
                $result.String | Should -Match 'System Cache directory exists, but could not be locked down'
            }

            It 'fails to purge the cache directory' {
                $File | Should -Exist -Because 'Chocolatey should not have been able to purge the cache folder'
            }

            It 'fails to lock down the cache directory' {
                { Assert-HttpCacheLockdown } | Should -Throw -Because 'we expect the cache directory to not be locked down'
            }
        }
    }

    Context 'Background Service' -Tag Background {
        BeforeAll {
            New-ChocolateyInstallSnapshot

            # enable background service and make sure it's used for this operation
            Enable-ChocolateyFeature -Name useBackgroundService
            Disable-ChocolateyFeature -Name useBackgroundServiceWithNonAdministratorsOnly
            Disable-ChocolateyFeature -Name useBackgroundServiceWithSelfServiceSourcesOnly
            Invoke-Choco config set --name backgroundServiceAllowedCommands --value "install,upgrade,uninstall,download"
        }

        AfterAll {
            Remove-ChocolateyInstallSnapshot
        }

        Describe 'With a suspicious HttpCache directory' {
            BeforeAll {
                # Remove any original cache folder, if there is one.
                $CacheFolder = "$env:ProgramData/ChocolateyHttpCache"
                Remove-Item -Path $cacheFolder -Force -Recurse

                # Create normal folder and file with no special ACLs in its place.
                New-Item -Path $cacheFolder -ItemType Directory
                New-Item -Path "$cacheFolder/test-file.dat"

                $result = Invoke-Choco download chocolatey
            }

            AfterAll {
                # Ensure no files within the cache folder are set read-only so it can be removed
                Get-ChildItem -Path $CacheFolder -Recurse -File |
                    Where-Object { $_.IsReadOnly } |
                    ForEach-Object { $_.IsReadOnly = $false }

                if (Test-Path "$env:ProgramData\download") {
                    Remove-Item "$env:ProgramData\download" -Recurse -Force
                }
            }

            It 'exits with 0' {
                $result.ExitCode | Should -Be 0
            }

            It 'should run in background mode' {
                $result.Lines | Should -Contain "Running in background mode" -Because $result.String
            }

            It 'warns that the pre-existing cache folder has been purged' {
                $result.String | Should -Match 'Validation Warnings:'
                $result.String | Should -Match 'The HTTP Cache was not correctly locked down, purging the cache'
            }

            It 'creates a new cache directory with the correct permissions' {
                Assert-HttpCacheLockdown
            }
        }

        Describe 'With Read-Only files in the suspicious HttpCache directory' {
            BeforeAll {
                # Remove any original cache folder, if there is one.
                $CacheFolder = "$env:ProgramData/ChocolateyHttpCache"
                Remove-Item -Path $cacheFolder -Force -Recurse

                # Create normal folder and file with no special ACLs in its place.
                New-Item -Path $cacheFolder -ItemType Directory
                $File = New-Item -Path "$cacheFolder/test-file.dat"

                # Make the file read only so the removal can't work
                $File.IsReadOnly = $true

                $result = Invoke-Choco download chocolatey
            }

            AfterAll {
                # Ensure no files within the cache folder are set read-only so it can be removed
                Get-ChildItem -Path $CacheFolder -Recurse -File |
                    Where-Object { $_.IsReadOnly } |
                    ForEach-Object { $_.IsReadOnly = $false }

                if (Test-Path "$env:ProgramData\download") {
                    Remove-Item "$env:ProgramData\download" -Recurse -Force
                }
            }

            It 'exits with 1' {
                $result.ExitCode | Should -Be 1
            }

            It 'should run in background mode' {
                $result.Lines | Should -Contain "Running in background mode" -Because $result.String
            }

            It 'fails with an error message indicating that the cache folder could not be purged' {
                $result.String | Should -Match 'Validation Errors:'
                $result.String | Should -Match 'System Cache directory exists, but could not be locked down'
            }

            It 'fails to purge the cache directory' {
                $File | Should -Exist -Because 'Chocolatey should not have been able to purge the cache folder'
            }
        }

        Describe 'With Read-Only files in the suspicious HttpCache directory and --ignore-http-cache' {
            BeforeAll {
                # Remove any original cache folder, if there is one.
                $CacheFolder = "$env:ProgramData/ChocolateyHttpCache"
                Remove-Item -Path $cacheFolder -Force -Recurse

                # Create normal folder and file with no special ACLs in its place.
                New-Item -Path $cacheFolder -ItemType Directory
                $File = New-Item -Path "$cacheFolder/test-file.dat"

                # Make the file read only so the removal can't work
                $File.IsReadOnly = $true

                $result = Invoke-Choco download chocolatey --ignore-http-cache
            }

            AfterAll {
                # Ensure no files within the cache folder are set read-only so it can be removed
                Get-ChildItem -Path $CacheFolder -Recurse -File |
                    Where-Object { $_.IsReadOnly } |
                    ForEach-Object { $_.IsReadOnly = $false }

                if (Test-Path "$env:ProgramData\download") {
                    Remove-Item "$env:ProgramData\download" -Recurse -Force
                }
            }

            It 'exits with 0' {
                $result.ExitCode | Should -Be 0
            }

            It 'should run in background mode' {
                $result.Lines | Should -Contain "Running in background mode" -Because $result.String
            }

            It 'warns that the cache folder could not be purged' {
                $result.String | Should -Match 'Validation Warnings:'
                $result.String | Should -Match 'System Cache directory exists, but could not be locked down'
            }

            It 'fails to purge the cache directory' {
                $File | Should -Exist -Because 'Chocolatey should not have been able to purge the cache folder'
            }

            It 'fails to lock down the cache directory' {
                { Assert-HttpCacheLockdown } | Should -Throw -Because 'we expect the cache directory to not be locked down'
            }
        }
    }
}