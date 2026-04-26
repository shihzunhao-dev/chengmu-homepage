<#
.SYNOPSIS
    澄沐官網一鍵部署：git push + Cloudflare Pages deploy

.DESCRIPTION
    1. 檢查 git 狀態
    2. 若有變更：stage / commit / push 到 GitHub（→ 自動觸發 GitHub Pages 重建）
    3. 用 wrangler 推到 Cloudflare Pages（即時生效）
    4. 顯示兩個 production URL

.EXAMPLE
    .\deploy.ps1 "fix: 修正聯絡表單樣式"

.EXAMPLE
    .\deploy.ps1
    # 會互動式詢問 commit message
#>

[CmdletBinding()]
param(
    [Parameter(Position=0)]
    [string]$Message,

    # 跳過 git，只推 CF Pages（適合 CF 改錯重推時）
    [switch]$SkipGit,

    # 跳過 wrangler，只 push GitHub
    [switch]$SkipPages
)

$ErrorActionPreference = 'Continue'  # git 把 warning 寫到 stderr，'Stop' 會誤判
$ProgressPreference   = 'SilentlyContinue'

# ── helpers ───────────────────────────────────────────
function Write-Step($msg)    { Write-Host "`n▶ $msg" -ForegroundColor Cyan }
function Write-Ok($msg)      { Write-Host "  $msg" -ForegroundColor Green }
function Write-Warn($msg)    { Write-Host "  $msg" -ForegroundColor Yellow }
function Write-Err($msg)     { Write-Host "  $msg" -ForegroundColor Red }
function Write-Url($label,$url) {
    Write-Host "  $label" -NoNewline -ForegroundColor Gray
    Write-Host " $url" -ForegroundColor White
}

# 切到 script 所在目錄（不管你從哪裡呼叫都可以）
Set-Location $PSScriptRoot

$startedAt = Get-Date
$gitDone = $false
$pagesDone = $false

# ── Step 1: Git ───────────────────────────────────────
if (-not $SkipGit) {
    Write-Step "Step 1/2  Git push → GitHub Pages"

    $status = git status --porcelain 2>$null
    if (-not $status) {
        Write-Warn "沒有變更，跳過 git commit"
        $gitDone = $true
    } else {
        # 互動式取得 commit message（如果沒給）
        if (-not $Message) {
            $Message = Read-Host "  commit message"
            if (-not $Message) {
                Write-Err "commit message 不能空白"
                exit 1
            }
        }

        # 用 $LASTEXITCODE 而非 try/catch（git 的 warning 不是 exception）
        git add . 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Err "git add 失敗"; exit 1 }

        git commit -m $Message 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Err "git commit 失敗"; exit 1 }

        git push 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Err "git push 失敗"
            $gitDone = $false
        } else {
            Write-Ok "git push 完成 → $Message"
            $gitDone = $true
        }
    }
} else {
    Write-Step "Step 1/2  Git push  →  跳過 (-SkipGit)"
    $gitDone = $true
}

# ── Step 2: Cloudflare Pages ──────────────────────────
if (-not $SkipPages) {
    Write-Step "Step 2/2  Wrangler deploy → Cloudflare Pages"

    $wranglerOutput = wrangler pages deploy . `
        --project-name=chengmu-homepage `
        --branch=main `
        --commit-dirty=true 2>&1

    if ($LASTEXITCODE -eq 0) {
        # 從 wrangler 輸出抓出預覽 URL
        $previewUrl = ($wranglerOutput | Select-String 'https://[a-z0-9]+\.chengmu-homepage\.pages\.dev' | Select-Object -First 1).Matches.Value
        Write-Ok "wrangler deploy 完成"
        if ($previewUrl) {
            Write-Url "  本次預覽：" $previewUrl
        }
        $pagesDone = $true
    } else {
        Write-Err "wrangler deploy 失敗"
        Write-Host $wranglerOutput
        $pagesDone = $false
    }
} else {
    Write-Step "Step 2/2  Wrangler deploy  →  跳過 (-SkipPages)"
    $pagesDone = $true
}

# ── Summary ───────────────────────────────────────────
$elapsed = [int]((Get-Date) - $startedAt).TotalSeconds
Write-Host "`n────────────────────────────────────────────" -ForegroundColor DarkGray
Write-Host "完成 (耗時 ${elapsed}s)" -ForegroundColor White
Write-Url "CF Pages   :" "https://chengmu-homepage.pages.dev/"
Write-Url "GitHub Pages:" "https://shihzunhao-dev.github.io/chengmu-homepage/"
Write-Host "────────────────────────────────────────────`n" -ForegroundColor DarkGray

if (-not ($gitDone -and $pagesDone)) { exit 1 }
