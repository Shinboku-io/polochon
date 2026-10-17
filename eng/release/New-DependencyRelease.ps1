#Requires -Version 7.2
<#
.SYNOPSIS
    Prepares a dependency-only patch release of the Polochon packages whose dependencies moved.

.DESCRIPTION
    A dependency release ships the code of the latest full release (the newest 'v<major>.<minor>.<patch>'
    tag) with the central package versions of the source ref (master after a Dependabot merge), and bumps
    the patch version of the packages whose NuGet dependencies changed, and only those. The other packages
    keep their published version.

    The script:
      1. checks out the latest full release tag in a git worktree (the build tree) and copies onto its
         Directory.Packages.props the versions the source ref sets for the packages it already lists;
      2. evaluates every packable project under src/ there, and again at the commit its current version was
         released from ('v<x.y.z>' or '<PackageId>/v<x.y.z>'), and compares their non-private package
         dependencies;
      3. releases a package when one of them changed, or when a Polochon package it references is released
         (so its dependency floor moves too), at its current version with the patch number bumped;
      4. commits the build tree (deterministically, so a re-run yields the same commit) and writes into the
         output directory:
           - plan.json             the packages, their versions and dependency changes;
           - versions.props        per-project <Version> and <PackageReleaseNotes>, to pass to MSBuild with
                                   -p:CustomBeforeMicrosoftCommonProps=<path>;
           - notes/<PackageId>.md  the changelog of each released package (GitHub release body);
           - summary.md            every released package's changelog in one page.

    Nothing is built, pushed or published: the Dependency release workflow does that from the build tree.

.PARAMETER SourceRef
    The commit whose central package versions are released. Defaults to HEAD.

.PARAMETER WorkDirectory
    Where the git worktrees are created. Emptied first.

.PARAMETER OutputDirectory
    Where plan.json, versions.props and the release notes are written. Emptied first.

.EXAMPLE
    pwsh eng/release/New-DependencyRelease.ps1
    Shows which packages a dependency release of HEAD would publish, and at which version.
#>
[CmdletBinding()]
param(
    [string] $SourceRef = 'HEAD',
    [string] $WorkDirectory = (Join-Path ([IO.Path]::GetTempPath()) 'polochon-dependency-release'),
    [string] $OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$repositoryRoot = (git -C $PSScriptRoot rev-parse --show-toplevel).Trim()
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repositoryRoot 'artifacts/dependency-release'
}

$fullReleaseTagPattern = '^v(?<version>\d+\.\d+\.\d+)$'
$packageTagPattern = '^(?<id>[A-Za-z0-9_.-]+)/v(?<version>\d+\.\d+\.\d+)$'
$centralPackagesFile = 'Directory.Packages.props'

function Invoke-Git {
    & git -C $repositoryRoot @args
}

function Get-CentralPackageVersions([string] $content) {
    $versions = [ordered]@{}
    foreach ($node in ([xml]$content).SelectNodes('//PackageVersion[@Include and @Version]')) {
        $versions[$node.Include] = $node.Version
    }

    return $versions
}

function New-Worktree([string] $name, [string] $ref) {
    $path = Join-Path $WorkDirectory $name
    Invoke-Git worktree add --quiet --detach $path $ref | Out-Null
    return $path
}

# One worktree per release the current packages were published from.
$releaseTrees = @{}
function Get-ReleaseTree([string] $tag) {
    if (-not $releaseTrees.ContainsKey($tag)) {
        $releaseTrees[$tag] = New-Worktree ('release-' + ($tag -replace '[^A-Za-z0-9.]', '-')) $tag
    }

    return $releaseTrees[$tag]
}

function Step-Patch([version] $version) {
    return [version]::new($version.Major, $version.Minor, $version.Build + 1)
}

