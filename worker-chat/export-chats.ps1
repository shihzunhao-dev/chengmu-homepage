<#
.SYNOPSIS
    把官網服務助理的對話紀錄（D1：chengmu-chat-log）匯出到本機硬碟。

.DESCRIPTION
    產生兩個檔案（檔名含匯出時間）：
      chat-log-<時間>.csv  用 Excel 開，可篩選、排序（UTF-8 BOM，中文不亂碼）
      chat-log-<時間>.md   依對話分組，照時間排列，方便閱讀
    時間一律轉成台灣時間（UTC+8）。需要先 wrangler login。

.EXAMPLE
    .\export-chats.ps1
    # 匯出全部紀錄到 D:\澄沐公司\官網客服紀錄

.EXAMPLE
    .\export-chats.ps1 -Days 7 -OutDir D:\Temp
    # 只匯出最近 7 天
#>
[CmdletBinding()]
param(
    [string]$OutDir = 'D:\澄沐公司\官網客服紀錄',
    [int]$Days = 0,
    [string]$Database = 'chengmu-chat-log'
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$where = ''
if ($Days -gt 0) {
    $since = (Get-Date).ToUniversalTime().AddDays(-$Days).ToString('yyyy-MM-ddTHH:mm:ss')
    $where = "WHERE created_at >= '$since'"
}
$sql = "SELECT id, conversation_id, created_at, role, content, country, city, usage, ip FROM chat_messages $where ORDER BY id"

Write-Host "讀取 D1 資料庫 $Database ..." -ForegroundColor Cyan
$raw = wrangler d1 execute $Database --remote --json --command $sql 2>$null
if ($LASTEXITCODE -ne 0 -or -not $raw) {
    Write-Host "讀取失敗。請確認已執行 wrangler login，且資料庫 $Database 已建立。" -ForegroundColor Red
    exit 1
}

# wrangler --json 回傳陣列（每個 SQL 一個結果物件），紀錄在 results 裡
$parsed = ($raw -join "`n") | ConvertFrom-Json
$rows = @()
foreach ($block in @($parsed)) {
    if ($null -ne $block.results) { $rows += @($block.results) }
}

if ($rows.Count -eq 0) {
    Write-Host "目前沒有任何對話紀錄。" -ForegroundColor Yellow
    exit 0
}

$tw = [TimeZoneInfo]::FindSystemTimeZoneById('Taipei Standard Time')
$records = foreach ($r in $rows) {
    $utc = [DateTime]::Parse($r.created_at, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::AdjustToUniversal)
    [pscustomobject]@{
        '編號'     = $r.id
        '對話編號' = $r.conversation_id
        '時間'     = [TimeZoneInfo]::ConvertTimeFromUtc($utc, $tw).ToString('yyyy-MM-dd HH:mm:ss')
        '角色'     = switch ($r.role) { 'user' { '客人' } 'assistant' { '助理' } default { '錯誤' } }
        '內容'     = $r.content
        'IP'       = if ($r.ip) { $r.ip } else { '未記錄' }
        '地區'     = (@($r.country, $r.city) | Where-Object { $_ }) -join ' '
        '用量'     = $r.usage
    }
}

New-Item -ItemType Directory -Force $OutDir | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmm'
$csvPath = Join-Path $OutDir "chat-log-$stamp.csv"
$mdPath  = Join-Path $OutDir "chat-log-$stamp.md"

$records | Export-Csv -Path $csvPath -NoTypeInformation -Encoding utf8BOM

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("# 官網服務助理對話紀錄（匯出時間 $(Get-Date -Format 'yyyy-MM-dd HH:mm')）")
[void]$sb.AppendLine()
$groups = $records | Group-Object '對話編號' | Sort-Object { ($_.Group | Select-Object -First 1).'編號' }
foreach ($g in $groups) {
    $first = $g.Group | Select-Object -First 1
    $questions = @($g.Group | Where-Object { $_.'角色' -eq '客人' }).Count
    [void]$sb.AppendLine("## $($first.'時間')　IP $($first.'IP')　$($first.'地區')　（$questions 個提問）")
    [void]$sb.AppendLine("對話編號：$($g.Name)")
    [void]$sb.AppendLine()
    foreach ($m in $g.Group) {
        $text = ($m.'內容' -replace "`r?`n", "`n> ")
        [void]$sb.AppendLine("**$($m.'角色')**　$($m.'時間'.Substring(11))")
        [void]$sb.AppendLine("> $text")
        [void]$sb.AppendLine()
    }
}
[System.IO.File]::WriteAllText($mdPath, $sb.ToString(), [System.Text.UTF8Encoding]::new($true))

$convCount = @($groups).Count
$askCount = @($records | Where-Object { $_.'角色' -eq '客人' }).Count
Write-Host "匯出完成：$convCount 段對話、$askCount 個提問" -ForegroundColor Green
Write-Host "  $csvPath"
Write-Host "  $mdPath"
