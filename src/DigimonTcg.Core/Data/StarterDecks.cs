using System;
using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Models;

namespace DigimonTcg.Core.Data;

public record RecommendedDeckPreset(
    string Id,
    string Name,
    string ArchetypeBadge,
    string Description,
    CardColor PrimaryColor,
    string BossCardId,
    Func<(List<CardData> MainDeck, List<CardData> DigitamaDeck)> DeckFactory);

public static class StarterDecks
{
    public static List<CardData> GetGaiaRedSampleCards() =>
        GetAllCatalogCards().Where(c => c.CardId.StartsWith("ST1-") || c.Color == CardColor.Red).ToList();

    public static List<CardData> GetCocytusBlueSampleCards() =>
        GetAllCatalogCards().Where(c => c.CardId.StartsWith("ST2-") || c.Color == CardColor.Blue).ToList();

    /// <summary>
    /// 전체 7색상 통합 공식 카드 도감 (ST-1 ~ ST-6 스타터 전종 + BT-1 ~ BT-5 핵심 메타/궁극체/초궁극체)
    /// </summary>
    public static List<CardData> GetAllCatalogCards()
    {
        var cards = new List<CardData>
        {
            // ==================== 🔥 RED: ST-1 가이아 레드 & BT1/BT2 레드 ====================
            new CardData
            {
                CardId = "ST1-01", Name = "코로몬", Type = CardType.Digitama, Color = CardColor.Red, Level = 2,
                InheritedDpBonus = 1000,
                InheritedEffectText = "[자신의 턴] 진화원을 가진 이 디지몬의 DP를 +1000."
            },
            new CardData
            {
                CardId = "BT1-001", Name = "깜몬(히히몬)", Type = CardType.Digitama, Color = CardColor.Red, Level = 2,
                InheritedDpBonus = 1000,
                InheritedEffectText = "[어택 시] 플레이어를 어택했을 때 이 턴 동안 이 디지몬의 DP를 +1000."
            },
            new CardData
            {
                CardId = "ST1-02", Name = "피요몬", Type = CardType.Digimon, Color = CardColor.Red, Level = 3,
                PlayCost = 2, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Red, DP = 3000,
                EffectText = "등장 코스트 2의 경량 성장기 디지몬."
            },
            new CardData
            {
                CardId = "ST1-03", Name = "아구몬", Type = CardType.Digimon, Color = CardColor.Red, Level = 3,
                PlayCost = 3, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Red, DP = 2000,
                InheritedDpBonus = 1000,
                InheritedEffectText = "[자신의 턴] 이 디지몬의 DP를 +1000."
            },
            new CardData
            {
                CardId = "ST1-04", Name = "드라코몬", Type = CardType.Digimon, Color = CardColor.Red, Level = 3,
                PlayCost = 3, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Red, DP = 4000,
                EffectText = "높은 기본 DP를 보유한 바닐라 성장기 디지몬."
            },
            new CardData
            {
                CardId = "BT1-009", Name = "모노드라몬", Type = CardType.Digimon, Color = CardColor.Red, Level = 3,
                PlayCost = 2, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Red, DP = 3000,
                EffectText = "빠른 필드 전개를 돕는 코스트 2 레드 성장기 디지몬."
            },
            new CardData
            {
                CardId = "BT1-010", Name = "아구몬(BT1)", Type = CardType.Digimon, Color = CardColor.Red, Level = 3,
                PlayCost = 3, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Red, DP = 2000,
                InheritedDpBonus = 1000,
                EffectText = "[등장 시] 자신의 덱 위에서 5장을 오픈하여 테이머 카드를 서치한다.",
                InheritedEffectText = "[자신의 턴] 이 디지몬의 DP를 +1000."
            },
            new CardData
            {
                CardId = "BT2-009", Name = "길몬", Type = CardType.Digimon, Color = CardColor.Red, Level = 3,
                PlayCost = 3, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Red, DP = 2000,
                InheritedDpBonus = 1000,
                InheritedEffectText = "[자신의 턴] 상대 디지몬이 소멸했을 때 DP +1000."
            },
            new CardData
            {
                CardId = "ST1-05", Name = "버드라몬", Type = CardType.Digimon, Color = CardColor.Red, Level = 4,
                PlayCost = 4, EvoCost = 2, EvoRequirementLevel = 3, EvoRequirementColor = CardColor.Red, DP = 5000,
                EffectText = "균형 잡힌 레드 성숙기 디지몬."
            },
            new CardData
            {
                CardId = "ST1-06", Name = "코어드라몬", Type = CardType.Digimon, Color = CardColor.Red, Level = 4,
                PlayCost = 6, EvoCost = 2, EvoRequirementLevel = 3, EvoRequirementColor = CardColor.Red, DP = 6000,
                IsBlocker = true,
                EffectText = "≪블로커≫ [상대 턴] 상대 디지몬이 어택했을 때 방어 가능."
            },
            new CardData
            {
                CardId = "ST1-07", Name = "그레이몬", Type = CardType.Digimon, Color = CardColor.Red, Level = 4,
                PlayCost = 5, EvoCost = 2, EvoRequirementLevel = 3, EvoRequirementColor = CardColor.Red, DP = 4000,
                InheritedSecurityAttackBonus = 1,
                InheritedEffectText = "≪시큐리티 어택 +1≫ (이 디지몬이 체크하는 시큐리티의 매수를 +1)"
            },
            new CardData
            {
                CardId = "BT1-015", Name = "다크티라노몬(레드)", Type = CardType.Digimon, Color = CardColor.Red, Level = 4,
                PlayCost = 4, EvoCost = 1, EvoRequirementLevel = 3, EvoRequirementColor = CardColor.Red, DP = 6000,
                EffectText = "진화 코스트 1로 초고속 템포 진화를 지원하는 성숙기 디지몬."
            },
            new CardData
            {
                CardId = "ST1-08", Name = "가루다몬", Type = CardType.Digimon, Color = CardColor.Red, Level = 5,
                PlayCost = 6, EvoCost = 3, EvoRequirementLevel = 4, EvoRequirementColor = CardColor.Red, DP = 7000,
                InheritedDpBonus = 1000,
                EffectText = "[진화 시] 자신의 디지몬 1체의 DP를 이 턴 동안 +3000."
            },
            new CardData
            {
                CardId = "ST1-09", Name = "메탈그레이몬", Type = CardType.Digimon, Color = CardColor.Red, Level = 5,
                PlayCost = 7, EvoCost = 3, EvoRequirementLevel = 4, EvoRequirementColor = CardColor.Red, DP = 7000,
                InheritedDpBonus = 2000,
                InheritedEffectText = "[자신의 턴] 진화원 DP +2000 보너스 및 돌파력 강화."
            },
            new CardData
            {
                CardId = "BT1-020", Name = "그라운드라몬", Type = CardType.Digimon, Color = CardColor.Red, Level = 5,
                PlayCost = 6, EvoCost = 2, EvoRequirementLevel = 4, EvoRequirementColor = CardColor.Red, DP = 6000,
                EffectText = "진화 코스트 2로 빠르게 궁극체로 이어주는 완전체 디지몬."
            },
            new CardData
            {
                CardId = "BT1-021", Name = "메탈그레이몬(BT1)", Type = CardType.Digimon, Color = CardColor.Red, Level = 5,
                PlayCost = 7, EvoCost = 3, EvoRequirementLevel = 4, EvoRequirementColor = CardColor.Red, DP = 7000,
                InheritedDpBonus = 2000,
                EffectText = "[어택 시] 메모리를 +3 얻고 턴 종료 시 메모리를 조정한다."
            },
            new CardData
            {
                CardId = "ST1-10", Name = "페닉스몬", Type = CardType.Digimon, Color = CardColor.Red, Level = 6,
                PlayCost = 10, EvoCost = 2, EvoRequirementLevel = 5, EvoRequirementColor = CardColor.Red, DP = 12000,
                EffectText = "진화 코스트 2로 빠르게 궁극체에 도달하는 속도형 피니셔."
            },
            new CardData
            {
                CardId = "ST1-11", Name = "워그레이몬", Type = CardType.Digimon, Color = CardColor.Red, Level = 6,
                PlayCost = 12, EvoCost = 4, EvoRequirementLevel = 5, EvoRequirementColor = CardColor.Red, DP = 12000,
                SecurityAttackBonus = 1,
                EffectText = "[진화 시] 이 디지몬의 진화원 2장마다, 이 턴 동안 ≪시큐리티 어택 +1≫을 얻는다."
            },
            new CardData
            {
                CardId = "BT1-025", Name = "워그레이몬(BT1)", Type = CardType.Digimon, Color = CardColor.Red, Level = 6,
                PlayCost = 12, EvoCost = 4, EvoRequirementLevel = 5, EvoRequirementColor = CardColor.Red, DP = 11000,
                SecurityAttackBonus = 1,
                EffectText = "[진화 시] 이 턴 동안 상대의 [시큐리티] 효과를 무시하고 안전하게 돌파한다."
            },
            new CardData
            {
                CardId = "BT2-020", Name = "듀크몬", Type = CardType.Digimon, Color = CardColor.Red, Level = 6,
                PlayCost = 12, EvoCost = 4, EvoRequirementLevel = 5, EvoRequirementColor = CardColor.Red, DP = 11000,
                SecurityAttackBonus = 1,
                EffectText = "[진화 시] 상대 디지몬을 압도하며 시큐리티를 추가 파기하는 성기사형 궁극체."
            },
            new CardData
            {
                CardId = "ST1-12", Name = "신태일", Type = CardType.Tamer, Color = CardColor.Red, Level = 0,
                PlayCost = 2,
                EffectText = "[자신의 턴] 자신의 모든 디지몬의 DP를 +1000. 턴 시작 시 메모리 최소 3 보장. [시큐리티] 코스트 지불 없이 등장."
            },
            new CardData
            {
                CardId = "ST1-13", Name = "섀도우 윙", Type = CardType.Option, Color = CardColor.Red, Level = 0,
                PlayCost = 1,
                EffectText = "[메인] 이 턴 동안 자신의 디지몬 1체의 DP를 +3000. [시큐리티] 자신의 모든 디지몬 DP +3000 & 시큐어택 +1."
            },
            new CardData
            {
                CardId = "ST1-14", Name = "스타라이트 익스플로전", Type = CardType.Option, Color = CardColor.Red, Level = 0,
                PlayCost = 2,
                EffectText = "[메인] 자신의 디지몬 1체에 DP +2000 및 ≪시큐리티 어택 +1≫ 부여. [시큐리티] 아군 전체 DP +7000."
            },
            new CardData
            {
                CardId = "ST1-15", Name = "기가 디스트로이어", Type = CardType.Option, Color = CardColor.Red, Level = 0,
                PlayCost = 6,
                EffectText = "[메인] DP 4000 이하의 상대 디지몬 2체까지 소멸시킨다. [시큐리티] [메인] 효과를 발동한다."
            },
            new CardData
            {
                CardId = "ST1-16", Name = "가이아 포스", Type = CardType.Option, Color = CardColor.Red, Level = 0,
                PlayCost = 8,
                EffectText = "[메인] 상대의 디지몬 1체를 소멸시킨다. [시큐리티] 이 카드의 [메인] 효과를 발동한다."
            },

            // ==================== ❄️ BLUE: ST-2 코큐토스 블루 & BT1/BT2 블루 ====================
            new CardData
            {
                CardId = "ST2-01", Name = "뿔몬", Type = CardType.Digitama, Color = CardColor.Blue, Level = 2,
                InheritedDpBonus = 1000,
                InheritedEffectText = "[자신의 턴] 진화원을 가진 이 디지몬의 DP를 +1000."
            },
            new CardData
            {
                CardId = "BT1-003", Name = "어니몬(우파몬)", Type = CardType.Digitama, Color = CardColor.Blue, Level = 2,
                InheritedDpBonus = 1000,
                InheritedEffectText = "[어택 시] 상대의 진화원 없는 디지몬이 있을 때 드로우 및 DP +1000."
            },
            new CardData
            {
                CardId = "ST2-02", Name = "고마몬", Type = CardType.Digimon, Color = CardColor.Blue, Level = 3,
                PlayCost = 2, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Blue, DP = 3000,
                EffectText = "빠른 전개에 특화된 코스트 2 성장기 디지몬."
            },
            new CardData
            {
                CardId = "ST2-03", Name = "파피몬", Type = CardType.Digimon, Color = CardColor.Blue, Level = 3,
                PlayCost = 3, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Blue, DP = 2000,
                InheritedDpBonus = 1000,
                InheritedEffectText = "[어택 시] 상대 디지몬의 진화원을 아래에서 1장 파기하고 DP +1000."
            },
            new CardData
            {
                CardId = "ST2-04", Name = "베어몬", Type = CardType.Digimon, Color = CardColor.Blue, Level = 3,
                PlayCost = 3, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Blue, DP = 4000,
                EffectText = "안정적인 DP 4000을 지닌 블루 성장기 디지몬."
            },
            new CardData
            {
                CardId = "BT1-029", Name = "파피몬(BT1)", Type = CardType.Digimon, Color = CardColor.Blue, Level = 3,
                PlayCost = 3, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Blue, DP = 2000,
                InheritedDpBonus = 1000,
                EffectText = "[등장 시] 자신의 덱에서 카드를 1장 드로우한다."
            },
            new CardData
            {
                CardId = "BT2-021", Name = "브이몬", Type = CardType.Digimon, Color = CardColor.Blue, Level = 3,
                PlayCost = 3, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Blue, DP = 2000,
                InheritedDpBonus = 1000,
                InheritedEffectText = "[자신의 턴] 이 디지몬이 액티브 상태인 동안 DP를 +1000."
            },
            new CardData
            {
                CardId = "ST2-05", Name = "원뿔몬", Type = CardType.Digimon, Color = CardColor.Blue, Level = 4,
                PlayCost = 4, EvoCost = 2, EvoRequirementLevel = 3, EvoRequirementColor = CardColor.Blue, DP = 5000,
                EffectText = "저렴한 등장 코스트를 가진 블루 성숙기 디지몬."
            },
            new CardData
            {
                CardId = "ST2-06", Name = "가루몬", Type = CardType.Digimon, Color = CardColor.Blue, Level = 4,
                PlayCost = 5, EvoCost = 2, EvoRequirementLevel = 3, EvoRequirementColor = CardColor.Blue, DP = 4000,
                InheritedDpBonus = 1000,
                InheritedEffectText = "[어택 시] 상대 디지몬 1체의 진화원을 1장 파기한다."
            },
            new CardData
            {
                CardId = "ST2-07", Name = "그리즈몬", Type = CardType.Digimon, Color = CardColor.Blue, Level = 4,
                PlayCost = 6, EvoCost = 2, EvoRequirementLevel = 3, EvoRequirementColor = CardColor.Blue, DP = 6000,
                IsBlocker = true,
                EffectText = "≪블로커≫ 아군을 수호하는 블루 성숙기 디지몬."
            },
            new CardData
            {
                CardId = "BT1-034", Name = "쉬라몬", Type = CardType.Digimon, Color = CardColor.Blue, Level = 4,
                PlayCost = 4, EvoCost = 1, EvoRequirementLevel = 3, EvoRequirementColor = CardColor.Blue, DP = 5000,
                EffectText = "진화 코스트 1로 완전체 진입을 앞당기는 블루 성숙기."
            },
            new CardData
            {
                CardId = "ST2-08", Name = "워가루몬", Type = CardType.Digimon, Color = CardColor.Blue, Level = 5,
                PlayCost = 7, EvoCost = 3, EvoRequirementLevel = 4, EvoRequirementColor = CardColor.Blue, DP = 7000,
                InheritedSecurityAttackBonus = 1,
                InheritedEffectText = "[자신의 턴] 진화원이 3장 이상인 동안 ≪시큐리티 어택 +1≫."
            },
            new CardData
            {
                CardId = "ST2-09", Name = "쥬드몬", Type = CardType.Digimon, Color = CardColor.Blue, Level = 5,
                PlayCost = 6, EvoCost = 3, EvoRequirementLevel = 4, EvoRequirementColor = CardColor.Blue, DP = 7000,
                EffectText = "[진화 시] 상대 디지몬의 진화원 2장을 파기한다."
            },
            new CardData
            {
                CardId = "BT1-040", Name = "워가루몬(BT1)", Type = CardType.Digimon, Color = CardColor.Blue, Level = 5,
                PlayCost = 6, EvoCost = 3, EvoRequirementLevel = 4, EvoRequirementColor = CardColor.Blue, DP = 6000,
                InheritedDpBonus = 1000,
                EffectText = "기동력이 뛰어난 블루 완전체 디지몬."
            },
            new CardData
            {
                CardId = "ST2-10", Name = "플레시오몬", Type = CardType.Digimon, Color = CardColor.Blue, Level = 6,
                PlayCost = 10, EvoCost = 2, EvoRequirementLevel = 5, EvoRequirementColor = CardColor.Blue, DP = 12000,
                EffectText = "진화 코스트 2로 빠르게 필드를 장악하는 블루 궁극체."
            },
            new CardData
            {
                CardId = "ST2-11", Name = "메탈가루몬", Type = CardType.Digimon, Color = CardColor.Blue, Level = 6,
                PlayCost = 12, EvoCost = 4, EvoRequirementLevel = 5, EvoRequirementColor = CardColor.Blue, DP = 11000,
                SecurityAttackBonus = 1,
                EffectText = "[어택 시] 이 디지몬을 액티브 상태로 만들어 재행동 및 2회 연속 공격을 가한다."
            },
            new CardData
            {
                CardId = "BT2-032", Name = "알포스브이드라몬", Type = CardType.Digimon, Color = CardColor.Blue, Level = 6,
                PlayCost = 12, EvoCost = 4, EvoRequirementLevel = 5, EvoRequirementColor = CardColor.Blue, DP = 11000,
                SecurityAttackBonus = 1,
                EffectText = "[자신의 턴] 블루 테이머가 레스트될 때 액티브되어 연속 공격을 수행하는 성기사형 궁극체."
            },
            new CardData
            {
                CardId = "BT3-031", Name = "황제드라몬(드래곤 모드)", Type = CardType.Digimon, Color = CardColor.Blue, Level = 6,
                PlayCost = 12, EvoCost = 3, EvoRequirementLevel = 5, EvoRequirementColor = CardColor.Blue, DP = 12000,
                SecurityAttackBonus = 1,
                EffectText = "[진화 시] 진화 코스트 3으로 강림하여 스스로 액티브 재기상하는 고대룡형 궁극체."
            },
            new CardData
            {
                CardId = "ST2-12", Name = "매튜", Type = CardType.Tamer, Color = CardColor.Blue, Level = 0,
                PlayCost = 2,
                EffectText = "[자신의 턴 시작 시] 메모리를 +1 추가 확보한다. 진화 디지몬 DP +1000. [시큐리티] 코스트 없이 등장."
            },
            new CardData
            {
                CardId = "ST2-13", Name = "해머 스파크", Type = CardType.Option, Color = CardColor.Blue, Level = 0,
                PlayCost = 0,
                EffectText = "[메인] 메모리를 +1(실전 +2) 얻는다. [시큐리티] 메모리를 +2 얻는다."
            },
            new CardData
            {
                CardId = "ST2-14", Name = "소로우 블루", Type = CardType.Option, Color = CardColor.Blue, Level = 0,
                PlayCost = 2,
                EffectText = "[메인] 상대 디지몬 1체를 레스트/행동불가로 만든다. [시큐리티] 상대 디지몬 1체를 레스트시킨다."
            },
            new CardData
            {
                CardId = "ST2-15", Name = "카이저 네일", Type = CardType.Option, Color = CardColor.Blue, Level = 0,
                PlayCost = 4,
                EffectText = "[메인] 자신의 디지몬 진화원 1장을 코스트 지불 없이 배틀 에어리어에 등장시킨다."
            },
            new CardData
            {
                CardId = "ST2-16", Name = "코큐토스 브레스", Type = CardType.Option, Color = CardColor.Blue, Level = 0,
                PlayCost = 7,
                EffectText = "[메인] 상대의 디지몬 1체를 주인의 패로 되돌린다(바운스). [시큐리티] [메인] 효과를 발동한다."
            },

            // ==================== ⚡ YELLOW: ST-3 헤븐즈 옐로우 & BT1/BT2 옐로우 ====================
            new CardData
            {
                CardId = "ST3-01", Name = "토코몬", Type = CardType.Digitama, Color = CardColor.Yellow, Level = 2,
                InheritedDpBonus = 1000,
                InheritedEffectText = "[자신의 턴] 이 디지몬의 DP를 +1000."
            },
            new CardData
            {
                CardId = "ST3-02", Name = "플롯트몬", Type = CardType.Digimon, Color = CardColor.Yellow, Level = 3,
                PlayCost = 2, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Yellow, DP = 3000,
                EffectText = "코스트 2로 가볍게 전개하는 옐로우 성수형 성장기."
            },
            new CardData
            {
                CardId = "ST3-03", Name = "가브몬(옐로우)", Type = CardType.Digimon, Color = CardColor.Yellow, Level = 3,
                PlayCost = 3, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Yellow, DP = 4000,
                EffectText = "DP 4000의 튼튼한 옐로우 성장기 디지몬."
            },
            new CardData
            {
                CardId = "ST3-04", Name = "파닥몬", Type = CardType.Digimon, Color = CardColor.Yellow, Level = 3,
                PlayCost = 3, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Yellow, DP = 2000,
                InheritedDpBonus = 1000,
                InheritedEffectText = "[자신의 턴] 상대 디지몬 소멸 시 메모리 +1 및 DP +1000."
            },
            new CardData
            {
                CardId = "BT1-048", Name = "파닥몬(BT1)", Type = CardType.Digimon, Color = CardColor.Yellow, Level = 3,
                PlayCost = 3, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Yellow, DP = 2000,
                InheritedDpBonus = 1000,
                EffectText = "천사 계보 진화의 핵심 축을 담당하는 성장기."
            },
            new CardData
            {
                CardId = "ST3-05", Name = "엔젤몬", Type = CardType.Digimon, Color = CardColor.Yellow, Level = 4,
                PlayCost = 5, EvoCost = 2, EvoRequirementLevel = 3, EvoRequirementColor = CardColor.Yellow, DP = 4000,
                InheritedSecurityAttackBonus = 1,
                InheritedEffectText = "[어택 시] ≪시큐리티 어택 +1≫ 및 상대 디지몬 약화."
            },
            new CardData
            {
                CardId = "ST3-06", Name = "가트몬", Type = CardType.Digimon, Color = CardColor.Yellow, Level = 4,
                PlayCost = 4, EvoCost = 2, EvoRequirementLevel = 3, EvoRequirementColor = CardColor.Yellow, DP = 5000,
                EffectText = "코스트 4로 빠르게 필드에 합류하는 성숙기 디지몬."
            },
            new CardData
            {
                CardId = "ST3-07", Name = "유니몬", Type = CardType.Digimon, Color = CardColor.Yellow, Level = 4,
                PlayCost = 5, EvoCost = 2, EvoRequirementLevel = 3, EvoRequirementColor = CardColor.Yellow, DP = 6000,
                IsBlocker = true,
                EffectText = "≪블로커≫ 천사 군단을 수호하는 성숙기 블로커."
            },
            new CardData
            {
                CardId = "ST3-08", Name = "홀리엔젤몬", Type = CardType.Digimon, Color = CardColor.Yellow, Level = 5,
                PlayCost = 7, EvoCost = 3, EvoRequirementLevel = 4, EvoRequirementColor = CardColor.Yellow, DP = 7000,
                InheritedDpBonus = 2000,
                InheritedEffectText = "[자신의 턴] 이 디지몬의 DP를 +2000."
            },
            new CardData
            {
                CardId = "ST3-09", Name = "엔젤우몬", Type = CardType.Digimon, Color = CardColor.Yellow, Level = 5,
                PlayCost = 6, EvoCost = 3, EvoRequirementLevel = 4, EvoRequirementColor = CardColor.Yellow, DP = 6000,
                InheritedDpBonus = 1000,
                EffectText = "[진화 시] 아군 디지몬을 강화하고 시큐리티 방어력을 높이는 대천사형 완전체."
            },
            new CardData
            {
                CardId = "BT1-060", Name = "홀리엔젤몬(BT1)", Type = CardType.Digimon, Color = CardColor.Yellow, Level = 5,
                PlayCost = 7, EvoCost = 3, EvoRequirementLevel = 4, EvoRequirementColor = CardColor.Yellow, DP = 6000,
                EffectText = "[등장 시] 자신의 시큐리티를 1장 회복(리커버리 +1)한다."
            },
            new CardData
            {
                CardId = "ST3-10", Name = "마그나드라몬(홀리드라몬)", Type = CardType.Digimon, Color = CardColor.Yellow, Level = 6,
                PlayCost = 10, EvoCost = 2, EvoRequirementLevel = 5, EvoRequirementColor = CardColor.Yellow, DP = 12000,
                EffectText = "진화 코스트 2로 강림하는 4대룡 성룡형 궁극체."
            },
            new CardData
            {
                CardId = "ST3-11", Name = "세라피몬", Type = CardType.Digimon, Color = CardColor.Yellow, Level = 6,
                PlayCost = 12, EvoCost = 3, EvoRequirementLevel = 5, EvoRequirementColor = CardColor.Yellow, DP = 11000,
                SecurityAttackBonus = 1,
                EffectText = "≪시큐리티 어택 +1≫ 3대 천사의 정점에 선 치천사형 궁극체."
            },
            new CardData
            {
                CardId = "BT2-039", Name = "슬래쉬엔젤몬", Type = CardType.Digimon, Color = CardColor.Yellow, Level = 6,
                PlayCost = 12, EvoCost = 3, EvoRequirementLevel = 5, EvoRequirementColor = CardColor.Yellow, DP = 11000,
                SecurityAttackBonus = 1,
                EffectText = "[진화 시] 상대 디지몬의 DP를 -8000 감소시켜 제압하는 권능형 궁극체."
            },
            new CardData
            {
                CardId = "ST3-12", Name = "리키", Type = CardType.Tamer, Color = CardColor.Yellow, Level = 0,
                PlayCost = 2,
                EffectText = "[자신의 턴] 아군 디지몬 DP +1000 & 턴 시작 시 메모리 +1. [시큐리티] 코스트 없이 등장."
            },
            new CardData
            {
                CardId = "ST3-13", Name = "헤븐즈 게이트", Type = CardType.Option, Color = CardColor.Yellow, Level = 0,
                PlayCost = 1,
                EffectText = "[메인] 이 턴 동안 자신의 디지몬 1체의 DP를 +3000."
            },
            new CardData
            {
                CardId = "ST3-16", Name = "세븐 헤븐즈", Type = CardType.Option, Color = CardColor.Yellow, Level = 0,
                PlayCost = 7,
                EffectText = "[메인] 상대 디지몬 1체의 DP를 -10000 감소시켜 소멸시킨다. [시큐리티] [메인] 효과를 발동한다."
            },

            // ==================== 🌿 GREEN: ST-4 기가 그린 & BT1/BT2 그린 ====================
            new CardData
            {
                CardId = "ST4-01", Name = "모티몬", Type = CardType.Digitama, Color = CardColor.Green, Level = 2,
                InheritedDpBonus = 1000,
                InheritedEffectText = "[자신의 턴] 이 디지몬이 Lv.6 이상인 동안 DP를 +1000."
            },
            new CardData
            {
                CardId = "ST4-02", Name = "플로라몬", Type = CardType.Digimon, Color = CardColor.Green, Level = 3,
                PlayCost = 2, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Green, DP = 3000,
                EffectText = "등장 코스트 2의 초경량 그린 성장기 디지몬."
            },
            new CardData
            {
                CardId = "ST4-03", Name = "텐토몬", Type = CardType.Digimon, Color = CardColor.Green, Level = 3,
                PlayCost = 3, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Green, DP = 2000,
                InheritedDpBonus = 1000,
                InheritedEffectText = "[어택 시] 상대 디지몬을 레스트시키고 이 디지몬의 DP를 +1000."
            },
            new CardData
            {
                CardId = "ST4-04", Name = "팔몬", Type = CardType.Digimon, Color = CardColor.Green, Level = 3,
                PlayCost = 3, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Green, DP = 4000,
                EffectText = "높은 기본 DP를 지닌 그린 식물형 성장기."
            },
            new CardData
            {
                CardId = "BT1-067", Name = "버섯몬", Type = CardType.Digimon, Color = CardColor.Green, Level = 3,
                PlayCost = 2, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Green, DP = 3000,
                EffectText = "빠른 진화 템포를 제공하는 그린 성장기."
            },
            new CardData
            {
                CardId = "ST4-05", Name = "쿠네몬", Type = CardType.Digimon, Color = CardColor.Green, Level = 4,
                PlayCost = 4, EvoCost = 1, EvoRequirementLevel = 3, EvoRequirementColor = CardColor.Green, DP = 4000,
                EffectText = "진화 코스트 1로 초고속 진화 체인을 연결하는 그린 성숙기."
            },
            new CardData
            {
                CardId = "ST4-06", Name = "니드몬", Type = CardType.Digimon, Color = CardColor.Green, Level = 4,
                PlayCost = 4, EvoCost = 2, EvoRequirementLevel = 3, EvoRequirementColor = CardColor.Green, DP = 5000,
                EffectText = "공수 밸런스가 뛰어난 그린 성숙기."
            },
            new CardData
            {
                CardId = "ST4-07", Name = "정글모자몬", Type = CardType.Digimon, Color = CardColor.Green, Level = 4,
                PlayCost = 6, EvoCost = 2, EvoRequirementLevel = 3, EvoRequirementColor = CardColor.Green, DP = 6000,
                IsBlocker = true,
                EffectText = "≪블로커≫ 그린 진영의 전선을 수비하는 성숙기 블로커."
            },
            new CardData
            {
                CardId = "ST4-08", Name = "메가카부테리몬", Type = CardType.Digimon, Color = CardColor.Green, Level = 5,
                PlayCost = 7, EvoCost = 3, EvoRequirementLevel = 4, EvoRequirementColor = CardColor.Green, DP = 7000,
                InheritedSecurityAttackBonus = 1,
                InheritedEffectText = "[자신의 턴] 상대 디지몬 격파 및 ≪시큐리티 어택 +1≫ 돌파."
            },
            new CardData
            {
                CardId = "ST4-09", Name = "릴리몬", Type = CardType.Digimon, Color = CardColor.Green, Level = 5,
                PlayCost = 6, EvoCost = 3, EvoRequirementLevel = 4, EvoRequirementColor = CardColor.Green, DP = 7000,
                InheritedDpBonus = 2000,
                EffectText = "[진화 시] 덱 위에서 궁극체 디지몬을 서치하는 요정형 완전체."
            },
            new CardData
            {
                CardId = "BT1-074", Name = "쥬레이몬", Type = CardType.Digimon, Color = CardColor.Green, Level = 5,
                PlayCost = 7, EvoCost = 2, EvoRequirementLevel = 4, EvoRequirementColor = CardColor.Green, DP = 7000,
                EffectText = "진화 코스트 2로 Lv.6 궁극체 진화를 앞당기는 고목형 완전체."
            },
            new CardData
            {
                CardId = "BT1-079", Name = "반쵸스팅몬", Type = CardType.Digimon, Color = CardColor.Green, Level = 6,
                PlayCost = 11, EvoCost = 3, EvoRequirementLevel = 5, EvoRequirementColor = CardColor.Green, DP = 12000,
                SecurityAttackBonus = 1,
                EffectText = "[어택 시] 고DP와 관통 화력으로 적진을 궤멸시키는 곤충형 궁극체."
            },
            new CardData
            {
                CardId = "ST4-10", Name = "로제몬", Type = CardType.Digimon, Color = CardColor.Green, Level = 6,
                PlayCost = 10, EvoCost = 2, EvoRequirementLevel = 5, EvoRequirementColor = CardColor.Green, DP = 12000,
                EffectText = "진화 코스트 2로 초고속 강림하는 요정형 궁극체."
            },
            new CardData
            {
                CardId = "ST4-11", Name = "헤라클레스카부테리몬", Type = CardType.Digimon, Color = CardColor.Green, Level = 6,
                PlayCost = 12, EvoCost = 4, EvoRequirementLevel = 5, EvoRequirementColor = CardColor.Green, DP = 12000,
                SecurityAttackBonus = 1,
                EffectText = "≪관통≫ [메인] 상대 디지몬을 레스트시키고 시큐리티까지 돌파하는 궁극체."
            },
            new CardData
            {
                CardId = "ST4-12", Name = "이미나", Type = CardType.Tamer, Color = CardColor.Green, Level = 0,
                PlayCost = 2,
                EffectText = "[자신의 턴] 아군 디지몬 DP +1000 & 턴 시작 시 메모리 +1. [시큐리티] 코스트 없이 등장."
            },
            new CardData
            {
                CardId = "ST4-13", Name = "일렉트릭 러시", Type = CardType.Option, Color = CardColor.Green, Level = 0,
                PlayCost = 1,
                EffectText = "[메인] 이 턴 동안 자신의 디지몬 1체의 DP를 +3000."
            },
            new CardData
            {
                CardId = "ST4-14", Name = "니들 스프레이", Type = CardType.Option, Color = CardColor.Green, Level = 0,
                PlayCost = 2,
                EffectText = "[메인] 상대의 디지몬 1체를 강제로 레스트시킨다."
            },
            new CardData
            {
                CardId = "ST4-16", Name = "기가 블래스터", Type = CardType.Option, Color = CardColor.Green, Level = 0,
                PlayCost = 6,
                EffectText = "[메인] 상대의 디지몬을 제압하고 소멸시킨다. [시큐리티] [메인] 효과를 발동한다."
            },

            // ==================== ⚙️ BLACK: ST-5 머신 블랙 & BT2 블랙 ====================
            new CardData
            {
                CardId = "ST5-01", Name = "츠메몬", Type = CardType.Digitama, Color = CardColor.Black, Level = 2,
                InheritedDpBonus = 1000,
                InheritedEffectText = "[자신의 턴] 이 디지몬의 DP를 +1000."
            },
            new CardData
            {
                CardId = "ST5-02", Name = "토이아구몬", Type = CardType.Digimon, Color = CardColor.Black, Level = 3,
                PlayCost = 2, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Black, DP = 3000,
                EffectText = "등장 코스트 2의 경량 사이보그 성장기."
            },
            new CardData
            {
                CardId = "ST5-03", Name = "아구몬(흑)", Type = CardType.Digimon, Color = CardColor.Black, Level = 3,
                PlayCost = 3, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Black, DP = 2000,
                InheritedDpBonus = 1000,
                InheritedEffectText = "[양쪽의 턴] 이 디지몬의 DP를 +1000."
            },
            new CardData
            {
                CardId = "ST5-04", Name = "커맨드라몬", Type = CardType.Digimon, Color = CardColor.Black, Level = 3,
                PlayCost = 3, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Black, DP = 4000,
                EffectText = "특수부대 D-브리가드의 선봉 성장기 디지몬."
            },
            new CardData
            {
                CardId = "BT2-052", Name = "하구루몬", Type = CardType.Digimon, Color = CardColor.Black, Level = 3,
                PlayCost = 3, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Black, DP = 2000,
                InheritedDpBonus = 1000,
                EffectText = "기계형/사이보그형 진화 시너지를 가진 톱니바퀴 디지몬."
            },
            new CardData
            {
                CardId = "ST5-05", Name = "그레이몬(흑)", Type = CardType.Digimon, Color = CardColor.Black, Level = 4,
                PlayCost = 4, EvoCost = 2, EvoRequirementLevel = 3, EvoRequirementColor = CardColor.Black, DP = 5000,
                EffectText = "강인한 내구력을 지닌 바이러스종 그레이몬."
            },
            new CardData
            {
                CardId = "ST5-06", Name = "다크티라노몬", Type = CardType.Digimon, Color = CardColor.Black, Level = 4,
                PlayCost = 6, EvoCost = 2, EvoRequirementLevel = 3, EvoRequirementColor = CardColor.Black, DP = 6000,
                IsBlocker = true,
                EffectText = "≪블로커≫ 머신 군단의 핵심 성숙기 블로커."
            },
            new CardData
            {
                CardId = "ST5-07", Name = "탱크몬", Type = CardType.Digimon, Color = CardColor.Black, Level = 4,
                PlayCost = 5, EvoCost = 1, EvoRequirementLevel = 3, EvoRequirementColor = CardColor.Black, DP = 5000,
                EffectText = "진화 코스트 1로 빠르게 완전체로 이어주는 중장갑 성숙기."
            },
            new CardData
            {
                CardId = "ST5-08", Name = "메탈그레이몬(흑)", Type = CardType.Digimon, Color = CardColor.Black, Level = 5,
                PlayCost = 7, EvoCost = 3, EvoRequirementLevel = 4, EvoRequirementColor = CardColor.Black, DP = 7000,
                InheritedSecurityAttackBonus = 1,
                InheritedEffectText = "≪시큐리티 어택 +1≫ 및 사이보그 진화 시너지."
            },
            new CardData
            {
                CardId = "ST5-09", Name = "메가드라몬", Type = CardType.Digimon, Color = CardColor.Black, Level = 5,
                PlayCost = 6, EvoCost = 3, EvoRequirementLevel = 4, EvoRequirementColor = CardColor.Black, DP = 7000,
                IsBlocker = true,
                InheritedDpBonus = 1000,
                EffectText = "≪블로커≫ 진화원에도 블로커 및 DP 보너스를 부여하는 완전체."
            },
            new CardData
            {
                CardId = "BT2-059", Name = "안드로몬", Type = CardType.Digimon, Color = CardColor.Black, Level = 5,
                PlayCost = 7, EvoCost = 3, EvoRequirementLevel = 4, EvoRequirementColor = CardColor.Black, DP = 7000,
                IsBlocker = true,
                EffectText = "≪블로커≫ 철벽 방어력을 자랑하는 사이보그형 완전체."
            },
            new CardData
            {
                CardId = "ST5-11", Name = "무겐드라몬", Type = CardType.Digimon, Color = CardColor.Black, Level = 6,
                PlayCost = 11, EvoCost = 3, EvoRequirementLevel = 5, EvoRequirementColor = CardColor.Black, DP = 12000,
                SecurityAttackBonus = 1,
                EffectText = "100% 풀 메탈 파츠로 무장한 최강의 머신형 궁극체."
            },
            new CardData
            {
                CardId = "ST5-12", Name = "파워드라몬(블리츠드라몬)", Type = CardType.Digimon, Color = CardColor.Black, Level = 6,
                PlayCost = 12, EvoCost = 4, EvoRequirementLevel = 5, EvoRequirementColor = CardColor.Black, DP = 12000,
                IsBlocker = true,
                SecurityAttackBonus = 1,
                EffectText = "≪블로커≫ ≪시큐리티 어택 +1≫ 공방을 동시에 지배하는 중장갑 궁극체."
            },
            new CardData
            {
                CardId = "BT2-065", Name = "크레니엄몬", Type = CardType.Digimon, Color = CardColor.Black, Level = 6,
                PlayCost = 12, EvoCost = 4, EvoRequirementLevel = 5, EvoRequirementColor = CardColor.Black, DP = 12000,
                IsBlocker = true,
                EffectText = "≪블로커≫ 모든 아군 블로커에게 소멸 내성을 부여하는 로열 나이츠."
            },
            new CardData
            {
                CardId = "ST5-14", Name = "한소라 & 매튜(블랙)", Type = CardType.Tamer, Color = CardColor.Black, Level = 0,
                PlayCost = 2,
                EffectText = "[자신의 턴] 아군 디지몬 DP +1000 & 턴 시작 메모리 +1. [시큐리티] 코스트 없이 등장."
            },
            new CardData
            {
                CardId = "ST5-15", Name = "레이저 아이", Type = CardType.Option, Color = CardColor.Black, Level = 0,
                PlayCost = 2,
                EffectText = "[메인] 상대 디지몬을 약화/레스트시켜 방어선을 무력화한다."
            },
            new CardData
            {
                CardId = "ST5-16", Name = "인피니티 캐논", Type = CardType.Option, Color = CardColor.Black, Level = 0,
                PlayCost = 7,
                EffectText = "[메인] 상대의 디지몬 1체를 즉시 소멸시킨다. [시큐리티] [메인] 효과를 발동한다."
            },

            // ==================== 💜 PURPLE: ST-6 베놈 바이올렛 & BT2 퍼플 ====================
            new CardData
            {
                CardId = "ST6-01", Name = "파그몬", Type = CardType.Digitama, Color = CardColor.Purple, Level = 2,
                InheritedDpBonus = 1000,
                InheritedEffectText = "[어택 시] 트래시 연계 및 DP +1000."
            },
            new CardData
            {
                CardId = "ST6-02", Name = "피코데블몬", Type = CardType.Digimon, Color = CardColor.Purple, Level = 3,
                PlayCost = 2, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Purple, DP = 3000,
                EffectText = "등장 코스트 2의 속공형 퍼플 소악마 디지몬."
            },
            new CardData
            {
                CardId = "ST6-03", Name = "가트몬(퍼플)", Type = CardType.Digimon, Color = CardColor.Purple, Level = 3,
                PlayCost = 3, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Purple, DP = 2000,
                InheritedDpBonus = 1000,
                InheritedEffectText = "[자신의 턴] 이 디지몬의 DP를 +1000."
            },
            new CardData
            {
                CardId = "ST6-04", Name = "드라큐몬", Type = CardType.Digimon, Color = CardColor.Purple, Level = 3,
                PlayCost = 3, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Purple, DP = 4000,
                EffectText = "언데드 군단의 주력 성장기 디지몬."
            },
            new CardData
            {
                CardId = "BT2-068", Name = "임프몬", Type = CardType.Digimon, Color = CardColor.Purple, Level = 3,
                PlayCost = 3, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Purple, DP = 2000,
                InheritedDpBonus = 1000,
                EffectText = "트래시를 쌓으며 마왕형 진화를 준비하는 소악마형 성장기."
            },
            new CardData
            {
                CardId = "ST6-05", Name = "데블몬", Type = CardType.Digimon, Color = CardColor.Purple, Level = 4,
                PlayCost = 5, EvoCost = 2, EvoRequirementLevel = 3, EvoRequirementColor = CardColor.Purple, DP = 5000,
                IsBlocker = true,
                InheritedDpBonus = 1000,
                EffectText = "≪블로커≫ 어둠의 힘으로 전장을 지배하는 타천사형 성숙기."
            },
            new CardData
            {
                CardId = "ST6-06", Name = "블랙가트몬", Type = CardType.Digimon, Color = CardColor.Purple, Level = 4,
                PlayCost = 4, EvoCost = 2, EvoRequirementLevel = 3, EvoRequirementColor = CardColor.Purple, DP = 5000,
                InheritedSecurityAttackBonus = 1,
                InheritedEffectText = "≪속공 / 시큐리티 어택 +1≫ 공격적인 진화원 효과를 부여한다."
            },
            new CardData
            {
                CardId = "ST6-07", Name = "위자몬", Type = CardType.Digimon, Color = CardColor.Purple, Level = 4,
                PlayCost = 5, EvoCost = 2, EvoRequirementLevel = 3, EvoRequirementColor = CardColor.Purple, DP = 5000,
                EffectText = "마법의 힘으로 아군을 지원하는 마인형 성숙기 디지몬."
            },
            new CardData
            {
                CardId = "ST6-08", Name = "레디데블몬", Type = CardType.Digimon, Color = CardColor.Purple, Level = 5,
                PlayCost = 6, EvoCost = 3, EvoRequirementLevel = 4, EvoRequirementColor = CardColor.Purple, DP = 7000,
                InheritedDpBonus = 2000,
                EffectText = "[진화 시] 덱에서 카드를 보충하고 타천사 궁극체로 연결하는 완전체."
            },
            new CardData
            {
                CardId = "ST6-09", Name = "스컬사탄몬", Type = CardType.Digimon, Color = CardColor.Purple, Level = 5,
                PlayCost = 7, EvoCost = 3, EvoRequirementLevel = 4, EvoRequirementColor = CardColor.Purple, DP = 7000,
                InheritedSecurityAttackBonus = 1,
                EffectText = "[등장/진화 시] 트래시의 악마형 카드를 회수하며 돌파력을 높인다."
            },
            new CardData
            {
                CardId = "BT2-074", Name = "묘티스몬", Type = CardType.Digimon, Color = CardColor.Purple, Level = 5,
                PlayCost = 7, EvoCost = 3, EvoRequirementLevel = 4, EvoRequirementColor = CardColor.Purple, DP = 8000,
                EffectText = "언데드 디지몬의 왕으로 군림하는 고화력 완전체."
            },
            new CardData
            {
                CardId = "ST6-10", Name = "베놈묘티스몬", Type = CardType.Digimon, Color = CardColor.Purple, Level = 6,
                PlayCost = 11, EvoCost = 3, EvoRequirementLevel = 5, EvoRequirementColor = CardColor.Purple, DP = 12000,
                SecurityAttackBonus = 1,
                EffectText = "≪시큐리티 어택 +1≫ 압도적 파괴력을 지닌 마수형 궁극체."
            },
            new CardData
            {
                CardId = "ST6-11", Name = "메탈가루몬(퍼플)", Type = CardType.Digimon, Color = CardColor.Purple, Level = 6,
                PlayCost = 11, EvoCost = 3, EvoRequirementLevel = 5, EvoRequirementColor = CardColor.Purple, DP = 11000,
                SecurityAttackBonus = 1,
                EffectText = "[어택 시] 어둠의 속공 연타로 상대 시큐리티를 유린하는 궁극체."
            },
            new CardData
            {
                CardId = "BT2-080", Name = "피에몬", Type = CardType.Digimon, Color = CardColor.Purple, Level = 6,
                PlayCost = 12, EvoCost = 4, EvoRequirementLevel = 5, EvoRequirementColor = CardColor.Purple, DP = 12000,
                SecurityAttackBonus = 1,
                EffectText = "[등장/진화 시] 다크 마스터즈의 수장으로 전장을 장악하는 마인형 궁극체."
            },
            new CardData
            {
                CardId = "ST6-14", Name = "매튜(퍼플)", Type = CardType.Tamer, Color = CardColor.Purple, Level = 0,
                PlayCost = 2,
                EffectText = "[자신의 턴] 아군 디지몬 DP +1000 & 턴 시작 시 메모리 +1. [시큐리티] 코스트 없이 등장."
            },
            new CardData
            {
                CardId = "ST6-15", Name = "데스 클로", Type = CardType.Option, Color = CardColor.Purple, Level = 0,
                PlayCost = 1,
                EffectText = "[메인] 상대의 디지몬 1체를 소멸시킨다. [시큐리티] [메인] 효과를 발동한다."
            },
            new CardData
            {
                CardId = "ST6-16", Name = "트럼프 소드(네일 본)", Type = CardType.Option, Color = CardColor.Purple, Level = 0,
                PlayCost = 7,
                EffectText = "[메인] 상대의 액티브/레스트 디지몬을 처단한다. [시큐리티] [메인] 효과를 발동한다."
            },

            // ==================== 👑 WHITE / ULTRA (Lv.7 초궁극체 로열 나이츠 & 사신) ====================
            new CardData
            {
                CardId = "BT1-084", Name = "오메가몬", Type = CardType.Digimon, Color = CardColor.White, Level = 7,
                PlayCost = 15, EvoCost = 6, EvoRequirementLevel = 6, EvoRequirementColor = CardColor.Red, DP = 15000,
                SecurityAttackBonus = 1,
                EffectText = "[진화 시] 레드/블루 Lv.6 위에서 초진화! 상대 디지몬을 쓸어버리고 액티브 재기상하는 궁극의 로열 나이츠(Lv.7)."
            },
            new CardData
            {
                CardId = "BT3-112", Name = "오메가몬 알터-S", Type = CardType.Digimon, Color = CardColor.White, Level = 7,
                PlayCost = 15, EvoCost = 6, EvoRequirementLevel = 6, EvoRequirementColor = CardColor.Blue, DP = 15000,
                SecurityAttackBonus = 1,
                EffectText = "[진화 시] 블리츠그레이몬 & 크레스가루몬의 힘이 융합된 또 하나의 초궁극체 오메가몬(Lv.7)."
            },
            new CardData
            {
                CardId = "BT5-086", Name = "오메가몬 즈와르트", Type = CardType.Digimon, Color = CardColor.White, Level = 7,
                PlayCost = 15, EvoCost = 6, EvoRequirementLevel = 6, EvoRequirementColor = CardColor.Black, DP = 15000,
                SecurityAttackBonus = 1,
                EffectText = "[진화 시] 블랙/퍼플/블루 Lv.6 위에서 강림하는 칠흑의 성기사 초궁극체(Lv.7)."
            }
        };

        foreach (var card in cards)
        {
            if (string.IsNullOrWhiteSpace(card.ImageUrl))
            {
                card.ImageUrl = $"https://images.digimoncard.io/images/cards/{card.CardId}.jpg";
            }
        }

        return cards;
    }

