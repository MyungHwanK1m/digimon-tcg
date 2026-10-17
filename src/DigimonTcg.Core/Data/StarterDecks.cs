using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Models;

namespace DigimonTcg.Core.Data;

public static class StarterDecks
{
    public static List<CardData> GetGaiaRedSampleCards() =>
        GetAllCatalogCards().Where(c => c.CardId.StartsWith("ST1-") || c.Color == CardColor.Red).ToList();

    public static List<CardData> GetCocytusBlueSampleCards() =>
        GetAllCatalogCards().Where(c => c.CardId.StartsWith("ST2-") || c.Color == CardColor.Blue).ToList();

    /// <summary>
    /// 전체 기본 내장 카드 도감 (ST-1 가이아 레드, ST-2 코큐토스 블루 및 BT-1 주요 카드 - 실제 공식 카드 이미지 URL 연동)
    /// </summary>
    public static List<CardData> GetAllCatalogCards()
    {
        var cards = new List<CardData>
        {
            // ==================== ST-1 가이아 레드 (공식 번호 ST1-01 ~ ST1-16 + BT1) ====================
            new CardData
            {
                CardId = "ST1-01", Name = "코로몬", Type = CardType.Digitama, Color = CardColor.Red, Level = 2,
                InheritedDpBonus = 1000,
                InheritedEffectText = "[자신의 턴] 진화원을 가진 이 디지몬의 DP를 +1000."
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
                EffectText = "빠른 필드 전개를 돕는 레드 성장기 디지몬."
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
                InheritedEffectText = "[자신의 턴] 진화원 DP 보너스 및 돌파력 강화."
            },
            new CardData
            {
                CardId = "BT1-020", Name = "그라운드라몬", Type = CardType.Digimon, Color = CardColor.Red, Level = 5,
                PlayCost = 6, EvoCost = 2, EvoRequirementLevel = 4, EvoRequirementColor = CardColor.Red, DP = 6000,
                EffectText = "진화 코스트 2로 빠르게 궁극체로 이어주는 완전체 디지몬."
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
                CardId = "ST1-12", Name = "신태일", Type = CardType.Tamer, Color = CardColor.Red, Level = 0,
                PlayCost = 2,
                EffectText = "[자신의 턴] 자신의 모든 디지몬의 DP를 +1000. [시큐리티] 코스트를 지불하지 않고 이 카드를 등장시킨다."
            },
            new CardData
            {
                CardId = "ST1-13", Name = "섀도우 윙", Type = CardType.Option, Color = CardColor.Red, Level = 0,
                PlayCost = 1,
                EffectText = "[메인] 이 턴 동안 자신의 디지몬 1체의 DP를 +3000. [시큐리티] 이 턴 동안 자신의 모든 디지몬의 DP +3000 & 시큐리티 어택 +1."
            },
            new CardData
            {
                CardId = "ST1-14", Name = "스타라이트 익스플로전", Type = CardType.Option, Color = CardColor.Red, Level = 0,
                PlayCost = 2,
                EffectText = "[메인] 이 턴 동안 자신의 디지몬 1체에 DP +2000 및 ≪시큐리티 어택 +1≫ 부여. [시큐리티] 이 턴 동안 자신의 모든 디지몬의 DP를 +7000."
            },
            new CardData
            {
                CardId = "ST1-15", Name = "기가 디스트로이어", Type = CardType.Option, Color = CardColor.Red, Level = 0,
                PlayCost = 6,
                EffectText = "[메인] DP 4000 이하의 상대 디지몬 2체까지 소멸시킨다. [시큐리티] 이 카드의 [메인] 효과를 발동한다."
            },
            new CardData
            {
                CardId = "ST1-16", Name = "가이아 포스", Type = CardType.Option, Color = CardColor.Red, Level = 0,
                PlayCost = 8,
                EffectText = "[메인] 상대의 디지몬 1체를 소멸시킨다. [시큐리티] 이 카드의 [메인] 효과를 발동한다."
            },

            // ==================== ST-2 코큐토스 블루 (공식 번호 ST2-01 ~ ST2-16 + BT1) ====================
            new CardData
            {
                CardId = "ST2-01", Name = "뿔몬", Type = CardType.Digitama, Color = CardColor.Blue, Level = 2,
                InheritedDpBonus = 1000,
                InheritedEffectText = "[자신의 턴] 진화원을 가진 이 디지몬의 DP를 +1000."
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
                EffectText = "[어택 시] 이 디지몬을 액티브 상태로 만들어 재행동 및 추가 타격을 가한다."
            },
            new CardData
            {
                CardId = "ST2-12", Name = "매튜", Type = CardType.Tamer, Color = CardColor.Blue, Level = 0,
                PlayCost = 2,
                EffectText = "[자신의 턴 시작 시] 메모리를 +1 추가 확보한다. [시큐리티] 코스트를 지불하지 않고 이 카드를 등장시킨다."
            },
            new CardData
            {
                CardId = "ST2-13", Name = "해머 스파크", Type = CardType.Option, Color = CardColor.Blue, Level = 0,
                PlayCost = 0,
                EffectText = "[메인] 메모리를 +1 얻는다. [시큐리티] 메모리를 +2 얻는다."
            },
            new CardData
            {
                CardId = "ST2-14", Name = "소로우 블루", Type = CardType.Option, Color = CardColor.Blue, Level = 0,
                PlayCost = 2,
                EffectText = "[메인] 진화원이 없는 상대 디지몬 1체를 레스트/행동불가로 만든다. [시큐리티] 상대 디지몬 1체를 레스트시킨다."
            },
            new CardData
            {
                CardId = "ST2-15", Name = "카이저 네일", Type = CardType.Option, Color = CardColor.Blue, Level = 0,
                PlayCost = 4,
                EffectText = "[메인] 자신의 디지몬 진화원 1장을 코스트 지불 없이 배틀 에어리어에 등장시킨다. [시큐리티] 이 카드의 [메인] 효과를 발동한다."
            },
            new CardData
            {
                CardId = "ST2-16", Name = "코큐토스 브레스", Type = CardType.Option, Color = CardColor.Blue, Level = 0,
                PlayCost = 7,
                EffectText = "[메인] 상대의 디지몬 1체를 패로 되돌린다(바운스). [시큐리티] 이 카드의 [메인] 효과를 발동한다."
            },

            // ==================== 기타 색상 도감 검색용 대표 카드 ====================
            new CardData
            {
                CardId = "ST3-04", Name = "파닥몬", Type = CardType.Digimon, Color = CardColor.Yellow, Level = 3,
                PlayCost = 3, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Yellow, DP = 2000,
                EffectText = "헤븐즈 옐로우 대표 성장기 디지몬."
            },
            new CardData
            {
                CardId = "ST4-03", Name = "텐토몬", Type = CardType.Digimon, Color = CardColor.Green, Level = 3,
                PlayCost = 3, EvoCost = 0, EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Green, DP = 2000,
                EffectText = "기가 그린 대표 성장기 디지몬."
            },
            new CardData
            {
                CardId = "BT1-084", Name = "오메가몬", Type = CardType.Digimon, Color = CardColor.White, Level = 7,
                PlayCost = 15, EvoCost = 6, EvoRequirementLevel = 6, EvoRequirementColor = CardColor.Red, DP = 15000,
                SecurityAttackBonus = 1,
                EffectText = "[진화 시] 워그레이몬/메탈가루몬 위에서 초진화하는 궁극의 로열 나이츠(Lv.7)."
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
    /// ST-1 가이아 레드 50장 메인 덱 + 4장 디지타마 프리셋 생성 (황금 비율 준수)
    /// Lv.3(14) + Lv.4(12) + Lv.5(10) + Lv.6(6) + 테이머/옵션(8) = 50장
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
        AddCopies(main, catalog["BT1-009"], 2); // 모노드라몬 x2

        // Lv.4 성숙기: 12장
        AddCopies(main, catalog["ST1-07"], 4); // 그레이몬 x4
        AddCopies(main, catalog["ST1-05"], 4); // 버드라몬 x4
        AddCopies(main, catalog["ST1-06"], 4); // 코어드라몬 x4

        // Lv.5 완전체: 10장
        AddCopies(main, catalog["ST1-09"], 4);  // 메탈그레이몬 x4
        AddCopies(main, catalog["ST1-08"], 4);  // 가루다몬 x4
        AddCopies(main, catalog["BT1-020"], 2); // 그라운드라몬 x2

        // Lv.6 궁극체: 6장
        AddCopies(main, catalog["ST1-11"], 4); // 워그레이몬 x4
        AddCopies(main, catalog["ST1-10"], 2); // 페닉스몬 x2

        // 테이머/옵션: 8장 (신태일, 섀도우 윙, 스타라이트 익스플로전, 가이아 포스)
        AddCopies(main, catalog["ST1-12"], 2); // 신태일 x2 (아군 전체 DP+1000 & 3메모리 보장)
        AddCopies(main, catalog["ST1-13"], 2); // 섀도우 윙 x2 (DP+3000)
        AddCopies(main, catalog["ST1-14"], 2); // 스타라이트 익스플로전 x2 (DP+2000 & 시큐리티 어택+1)
        AddCopies(main, catalog["ST1-16"], 2); // 가이아 포스 x2 (상대 디지몬 소멸)

        return (main, digitama);
    }

    /// <summary>
    /// ST-2 코큐토스 블루 50장 메인 덱 + 4장 디지타마 프리셋 생성 (황금 비율 준수)
    /// Lv.3(14) + Lv.4(12) + Lv.5(10) + Lv.6(6) + 테이머/옵션(8) = 50장
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

        // Lv.6 궁극체: 6장
        AddCopies(main, catalog["ST2-11"], 4); // 메탈가루몬 x4
        AddCopies(main, catalog["ST2-10"], 2); // 플레시오몬 x2

        // 테이머/옵션: 8장 (매튜, 해머 스파크, 소로우 블루, 코큐토스 브레스)
        AddCopies(main, catalog["ST2-12"], 2); // 매튜 x2
        AddCopies(main, catalog["ST2-13"], 2); // 해머 스파크 x2
        AddCopies(main, catalog["ST2-14"], 2); // 소로우 블루 x2
        AddCopies(main, catalog["ST2-16"], 2); // 코큐토스 브레스 x2

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
