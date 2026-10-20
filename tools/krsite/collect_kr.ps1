# 디지몬 카드 게임 한국 공식 사이트(digimoncard.co.kr)에서 카드 데이터 · 이미지 · 룰 자료를 받아 온다.
# 개인 로컬 사용 전용 — 받은 파일은 data/kr/ · data/images_kr/ (git 제외)에만 저장한다.
#
#   .\collect_kr.ps1                 # 카드 데이터 (세트별로 이어받기)
#   .\collect_kr.ps1 -Images         # + 카드 이미지
#   .\collect_kr.ps1 -Docs           # + 룰북 · Q&A 등 자료실 파일
#   .\collect_kr.ps1 -Only 153,488   # 지정한 세트 카테고리만
#   .\collect_kr.ps1 -Refresh        # 이미 받은 세트도 다시 받기
#
# 요청은 한 번에 하나씩, 요청 사이 -DelayMs(기본 1500ms)만큼 쉰다.
param(
  [string]$DataDir = (Join-Path $PSScriptRoot "..\..\data"),
  [int]$DelayMs = 1500,
  [int[]]$Only = @(),
  [switch]$Images,
  [switch]$Docs,
  [switch]$Refresh
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http
$Base = "https://digimoncard.co.kr"
$DataDir = [System.IO.Path]::GetFullPath($DataDir)
$KrDir = Join-Path $DataDir "kr"
$SetDir = Join-Path $KrDir "sets"
$DocDir = Join-Path $KrDir "docs"
$ImgDir = Join-Path $DataDir "images_kr"
foreach ($d in @($KrDir, $SetDir)) { New-Item -ItemType Directory -Force -Path $d | Out-Null }

$http = New-Object System.Net.Http.HttpClient
$http.Timeout = [TimeSpan]::FromSeconds(60)
$http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) DigimonTcgPersonalCollector/1.0")

function Get-Bytes([string]$url) {
  for ($try = 1; $try -le 3; $try++) {
    try {
      Start-Sleep -Milliseconds $DelayMs
      $resp = $http.GetAsync($url).Result
      if ([int]$resp.StatusCode -eq 429 -or [int]$resp.StatusCode -ge 500) { throw "HTTP $([int]$resp.StatusCode)" }
      if (-not $resp.IsSuccessStatusCode) { return $null }
      return $resp.Content.ReadAsByteArrayAsync().Result
    } catch {
      Write-Warning "요청 실패 ($try/3): $url — $_"
      Start-Sleep -Seconds (10 * $try)
    }
  }
  return $null
}

function Get-Html([string]$url) {
  $bytes = Get-Bytes $url
  if ($null -eq $bytes) { return $null }
  return [System.Text.Encoding]::UTF8.GetString($bytes)
}

function Clean([string]$s) {
  if ($null -eq $s) { return "" }
  $s = [regex]::Replace($s, "<br\s*/?>", "`n")
  $s = [regex]::Replace($s, "<[^>]+>", "")
  $s = [System.Net.WebUtility]::HtmlDecode($s)
  $s = [regex]::Replace($s, "[ \t\r]+", " ")
  $s = [regex]::Replace($s, " *\n *", "`n")
  return $s.Trim()
}