    /// <summary>
    /// 6대 메타/스타터 추천 덱 프리셋 목록 반환
    /// </summary>
    public static List<RecommendedDeckPreset> GetRecommendedDeckPresets() => new()
    {
        new RecommendedDeckPreset(
            "RED_OMEGA",
            "🔥 [추천 1] 워그레이몬 & 오메가몬 초화력 비트",
            "Red Tier 1 · 시큐리티 연쇄 돌파",
            "신태일과 그레이몬 진화원으로 DP와 시큐리티 어택 매수를 극대화해 단숨에 5장 시큐리티를 증발시키는 정통 레드 어그로/비트 덱입니다.",
            CardColor.Red,
            "ST1-11",
            CreateSt1GaiaRedPreset),

        new RecommendedDeckPreset(
            "BLUE_OMEGA",
            "❄️ [추천 2] 메탈가루몬 & 오메가몬 연타 템포",
            "Blue Tier 1 · 액티브 재기상 2연타",
            "매튜와 해머 스파크로 메모리를 확보하고, 메탈가루몬의 [어택 시 재기상]과 오메가몬 초진화로 연속 공격을 퍼붓는 블루 템포 덱입니다.",
            CardColor.Blue,
            "ST2-11",
            CreateSt2CocytusBluePreset),

        new RecommendedDeckPreset(
            "YELLOW_SERAPHI",
            "⚡ [추천 3] 세라피몬 & 슬래쉬엔젤몬 천사 컨트롤",
            "Yellow Control · 시큐리티 압박 & DP 제압",
            "파닥몬-엔젤몬-홀리엔젤몬-세라피몬으로 이어지는 성스러운 천사 계보로 공방을 장악하는 헤븐즈 옐로우 추천 덱입니다.",
            CardColor.Yellow,
            "ST3-11",
            CreateSt3HeavensYellowPreset),

        new RecommendedDeckPreset(
            "GREEN_HERCULES",
            "🌿 [추천 4] 헤라클레스카부테리몬 초고속 진화",
            "Green Ramp · 저코스트 고속 궁극체",
            "저렴한 진화 코스트로 누구보다 빠르게 Lv.6 헤라클레스카부테리몬과 반쵸스팅몬에 도달해 필드를 제압하는 그린 램프 덱입니다.",
            CardColor.Green,
            "ST4-11",
            CreateSt4GigaGreenPreset),

        new RecommendedDeckPreset(
            "BLACK_MACHINEDRA",
            "⚙️ [추천 5] 무겐드라몬 & 크레니엄몬 철벽 기갑",
            "Black Defense · 중장갑 블로커 군단",
            "다크티라노몬, 메가드라몬, 파워드라몬, 크레니엄몬의 철벽 블로커 라인에 무겐드라몬과 오메가몬 즈와르트를 더한 기갑 요새 덱입니다.",
            CardColor.Black,
            "ST5-11",
            CreateSt5MachineBlackPreset),

        new RecommendedDeckPreset(
            "PURPLE_VENOM",
            "💜 [추천 6] 베놈묘티스몬 & 피에몬 다크니스",
            "Purple Aggro · 타천사 & 마인 속공",
            "데블몬과 레디데블몬의 공격적인 진화원 보너스를 바탕으로 베놈묘티스몬, 피에몬, 오메가몬 즈와르트로 몰아치는 다크니스 덱입니다.",
            CardColor.Purple,
            "ST6-10",
            CreateSt6VenomVioletPreset)
    };

