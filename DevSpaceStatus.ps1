[CmdletBinding()]
param(
    [switch]$Once,
    [ValidateRange(1, 60)]
    [int]$RefreshSeconds = 3,
    [ValidateRange(1, 1440)]
    [int]$StallMinutes = 30,
    [ValidateRange(1, 3600)]
    [int]$NotifyAfterSeconds = 10,
    [ValidateRange(5, 600)]
    [int]$CompletionQuietSeconds = 45,
    [int]$Port = 7676,
    [string]$LogPath = "$env:USERPROFILE\.devspace\serve.log",
    [string]$StatePath = "$env:USERPROFILE\.devspace\devspace-status.json"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$script:lastLogWriteUtc = [DateTime]::MinValue
$script:logSnapshotCache = $null
$script:workspaceMapCache = @{}
$script:workspaceMapInitialized = $false
$script:activityActive = $false
$script:activityStartedAt = $null
$script:lastActivityAt = $null
$script:lastCpuSeconds = 0.0
$script:lastObservedLogWriteUtc = [DateTime]::MinValue
$script:previousState = $null
$script:lastSeenToolKey = $null
$script:initialized = $false
$script:stallNotified = $false
$script:workSessionActive = $false
$script:workSessionStartedAt = $null
$script:workSessionLastActivityAt = $null
$script:workSessionProject = '不明'
$script:workSessionOperation = '作業'
$script:workSessionLastTool = $null

function Get-DevSpaceServerProcessId {
    param([int]$ListenPort)

    try {
        $listener = Get-NetTCPConnection -LocalPort $ListenPort -State Listen -ErrorAction Stop |
            Select-Object -First 1
        if ($null -ne $listener) {
            return [int]$listener.OwningProcess
        }
    }
    catch {
        # Fall back to command-line inspection.
    }

    try {
        $server = Get-CimInstance Win32_Process -ErrorAction Stop |
            Where-Object {
                $_.Name -eq 'node.exe' -and
                $_.CommandLine -match '@waishnav[\\/]devspace' -and
                $_.CommandLine -match '\bserve\b'
            } |
            Select-Object -First 1
        if ($null -ne $server) {
            return [int]$server.ProcessId
        }
    }
    catch {
        return $null
    }

    return $null
}

function Get-ProcessSnapshot {
    try {
        return @(Get-CimInstance Win32_Process -ErrorAction Stop |
            Select-Object ProcessId, ParentProcessId, Name, CommandLine, CreationDate)
    }
    catch {
        return @()
    }
}

function Get-DevSpaceChildProcesses {
    param(
        [int]$ServerProcessId,
        [object[]]$Processes
    )

    if ($ServerProcessId -le 0 -or $Processes.Count -eq 0) {
        return @()
    }

    $childrenByParent = @{}
    foreach ($process in $Processes) {
        $parentId = [int]$process.ParentProcessId
        if (-not $childrenByParent.ContainsKey($parentId)) {
            $childrenByParent[$parentId] = New-Object System.Collections.ArrayList
        }
        [void]$childrenByParent[$parentId].Add($process)
    }

    $result = New-Object System.Collections.ArrayList
    $queue = New-Object 'System.Collections.Generic.Queue[int]'
    $queue.Enqueue($ServerProcessId)

    while ($queue.Count -gt 0) {
        $parentId = $queue.Dequeue()
        if (-not $childrenByParent.ContainsKey($parentId)) {
            continue
        }

        foreach ($child in $childrenByParent[$parentId]) {
            $queue.Enqueue([int]$child.ProcessId)

            $commandLine = [string]$child.CommandLine
            $isMonitorProcess = $commandLine -match '(?i)DevSpaceStatus\.ps1|DevSpacePet\.ps1|Start-DevSpaceStatus|Check-DevSpaceStatus'
            $isConsoleHost = $child.Name -in @('conhost.exe', 'OpenConsole.exe')

            if (-not $isMonitorProcess -and -not $isConsoleHost) {
                [void]$result.Add($child)
            }
        }
    }

    return @($result)
}

function Get-DevSpaceProcessGroups {
    param(
        [int]$ServerProcessId,
        [object[]]$Processes
    )

    if ($ServerProcessId -le 0 -or $Processes.Count -eq 0) {
        return @()
    }

    $processById = @{}
    $childrenByParent = @{}
    foreach ($process in $Processes) {
        $processById[[int]$process.ProcessId] = $process
        $parentId = [int]$process.ParentProcessId
        if (-not $childrenByParent.ContainsKey($parentId)) {
            $childrenByParent[$parentId] = New-Object System.Collections.ArrayList
        }
        [void]$childrenByParent[$parentId].Add($process)
    }

    if (-not $childrenByParent.ContainsKey($ServerProcessId)) {
        return @()
    }

    $groups = New-Object System.Collections.ArrayList
    foreach ($rootProcess in $childrenByParent[$ServerProcessId]) {
        $groupProcesses = New-Object System.Collections.ArrayList
        $queue = New-Object 'System.Collections.Generic.Queue[int]'
        $queue.Enqueue([int]$rootProcess.ProcessId)

        while ($queue.Count -gt 0) {
            $processId = $queue.Dequeue()
            if ($processById.ContainsKey($processId)) {
                $process = $processById[$processId]
                $commandLine = [string]$process.CommandLine
                $isMonitorProcess = $commandLine -match '(?i)DevSpaceStatus\.ps1|DevSpacePet\.ps1|Start-DevSpaceStatus|Check-DevSpaceStatus'
                $isConsoleHost = $process.Name -in @('conhost.exe', 'OpenConsole.exe')
                if (-not $isMonitorProcess -and -not $isConsoleHost) {
                    [void]$groupProcesses.Add($process)
                }
            }

            if ($childrenByParent.ContainsKey($processId)) {
                foreach ($child in $childrenByParent[$processId]) {
                    $queue.Enqueue([int]$child.ProcessId)
                }
            }
        }

        if ($groupProcesses.Count -gt 0) {
            [void]$groups.Add([pscustomobject]@{
                RootProcessId = [int]$rootProcess.ProcessId
                Processes     = @($groupProcesses)
            })
        }
    }

    return @($groups)
}

function Get-PropertyValue {
    param(
        $Object,
        [string]$Name,
        $DefaultValue = $null
    )

    if ($null -ne $Object -and $Object.PSObject.Properties.Name -contains $Name) {
        return $Object.$Name
    }
    return $DefaultValue
}

function Get-ProjectNameFromPath {
    param(
        [string]$WorkspacePath,
        [string]$RelativePath
    )

    $workspaceName = ''
    if (-not [string]::IsNullOrWhiteSpace($WorkspacePath)) {
        $workspaceName = Split-Path -Leaf ($WorkspacePath.TrimEnd('\', '/'))
    }

    if ($workspaceName -ieq 'Projects' -and -not [string]::IsNullOrWhiteSpace($RelativePath)) {
        $normalized = $RelativePath.TrimStart('.', '\', '/')
        $firstPart = ($normalized -split '[\\/]')[0]
        if (-not [string]::IsNullOrWhiteSpace($firstPart)) {
            return $firstPart
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($workspaceName)) {
        return $workspaceName
    }

    if (-not [string]::IsNullOrWhiteSpace($RelativePath)) {
        $normalized = $RelativePath.TrimStart('.', '\', '/')
        $firstPart = ($normalized -split '[\\/]')[0]
        if (-not [string]::IsNullOrWhiteSpace($firstPart)) {
            return $firstPart
        }
    }

    return '不明'
}

function Format-ToolOperation {
    param($Entry)

    $tool = [string](Get-PropertyValue -Object $Entry -Name 'tool' -DefaultValue 'unknown')
    $path = [string](Get-PropertyValue -Object $Entry -Name 'path' -DefaultValue '')
    $workingDirectory = [string](Get-PropertyValue -Object $Entry -Name 'workingDirectory' -DefaultValue '')

    switch ($tool) {
        'read'  { if ($path) { return "読取: $path" }; return 'ファイル読取' }
        'edit'  { if ($path) { return "編集: $path" }; return 'ファイル編集' }
        'write' { if ($path) { return "作成: $path" }; return 'ファイル作成' }
        'bash'  { if ($workingDirectory -and $workingDirectory -ne '.') { return "コマンド: $workingDirectory" }; return 'コマンド実行' }
        'open_workspace' { return 'ワークスペースを開く' }
        default { return $tool }
    }
}

function Initialize-WorkspaceMap {
    param([string]$Path)

    if ($script:workspaceMapInitialized -or -not (Test-Path -LiteralPath $Path)) {
        return
    }

    try {
        $matches = Select-String -LiteralPath $Path -SimpleMatch '"tool":"open_workspace"' -ErrorAction Stop
        foreach ($match in $matches) {
            try {
                $entry = $match.Line | ConvertFrom-Json -ErrorAction Stop
                if ([string](Get-PropertyValue -Object $entry -Name 'event' -DefaultValue '') -ne 'tool_call') {
                    continue
                }
                $workspaceId = [string](Get-PropertyValue -Object $entry -Name 'workspaceId' -DefaultValue '')
                $workspacePath = [string](Get-PropertyValue -Object $entry -Name 'path' -DefaultValue '')
                if ($workspaceId -and $workspacePath) {
                    $script:workspaceMapCache[$workspaceId] = $workspacePath
                }
            }
            catch {
                continue
            }
        }
    }
    catch {
        # The recent log tail can still provide mappings.
    }
    finally {
        $script:workspaceMapInitialized = $true
    }
}

function Get-LogSnapshot {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return [pscustomobject]@{
            LastTool         = $null
            RecentTools      = @()
            LastWriteTimeUtc = [DateTime]::MinValue
        }
    }

    try {
        Initialize-WorkspaceMap -Path $Path
        $logFile = Get-Item -LiteralPath $Path -ErrorAction Stop
        if ($null -ne $script:logSnapshotCache -and $logFile.LastWriteTimeUtc -eq $script:lastLogWriteUtc) {
            return $script:logSnapshotCache
        }

        $lastTool = $null
        $latestToolsByWorkspace = @{}
        $lines = @(Get-Content -LiteralPath $Path -Tail 1600 -ErrorAction Stop)
        foreach ($line in $lines) {
            if ($line -notmatch '"event":"(?:tool_call|mcp_session_created)"') {
                continue
            }

            try {
                $entry = $line | ConvertFrom-Json -ErrorAction Stop
                $eventName = [string](Get-PropertyValue -Object $entry -Name 'event' -DefaultValue '')
                if ($eventName -ne 'tool_call') {
                    continue
                }

                $tool = [string](Get-PropertyValue -Object $entry -Name 'tool' -DefaultValue '')
                $workspaceId = [string](Get-PropertyValue -Object $entry -Name 'workspaceId' -DefaultValue '')
                $entryPath = [string](Get-PropertyValue -Object $entry -Name 'path' -DefaultValue '')
                $workingDirectory = [string](Get-PropertyValue -Object $entry -Name 'workingDirectory' -DefaultValue '')

                if ($tool -eq 'open_workspace' -and $workspaceId -and $entryPath) {
                    $script:workspaceMapCache[$workspaceId] = $entryPath
                }

                $workspacePath = ''
                if ($workspaceId -and $script:workspaceMapCache.ContainsKey($workspaceId)) {
                    $workspacePath = [string]$script:workspaceMapCache[$workspaceId]
                }

                $localTime = [DateTimeOffset]::Parse([string]$entry.ts).ToLocalTime()
                $durationMs = [int64](Get-PropertyValue -Object $entry -Name 'durationMs' -DefaultValue 0)
                $successValue = Get-PropertyValue -Object $entry -Name 'success' -DefaultValue $true
                $projectRelativePath = if ($entryPath) { $entryPath } elseif ($workingDirectory -and $workingDirectory -ne '.') { $workingDirectory } else { '' }
                $projectName = Get-ProjectNameFromPath -WorkspacePath $workspacePath -RelativePath $projectRelativePath

                $toolRecord = [pscustomobject]@{
                    Time          = $localTime
                    Tool          = $tool
                    Success       = [bool]$successValue
                    DurationMs    = $durationMs
                    Workspace     = $workspaceId
                    WorkspacePath = $workspacePath
                    Path          = $entryPath
                    ProjectName   = $projectName
                    Operation     = Format-ToolOperation -Entry $entry
                }
                $lastTool = $toolRecord
                if (-not [string]::IsNullOrWhiteSpace($workspaceId)) {
                    $latestToolsByWorkspace[$workspaceId] = $toolRecord
                }
            }
            catch {
                continue
            }
        }

        $script:lastLogWriteUtc = $logFile.LastWriteTimeUtc
        $script:logSnapshotCache = [pscustomobject]@{
            LastTool         = $lastTool
            RecentTools      = @($latestToolsByWorkspace.Values | Sort-Object -Property Time -Descending)
            LastWriteTimeUtc = $logFile.LastWriteTimeUtc
        }
        return $script:logSnapshotCache
    }
    catch {
        if ($null -ne $script:logSnapshotCache) {
            return $script:logSnapshotCache
        }
        return [pscustomobject]@{
            LastTool         = $null
            RecentTools      = @()
            LastWriteTimeUtc = [DateTime]::MinValue
        }
    }
}

function Get-SafeProcessOperation {
    param([object[]]$Processes)

    if ($Processes.Count -eq 0) {
        return 'ローカル処理'
    }

    $candidates = foreach ($process in $Processes) {
        $name = [string]$process.Name
        $commandLine = [string]$process.CommandLine
        $operation = ''
        $score = 10

        if ($commandLine -match '(?i)\bdotnet(?:\.exe)?\s+(test|build|publish|run|restore|clean|pack)\b') {
            $operation = "dotnet $($Matches[1].ToLowerInvariant())"
            $score = 100
        }
        elseif ($commandLine -match '(?i)\b(msbuild)(?:\.exe)?\b') {
            $operation = 'MSBuild'
            $score = 95
        }
        elseif ($commandLine -match '(?i)\bgit(?:\.exe)?\s+(status|diff|log|fetch|pull|push|commit|checkout|switch|merge|rebase|clone)\b') {
            $operation = "git $($Matches[1].ToLowerInvariant())"
            $score = 90
        }
        elseif ($commandLine -match '(?i)\b(npm|pnpm|yarn|npx)(?:\.cmd|\.exe)?\s+(install|test|run|build|start|publish|audit)\b') {
            $operation = "$($Matches[1].ToLowerInvariant()) $($Matches[2].ToLowerInvariant())"
            $score = 85
        }
        elseif ($commandLine -match '(?i)\b(ffmpeg|ffprobe)(?:\.exe)?\b') {
            $operation = $Matches[1].ToLowerInvariant()
            $score = 85
        }
        elseif ($commandLine -match '(?i)(?:-File|-f)\s+"?([^"\s]+\.ps1)') {
            $operation = [IO.Path]::GetFileNameWithoutExtension([string]$Matches[1])
            $score = 88
        }
        elseif ($commandLine -match '(?i)\b(python|python3|py)(?:\.exe)?\b') {
            $operation = 'Python'
            $score = 75
        }
        elseif ($commandLine -match '(?i)\bping(?:\.exe)?\b') {
            $operation = 'PING'
            $score = 70
        }
        elseif ($name -match '(?i)^powershell(?:\.exe)?$|^pwsh(?:\.exe)?$') {
            $operation = 'PowerShell'
            $score = 30
        }
        elseif ($name -match '(?i)^cmd(?:\.exe)?$|^bash(?:\.exe)?$|^sh(?:\.exe)?$') {
            $operation = [IO.Path]::GetFileNameWithoutExtension($name)
            $score = 20
        }
        else {
            $operation = [IO.Path]::GetFileNameWithoutExtension($name)
            $score = 40
        }

        [pscustomobject]@{
            Score       = $score
            Operation   = $operation
            CreationDate = $process.CreationDate
        }
    }

    $best = $candidates | Sort-Object -Property Score, CreationDate -Descending | Select-Object -First 1
    if ($null -eq $best -or [string]::IsNullOrWhiteSpace([string]$best.Operation)) {
        return 'ローカル処理'
    }
    return [string]$best.Operation
}

function Get-ProjectNameFromProcesses {
    param([object[]]$Processes)

    $projectNames = New-Object System.Collections.ArrayList
    foreach ($process in $Processes) {
        $commandLine = [string]$process.CommandLine
        if ($commandLine -match '(?i)[A-Z]:[\\/]Users[\\/][^\\/\s"]+[\\/]Documents[\\/]Projects[\\/]([^\\/\s"]+)') {
            $projectName = [string]$Matches[1]
            if (-not [string]::IsNullOrWhiteSpace($projectName) -and $projectNames -notcontains $projectName) {
                [void]$projectNames.Add($projectName)
            }
        }
    }

    if ($projectNames.Count -eq 0) {
        return $null
    }
    return ($projectNames -join ' + ')
}

function Get-ActiveStartTime {
    param([object[]]$Processes)

    $dates = @($Processes |
        Where-Object { $null -ne $_.CreationDate } |
        Select-Object -ExpandProperty CreationDate)
    if ($dates.Count -eq 0) {
        return Get-Date
    }
    return [DateTime]($dates | Sort-Object | Select-Object -First 1)
}

function Get-ActiveCpuSeconds {
    param([object[]]$Processes)

    $ids = @($Processes | Select-Object -ExpandProperty ProcessId -Unique)
    if ($ids.Count -eq 0) {
        return 0.0
    }

    try {
        $measurement = Get-Process -Id $ids -ErrorAction SilentlyContinue | Measure-Object -Property CPU -Sum
        if ($null -ne $measurement.Sum) {
            return [double]$measurement.Sum
        }
    }
    catch {
        return 0.0
    }
    return 0.0
}

function New-ProcessActivity {
    param(
        $Group,
        $FallbackTool
    )

    $processes = @($Group.Processes)
    $startedAt = Get-ActiveStartTime -Processes $processes
    $projectName = Get-ProjectNameFromProcesses -Processes $processes
    $estimated = $false
    if ([string]::IsNullOrWhiteSpace([string]$projectName)) {
        $projectName = if ($null -ne $FallbackTool) { [string]$FallbackTool.ProjectName } else { '不明' }
        $estimated = $true
    }

    return [pscustomobject]@{
        Id               = "process:$($Group.RootProcessId)"
        Workspace        = ''
        State            = 'Working'
        Label            = '作業中'
        ProjectName      = $projectName
        ProjectEstimated = $estimated
        Operation        = Get-SafeProcessOperation -Processes $processes
        StartedAt        = $startedAt
        ElapsedSeconds   = [Math]::Max(0.0, ((Get-Date) - $startedAt).TotalSeconds)
        Processes        = $processes
    }
}

function Get-RecentWorkspaceActivities {
    param(
        [object[]]$RecentTools,
        [object[]]$ActiveActivities,
        [int]$WindowSeconds
    )

    $now = Get-Date
    $activeProjectCounts = @{}
    foreach ($activity in $ActiveActivities) {
        $project = [string]$activity.ProjectName
        if (-not $activeProjectCounts.ContainsKey($project)) {
            $activeProjectCounts[$project] = 0
        }
        $activeProjectCounts[$project]++
    }

    $activities = New-Object System.Collections.ArrayList
    foreach ($tool in @($RecentTools | Sort-Object -Property Time -Descending)) {
        $ageSeconds = ($now - $tool.Time.LocalDateTime).TotalSeconds
        if ($ageSeconds -lt 0 -or $ageSeconds -gt $WindowSeconds) {
            continue
        }

        $project = [string]$tool.ProjectName
        if ($activeProjectCounts.ContainsKey($project) -and $activeProjectCounts[$project] -gt 0) {
            $activeProjectCounts[$project]--
            continue
        }

        [void]$activities.Add([pscustomobject]@{
            Id               = "workspace:$($tool.Workspace)"
            Workspace        = [string]$tool.Workspace
            State            = if ($tool.Success) { 'Waiting' } else { 'Failed' }
            Label            = if ($tool.Success) { '次の処理待ち' } else { '処理失敗' }
            ProjectName      = $project
            ProjectEstimated = $false
            Operation        = [string]$tool.Operation
            StartedAt        = $tool.Time.LocalDateTime
            ElapsedSeconds   = [Math]::Max(0.0, $ageSeconds)
            Processes        = @()
        })

        if ($activities.Count -ge 6) {
            break
        }
    }

    return @($activities)
}

function Get-DevSpaceStatus {
    param(
        [int]$ListenPort,
        [string]$ServeLogPath
    )

    $serverProcessId = Get-DevSpaceServerProcessId -ListenPort $ListenPort
    $logSnapshot = Get-LogSnapshot -Path $ServeLogPath
    $lastTool = $logSnapshot.LastTool
    $recentTools = @((Get-PropertyValue -Object $logSnapshot -Name 'RecentTools' -DefaultValue @()))

    if ($null -eq $serverProcessId) {
        return [pscustomobject]@{
            State            = 'Stopped'
            Label            = '停止中'
            Summary          = 'DevSpaceは停止しています'
            ServerProcessId  = $null
            ActiveProcesses  = @()
            Activities       = @()
            LastTool         = $lastTool
            ProjectName      = if ($null -ne $lastTool) { $lastTool.ProjectName } else { '不明' }
            ProjectEstimated = $false
            Operation        = '停止'
            StartedAt        = $null
            ElapsedSeconds   = 0.0
            ActiveCpuSeconds = 0.0
            LogWriteTimeUtc  = $logSnapshot.LastWriteTimeUtc
        }
    }

    $snapshot = Get-ProcessSnapshot
    $processGroups = @(Get-DevSpaceProcessGroups -ServerProcessId $serverProcessId -Processes $snapshot)
    $processActivities = @($processGroups | ForEach-Object { New-ProcessActivity -Group $_ -FallbackTool $lastTool })
    $recentActivities = @(Get-RecentWorkspaceActivities -RecentTools $recentTools -ActiveActivities $processActivities -WindowSeconds $CompletionQuietSeconds)
    $activities = @($processActivities) + @($recentActivities)

    if ($processActivities.Count -gt 0) {
        $activeProcesses = @($processActivities | ForEach-Object { @($_.Processes) })
        $startedAt = [DateTime](($processActivities | Sort-Object -Property StartedAt | Select-Object -First 1).StartedAt)
        $elapsed = [Math]::Max(0.0, ((Get-Date) - $startedAt).TotalSeconds)
        $projects = @($processActivities | Select-Object -ExpandProperty ProjectName -Unique)
        $projectName = $projects -join ' + '
        $projectEstimated = @($processActivities | Where-Object { $_.ProjectEstimated }).Count -gt 0
        $operation = if ($processActivities.Count -eq 1) {
            [string]$processActivities[0].Operation
        }
        else {
            "$($processActivities.Count)件を並列実行"
        }

        return [pscustomobject]@{
            State            = 'Working'
            Label            = '作業中'
            Summary          = if ($processActivities.Count -eq 1) { "DevSpaceが作業中です ($operation)" } else { "DevSpaceが$($processActivities.Count)件を並列実行中です" }
            ServerProcessId  = $serverProcessId
            ActiveProcesses  = $activeProcesses
            Activities       = $activities
            LastTool         = $lastTool
            ProjectName      = $projectName
            ProjectEstimated = $projectEstimated
            Operation        = $operation
            StartedAt        = $startedAt
            ElapsedSeconds   = $elapsed
            ActiveCpuSeconds = Get-ActiveCpuSeconds -Processes $activeProcesses
            LogWriteTimeUtc  = $logSnapshot.LastWriteTimeUtc
        }
    }

    if ($null -ne $lastTool) {
        $secondsSinceCompletion = ((Get-Date) - $lastTool.Time.LocalDateTime).TotalSeconds
        if ($secondsSinceCompletion -ge 0 -and $secondsSinceCompletion -lt 12) {
            return [pscustomobject]@{
                State            = if ($lastTool.Success) { 'JustFinished' } else { 'Failed' }
                Label            = if ($lastTool.Success) { '次の処理待ち' } else { '処理失敗' }
                Summary          = if ($lastTool.Success) { '直前の処理が終了し、次の操作を待っています' } else { '直前の処理が失敗しました' }
                ServerProcessId  = $serverProcessId
                ActiveProcesses  = @()
                Activities       = $activities
                LastTool         = $lastTool
                ProjectName      = $lastTool.ProjectName
                ProjectEstimated = $false
                Operation        = $lastTool.Operation
                StartedAt        = $lastTool.Time.LocalDateTime.AddMilliseconds(-1 * $lastTool.DurationMs)
                ElapsedSeconds   = $lastTool.DurationMs / 1000.0
                ActiveCpuSeconds = 0.0
                LogWriteTimeUtc  = $logSnapshot.LastWriteTimeUtc
            }
        }
    }

    return [pscustomobject]@{
        State            = 'Idle'
        Label            = '待機中'
        Summary          = 'DevSpaceは起動済みで、現在は待機中です'
        ServerProcessId  = $serverProcessId
        ActiveProcesses  = @()
        Activities       = $activities
        LastTool         = $lastTool
        ProjectName      = if ($null -ne $lastTool) { $lastTool.ProjectName } else { '不明' }
        ProjectEstimated = $false
        Operation        = '待機'
        StartedAt        = $null
        ElapsedSeconds   = 0.0
        ActiveCpuSeconds = 0.0
        LogWriteTimeUtc  = $logSnapshot.LastWriteTimeUtc
    }
}

function Update-StallState {
    param($Status)

    $now = Get-Date
    if ($Status.State -eq 'Working') {
        if (-not $script:activityActive) {
            $script:activityActive = $true
            $script:activityStartedAt = $Status.StartedAt
            $script:lastActivityAt = $now
            $script:lastCpuSeconds = [double]$Status.ActiveCpuSeconds
            $script:lastObservedLogWriteUtc = $Status.LogWriteTimeUtc
            $script:stallNotified = $false
        }
        else {
            $cpuDelta = [double]$Status.ActiveCpuSeconds - $script:lastCpuSeconds
            $logChanged = $Status.LogWriteTimeUtc -ne $script:lastObservedLogWriteUtc
            if ($cpuDelta -gt 0.05 -or $logChanged) {
                $script:lastActivityAt = $now
            }
            $script:lastCpuSeconds = [double]$Status.ActiveCpuSeconds
            $script:lastObservedLogWriteUtc = $Status.LogWriteTimeUtc
        }

        if ($null -ne $script:activityStartedAt) {
            $Status.StartedAt = $script:activityStartedAt
            $Status.ElapsedSeconds = [Math]::Max(0.0, ($now - $script:activityStartedAt).TotalSeconds)
        }

        $inactiveSeconds = if ($null -ne $script:lastActivityAt) { ($now - $script:lastActivityAt).TotalSeconds } else { 0.0 }
        if ($Status.ElapsedSeconds -ge ($StallMinutes * 60) -and $inactiveSeconds -ge ($StallMinutes * 60)) {
            $Status.State = 'Stalled'
            $Status.Label = '停滞の疑い'
            $Status.Summary = "$StallMinutes分以上、CPU・ログ更新が確認できません"
        }
    }
    else {
        $script:activityActive = $false
        $script:activityStartedAt = $null
        $script:lastActivityAt = $null
        $script:lastCpuSeconds = 0.0
        $script:stallNotified = $false
    }

    return $Status
}

function Format-Duration {
    param([double]$TotalSeconds)

    $seconds = [Math]::Max(0, [int][Math]::Floor($TotalSeconds))
    $span = [TimeSpan]::FromSeconds($seconds)
    if ($span.TotalHours -ge 1) {
        return '{0}:{1:00}:{2:00}' -f [int]$span.TotalHours, $span.Minutes, $span.Seconds
    }
    return '{0:00}:{1:00}' -f $span.Minutes, $span.Seconds
}

function Format-LastToolText {
    param($LastTool)

    if ($null -eq $LastTool) {
        return '最終作業: 記録なし'
    }

    $result = if ($LastTool.Success) { '成功' } else { '失敗' }
    $duration = Format-Duration -TotalSeconds ($LastTool.DurationMs / 1000.0)
    return "最終作業: $($LastTool.Tool) / $result / $duration / $($LastTool.Time.ToString('HH:mm:ss'))"
}

function Get-ToolKey {
    param($LastTool)

    if ($null -eq $LastTool) {
        return $null
    }
    return "$($LastTool.Time.ToString('o'))|$($LastTool.Tool)|$($LastTool.Workspace)|$($LastTool.DurationMs)"
}

function Write-PetState {
    param(
        $Status,
        [string]$Path
    )

    try {
        $directory = Split-Path -Parent $Path
        if (-not (Test-Path -LiteralPath $directory)) {
            [void](New-Item -ItemType Directory -Path $directory -Force)
        }

        $lastTool = $null
        if ($null -ne $Status.LastTool) {
            $lastTool = [ordered]@{
                Time       = $Status.LastTool.Time.ToString('o')
                Tool       = [string]$Status.LastTool.Tool
                Success    = [bool]$Status.LastTool.Success
                DurationMs = [int64]$Status.LastTool.DurationMs
                Project    = [string]$Status.LastTool.ProjectName
                Operation  = [string]$Status.LastTool.Operation
            }
        }

        $activities = @()
        foreach ($activity in @((Get-PropertyValue -Object $Status -Name 'Activities' -DefaultValue @()))) {
            $activities += [ordered]@{
                Id               = [string]$activity.Id
                Workspace        = [string]$activity.Workspace
                State            = [string]$activity.State
                Label            = [string]$activity.Label
                ProjectName      = [string]$activity.ProjectName
                ProjectEstimated = [bool]$activity.ProjectEstimated
                Operation        = [string]$activity.Operation
                ElapsedSeconds   = [Math]::Round([double]$activity.ElapsedSeconds, 1)
            }
        }

        $payload = [ordered]@{
            SchemaVersion   = 2
            State           = [string]$Status.State
            Label           = [string]$Status.Label
            Summary         = [string]$Status.Summary
            ProjectName     = [string]$Status.ProjectName
            ProjectEstimated = [bool]$Status.ProjectEstimated
            Operation       = [string]$Status.Operation
            ElapsedSeconds  = [Math]::Round([double]$Status.ElapsedSeconds, 1)
            UpdatedAt       = (Get-Date).ToString('o')
            Activities      = $activities
            LastTool        = $lastTool
        }

        $json = $payload | ConvertTo-Json -Depth 5
        $tempPath = "$Path.tmp.$PID"
        $encoding = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($tempPath, $json, $encoding)
        if (Test-Path -LiteralPath $Path) {
            Remove-Item -LiteralPath $Path -Force
        }
        Move-Item -LiteralPath $tempPath -Destination $Path -Force
    }
    catch {
        # The tray monitor must keep running even if the pet state file cannot be written.
    }
}

function Write-OneShotStatus {
    param($Status)

    $status = Update-StallState -Status $Status
    $stateColor = switch ($status.State) {
        'Working'      { 'Green' }
        'Stalled'      { 'Magenta' }
        'JustFinished' { 'Yellow' }
        'Failed'       { 'Red' }
        'Idle'         { 'Cyan' }
        default        { 'Red' }
    }

    Write-Host "DevSpace: $($status.Label)" -ForegroundColor $stateColor
    Write-Host $status.Summary
    Write-Host "プロジェクト: $($status.ProjectName)"
    Write-Host "処理: $($status.Operation)"
    if ($status.ElapsedSeconds -gt 0) {
        Write-Host "経過時間: $(Format-Duration -TotalSeconds $status.ElapsedSeconds)"
    }
    if ($null -ne $status.ServerProcessId) {
        Write-Host "Server PID: $($status.ServerProcessId) / Port: $Port"
    }
    Write-Host (Format-LastToolText -LastTool $status.LastTool)

    if ($status.ActiveProcesses.Count -gt 0) {
        Write-Host ''
        Write-Host '実行中プロセス:'
        $status.ActiveProcesses |
            Select-Object ProcessId, Name, CreationDate, CommandLine |
            Format-Table -AutoSize -Wrap
    }
}

if ($Once) {
    $onceStatus = Get-DevSpaceStatus -ListenPort $Port -ServeLogPath $LogPath
    $onceStatus = Update-StallState -Status $onceStatus
    Write-PetState -Status $onceStatus -Path $StatePath
    Write-OneShotStatus -Status $onceStatus
    exit 0
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class DevSpaceStatusNativeMethods
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool DestroyIcon(IntPtr handle);
}
'@

function New-ColoredIcon {
    param([System.Drawing.Color]$Color)

    $bitmap = New-Object System.Drawing.Bitmap 16, 16
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $brush = New-Object System.Drawing.SolidBrush $Color
    $borderPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(90, 0, 0, 0)), 1
    $graphics.FillEllipse($brush, 1, 1, 13, 13)
    $graphics.DrawEllipse($borderPen, 1, 1, 13, 13)

    $handle = $bitmap.GetHicon()
    $icon = [System.Drawing.Icon]::FromHandle($handle).Clone()
    [void][DevSpaceStatusNativeMethods]::DestroyIcon($handle)

    $borderPen.Dispose()
    $brush.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()

    return $icon
}

function Show-TrayNotification {
    param(
        [string]$Title,
        [string]$Text,
        [System.Windows.Forms.ToolTipIcon]$Icon = [System.Windows.Forms.ToolTipIcon]::Info
    )

    $notifyIcon.BalloonTipTitle = $Title
    $notifyIcon.BalloonTipText = $Text
    $notifyIcon.BalloonTipIcon = $Icon
    $notifyIcon.ShowBalloonTip(6000)
}

function Update-WorkSessionNotification {
    param($Status)

    $now = Get-Date
    $toolKey = Get-ToolKey -LastTool $Status.LastTool
    $newToolDetected = $false

    if (-not $script:initialized) {
        $script:lastSeenToolKey = $toolKey
        $script:initialized = $true
    }
    elseif ($null -ne $toolKey -and $toolKey -ne $script:lastSeenToolKey) {
        $script:lastSeenToolKey = $toolKey
        $newToolDetected = $true
    }

    $isActivelyWorking = $Status.State -in @('Working', 'Stalled')
    if ($isActivelyWorking -or $newToolDetected) {
        if (-not $script:workSessionActive) {
            $script:workSessionActive = $true
            $script:workSessionStartedAt = if ($null -ne $Status.StartedAt) { $Status.StartedAt } else { $now }
        }

        $script:workSessionLastActivityAt = $now
        if (-not [string]::IsNullOrWhiteSpace([string]$Status.ProjectName) -and $Status.ProjectName -ne '不明') {
            $script:workSessionProject = [string]$Status.ProjectName
        }
        if (-not [string]::IsNullOrWhiteSpace([string]$Status.Operation) -and $Status.Operation -notin @('待機', '停止')) {
            $script:workSessionOperation = [string]$Status.Operation
        }
        if ($newToolDetected -and $null -ne $Status.LastTool) {
            $script:workSessionLastTool = $Status.LastTool
        }
        return
    }

    if (-not $script:workSessionActive -or $null -eq $script:workSessionLastActivityAt) {
        return
    }

    $quietSeconds = ($now - $script:workSessionLastActivityAt).TotalSeconds
    if ($quietSeconds -lt $CompletionQuietSeconds) {
        return
    }

    $sessionSeconds = if ($null -ne $script:workSessionStartedAt) {
        [Math]::Max(0.0, ($now - $script:workSessionStartedAt).TotalSeconds - $quietSeconds)
    }
    else {
        0.0
    }

    if ($sessionSeconds -ge $NotifyAfterSeconds) {
        $finalFailed = $null -ne $script:workSessionLastTool -and -not [bool]$script:workSessionLastTool.Success
        $title = if ($finalFailed) { 'DevSpace 作業失敗' } else { 'DevSpace 作業区切り完了' }
        $icon = if ($finalFailed) { [System.Windows.Forms.ToolTipIcon]::Error } else { [System.Windows.Forms.ToolTipIcon]::Info }
        $text = "$($script:workSessionProject)`n$($script:workSessionOperation)`n作業時間: $(Format-Duration -TotalSeconds $sessionSeconds)"
        Show-TrayNotification -Title $title -Text $text -Icon $icon
    }

    $script:workSessionActive = $false
    $script:workSessionStartedAt = $null
    $script:workSessionLastActivityAt = $null
    $script:workSessionProject = '不明'
    $script:workSessionOperation = '作業'
    $script:workSessionLastTool = $null
}

$createdNew = $false
$mutex = New-Object System.Threading.Mutex($true, 'Local\DevSpaceStatusTray', [ref]$createdNew)
if (-not $createdNew) {
    $mutex.Dispose()
    exit 0
}

$icons = @{
    Working      = New-ColoredIcon -Color ([System.Drawing.Color]::LimeGreen)
    Stalled      = New-ColoredIcon -Color ([System.Drawing.Color]::MediumPurple)
    JustFinished = New-ColoredIcon -Color ([System.Drawing.Color]::Gold)
    Failed       = New-ColoredIcon -Color ([System.Drawing.Color]::OrangeRed)
    Idle         = New-ColoredIcon -Color ([System.Drawing.Color]::DodgerBlue)
    Stopped      = New-ColoredIcon -Color ([System.Drawing.Color]::Crimson)
}

$notifyIcon = New-Object System.Windows.Forms.NotifyIcon
$notifyIcon.Visible = $true
$notifyIcon.Icon = $icons.Idle
$notifyIcon.Text = 'DevSpace: 確認中'

$menu = New-Object System.Windows.Forms.ContextMenuStrip
$statusMenuItem = New-Object System.Windows.Forms.ToolStripMenuItem
$statusMenuItem.Text = '状態を確認中...'
$statusMenuItem.Enabled = $false
[void]$menu.Items.Add($statusMenuItem)

$projectMenuItem = New-Object System.Windows.Forms.ToolStripMenuItem
$projectMenuItem.Text = 'プロジェクト: 確認中'
$projectMenuItem.Enabled = $false
[void]$menu.Items.Add($projectMenuItem)

$operationMenuItem = New-Object System.Windows.Forms.ToolStripMenuItem
$operationMenuItem.Text = '処理: 確認中'
$operationMenuItem.Enabled = $false
[void]$menu.Items.Add($operationMenuItem)

$elapsedMenuItem = New-Object System.Windows.Forms.ToolStripMenuItem
$elapsedMenuItem.Text = '経過時間: --:--'
$elapsedMenuItem.Enabled = $false
[void]$menu.Items.Add($elapsedMenuItem)

$lastToolMenuItem = New-Object System.Windows.Forms.ToolStripMenuItem
$lastToolMenuItem.Text = '最終作業: 記録なし'
$lastToolMenuItem.Enabled = $false
[void]$menu.Items.Add($lastToolMenuItem)

[void]$menu.Items.Add((New-Object System.Windows.Forms.ToolStripSeparator))

$refreshMenuItem = New-Object System.Windows.Forms.ToolStripMenuItem
$refreshMenuItem.Text = '今すぐ再確認'
[void]$menu.Items.Add($refreshMenuItem)

$detailsMenuItem = New-Object System.Windows.Forms.ToolStripMenuItem
$detailsMenuItem.Text = '詳細を表示'
[void]$menu.Items.Add($detailsMenuItem)

$logMenuItem = New-Object System.Windows.Forms.ToolStripMenuItem
$logMenuItem.Text = 'ログを開く'
[void]$menu.Items.Add($logMenuItem)

$folderMenuItem = New-Object System.Windows.Forms.ToolStripMenuItem
$folderMenuItem.Text = '.devspaceフォルダーを開く'
[void]$menu.Items.Add($folderMenuItem)

[void]$menu.Items.Add((New-Object System.Windows.Forms.ToolStripSeparator))
$exitMenuItem = New-Object System.Windows.Forms.ToolStripMenuItem
$exitMenuItem.Text = '終了'
[void]$menu.Items.Add($exitMenuItem)

$notifyIcon.ContextMenuStrip = $menu
$script:currentStatus = $null

function Update-TrayStatus {
    try {
        $status = Get-DevSpaceStatus -ListenPort $Port -ServeLogPath $LogPath
        $status = Update-StallState -Status $status
        $script:currentStatus = $status
        Write-PetState -Status $status -Path $StatePath
        $notifyIcon.Icon = $icons[$status.State]

        $elapsedText = if ($status.ElapsedSeconds -gt 0) { Format-Duration -TotalSeconds $status.ElapsedSeconds } else { '--:--' }
        $tooltip = "DevSpace: $($status.Label)"
        if ($status.State -eq 'Working' -or $status.State -eq 'Stalled') {
            $tooltip += " / $($status.ProjectName) / $elapsedText"
        }
        if ($tooltip.Length -gt 63) {
            $tooltip = $tooltip.Substring(0, 63)
        }
        $notifyIcon.Text = $tooltip

        $statusMenuItem.Text = "$($status.Label) — $($status.Summary)"
        $projectSuffix = if ($status.ProjectEstimated) { '（推定）' } else { '' }
        $projectMenuItem.Text = "プロジェクト: $($status.ProjectName)$projectSuffix"
        $operationMenuItem.Text = "処理: $($status.Operation)"
        $elapsedMenuItem.Text = "経過時間: $elapsedText"
        $lastToolMenuItem.Text = Format-LastToolText -LastTool $status.LastTool

        Update-WorkSessionNotification -Status $status

        if ($status.State -eq 'Stalled' -and -not $script:stallNotified) {
            $script:stallNotified = $true
            Show-TrayNotification -Title 'DevSpace 停滞の疑い' -Text "$($status.ProjectName)`n$($status.Operation)`n$($status.Summary)" -Icon ([System.Windows.Forms.ToolTipIcon]::Warning)
        }

        if ($script:previousState -ne $null -and $status.State -eq 'Stopped' -and $script:previousState -ne 'Stopped') {
            Show-TrayNotification -Title 'DevSpace 停止' -Text 'DevSpaceサーバーが停止しました。' -Icon ([System.Windows.Forms.ToolTipIcon]::Error)
        }
        $script:previousState = $status.State
    }
    catch {
        $notifyIcon.Icon = $icons.Stopped
        $notifyIcon.Text = 'DevSpace: 状態確認エラー'
        $statusMenuItem.Text = '状態確認エラー'
    }
}

$refreshMenuItem.Add_Click({ Update-TrayStatus })
$detailsMenuItem.Add_Click({
    if ($null -ne $script:currentStatus) {
        $status = $script:currentStatus
        $elapsedText = if ($status.ElapsedSeconds -gt 0) { Format-Duration -TotalSeconds $status.ElapsedSeconds } else { '--:--' }
        $processText = if ($status.ActiveProcesses.Count -gt 0) {
            ($status.ActiveProcesses | ForEach-Object { "PID $($_.ProcessId): $($_.Name)" }) -join "`n"
        }
        else {
            'なし'
        }
        $message = @(
            "状態: $($status.Label)",
            "プロジェクト: $($status.ProjectName)",
            "処理: $($status.Operation)",
            "経過時間: $elapsedText",
            (Format-LastToolText -LastTool $status.LastTool),
            '',
            '実行中プロセス:',
            $processText
        ) -join "`n"
        [System.Windows.Forms.MessageBox]::Show($message, 'DevSpace Status') | Out-Null
    }
})
$logMenuItem.Add_Click({
    if (Test-Path -LiteralPath $LogPath) {
        Start-Process notepad.exe -ArgumentList @($LogPath)
    }
    else {
        [System.Windows.Forms.MessageBox]::Show("ログが見つかりません。`n$LogPath", 'DevSpace Status') | Out-Null
    }
})
$folderMenuItem.Add_Click({
    $folder = Split-Path -Parent $LogPath
    if (Test-Path -LiteralPath $folder) {
        Start-Process explorer.exe -ArgumentList @($folder)
    }
})
$notifyIcon.Add_MouseClick({
    param($sender, $eventArgs)
    if ($eventArgs.Button -eq [System.Windows.Forms.MouseButtons]::Left -and $null -ne $script:currentStatus) {
        $status = $script:currentStatus
        $elapsedText = if ($status.ElapsedSeconds -gt 0) { Format-Duration -TotalSeconds $status.ElapsedSeconds } else { '--:--' }
        $notifyIcon.BalloonTipTitle = "DevSpace: $($status.Label)"
        $notifyIcon.BalloonTipText = "$($status.ProjectName)`n$($status.Operation)`n経過時間: $elapsedText"
        $notifyIcon.BalloonTipIcon = if ($status.State -eq 'Stopped' -or $status.State -eq 'Failed') {
            [System.Windows.Forms.ToolTipIcon]::Error
        }
        elseif ($status.State -eq 'Stalled') {
            [System.Windows.Forms.ToolTipIcon]::Warning
        }
        else {
            [System.Windows.Forms.ToolTipIcon]::Info
        }
        $notifyIcon.ShowBalloonTip(5000)
    }
})
$exitMenuItem.Add_Click({ [System.Windows.Forms.Application]::Exit() })

$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = $RefreshSeconds * 1000
$timer.Add_Tick({ Update-TrayStatus })

Update-TrayStatus
$timer.Start()

try {
    [System.Windows.Forms.Application]::Run()
}
finally {
    $timer.Stop()
    $timer.Dispose()
    $notifyIcon.Visible = $false
    $notifyIcon.Dispose()
    $menu.Dispose()
    foreach ($icon in $icons.Values) {
        $icon.Dispose()
    }
    $mutex.ReleaseMutex()
    $mutex.Dispose()
}
