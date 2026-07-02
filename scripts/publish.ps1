param(
  [Parameter(Mandatory = $true)]
  [string]$Message
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

git add --all
if (-not (git diff --cached --quiet)) {
  git commit -m $Message
}

if (-not (git remote get-url production 2>$null)) {
  throw '尚未配置 production 远端。请先添加腾讯云服务器上的 Git 仓库地址。'
}

git push production main
Write-Host '代码已提交并发布到腾讯云。' -ForegroundColor Green