    /// <summary>
    /// 현재 짜고 있는 덱(또는 선택한 색상)을 분석하여 부족한 카드를 황금 비율(50장 메인 + 4장 디지타마)에 맞게 자동 추천 완성!
    /// </summary>
    public static (List<CardData> MainDeck, List<CardData> DigitamaDeck, string SummaryMessage) AutoCompleteRecommendedDeck(
        IReadOnlyList<CardData> currentMain,
        IReadOnlyList<CardData> currentDigitama,
        IReadOnlyList<CardData>? customCatalog,
        CardColor? preferredColor = null)
    {
        return AutoCompleteRecommendedDeck(currentMain, currentDigitama, preferredColor, customCatalog);
    }

    public static (List<CardData> MainDeck, List<CardData> DigitamaDeck, string SummaryMessage) AutoCompleteRecommendedDeck(
        IReadOnlyList<CardData> currentMain,
        IReadOnlyList<CardData> currentDigitama,
        CardColor? preferredColor = null,
        IReadOnlyList<CardData>? customCatalog = null)
    {
        var catalog = customCatalog ?? GetAllCatalogCards();

        // 1) 주력 색상 판별 (현재 덱에 가장 많은 색상 -> 필터 선택 색상 -> 기본 Red)
        CardColor targetColor = preferredColor
            ?? currentMain.Where(c => c.Color != CardColor.White)
                          .GroupBy(c => c.Color)
                          .OrderByDescending(g => g.Count())
                          .Select(g => g.Key)
                          .FirstOrDefault(CardColor.Red);

        if (targetColor == CardColor.White)
            targetColor = CardColor.Red;

        var main = currentMain.Take(50).ToList();
        var digitama = currentDigitama.Take(5).ToList();

        int CountCopies(IEnumerable<CardData> list, string id) => list.Count(c => c.CardId == id);

        // 2) 디지타마 4장 채우기
        var colorEggs = catalog.Where(c => c.Type == CardType.Digitama && c.Color == targetColor).ToList();
        if (colorEggs.Count == 0)
            colorEggs = catalog.Where(c => c.Type == CardType.Digitama).ToList();

        foreach (var egg in colorEggs)
        {
            while (digitama.Count < 4 && CountCopies(digitama, egg.CardId) < 4)
            {
                digitama.Add(egg);
            }
        }

        // 3) 황금 비율 목표치: Lv.3(14장), Lv.4(12장), Lv.5(10장), Lv.6~7(6장), 테이머/옵션(8장) = 50장
        void FillPool(Func<CardData, bool> predicate, int targetCategoryCount)
        {
            var candidates = catalog
                .Where(predicate)
                .OrderByDescending(c => c.SecurityAttackBonus + c.InheritedSecurityAttackBonus)
                .ThenByDescending(c => c.InheritedDpBonus)
                .ThenBy(c => c.EvoCost)
                .ToList();

            foreach (var cand in candidates)
            {
                while (main.Count < 50 &&
                       main.Count(predicate) < targetCategoryCount &&
                       CountCopies(main, cand.CardId) < 4)
                {
                    main.Add(cand);
                }
            }
        }

        FillPool(c => c.Type == CardType.Digimon && c.Color == targetColor && c.Level == 3, 14);
        FillPool(c => c.Type == CardType.Digimon && c.Color == targetColor && c.Level == 4, 12);
        FillPool(c => c.Type == CardType.Digimon && c.Color == targetColor && c.Level == 5, 10);
        FillPool(c => c.Type == CardType.Digimon && c.Color == targetColor && c.Level == 6, 5);

        // Lv.7 초궁극체 1장 조커 투입 (Lv.6 5장 + Lv.7 1장 = 총 6장 황금비율!)
        var ultraId = targetColor is CardColor.Black or CardColor.Purple ? "BT5-086" : "BT1-084";
        var ultraCard = catalog.FirstOrDefault(c => c.CardId == ultraId);
        if (ultraCard != null && targetColor is CardColor.Red or CardColor.Blue or CardColor.Black or CardColor.Purple)
        {
            while (main.Count < 50 && CountCopies(main, ultraCard.CardId) < 1 && main.Count(c => c.Level >= 6) < 6)
            {
                main.Add(ultraCard);
            }
        }
        else
        {
            FillPool(c => c.Type == CardType.Digimon && c.Color == targetColor && c.Level == 6, 6);
        }

        // 테이머 (2장) & 옵션 (6장) = 8장 채우기
        FillPool(c => c.Type == CardType.Tamer && c.Color == targetColor, 2);
        FillPool(c => c.Type == CardType.Option && c.Color == targetColor, 6);

        // 만약 아직 50장이 안 되었다면 해당 색상의 남은 카드로 50장 완성
        var fallbackPool = catalog.Where(c => c.Type != CardType.Digitama && c.Color == targetColor && c.Level <= 5).ToList();
        foreach (var cand in fallbackPool)
        {
            while (main.Count < 50 && CountCopies(main, cand.CardId) < 4)
            {
                main.Add(cand);
            }
        }

        string msg = $"🤖 [{targetColor}] 속성 황금 비율(Lv.3 14장 / Lv.4 12장 / Lv.5 10장 / Lv.6~7 6장 / 테이머·옵션 8장)에 맞춰 메인 덱 {main.Count}장 + 디지타마 {digitama.Count}장을 자동 완성했습니다!";
        return (main, digitama, msg);
    }