function Parse-Cards([string]$html, [string]$setTitle) {
  $cards = @()
  $blocks = [regex]::Split($html, '<li class="image_lists_item')
  for ($i = 1; $i -lt $blocks.Length; $i++) {
    $b = $blocks[$i]
    $no = Clean ([regex]::Match($b, '<li class="cardno">(.*?)</li>', 'Singleline').Groups[1].Value)
    if (-not $no) { continue }
    $head = [regex]::Match($b, '<ul class="cardinfo_head">(.*?)</ul>', 'Singleline').Groups[1].Value
    $lis = [regex]::Matches($head, '<li([^>]*)>(.*?)</li>', 'Singleline') | ForEach-Object { @{ Attr = $_.Groups[1].Value; Text = Clean $_.Groups[2].Value } }
    $rarity = ($lis | Where-Object { $_.Attr -notmatch 'class=' } | Select-Object -First 1).Text
    $type = ($lis | Where-Object { $_.Attr -match 'class="cardtype"' } | Select-Object -First 1).Text
    $lv = ($lis | Where-Object { $_.Attr -match 'cardlv' } | Select-Object -First 1).Text
    $parallel = $b -match 'cardParallel'
    $rawName = Clean ([regex]::Match($b, '<div class="card_name">(.*?)</div>', 'Singleline').Groups[1].Value)
    $name = [regex]::Replace($rawName, '^' + [regex]::Escape($no) + '\S*\s*', '')
    $colors = [regex]::Match($b, '<div class="card_detail ([^"]*)"').Groups[1].Value -split '\s+' |
      Where-Object { $_ -like 'card_detail_*' } | ForEach-Object { $_.Substring(12) }
    $img = [regex]::Match($b, '<img src="([^"]+)"').Groups[1].Value
    $fields = [ordered]@{}
    foreach ($m in [regex]::Matches($b, '<dt>(.*?)</dt>\s*<dd>(.*?)</dd>', 'Singleline')) {
      $k = Clean $m.Groups[1].Value
      if (-not $fields.Contains($k)) { $fields[$k] = Clean $m.Groups[2].Value }
    }
    $traits = @()
    foreach ($k in @('형태', '속성', '유형')) {
      if ($fields.Contains($k) -and $fields[$k] -and $fields[$k] -ne '-') {
        $traits += ($fields[$k] -split '[,/]' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
      }
    }
    $cards += [pscustomobject][ordered]@{
      CardId = $no.ToUpperInvariant(); Name = $name; Rarity = $rarity; Type = $type; Level = $lv
      Colors = @($colors); Traits = @($traits); Parallel = [bool]$parallel
      DP = $fields['DP']; PlayCost = $fields['등장 코스트']; EvoCost1 = $fields['진화 코스트 1']; EvoCost2 = $fields['진화 코스트 2']
      MainText = $fields['상단 텍스트']; SourceText = $fields['하단 텍스트']; Source = $fields['입수 정보']
      Image = if ($img) { if ($img.StartsWith('http')) { $img } else { $Base + $img } } else { "" }
      Set = $setTitle
    }
  }
  return ,$cards
}

# ---- 1) 세트(카테고리) 목록 ----
Write-Host "세트 목록 받는 중..."
$top = Get-Html "$Base/index.php?mid=cardlist"
if (-not $top) { throw "카드 목록 페이지를 받지 못했습니다." }
$sets = @()
foreach ($m in [regex]::Matches($top, 'category=(\d+)">\s*<span class="nav">.*?<span class="title">(.*?)</span>', 'Singleline')) {
  $sets += [pscustomobject]@{ Id = [int]$m.Groups[1].Value; Title = Clean $m.Groups[2].Value }
}
$sets = @($sets | Sort-Object Id -Unique)
if ($Only.Count -gt 0) { $sets = @($sets | Where-Object { $Only -contains $_.Id }) }
Write-Host "세트 $($sets.Count)개"

# ---- 2) 세트별 카드 (세트마다 파일 하나 — 다시 실행하면 이미 받은 세트는 건너뜀) ----
foreach ($s in $sets) {
  $out = Join-Path $SetDir "$($s.Id).json"
  if ((Test-Path $out) -and -not $Refresh) { Write-Host "건너뜀: $($s.Title)"; continue }
  $all = @(); $seen = @{}
  for ($page = 1; $page -le 40; $page++) {
    $html = Get-Html "$Base/index.php?mid=cardlist&category=$($s.Id)&page=$page"
    if (-not $html) { break }
    $cards = Parse-Cards $html $s.Title
    $new = @($cards | Where-Object { $key = "$($_.CardId)|$($_.Image)"; if ($seen.ContainsKey($key)) { $false } else { $seen[$key] = 1; $true } })
    if ($new.Count -eq 0) { break }
    $all += $new
    if ($html -notmatch "category=$($s.Id)&amp;page=$($page + 1)") { break }
  }
  [ordered]@{ Id = $s.Id; Title = $s.Title; Fetched = (Get-Date).ToString("s"); Cards = $all } |
    ConvertTo-Json -Depth 6 | Set-Content -Path $out -Encoding UTF8
  Write-Host ("{0,-40} {1,4}장" -f $s.Title, $all.Count)
}

# ---- 3) 합치기: data/kr/cards_ko.json (카드 번호 기준, 기본판 우선 · 패러렐 이미지는 따로 보관) ----
$merged = [ordered]@{}
foreach ($f in Get-ChildItem $SetDir -Filter *.json | Sort-Object { [int]$_.BaseName }) {
  $set = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
  foreach ($c in $set.Cards) {
    # 패러렐은 번호 뒤에 P가 붙어 있다 (예: "EX1-001P", 이름 "EX1-001 아구몬") → 기본 번호로 합친다
    $m = [regex]::Match($c.CardId, '^([A-Z]+\d*-\d+)(\S*)$')
    if ($m.Success -and $m.Groups[2].Value) {
      $c.CardId = $m.Groups[1].Value
      $c.Parallel = $true
    }
    $c.Name = [regex]::Replace($c.Name, '^[A-Z]+\d*-\d+\S*\s+', '')
    if (-not $c.Name) { continue }
    $id = $c.CardId
    if (-not $merged.Contains($id)) {
      $merged[$id] = [ordered]@{ Card = $c; Parallels = @() }
    } elseif ($merged[$id].Card.Parallel -and -not $c.Parallel) {
      $merged[$id].Parallels += $merged[$id].Card.Image
      $merged[$id].Card = $c
    } elseif ($c.Image -and $c.Image -ne $merged[$id].Card.Image -and -not ($merged[$id].Parallels -contains $c.Image)) {
      $merged[$id].Parallels += $c.Image
    }
  }
}
$list = foreach ($id in $merged.Keys) {
  $c = $merged[$id].Card
  $c | Add-Member -NotePropertyName ParallelImages -NotePropertyValue @($merged[$id].Parallels) -Force
  $c
}
$cardsOut = Join-Path $KrDir "cards_ko.json"
@($list) | ConvertTo-Json -Depth 6 | Set-Content -Path $cardsOut -Encoding UTF8
Write-Host "cards_ko.json: $($merged.Count)장 → $cardsOut"

# ---- 4) 이미지 (선택) ----
if ($Images) {
  New-Item -ItemType Directory -Force -Path $ImgDir | Out-Null
  foreach ($c in $list) {
    if (-not $c.Image) { continue }
    $ext = [System.IO.Path]::GetExtension(([uri]$c.Image).AbsolutePath); if (-not $ext) { $ext = ".png" }
    $path = Join-Path $ImgDir "$($c.CardId)$ext"
    if (Test-Path $path) { continue }
    $bytes = Get-Bytes $c.Image
    if ($bytes -and $bytes.Length -gt 500) { [System.IO.File]::WriteAllBytes($path, $bytes) }
  }
  Write-Host "이미지 폴더: $ImgDir ($((Get-ChildItem $ImgDir -File).Count)장)"
}

# ---- 5) 룰 자료 (선택) ----
if ($Docs) {
  New-Item -ItemType Directory -Force -Path $DocDir | Out-Null
  $qna = Get-Html "$Base/qna"
  if ($qna) {
    Set-Content -Path (Join-Path $DocDir "qna.html") -Value $qna -Encoding UTF8
    $links = [regex]::Matches($qna, 'href="([^"]*procFileDownload[^"]*)"[^>]*>(.*?)</a>', 'Singleline')
    foreach ($m in $links) {
      $url = [System.Net.WebUtility]::HtmlDecode($m.Groups[1].Value)
      if (-not $url.StartsWith('http')) { $url = $Base + '/' + $url.TrimStart('/') }
      $label = (Clean $m.Groups[2].Value) -replace '[\\/:*?"<>|]', '_'
      if (-not $label) { $label = "file_" + [math]::Abs($url.GetHashCode()) }
      $resp = $null
      Start-Sleep -Milliseconds $DelayMs
      try { $resp = $http.GetAsync($url).Result } catch { Write-Warning "자료 실패: $label"; continue }
      if (-not $resp.IsSuccessStatusCode) { Write-Warning "자료 실패($([int]$resp.StatusCode)): $label"; continue }
      $fname = $resp.Content.Headers.ContentDisposition.FileNameStar
      if (-not $fname) { $fname = $resp.Content.Headers.ContentDisposition.FileName }
      if ($fname) {
        $fname = [System.Uri]::UnescapeDataString($fname.Trim('"'))
        # 서버가 UTF-8 파일 이름을 그대로 보내면 .NET은 Latin-1로 읽는다 → UTF-8로 다시 해석
        if ($fname -match '[\u00C0-\u00FF]') { $fname = [System.Text.Encoding]::UTF8.GetString([System.Text.Encoding]::GetEncoding(28591).GetBytes($fname)) }
      } else { $fname = "$label.bin" }
      $fname = $fname -replace '[\\/:*?"<>|]', '_'
      $path = Join-Path $DocDir $fname
      if ((Test-Path $path) -and -not $Refresh) { continue }
      [System.IO.File]::WriteAllBytes($path, $resp.Content.ReadAsByteArrayAsync().Result)
      Write-Host "자료: $fname"
    }
    # 금지 · 제한 / 언어 통일 룰 / 재정 변경처럼 뉴스 글로 올라온 자료는 글 HTML을 저장한다
    $news = [regex]::Matches($qna, 'href="(https://digimoncard\.co\.kr/index\.php\?mid=news&amp;document_srl=(\d+))/?"') | ForEach-Object { @{ Url = [System.Net.WebUtility]::HtmlDecode($_.Groups[1].Value); Id = $_.Groups[2].Value } } | Sort-Object { $_.Id } -Unique
    foreach ($n in $news) {
      $path = Join-Path $DocDir "news_$($n.Id).html"
      if ((Test-Path $path) -and -not $Refresh) { continue }
      $html = Get-Html $n.Url
      if (-not $html) { continue }
      $title = Clean ([regex]::Match($html, '<title>(.*?)</title>', 'Singleline').Groups[1].Value)
      Set-Content -Path $path -Value $html -Encoding UTF8
      Write-Host "자료 글: news_$($n.Id).html — $title"
    }
  }
}
