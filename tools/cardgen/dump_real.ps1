# 내장 120장 카드 번호에 해당하는 실제 카드 데이터를 한눈에 볼 수 있게 표로 뽑는다 (번역 작업용)
param([string]$ApiDir, [string]$CoreList, [string]$Out)
$cards = @{}
Get-ChildItem $ApiDir -Filter "*.json" | Where-Object { $_.Name -like "real_*" -or $_.Name -eq "bt1.json" } | ForEach-Object {
  $arr = Get-Content $_.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
  foreach ($c in $arr) { if ($c.id) { $cards[$c.id] = $c } }
}
$lines = @()
foreach ($row in Get-Content $CoreList -Encoding UTF8) {
  $id = $row.Split('|')[0]
  $c = $cards[$id]
  if (-not $c) { $lines += "$id`tMISSING"; continue }
  $lines += "$id`t$($c.name)`t$($c.type)`t$($c.color)`tLv$($c.level)`tplay=$($c.play_cost)`tevo=$($c.evolution_cost)/Lv$($c.evolution_level)/$($c.evolution_color)`tDP=$($c.dp)`tR=$($c.rarity)`n   MAIN: $($c.main_effect)`n   SRC : $($c.source_effect)"
}
$lines | Set-Content -Path $Out -Encoding UTF8
"wrote $($lines.Count) rows"