    /// <summary>
    /// ST-1 가이아 레드 50장 메인 덱 + 4장 디지타마 프리셋 생성 (황금 비율 준수)
    /// </summary>
    public static (List<CardData> MainDeck, List<CardData> DigitamaDeck) CreateSt1GaiaRedPreset()
    {
        var catalog = GetAllCatalogCards().ToDictionary(c => c.CardId);
        var digitama = new List<CardData>();
        var main = new List<CardData>();

        AddCopies(digitama, catalog["ST1-01"], 4);

        // Lv.3 성장기: 14장
        AddCopies(main, catalog["ST1-03"], 4);  // 아구몬 x4
        AddCopies(main, catalog["ST1-02"], 4);  // 피요몬 x4
        AddCopies(main, catalog["ST1-04"], 4);  // 드라코몬 x4
        AddCopies(main, catalog["BT1-010"], 2); // 아구몬(BT1) x2

        // Lv.4 성숙기: 12장
        AddCopies(main, catalog["ST1-07"], 4); // 그레이몬 x4
        AddCopies(main, catalog["ST1-05"], 4); // 버드라몬 x4
        AddCopies(main, catalog["ST1-06"], 4); // 코어드라몬 x4

        // Lv.5 완전체: 10장
        AddCopies(main, catalog["ST1-09"], 4);  // 메탈그레이몬 x4
        AddCopies(main, catalog["ST1-08"], 4);  // 가루다몬 x4
        AddCopies(main, catalog["BT1-020"], 2); // 그라운드라몬 x2

        // Lv.6~7 궁극체/초궁극체: 6장 (황금비율 4~6장 적정)
        AddCopies(main, catalog["ST1-11"], 3);  // 워그레이몬 x3
        AddCopies(main, catalog["ST1-10"], 2);  // 페닉스몬 x2
        AddCopies(main, catalog["BT1-084"], 1); // 오메가몬(Lv.7) x1

        // 테이머/옵션: 8장 (황금비율 6~8장 적정)
        AddCopies(main, catalog["ST1-12"], 2); // 신태일 x2
        AddCopies(main, catalog["ST1-13"], 2); // 섀도우 윙 x2
        AddCopies(main, catalog["ST1-14"], 2); // 스타라이트 익스플로전 x2
        AddCopies(main, catalog["ST1-16"], 2); // 가이아 포스 x2

        return (main, digitama);
    }

