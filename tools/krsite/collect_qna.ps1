# 한국 공식 사이트 카드 Q&A(digimoncard.co.kr/qna)를 세트별로 받아 data/kr/qna/ 에 저장한다.
# 개인 로컬 사용 전용 — data/kr/ 는 git 제외. Q&A 문장은 코드나 커밋에 옮기지 않는다.
#
#   .\collect_qna.ps1               # 전체 세트 (받은 세트는 건너뜀)
#   .\collect_qna.ps1 -Only 261     # 지정한 세트 카테고리만
#   .\collect_qna.ps1 -Refresh      # 다시 받기
param(
  [string]$DataDir = (Join-Path $PSScriptRoot "..\..\data"),
  [int]$DelayMs = 1500,
  [int[]]$Only = @(),
  [switch]$Refresh
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http
$Base = "https://digimoncard.co.kr"
$DataDir = [System.IO.Path]::GetFullPath($DataDir)
$QnaDir = Join-Path $DataDir "kr\qna"
New-Item -ItemType Directory -Force -Path $QnaDir | Out-Null

$http = New-Object System.Net.Http.HttpClient
$http.Timeout = [TimeSpan]::FromSeconds(60)
$http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) DigimonTcgPersonalCollector/1.0")

function Get-Html([string]$url) {
  for ($try = 1; $try -le 3; $try++) {
    try {
      Start-Sleep -Milliseconds $DelayMs
      $resp = $http.GetAsync($url).Result
      if ([int]$resp.StatusCode -eq 429 -or [int]$resp.StatusCode -ge 500) { throw "HTTP $([int]$resp.StatusCode)" }
      if (-not $resp.IsSuccessStatusCode) { return $null }
      return [System.Text.Encoding]::UTF8.GetString($resp.Content.ReadAsByteArrayAsync().Result)
    } catch {
      Write-Warning "요청 실패 ($try/3): $url — $_"
      Start-Sleep -Seconds (10 * $try)
    }
  }
  return $null
}

function Clean([string]$s) {
  if ($null -eq $s) { return "" }
  $s = [regex]::Replace($s, "<br\s*/?>", "`n")
  $s = [regex]::Replace($s, "<[^>]+>", "")
  $s = [System.Net.WebUtility]::HtmlDecode($s)
  $s = [regex]::Replace($s, "[ \t\r]+", " ")
  $s = [regex]::Replace($s, " *\n *", "`n")
  $s = [regex]::Replace($s, "\n{2,}", "`n")
  return $s.Trim()
}

function Parse-Qna([string]$html) {
  $items = @()
  foreach ($box in [regex]::Split($html, '<dl class="qa_box')) {
    $cat = [regex]::Match($box, '<dt class="qa_category">(.*?)</dt>', 'Singleline')
    if (-not $cat.Success) { continue }
    $head = Clean $cat.Groups[1].Value
    $m = [regex]::Match($head, '^([A-Za-z]+\d*-\d+)\S*\s*(.*)$')
    $cardId = if ($m.Success) { $m.Groups[1].Value.ToUpperInvariant() } else { "" }
    $cardName = if ($m.Success) { $m.Groups[2].Value } else { $head }
    $q = [regex]::Match($box, '<dl class="questions">\s*<dt>(.*?)</dt>\s*<dd>(.*?)</dd>', 'Singleline')
    $a = [regex]::Match($box, '<dl class="answer">\s*<dt>(.*?)</dt>\s*<dd>(.*?)</dd>', 'Singleline')
    if (-not $q.Success) { continue }
    $qBody = $q.Groups[2].Value
    $date = Clean ([regex]::Match($qBody, '<span>(.*?)</span>', 'Singleline').Groups[1].Value)
    $qText = Clean ([regex]::Replace($qBody, '<span>.*?</span>', '', 'Singleline'))
    $items += [pscustomobject][ordered]@{
      CardId = $cardId; CardName = $cardName; No = Clean $q.Groups[1].Value
      Question = $qText; Answer = if ($a.Success) { Clean $a.Groups[2].Value } else { "" }; Updated = $date
    }
  }
  return ,$items
}

Write-Host "카드 Q&A 세트 목록 받는 중..."
$top = Get-Html "$Base/qna"
if (-not $top) { throw "qna 페이지를 받지 못했습니다." }
$sets = @()
foreach ($m in [regex]::Matches($top, '<option value="(\d+)"\s*>(.*?)</option>')) {
  $sets += [pscustomobject]@{ Id = [int]$m.Groups[1].Value; Title = Clean $m.Groups[2].Value }
}
if ($Only.Count -gt 0) { $sets = @($sets | Where-Object { $Only -contains $_.Id }) }
Write-Host "세트 $($sets.Count)개"

foreach ($s in $sets) {
  $out = Join-Path $QnaDir "$($s.Id).json"
  if ((Test-Path $out) -and -not $Refresh) { Write-Host "건너뜀: $($s.Title)"; continue }
  $all = @(); $seen = @{}
  for ($page = 1; $page -le 200; $page++) {
    $html = Get-Html "$Base/index.php?mid=qna&category=$($s.Id)&is_card_search=true&page=$page"
    if (-not $html) { break }
    $items = Parse-Qna $html
    $new = @($items | Where-Object { $k = "$($_.CardId)|$($_.No)|$($_.Question)"; if ($seen.ContainsKey($k)) { $false } else { $seen[$k] = 1; $true } })
    if ($new.Count -eq 0) { break }
    $all += $new
    if ($html -notmatch "category=$($s.Id)&amp;is_card_search=true&amp;page=$($page + 1)") { break }
  }
  [ordered]@{ Id = $s.Id; Title = $s.Title; Fetched = (Get-Date).ToString("s"); Items = $all } |
    ConvertTo-Json -Depth 5 | Set-Content -Path $out -Encoding UTF8
  Write-Host ("{0,-40} {1,4}건" -f $s.Title, $all.Count)
}

# 합치기: data/kr/qna/qna_all.json (카드 번호 · Q 번호 기준)
$merged = foreach ($f in Get-ChildItem $QnaDir -Filter "*.json" | Where-Object { $_.BaseName -match '^\d+$' }) {
  $set = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
  foreach ($it in $set.Items) { $it | Add-Member -NotePropertyName Set -NotePropertyValue $set.Title -Force; $it }
}
@($merged) | ConvertTo-Json -Depth 5 | Set-Content -Path (Join-Path $QnaDir "qna_all.json") -Encoding UTF8
Write-Host "qna_all.json: $(@($merged).Count)건"
