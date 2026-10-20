# 실제 카드 데이터(JSON) + 한글 번역(ko.tsv: id<TAB>이름<TAB>메인 효과<TAB>진화원 효과)으로 StarterDecks 카드 정의 C# 코드를 생성한다.
# 숫자(레벨 · 코스트 · DP · 진화 조건)는 전부 API 값을 그대로 쓴다.
param([string]$ApiDir, [string]$KoTsv, [string]$Out)
$cards = @{}
Get-ChildItem $ApiDir -Filter "*.json" | Where-Object { $_.Name -like "real_*" -or $_.Name -eq "bt1.json" } | ForEach-Object {
  $arr = Get-Content $_.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
  foreach ($c in $arr) { if ($c.id) { $cards[$c.id] = $c } }
}
function Esc([string]$s) { if ($null -eq $s) { return "" }; return $s.Replace('\', '\\').Replace('"', '\"').Replace('|', '\n') }
function ColorOf([string]$s) {
  switch -regex ($s) { '^Red' {'Red'} '^Blue' {'Blue'} '^Yellow' {'Yellow'} '^Green' {'Green'} '^Black' {'Black'} '^Purple' {'Purple'} '^White' {'White'} default {'Red'} }
}
function TypeOf([string]$s) { switch ($s) { 'Digi-Egg' {'Digitama'} 'Tamer' {'Tamer'} 'Option' {'Option'} default {'Digimon'} } }
function LeadingKeyword([string]$text, [string]$kw) { $t = ("" + $text).TrimStart(); return $t.StartsWith("<$kw>") -or $t.StartsWith("＜$kw＞") }
function LeadingSecAttack([string]$text) {
  $m = [regex]::Match(("" + $text).TrimStart(), '^[<＜]Security A(?:ttack|\.)\s*\+(\d)[>＞]')
  if ($m.Success) { return [int]$m.Groups[1].Value } else { return 0 }
}
function InheritedDp([string]$text) {
  # 조건 없는 "[Your Turn] This Digimon gets +N DP." 만 상시 보너스로 인정
  $m = [regex]::Match(("" + $text).Trim(), '^\[Your Turn\] This Digimon gets \+(\d+) DP\.?$')
  if ($m.Success) { return [int]$m.Groups[1].Value } else { return 0 }
}

$sb = New-Object System.Text.StringBuilder
$missing = @()
foreach ($line in Get-Content $KoTsv -Encoding UTF8) {
  if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('#')) { continue }
  $p = $line.Split("`t")
  $id = $p[0]; $ko = $p[1]; $koMain = if ($p.Count -gt 2) { $p[2] } else { "" }; $koSrc = if ($p.Count -gt 3) { $p[3] } else { "" }
  $c = $cards[$id]
  if (-not $c) { $missing += $id; continue }
  $type = TypeOf $c.type
  $color = ColorOf $c.color
  $level = if ($null -ne $c.level) { [int]$c.level } elseif ($type -eq 'Digitama') { 2 } else { 0 }
  $play = if ($null -ne $c.play_cost) { [int]$c.play_cost } else { 0 }
  $evo = if ($null -ne $c.evolution_cost) { [int]$c.evolution_cost } else { 0 }
  $evoLv = if ($null -ne $c.evolution_level) { [int]$c.evolution_level } else { [Math]::Max(2, $level - 1) }
  $evoColor = if ($c.evolution_color) { ColorOf $c.evolution_color } else { $color }
  $dp = if ($null -ne $c.dp) { [int]$c.dp } else { 0 }
  # API는 ST 카드의 진화원 효과를 main_effect 안에 "Inherited Effect ..." 로 넣어 둔다 → 분리
  $mainEn = "" + $c.main_effect; $srcEn = ("" + $c.source_effect) -replace "^Security Effect.*", ""
  $ix = $mainEn.IndexOf("Inherited Effect")
  if ($ix -ge 0) { $srcEn = $mainEn.Substring($ix + 16).Trim(); $mainEn = $mainEn.Substring(0, $ix).Trim() }
  $blocker = LeadingKeyword $mainEn "Blocker"
  $sec = LeadingSecAttack $mainEn
  $isec = LeadingSecAttack $srcEn
  $idp = InheritedDp $srcEn
  $rar = ("" + $c.rarity).ToUpperInvariant()

  [void]$sb.AppendLine("            new CardData")
  [void]$sb.AppendLine("            {")
  [void]$sb.AppendLine("                CardId = `"$id`", Name = `"$(Esc $ko)`", Type = CardType.$type, Color = CardColor.$color, Level = $level, Rarity = `"$rar`",")
  if ($type -eq 'Digimon') {
    [void]$sb.AppendLine("                PlayCost = $play, EvoCost = $evo, EvoRequirementLevel = $evoLv, EvoRequirementColor = CardColor.$evoColor, DP = $dp,")
  } elseif ($type -eq 'Digitama') {
    [void]$sb.AppendLine("                DP = 0,")
  } else {
    [void]$sb.AppendLine("                PlayCost = $play,")
  }
  $extras = @()
  if ($blocker) { $extras += "IsBlocker = true" }
  if ($sec -gt 0) { $extras += "SecurityAttackBonus = $sec" }
  if ($isec -gt 0) { $extras += "InheritedSecurityAttackBonus = $isec" }
  if ($idp -gt 0) { $extras += "InheritedDpBonus = $idp" }
  if ($extras.Count -gt 0) { [void]$sb.AppendLine("                " + ($extras -join ", ") + ",") }
  [void]$sb.AppendLine("                EffectText = `"$(Esc $koMain)`",")
  [void]$sb.AppendLine("                InheritedEffectText = `"$(Esc $koSrc)`"")
  [void]$sb.AppendLine("            },")
}
Set-Content -Path $Out -Value $sb.ToString() -Encoding UTF8
"generated; missing: $($missing -join ', ')"