# The NuGet dependencies of a project's package: its package references that flow to consumers, with the
# version central package management (or a VersionOverride) gives them, and its Polochon project references.
function Get-ProjectDependencies([string] $projectPath) {
    $evaluation = & dotnet msbuild $projectPath -nologo `
        -getProperty:PackageId -getItem:PackageReference -getItem:PackageVersion -getItem:ProjectReference |
        Out-String | ConvertFrom-Json -AsHashtable

    $central = @{}
    foreach ($item in @($evaluation.Items['PackageVersion'] ?? @())) {
        $central[$item.Identity] = $item.Version
    }

    $packages = [Collections.Generic.SortedDictionary[string, string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($item in @($evaluation.Items['PackageReference'] ?? @())) {
        if ($item['PrivateAssets'] -eq 'all' -or $item['IsImplicitlyDefined'] -eq 'true') {
            continue
        }

        $version = $item['VersionOverride'] ? $item['VersionOverride'] : ($item['Version'] ? $item['Version'] : $central[$item.Identity])
        $packages[$item.Identity] = $version ?? '(unversioned)'
    }

    $projects = @(
        foreach ($item in @($evaluation.Items['ProjectReference'] ?? @())) {
            [IO.Path]::GetFileNameWithoutExtension($item.Identity)
        }
    )

    return [pscustomobject]@{
        PackageId = $evaluation.Properties['PackageId']
        Packages = $packages
        ProjectReferences = $projects
    }
}

function Compare-Dependencies($released, $current) {
    $names = [Collections.Generic.SortedSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $released.Keys + $current.Keys | ForEach-Object { [void]$names.Add($_) }

    foreach ($name in $names) {
        $from = $released.ContainsKey($name) ? $released[$name] : $null
        $to = $current.ContainsKey($name) ? $current[$name] : $null
        if ($from -ne $to) {
            [pscustomobject]@{ Dependency = $name; From = $from; To = $to }
        }
    }
}

function Format-Version($version) {
    return $version ?? '_(none)_'
}

# --- Clean up the previous run ---------------------------------------------------------------------------

if (Test-Path $WorkDirectory) {
    Remove-Item $WorkDirectory -Recurse -Force
}

Invoke-Git worktree prune
New-Item $WorkDirectory -ItemType Directory -Force | Out-Null
if (Test-Path $OutputDirectory) {
    Remove-Item $OutputDirectory -Recurse -Force
}

New-Item (Join-Path $OutputDirectory 'notes') -ItemType Directory -Force | Out-Null

# --- Releases published so far ---------------------------------------------------------------------------

$sourceCommit = (Invoke-Git rev-parse --verify "$SourceRef^{commit}").Trim()
$sourceDate = (Invoke-Git log -1 --format=%cI $sourceCommit).Trim()

$fullReleases = @(
    Invoke-Git tag --list 'v*' | Where-Object { $_ -match $fullReleaseTagPattern } | ForEach-Object {
        [pscustomobject]@{ Tag = $_; Version = [version]$Matches.version }
    } | Sort-Object Version -Descending
)
if ($fullReleases.Count -eq 0) {
    throw "No full release tag ('v<major>.<minor>.<patch>') found: a dependency release patches an existing release."
}

$baseRelease = $fullReleases[0]

$packageReleases = @{}
foreach ($tag in Invoke-Git tag --list '*/v*') {
    if ($tag -match $packageTagPattern) {
        $packageReleases[$Matches.id] = @($packageReleases[$Matches.id] ?? @()) +
            [pscustomobject]@{ Tag = $tag; Version = [version]$Matches.version }
    }
}

# --- Build tree: the latest full release with the source ref's dependency versions -----------------------

$buildTree = New-Worktree 'build' $baseRelease.Tag
$buildPackagesFile = Join-Path $buildTree $centralPackagesFile
$buildPackagesContent = [IO.File]::ReadAllText($buildPackagesFile)
$sourceVersions = Get-CentralPackageVersions ((Invoke-Git show "${sourceCommit}:$centralPackagesFile") -join "`n")

foreach ($entry in (Get-CentralPackageVersions $buildPackagesContent).GetEnumerator()) {
    $sourceVersion = $sourceVersions[$entry.Key]
    if ($null -eq $sourceVersion -or $sourceVersion -eq $entry.Value) {
        continue
    }

    $pattern = '(<PackageVersion\s+Include="' + [regex]::Escape($entry.Key) + '"\s+Version=")[^"]*(")'
    $buildPackagesContent = [regex]::Replace($buildPackagesContent, $pattern, "`${1}$sourceVersion`${2}")
    if ((Get-CentralPackageVersions $buildPackagesContent)[$entry.Key] -ne $sourceVersion) {
        throw "Could not set $($entry.Key) to $sourceVersion in the build tree's $centralPackagesFile."
    }
}

[IO.File]::WriteAllText($buildPackagesFile, $buildPackagesContent)

# --- Which packages move ---------------------------------------------------------------------------------

$packages = [ordered]@{}
foreach ($project in Get-ChildItem (Join-Path $buildTree 'src') -Filter '*.csproj' -Recurse -Depth 1 | Sort-Object BaseName) {
    $projectPath = [IO.Path]::GetRelativePath($buildTree, $project.FullName).Replace('\', '/')
    $current = Get-ProjectDependencies $project.FullName

    # The package's current version and the commit it was released from: the newest of the latest full
    # release and the package's own dependency releases.
    $release = @(@($packageReleases[$current.PackageId] ?? @()) + $baseRelease | Sort-Object Version -Descending)[0]
    $released = Get-ProjectDependencies (Join-Path (Get-ReleaseTree $release.Tag) $projectPath)

    $changes = [Collections.Generic.List[object]]::new()
    Compare-Dependencies $released.Packages $current.Packages | ForEach-Object { $changes.Add($_) }

    $packages[$project.BaseName] = [pscustomobject]@{
        Project = $project.BaseName
        ProjectPath = $projectPath
        PackageId = $current.PackageId
        ReleasedFrom = $release.Tag
        CurrentVersion = $release.Version
        NewVersion = $changes.Count -gt 0 ? (Step-Patch $release.Version) : $null
        ProjectReferences = $current.ProjectReferences
        Changes = $changes
    }
}

# A package referencing a released Polochon package is released too, so its dependency floor follows.
do {
    $cascaded = $false
    foreach ($package in $packages.Values) {
        foreach ($reference in $package.ProjectReferences) {
            $dependency = $packages[$reference]
            if ($null -eq $dependency -or $null -eq $dependency.NewVersion -or
                @($package.Changes | Where-Object Dependency -EQ $dependency.PackageId).Count -gt 0) {
                continue
            }

            $package.Changes.Add([pscustomobject]@{
                Dependency = $dependency.PackageId
                From = "$($dependency.CurrentVersion)"
                To = "$($dependency.NewVersion)"
            })
            $package.NewVersion ??= Step-Patch $package.CurrentVersion
            $cascaded = $true
        }
    }
} while ($cascaded)

$releases = @($packages.Values | Where-Object NewVersion)

# --- Release commit --------------------------------------------------------------------------------------

$shortSource = $sourceCommit.Substring(0, 7)
$releaseCommit = $null
if ($releases.Count -gt 0) {
    $message = @(
        'chore(release): dependency release'
        ''
        $releases | ForEach-Object { "$($_.PackageId) $($_.NewVersion)" }
        ''
        "Code of $($baseRelease.Tag) with the dependency versions of $sourceCommit."
    ) -join "`n"

    # Same identity, dates, parent and tree on every run: a re-run of a release yields the same commit.
    $gitEnvironment = @{
        GIT_AUTHOR_NAME = 'github-actions[bot]'
        GIT_AUTHOR_EMAIL = '41898282+github-actions[bot]@users.noreply.github.com'
        GIT_AUTHOR_DATE = $sourceDate
        GIT_COMMITTER_NAME = 'github-actions[bot]'
        GIT_COMMITTER_EMAIL = '41898282+github-actions[bot]@users.noreply.github.com'
        GIT_COMMITTER_DATE = $sourceDate
    }
    $previousEnvironment = @{}
    foreach ($variable in $gitEnvironment.GetEnumerator()) {
        $previousEnvironment[$variable.Key] = [Environment]::GetEnvironmentVariable($variable.Key)
        [Environment]::SetEnvironmentVariable($variable.Key, $variable.Value)
    }

    try {
        git -C $buildTree commit --quiet --allow-empty --no-verify --all --message $message
    }
    finally {
        foreach ($variable in $previousEnvironment.GetEnumerator()) {
            [Environment]::SetEnvironmentVariable($variable.Key, $variable.Value)
        }
    }

    $releaseCommit = (git -C $buildTree rev-parse HEAD).Trim()
}

# --- Versions, release notes and plan --------------------------------------------------------------------

$releaseDate = ([datetimeoffset]$sourceDate).ToString('yyyy-MM-dd')
$props = [Collections.Generic.List[string]]@('<Project>')
$planPackages = foreach ($package in $packages.Values) {
    $version = $package.NewVersion ?? $package.CurrentVersion
    $props.Add('  <PropertyGroup Condition="''$(MSBuildProjectName)'' == ''{0}''">' -f $package.Project)
    $props.Add("    <Version>$version</Version>")

    $tag = $null
    $notesPath = $null
    if ($package.NewVersion) {
        $tag = "$($package.PackageId)/v$version"
        $notesPath = Join-Path $OutputDirectory "notes/$($package.PackageId).md"
        $rows = $package.Changes | ForEach-Object { "| $($_.Dependency) | $(Format-Version $_.From) | $(Format-Version $_.To) |" }
        @(
            "## $($package.PackageId) $version ($releaseDate)"
            ''
            "Dependency update of $($package.PackageId) $($package.CurrentVersion). The code is the one of the" +
                " $($baseRelease.Version) release (``$($baseRelease.Tag)``), only the dependency versions below move."
            ''
            '| Dependency | From | To |'
            '| --- | --- | --- |'
            $rows
        ) -join "`n" | Set-Content $notesPath -NoNewline

        $plainNotes = @(
            "Dependency update of $($package.PackageId) $($package.CurrentVersion), code unchanged since $($baseRelease.Version):"
            $package.Changes | ForEach-Object { "- $($_.Dependency): $($_.From ?? 'none') -> $($_.To ?? 'none')" }
        ) -join "`n"
        $props.Add("    <PackageReleaseNotes>$([Security.SecurityElement]::Escape($plainNotes))</PackageReleaseNotes>")
    }

    $props.Add('  </PropertyGroup>')

    [ordered]@{
        project = $package.Project
        projectPath = $package.ProjectPath
        packageId = $package.PackageId
        releasedFrom = $package.ReleasedFrom
        currentVersion = "$($package.CurrentVersion)"
        newVersion = $package.NewVersion ? "$($package.NewVersion)" : $null
        tag = $tag
        notes = $notesPath
        changes = @($package.Changes | ForEach-Object { [ordered]@{ dependency = $_.Dependency; from = $_.From; to = $_.To } })
    }
}

$props.Add('</Project>')
$versionsProps = Join-Path $OutputDirectory 'versions.props'
$props -join "`n" | Set-Content $versionsProps

$plan = [ordered]@{
    baseTag = $baseRelease.Tag
    sourceCommit = $sourceCommit
    buildTree = $buildTree
    releaseCommit = $releaseCommit
    versionsProps = $versionsProps
    packages = @($planPackages)
}
$planPath = Join-Path $OutputDirectory 'plan.json'
$plan | ConvertTo-Json -Depth 5 | Set-Content $planPath

$summary = [Collections.Generic.List[string]]@(
    '# Dependency release'
    ''
    "Code of ``$($baseRelease.Tag)`` with the central package versions of ``$shortSource``."
    ''
    '| Package | Version | Released |'
    '| --- | --- | --- |'
)
foreach ($package in $packages.Values) {
    $summary.Add($package.NewVersion ?
        "| $($package.PackageId) | $($package.CurrentVersion) → **$($package.NewVersion)** | yes |" :
        "| $($package.PackageId) | $($package.CurrentVersion) | no |")
}

if ($releases.Count -eq 0) {
    $summary.AddRange([string[]]@('', 'No package dependency changed: nothing to release.'))
}

foreach ($release in $releases) {
    $summary.Add('')
    $summary.Add((Get-Content (Join-Path $OutputDirectory "notes/$($release.PackageId).md") -Raw))
}

$summary -join "`n" | Set-Content (Join-Path $OutputDirectory 'summary.md')

if ($env:GITHUB_OUTPUT) {
    @(
        "released=$($releases.Count -gt 0 ? 'true' : 'false')"
        "build-tree=$buildTree"
        "release-commit=$releaseCommit"
        "versions-props=$versionsProps"
        "plan=$planPath"
    ) | Add-Content $env:GITHUB_OUTPUT
}

$packages.Values | Format-Table PackageId, CurrentVersion, NewVersion, @{ Label = 'Changes'; Expression = { $_.Changes.Dependency -join ', ' } } -AutoSize | Out-Host
Write-Host "Plan written to $planPath"