    /// <summary>
    /// ST-2 코큐토스 블루 50장 메인 덱 + 4장 디지타마 프리셋 생성 (황금 비율 준수)
    /// </summary>
    public static (List<CardData> MainDeck, List<CardData> DigitamaDeck) CreateSt2CocytusBluePreset()
    {
        var catalog = GetAllCatalogCards().ToDictionary(c => c.CardId);
        var digitama = new List<CardData>();
        var main = new List<CardData>();

        AddCopies(digitama, catalog["ST2-01"], 4);

        // Lv.3 성장기: 14장
        AddCopies(main, catalog["ST2-03"], 4);  // 파피몬 x4
        AddCopies(main, catalog["ST2-02"], 4);  // 고마몬 x4
        AddCopies(main, catalog["ST2-04"], 4);  // 베어몬 x4
        AddCopies(main, catalog["BT1-029"], 2); // 파피몬(BT1) x2

        // Lv.4 성숙기: 12장
        AddCopies(main, catalog["ST2-06"], 4); // 가루몬 x4
        AddCopies(main, catalog["ST2-05"], 4); // 원뿔몬 x4
        AddCopies(main, catalog["ST2-07"], 4); // 그리즈몬 x4

        // Lv.5 완전체: 10장
        AddCopies(main, catalog["ST2-08"], 4);  // 워가루몬 x4
        AddCopies(main, catalog["ST2-09"], 4);  // 쥬드몬 x4
        AddCopies(main, catalog["BT1-040"], 2); // 워가루몬(BT1) x2

        // Lv.6~7 궁극체/초궁극체: 6장 (황금비율 4~6장 적정)
        AddCopies(main, catalog["ST2-11"], 3);  // 메탈가루몬 x3
        AddCopies(main, catalog["ST2-10"], 2);  // 플레시오몬 x2
        AddCopies(main, catalog["BT1-084"], 1); // 오메가몬(Lv.7) x1

        // 테이머/옵션: 8장 (황금비율 6~8장 적정)
        AddCopies(main, catalog["ST2-12"], 2); // 매튜 x2
        AddCopies(main, catalog["ST2-13"], 2); // 해머 스파크 x2
        AddCopies(main, catalog["ST2-14"], 2); // 소로우 블루 x2
        AddCopies(main, catalog["ST2-16"], 2); // 코큐토스 브레스 x2

        return (main, digitama);
    }

