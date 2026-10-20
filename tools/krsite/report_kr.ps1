# 한국 공식 사이트 수집 결과(data/kr/cards_ko.json)와 영어 API 캐시(data/cards_cache.json)를 카드 번호로 맞춰 본다.
#   .\report_kr.ps1            # 요약을 화면에 출력하고 data/kr/report.md 로 저장
param([string]$DataDir = (Join-Path $PSScriptRoot "..\..\data"))

$DataDir = [System.IO.Path]::GetFullPath($DataDir)
$ko = Get-Content (Join-Path $DataDir "kr\cards_ko.json") -Raw -Encoding UTF8 | ConvertFrom-Json
$en = Get-Content (Join-Path $DataDir "cards_cache.json") -Raw -Encoding UTF8 | ConvertFrom-Json

$koIds = @{}; foreach ($c in $ko) { $koIds[$c.CardId.ToUpperInvariant()] = $c }
$enIds = @{}; foreach ($c in $en) { $enIds[$c.CardId.ToUpperInvariant()] = $c }

function SetOf([string]$id) { ($id -split '-')[0] }

$matched = @($koIds.Keys | Where-Object { $enIds.ContainsKey($_) })
$koOnly = @($koIds.Keys | Where-Object { -not $enIds.ContainsKey($_) } | Sort-Object)
$enOnly = @($enIds.Keys | Where-Object { -not $koIds.ContainsKey($_) } | Sort-Object)

$lines = @()
$lines += "# 한국 공식 사이트 수집 리포트 ($(Get-Date -Format 'yyyy-MM-dd HH:mm'))"
$lines += ""
$lines += "- 한국판 카드: **$($koIds.Count)장** (패러렐은 같은 번호로 합침)"
$lines += "- 영어 캐시 카드: **$($enIds.Count)장**"
$lines += "- 번호가 맞은 카드: **$($matched.Count)장**"
$lines += "- 한국에만 있는 카드: **$($koOnly.Count)장**"
$lines += "- 영어에만 있는 카드: **$($enOnly.Count)장**"
$lines += ""
$lines += "## 세트별 (카드 번호 앞부분 기준)"
$lines += ""
$lines += "| 세트 | 한국판 | 영어 | 일치 | 한국만 | 영어만 |"
$lines += "| :--- | ---: | ---: | ---: | ---: | ---: |"
$allSets = @($koIds.Keys + $enIds.Keys | ForEach-Object { SetOf $_ } | Sort-Object -Unique)
foreach ($s in $allSets) {
  $k = @($koIds.Keys | Where-Object { (SetOf $_) -eq $s })
  $e = @($enIds.Keys | Where-Object { (SetOf $_) -eq $s })
  $m = @($k | Where-Object { $enIds.ContainsKey($_) })
  $lines += "| $s | $($k.Count) | $($e.Count) | $($m.Count) | $($k.Count - $m.Count) | $($e.Count - $m.Count) |"
}
$lines += ""
$lines += "## 한국에만 있는 카드"
$lines += ""
$lines += ($koOnly | ForEach-Object { "- $_ $($koIds[$_].Name) ($($koIds[$_].Set))" })
$lines += ""
$lines += "## 영어에만 있는 카드 (세트별 매수)"
$lines += ""
$lines += ($enOnly | Group-Object { SetOf $_ } | Sort-Object Name | ForEach-Object { "- $($_.Name): $($_.Count)장" })

$out = Join-Path $DataDir "kr\report.md"
$lines | Set-Content -Path $out -Encoding UTF8
$lines | Select-Object -First 8
"리포트: $out"
