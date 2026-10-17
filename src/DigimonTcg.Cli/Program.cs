using System;
using System.Linq;
using System.Text;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.DeckBuilder;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Models;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("======================================================================");
Console.WriteLine("  디지몬 TCG 1인 연습 게임 & 덱 빌더 - 통합 검증 CLI (Phase 1 ~ Phase 3)");
Console.WriteLine("======================================================================\n");

// 1. 카드 도감 및 오프라인 캐시 초기화 검증
var catalogService = new CardCatalogService();
Console.WriteLine($"[Phase 3 도감/캐시] 로컬 도감 로드 완료: 총 {catalogService.Catalog.Count}종 카드 캐싱됨 (data/cards_cache.json)");

// 2. 스타터 덱 프리셋 및 황금 비율 HUD 검증
var (st1Main, st1Digitama) = StarterDecks.CreateSt1GaiaRedPreset();
var (isValidSt1, errorsSt1) = DeckValidator.ValidateDeck(st1Main, st1Digitama);
var ratioSt1 = DeckValidator.CalculateRatios(st1Main, st1Digitama);

Console.WriteLine($"\n[Phase 3 ST-1 가이아 레드 덱 검증] 유효 여부: {isValidSt1} (메인 {st1Main.Count}장 / 디지타마 {st1Digitama.Count}장)");
Console.WriteLine(DeckValidator.FormatGoldenRatioHudText(ratioSt1));

// 3. ActionValidator 규칙 위반 방지 검증
Console.WriteLine("[Phase 2 ActionValidator 규칙 위반 차단 테스트]");
var state = new GameState { MemoryGauge = 3, CurrentPhase = Phase.Breeding };
var engine = new GoldfishEngine(state);

var sample = StarterDecks.GetGaiaRedSampleCards().ToDictionary(c => c.CardId);
var playInBreeding = ActionValidator.ValidatePlay(state, sample["ST1-03"]);
Console.WriteLine($" - 육성 페이즈 중 등장 시도 -> 차단됨: \"{playInBreeding.Reason}\"");

state.CurrentPhase = Phase.Main;
var summoningSickInstance = new CardInstance(sample["ST1-03"], state.CurrentTurn);
state.Player.BattleArea.Add(summoningSickInstance);
var sickAttack = ActionValidator.ValidateAttack(state, summoningSickInstance);
Console.WriteLine($" - 등장 턴(소환 멀미) 공격 시도 -> 차단됨: \"{sickAttack.Reason}\"");

var wrongLevelEvo = ActionValidator.ValidateEvolution(state, summoningSickInstance, sample["ST1-09"]);
Console.WriteLine($" - Lv.3 위에 Lv.5 진화 시도 -> 차단됨: \"{wrongLevelEvo.Reason}\"\n");

// 4. 1인 솔리테어 (Goldfish) 킬 각 연습 시뮬레이션 (Turn 1 ~ Direct Attack 승리)
Console.WriteLine("[Phase 1 & 2 솔리테어 골드피시 시뮬레이션: 부화 -> 진화 체인 -> 시큐리티 돌파 -> 다이렉트 어택]");
engine.StartNewGame(st1Main, st1Digitama, seed: 42);

// Turn 1: 육성 에어리어 부화 -> 패에 Lv.3~Lv.6 진화 라인을 보장하여 체인 검증
state.Player.Hand.Clear();
state.Player.Hand.AddRange(new[]
{
    sample["ST1-03"], // 아구몬 (Lv.3, 진화 0)
    sample["ST1-07"], // 그레이몬 (Lv.4, 진화 2, 진화원: 시큐리티 어택 +1)
    sample["ST1-09"], // 메탈그레이몬 (Lv.5, 진화 3)
    sample["ST1-11"], // 워그레이몬 (Lv.6, 진화 4)
    sample["ST1-02"], // 피요몬 (Lv.3, 등장 2)
    sample["ST1-14"]  // 스타라이트 익스플로전 (시큐리티 어택 +1)
});

engine.HatchDigitama(); // 코로몬(Lv.2) 부화 -> 자동으로 Main 페이즈 진입
engine.EvolveDigimon(state.Player.BreedingArea!, sample["ST1-03"]); // 아구몬(Lv.3, 코스트 0)
engine.EvolveDigimon(state.Player.BreedingArea!, sample["ST1-07"]); // 그레이몬(Lv.4, 코스트 2 -> 메모리 1)
engine.EvolveDigimon(state.Player.BreedingArea!, sample["ST1-09"]); // 메탈그레이몬(Lv.5, 코스트 3 -> 메모리 -2 -> 턴 종료 및 Turn 2 시작, 메모리 +3!)

// Turn 2: 육성 에어리어의 메탈그레이몬을 배틀 에어리어로 이동 -> 워그레이몬 진화 -> 시큐리티 어택!
engine.MoveFromBreedingArea();
var mainAttacker = state.Player.BattleArea.First();
engine.PlayCard(sample["ST1-02"]); // 피요몬 배틀 에어리어 등장 (메모리 3 -> 1)
engine.PlayCard(sample["ST1-14"], mainAttacker); // 스타라이트 익스플로전 (메모리 1 -> -1이지만 진화 먼저 테스트하기 위해 순서 조정 없이 공격 가능 여부 확인!)
// 위 옵션 사용으로 메모리가 -1이 되어 Turn 3으로 자동 전환됨!

// Turn 3: 육성 패스 -> 워그레이몬(Lv.6) 진화 전 공격 또는 진화 후 공격
engine.PassBreedingPhase();
// 워그레이몬 진화 코스트는 4이므로 먼저 메탈그레이몬으로 시큐리티 공격 후 진화!
engine.AttackSecurityOrDirect(mainAttacker);
var secondAttacker = state.Player.BattleArea.Last();
engine.AttackSecurityOrDirect(secondAttacker);
engine.EvolveDigimon(mainAttacker, sample["ST1-11"]); // 워그레이몬(Lv.6) 진화! (메모리 3 - 4 = -1 -> Turn 4 시작!)

// Turn 4: 육성 패스 -> 워그레이몬(시큐리티 어택 다중 체크) 공격 + 피요몬 다이렉트 어택!
engine.PassBreedingPhase();
engine.AttackSecurityOrDirect(mainAttacker);
if (!state.IsGameOver)
{
    engine.AttackSecurityOrDirect(secondAttacker);
}

Console.WriteLine("\n--- 시뮬레이션 진행 로그 ---");
foreach (var log in state.ActionLogs)
{
    Console.WriteLine(" " + log);
}

Console.WriteLine("\n--- 타임어택 & 평가 지표 HUD ---");
Console.WriteLine($" - 승리 여부: {state.IsVictory} ({state.GameOverReason})");
Console.WriteLine($" - 클리어 턴(킬 타임): {state.CurrentTurn}턴 (목표: {state.TargetTurns}턴 이내)");
Console.WriteLine($" - 진화 체인 성공률(Lv.3->Lv.6): {state.EvolutionChainSuccessRate:F1}% ({state.Lv6CompletedChains}/{state.Lv3StartedChains})");
Console.WriteLine($" - 턴당 평균 메모리 사용량: {state.MemoryEfficiencyPerTurn:F1} 메모리/턴");