    public static (List<CardData> MainDeck, List<CardData> DigitamaDeck) CreateSt3HeavensYellowPreset()
    {
        var (main, digitama, _) = AutoCompleteRecommendedDeck(Array.Empty<CardData>(), Array.Empty<CardData>(), CardColor.Yellow);
        return (main, digitama);
    }

    public static (List<CardData> MainDeck, List<CardData> DigitamaDeck) CreateSt4GigaGreenPreset()
    {
        var (main, digitama, _) = AutoCompleteRecommendedDeck(Array.Empty<CardData>(), Array.Empty<CardData>(), CardColor.Green);
        return (main, digitama);
    }

    public static (List<CardData> MainDeck, List<CardData> DigitamaDeck) CreateSt5MachineBlackPreset()
    {
        var (main, digitama, _) = AutoCompleteRecommendedDeck(Array.Empty<CardData>(), Array.Empty<CardData>(), CardColor.Black);
        return (main, digitama);
    }

    public static (List<CardData> MainDeck, List<CardData> DigitamaDeck) CreateSt6VenomVioletPreset()
    {
        var (main, digitama, _) = AutoCompleteRecommendedDeck(Array.Empty<CardData>(), Array.Empty<CardData>(), CardColor.Purple);
        return (main, digitama);
    }

    private static void AddCopies(List<CardData> list, CardData card, int count)
    {
        for (int i = 0; i < count; i++)
        {
            list.Add(card);
        }
    }
}
