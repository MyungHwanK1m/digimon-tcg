using System.Collections.Generic;
using DigimonSurvivors.Models;
using DigimonTcg.Core.Models;

namespace DigimonSurvivors.Data;

public static class GameDatabase
{
    public static readonly Dictionary<string, DigimonProfile> Digimons = new();
    public static readonly Dictionary<string, SkillDefinition> Skills = new();
    public static readonly Dictionary<string, PassiveDefinition> Passives = new();

    static GameDatabase()
    {
        InitializeDigimons();
        InitializeSkills();
        InitializePassives();
        FinalizeSkillStages();
    }

    private static void InitializeDigimons()
    {
        // ======================== 스타터 디지몬 8종 (성장기 / Rookie) ========================

        // 1. 아구몬 (Red / Vaccine)
        AddDigimon(new DigimonProfile
        {
            Id = "agumon",
            Name = "아구몬",
            EnglishName = "Agumon",
            Stage = DigimonStage.Rookie,
            Color = CardColor.Red,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 130,
            BaseSpeed = 190,
            BaseDp = 2000,
            SpriteFileName = "Agumon.png",
            CardId = "ST1-03",
            SignatureWeaponId = "baby_flame",
            SubSkillIds = new() { "spit_fire", "mach_jab" },
            Description = "두려움을 모르는 용기의 파충류형 디지몬. 밸런스 잡힌 기본 공격과 강력한 화염탄을 발사합니다.",
            RequiredSignatureLevel = 3,
            EvolutionCandidateIds = new() { "greymon", "dark_tyrannomon", "coredramon" }
        });

        // 2. 파피몬 (Blue / Data)
        AddDigimon(new DigimonProfile
        {
            Id = "gabumon",
            Name = "파피몬",
            EnglishName = "Gabumon",
            Stage = DigimonStage.Rookie,
            Color = CardColor.Blue,
            Attribute = DigimonAttribute.Data,
            BaseMaxHp = 120,
            BaseSpeed = 205,
            BaseDp = 2000,
            SpriteFileName = "Gabumon.png",
            CardId = "ST2-03",
            SignatureWeaponId = "petit_fire",
            SubSkillIds = new() { "little_horn", "ice_dash" },
            Description = "가루몬의 모피를 뒤집어쓴 의리의 파충류형 디지몬. 몸 주변을 맴도는 푸른 도깨비불로 적을 견제합니다.",
            RequiredSignatureLevel = 3,
            EvolutionCandidateIds = new() { "garurumon", "ikkakumon", "grizzlymon" }
        });

        // 3. 피요몬 (Red / Vaccine)
        AddDigimon(new DigimonProfile
        {
            Id = "piyomon",
            Name = "피요몬",
            EnglishName = "Piyomon",
            Stage = DigimonStage.Rookie,
            Color = CardColor.Red,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 110,
            BaseSpeed = 220,
            BaseDp = 3000,
            SpriteFileName = "Piyomon.png",
            CardId = "ST1-02",
            SignatureWeaponId = "shadow_wing",
            SubSkillIds = new() { "magical_fire", "spiral_twister" },
            Description = "팔처럼 변한 날개를 파닥거리며 날아다니는 아기새형 디지몬. 적을 관통하는 화염 날개깃을 던집니다.",
            RequiredSignatureLevel = 3,
            EvolutionCandidateIds = new() { "birdramon", "unimon", "gatomon" }
        });

        // 4. 피코데블몬 (Purple / Virus)
        AddDigimon(new DigimonProfile
        {
            Id = "pico_devimon",
            Name = "피코데블몬",
            EnglishName = "PicoDevimon",
            Stage = DigimonStage.Rookie,
            Color = CardColor.Purple,
            Attribute = DigimonAttribute.Virus,
            BaseMaxHp = 105,
            BaseSpeed = 225,
            BaseDp = 2500,
            SpriteFileName = "PicoDevimon.png",
            CardId = "ST6-02",
            SignatureWeaponId = "pico_dart",
            SubSkillIds = new() { "bat_flutter", "dark_whisper" },
            Description = "박쥐 형태의 소악마 디지몬. 날카로운 암흑 피코 다트로 적을 지속 교란합니다.",
            RequiredSignatureLevel = 3,
            EvolutionCandidateIds = new() { "devimon", "wizardmon", "black_gatomon" }
        });

        // 5. 파닥몬 (Yellow / Vaccine)
        AddDigimon(new DigimonProfile
        {
            Id = "patamon",
            Name = "파닥몬",
            EnglishName = "Patamon",
            Stage = DigimonStage.Rookie,
            Color = CardColor.Yellow,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 115,
            BaseSpeed = 200,
            BaseDp = 2000,
            SpriteFileName = "Patamon.png",
            CardId = "ST3-04",
            SignatureWeaponId = "air_shot",
            SubSkillIds = new() { "slamming_attack", "sparking_air" },
            Description = "날개 같은 귀로 하늘을 나는 포유류형 디지몬. 들이마신 공기를 압축 탄환 '공기팡'으로 발사합니다.",
            RequiredSignatureLevel = 3,
            EvolutionCandidateIds = new() { "angemon", "gatomon", "unimon" }
        });

        // 6. 텐토몬 (Green / Vaccine)
        AddDigimon(new DigimonProfile
        {
            Id = "tentomon",
            Name = "텐토몬",
            EnglishName = "Tentomon",
            Stage = DigimonStage.Rookie,
            Color = CardColor.Green,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 135,
            BaseSpeed = 195,
            BaseDp = 3000,
            SpriteFileName = "Tentomon.png",
            CardId = "ST4-03",
            SignatureWeaponId = "petit_thunder",
            SubSkillIds = new() { "rolling_guard", "rhino_thrust" },
            Description = "단단한 갑각과 친근한 말투를 지닌 곤충형 디지몬. 미세 번개 '꼬마 벼락'으로 적을 감전시킵니다.",
            RequiredSignatureLevel = 3,
            EvolutionCandidateIds = new() { "kabuterimon", "needlemon", "woodmon" }
        });

        // 7. 고마몬 (Blue / Vaccine)
        AddDigimon(new DigimonProfile
        {
            Id = "gomamon",
            Name = "고마몬",
            EnglishName = "Gomamon",
            Stage = DigimonStage.Rookie,
            Color = CardColor.Blue,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 125,
            BaseSpeed = 210,
            BaseDp = 2000,
            SpriteFileName = "Gomamon.png",
            CardId = "ST2-02",
            SignatureWeaponId = "surfing_fish",
            SubSkillIds = new() { "claw_strike", "water_bubble" },
            Description = "체온을 유지하는 붉은 모피를 지닌 해수형 디지몬. 작은 물고기 떼를 소환하여 적을 휩씁니다.",
            RequiredSignatureLevel = 3,
            EvolutionCandidateIds = new() { "ikkakumon", "garurumon", "seadramon" }
        });

        // 8. 고블몬 (Black / Virus)
        AddDigimon(new DigimonProfile
        {
            Id = "goburimon",
            Name = "고블몬",
            EnglishName = "Goburimon",
            Stage = DigimonStage.Rookie,
            Color = CardColor.Black,
            Attribute = DigimonAttribute.Virus,
            BaseMaxHp = 140,
            BaseSpeed = 185,
            BaseDp = 3000,
            SpriteFileName = "Goburimon.png",
            CardId = "ST5-04",
            SignatureWeaponId = "goburi_strike",
            SubSkillIds = new() { "club_throw", "wild_swing" },
            Description = "작고 교활한 오니형 디지몬. 손에 쥔 곤봉으로 묵직한 타격을 날립니다.",
            RequiredSignatureLevel = 3,
            EvolutionCandidateIds = new() { "orgemon", "tankmon", "dark_tyrannomon" }
        });

        // ======================== 성숙기 (Champion / Lv.4) ========================

        AddDigimon(new DigimonProfile
        {
            Id = "greymon",
            Name = "그레이몬",
            EnglishName = "Greymon",
            Stage = DigimonStage.Champion,
            Color = CardColor.Red,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 240,
            BaseSpeed = 205,
            BaseDp = 4000,
            SpriteFileName = "Greymon.png",
            CardId = "ST1-07",
            SignatureWeaponId = "mega_flame",
            SubSkillIds = new() { "great_horns", "fire_wall" },
            Description = "머리 부분이 단단한 갑각으로 뒤덮인 육식 공룡형 디지몬. 거대한 폭발 화염 '메가 플레임'을 전방에 뿜어냅니다.",
            RequiredSignatureLevel = 5,
            EvolutionCandidateIds = new() { "metal_greymon", "skull_greymon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "garurumon",
            Name = "가루몬",
            EnglishName = "Garurumon",
            Stage = DigimonStage.Champion,
            Color = CardColor.Blue,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 220,
            BaseSpeed = 230,
            BaseDp = 4000,
            SpriteFileName = "Garurumon.png",
            CardId = "ST2-06",
            SignatureWeaponId = "fox_fire",
            SubSkillIds = new() { "freeze_fang", "ice_wall" },
            Description = "푸른 줄무늬 모피를 지닌 늑대형 수인 디지몬. 쾌속의 이동 속도와 고주파의 냉기 화염을 연속 발사합니다.",
            RequiredSignatureLevel = 5,
            EvolutionCandidateIds = new() { "were_garurumon", "zudomon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "birdramon",
            Name = "버드라몬",
            EnglishName = "Birdramon",
            Stage = DigimonStage.Champion,
            Color = CardColor.Red,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 220,
            BaseSpeed = 235,
            BaseDp = 4000,
            SpriteFileName = "Birdramon.png",
            CardId = "ST1-05",
            SignatureWeaponId = "meteor_wing",
            SubSkillIds = new() { "fire_flap", "wind_cutter" },
            Description = "타오르는 화염의 날개를 지닌 거대 거조형 디지몬. 유성처럼 떨어지는 불꽃을 전방위로 퍼붓습니다.",
            RequiredSignatureLevel = 5,
            EvolutionCandidateIds = new() { "garudamon", "holy_angemon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "devimon",
            Name = "데블몬",
            EnglishName = "Devimon",
            Stage = DigimonStage.Champion,
            Color = CardColor.Purple,
            Attribute = DigimonAttribute.Virus,
            BaseMaxHp = 230,
            BaseSpeed = 220,
            BaseDp = 5000,
            SpriteFileName = "Devimon.png",
            CardId = "ST6-05",
            SignatureWeaponId = "death_claw",
            SubSkillIds = new() { "razor_wing", "dark_contract" },
            Description = "칠흑의 박쥐 날개를 지닌 타천사형 악마 디지몬. 길게 뻗는 악마의 손톱 '데스 클로'로 적의 심장을 쥡니다.",
            RequiredSignatureLevel = 5,
            EvolutionCandidateIds = new() { "vamdemon", "lady_devimon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "angemon",
            Name = "엔젤몬",
            EnglishName = "Angemon",
            Stage = DigimonStage.Champion,
            Color = CardColor.Yellow,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 230,
            BaseSpeed = 225,
            BaseDp = 4000,
            SpriteFileName = "Angemon.png",
            CardId = "ST3-05",
            SignatureWeaponId = "heavens_knuckle",
            SubSkillIds = new() { "holy_rod", "god_blessing" },
            Description = "6장의 빛나는 날개를 지닌 성스러운 천사 디지몬. 황금의 성스러운 주먹 '헤븐즈 너클'로 악을 정화합니다.",
            RequiredSignatureLevel = 5,
            EvolutionCandidateIds = new() { "holy_angemon", "angewomon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "kabuterimon",
            Name = "카부테리몬",
            EnglishName = "Kabuterimon",
            Stage = DigimonStage.Champion,
            Color = CardColor.Green,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 260,
            BaseSpeed = 210,
            BaseDp = 5000,
            SpriteFileName = "Kabuterimon.png",
            CardId = "ST4-08",
            SignatureWeaponId = "mega_blaster",
            SubSkillIds = new() { "beetle_horn", "electro_shocker" },
            Description = "강인한 곤충형 디지몬. 뿔 끝에서 방출하는 고출력 전자포 '메가 블래스터'를 일제 발사합니다.",
            RequiredSignatureLevel = 5,
            EvolutionCandidateIds = new() { "mega_kabuterimon", "lilimon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "ikkakumon",
            Name = "원뿔몬",
            EnglishName = "Ikkakumon",
            Stage = DigimonStage.Champion,
            Color = CardColor.Blue,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 270,
            BaseSpeed = 200,
            BaseDp = 4000,
            SpriteFileName = "Ikkakumon.png",
            CardId = "ST2-05",
            SignatureWeaponId = "harpoon_torpedo",
            SubSkillIds = new() { "northern_lights", "horn_drill" },
            Description = "극지방에서 살아가는 바다짐승형 디지몬. 머리의 뿔 미사일 '하푼 토피도'를 연사합니다.",
            RequiredSignatureLevel = 5,
            EvolutionCandidateIds = new() { "zudomon", "were_garurumon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "orgemon",
            Name = "우가몬",
            EnglishName = "Orgemon",
            Stage = DigimonStage.Champion,
            Color = CardColor.Black,
            Attribute = DigimonAttribute.Virus,
            BaseMaxHp = 280,
            BaseSpeed = 195,
            BaseDp = 5000,
            SpriteFileName = "Orgemon.png",
            CardId = "ST5-06",
            SignatureWeaponId = "haouken",
            SubSkillIds = new() { "bone_axe", "fury_club" },
            Description = "거대한 뼈 몽둥이를 휘두르는 호전적인 오니형 디지몬. 전방에 충격파 '패왕권'을 내지릅니다.",
            RequiredSignatureLevel = 5,
            EvolutionCandidateIds = new() { "skull_greymon", "metal_greymon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "dark_tyrannomon",
            Name = "다크티라노몬",
            EnglishName = "DarkTyrannomon",
            Stage = DigimonStage.Champion,
            Color = CardColor.Black,
            Attribute = DigimonAttribute.Virus,
            BaseMaxHp = 260,
            BaseSpeed = 205,
            BaseDp = 5000,
            SpriteFileName = "DarkTyrannomon.png",
            CardId = "ST5-08",
            SignatureWeaponId = "iron_tail",
            SubSkillIds = new() { "dark_matter", "fire_blast" },
            Description = "바이러스에 감염되어 흉포해진 공룡 디지몬. 강력한 강철 꼬리와 검은 화염포를 난사합니다.",
            RequiredSignatureLevel = 5,
            EvolutionCandidateIds = new() { "skull_greymon", "metal_greymon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "coredramon",
            Name = "코어드라몬",
            EnglishName = "Coredramon",
            Stage = DigimonStage.Champion,
            Color = CardColor.Red,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 240,
            BaseSpeed = 220,
            BaseDp = 5000,
            SpriteFileName = "Coredramon.png",
            CardId = "ST1-06",
            SignatureWeaponId = "blue_flare",
            SubSkillIds = new() { "strike_tail", "dragon_breath" },
            Description = "순수 드래곤의 피를 진하게 물려받은 용인형 디지몬. 푸른 화염 '블루 플레어'를 뿜어냅니다.",
            RequiredSignatureLevel = 5,
            EvolutionCandidateIds = new() { "metal_greymon", "garudamon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "unimon",
            Name = "유니몬",
            EnglishName = "Unimon",
            Stage = DigimonStage.Champion,
            Color = CardColor.Yellow,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 220,
            BaseSpeed = 240,
            BaseDp = 4000,
            SpriteFileName = "Unimon.png",
            CardId = "ST3-07",
            SignatureWeaponId = "holy_shot",
            SubSkillIds = new() { "corn_thrust", "aerial_gallop" },
            Description = "전설의 유니콘과 페가수스가 융합된 환수형 디지몬. 성스러운 광탄을 연속으로 발사합니다.",
            RequiredSignatureLevel = 5,
            EvolutionCandidateIds = new() { "holy_angemon", "garudamon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "gatomon",
            Name = "가트몬",
            EnglishName = "Gatomon",
            Stage = DigimonStage.Champion,
            Color = CardColor.Yellow,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 210,
            BaseSpeed = 230,
            BaseDp = 4000,
            SpriteFileName = "Gatomon.png",
            CardId = "ST3-06",
            SignatureWeaponId = "cat_punch",
            SubSkillIds = new() { "cat_eye", "tail_ring_flash" },
            Description = "꼬리에 성스러운 링을 착용한 고양이형 성수 디지몬. 작지만 매서운 '네코 펀치'로 적을 강타합니다.",
            RequiredSignatureLevel = 5,
            EvolutionCandidateIds = new() { "angewomon", "holy_angemon", "lady_devimon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "wizardmon",
            Name = "위자몬",
            EnglishName = "Wizardmon",
            Stage = DigimonStage.Champion,
            Color = CardColor.Purple,
            Attribute = DigimonAttribute.Data,
            BaseMaxHp = 210,
            BaseSpeed = 215,
            BaseDp = 4000,
            SpriteFileName = "Wizardmon.png",
            CardId = "ST6-07",
            SignatureWeaponId = "thunder_ball",
            SubSkillIds = new() { "magical_game", "terror_illusion" },
            Description = "이세계 마법을 다루는 마인형 디지몬. 번개 구체 '썬더 볼'을 사방으로 소환합니다.",
            RequiredSignatureLevel = 5,
            EvolutionCandidateIds = new() { "vamdemon", "lady_devimon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "black_gatomon",
            Name = "블랙가트몬",
            EnglishName = "BlackGatomon",
            Stage = DigimonStage.Champion,
            Color = CardColor.Purple,
            Attribute = DigimonAttribute.Virus,
            BaseMaxHp = 220,
            BaseSpeed = 235,
            BaseDp = 4000,
            SpriteFileName = "BlackGatomon.png",
            CardId = "ST6-06",
            SignatureWeaponId = "cat_laser",
            SubSkillIds = new() { "dangerous_claw", "dark_charm" },
            Description = "사악한 마성을 지닌 검은 고양이 디지몬. 눈에서 암흑 레이저를 뿜어 적을 꿰뚫습니다.",
            RequiredSignatureLevel = 5,
            EvolutionCandidateIds = new() { "lady_devimon", "vamdemon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "needlemon",
            Name = "니드몬",
            EnglishName = "Togemon",
            Stage = DigimonStage.Champion,
            Color = CardColor.Green,
            Attribute = DigimonAttribute.Data,
            BaseMaxHp = 250,
            BaseSpeed = 200,
            BaseDp = 4000,
            SpriteFileName = "Togemon.png",
            CardId = "ST4-06",
            SignatureWeaponId = "chiku_chiku_bang",
            SubSkillIds = new() { "macha_punch", "needle_spray" },
            Description = "권투 글러브를 낀 선인장형 디지몬. 온몸에서 가시를 폭풍처럼 뿜어내는 '치쿠치쿠 뱅뱅'을 씁니다.",
            RequiredSignatureLevel = 5,
            EvolutionCandidateIds = new() { "lilimon", "mega_kabuterimon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "woodmon",
            Name = "정글모자몬",
            EnglishName = "Woodmon",
            Stage = DigimonStage.Champion,
            Color = CardColor.Green,
            Attribute = DigimonAttribute.Virus,
            BaseMaxHp = 270,
            BaseSpeed = 190,
            BaseDp = 4000,
            SpriteFileName = "Woodmon.png",
            CardId = "ST4-07",
            SignatureWeaponId = "branch_drain",
            SubSkillIds = new() { "woody_smash", "leaf_cutter" },
            Description = "말라붙은 거목 형태의 식물형 디지몬. 적의 생명력을 흡수하며 묵직한 줄기로 내려칩니다.",
            RequiredSignatureLevel = 5,
            EvolutionCandidateIds = new() { "mega_kabuterimon", "lilimon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "grizzlymon",
            Name = "그리즈몬",
            EnglishName = "Grizzlymon",
            Stage = DigimonStage.Champion,
            Color = CardColor.Blue,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 260,
            BaseSpeed = 210,
            BaseDp = 5000,
            SpriteFileName = "Grizzlymon.png",
            CardId = "ST2-07",
            SignatureWeaponId = "crescent_dawn",
            SubSkillIds = new() { "maul_attack", "bear_claw" },
            Description = "강인한 체격을 지닌 맹수형 곰 디지몬. 일격필살의 발톱으로 지면을 가릅니다.",
            RequiredSignatureLevel = 5,
            EvolutionCandidateIds = new() { "were_garurumon", "zudomon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "tankmon",
            Name = "탱크몬",
            EnglishName = "Tankmon",
            Stage = DigimonStage.Champion,
            Color = CardColor.Black,
            Attribute = DigimonAttribute.Data,
            BaseMaxHp = 300,
            BaseSpeed = 180,
            BaseDp = 5000,
            SpriteFileName = "Tankmon.png",
            CardId = "ST5-07",
            SignatureWeaponId = "hyper_cannon",
            SubSkillIds = new() { "machine_gun_fire", "tread_crush" },
            Description = "전차와 융합된 중무장 개틀링 디지몬. 고폭탄 '하이퍼 캐논'을 연사합니다.",
            RequiredSignatureLevel = 5,
            EvolutionCandidateIds = new() { "metal_greymon", "skull_greymon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "seadramon",
            Name = "쉬라몬(시드라몬)",
            EnglishName = "Seadramon",
            Stage = DigimonStage.Champion,
            Color = CardColor.Blue,
            Attribute = DigimonAttribute.Data,
            BaseMaxHp = 240,
            BaseSpeed = 220,
            BaseDp = 4000,
            SpriteFileName = "Seadramon.png",
            CardId = "BT1-034",
            SignatureWeaponId = "ice_arrow",
            SubSkillIds = new() { "water_breath", "constriction" },
            Description = "해양을 지배하는 거대 수룡형 디지몬. 절대영도의 얼음 화살 '아이스 애로우'를 쏩니다.",
            RequiredSignatureLevel = 5,
            EvolutionCandidateIds = new() { "zudomon", "were_garurumon" }
        });

        // ======================== 완전체 (Ultimate / Lv.5) ========================

        AddDigimon(new DigimonProfile
        {
            Id = "metal_greymon",
            Name = "메탈그레이몬",
            EnglishName = "MetalGreymon",
            Stage = DigimonStage.Ultimate,
            Color = CardColor.Red,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 420,
            BaseSpeed = 215,
            BaseDp = 7000,
            SpriteFileName = "MetalGreymon.png",
            CardId = "ST1-09",
            SignatureWeaponId = "giga_destroyer",
            SubSkillIds = new() { "trident_arm", "metal_slash" },
            Description = "몸의 절반 이상을 기계화하여 비약적인 화력을 손에 넣은 사이보그형 디지몬. 유도 미사일 '기가 디스트로이어'를 일제 발사합니다.",
            RequiredSignatureLevel = 7,
            EvolutionCandidateIds = new() { "war_greymon", "mugendramon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "were_garurumon",
            Name = "워가루몬",
            EnglishName = "WereGarurumon",
            Stage = DigimonStage.Ultimate,
            Color = CardColor.Blue,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 390,
            BaseSpeed = 245,
            BaseDp = 6000,
            SpriteFileName = "WereGarurumon.png",
            CardId = "ST2-08",
            SignatureWeaponId = "kaiser_nail",
            SubSkillIds = new() { "moon_sault_kick", "garuru_kick" },
            Description = "두 발로 서서 격투술을 연마한 늑대인간형 디지몬. 신속의 칼바람 손톱 '카이저 네일'로 주변 적들을 난도질합니다.",
            RequiredSignatureLevel = 7,
            EvolutionCandidateIds = new() { "metal_garurumon", "plesiomon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "garudamon",
            Name = "가루다몬",
            EnglishName = "Garudamon",
            Stage = DigimonStage.Ultimate,
            Color = CardColor.Red,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 400,
            BaseSpeed = 250,
            BaseDp = 7000,
            SpriteFileName = "Garudamon.png",
            CardId = "ST1-08",
            SignatureWeaponId = "shadow_wing_ex",
            SubSkillIds = new() { "crimson_claw", "great_spirit" },
            Description = "붉은 깃털과 강철 발톱을 지닌 조인형 디지몬. 진홍의 거대 검풍으로 적진을 일도양단합니다.",
            RequiredSignatureLevel = 7,
            EvolutionCandidateIds = new() { "hououmon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "vamdemon",
            Name = "묘티스몬",
            EnglishName = "Vamdemon",
            Stage = DigimonStage.Ultimate,
            Color = CardColor.Purple,
            Attribute = DigimonAttribute.Virus,
            BaseMaxHp = 430,
            BaseSpeed = 230,
            BaseDp = 8000,
            SpriteFileName = "Vamdemon.png",
            CardId = "BT2-074",
            SignatureWeaponId = "night_raid",
            SubSkillIds = new() { "blood_stream", "cloud_of_bats" },
            Description = "언데드의 왕으로 군림하는 흡혈귀 디지몬. 무수한 악마 박쥐 떼를 불러 적의 피를 흡수합니다.",
            RequiredSignatureLevel = 7,
            EvolutionCandidateIds = new() { "piemon", "mugendramon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "holy_angemon",
            Name = "홀리엔젤몬",
            EnglishName = "MagnaAngemon",
            Stage = DigimonStage.Ultimate,
            Color = CardColor.Yellow,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 410,
            BaseSpeed = 235,
            BaseDp = 7000,
            SpriteFileName = "HolyAngemon.png",
            CardId = "ST3-08",
            SignatureWeaponId = "heaven_gate",
            SubSkillIds = new() { "excalibur", "soul_vanish" },
            Description = "8장의 은빛 날개와 엑스칼리버를 장착한 대천사 디지몬. 아공간 문 '헤븐즈 게이트'를 열어 악을 소멸시킵니다.",
            RequiredSignatureLevel = 7,
            EvolutionCandidateIds = new() { "seraphimon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "mega_kabuterimon",
            Name = "메가카부테리몬",
            EnglishName = "MegaKabuterimon",
            Stage = DigimonStage.Ultimate,
            Color = CardColor.Green,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 460,
            BaseSpeed = 220,
            BaseDp = 8000,
            SpriteFileName = "MegaKabuterimon.png",
            CardId = "ST4-11",
            SignatureWeaponId = "horn_buster",
            SubSkillIds = new() { "wild_blaster", "mega_horn" },
            Description = "비행 능력과 장갑이 대폭 강화된 장수풍뎅이 디지몬. 뿔에서 초거대 번개 에너지탄을 방출합니다.",
            RequiredSignatureLevel = 7,
            EvolutionCandidateIds = new() { "hercules_kabuterimon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "zudomon",
            Name = "쥬드몬",
            EnglishName = "Zudomon",
            Stage = DigimonStage.Ultimate,
            Color = CardColor.Blue,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 470,
            BaseSpeed = 215,
            BaseDp = 8000,
            SpriteFileName = "Zudomon.png",
            CardId = "ST2-09",
            SignatureWeaponId = "hammer_spark",
            SubSkillIds = new() { "thor_hammer", "ice_smash" },
            Description = "크롬디지조이드제 토르 해머를 든 바다사자형 디지몬. 대지를 내리쳐 벼락과 충격파를 일으킵니다.",
            RequiredSignatureLevel = 7,
            EvolutionCandidateIds = new() { "plesiomon", "metal_garurumon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "skull_greymon",
            Name = "스컬그레이몬",
            EnglishName = "SkullGreymon",
            Stage = DigimonStage.Ultimate,
            Color = CardColor.Black,
            Attribute = DigimonAttribute.Virus,
            BaseMaxHp = 480,
            BaseSpeed = 225,
            BaseDp = 9000,
            SpriteFileName = "SkullGreymon.png",
            CardId = "ST1-09",
            SignatureWeaponId = "ground_zero",
            SubSkillIds = new() { "oblivion_bird", "bone_strike" },
            Description = "싸움에만 집착한 끝에 암흑 진화해 뼈만 남은 언데드형 디지몬. 극도의 파괴력과 광역 폭발을 자랑합니다.",
            RequiredSignatureLevel = 7,
            EvolutionCandidateIds = new() { "mugendramon", "war_greymon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "angewomon",
            Name = "엔젤우몬",
            EnglishName = "Angewomon",
            Stage = DigimonStage.Ultimate,
            Color = CardColor.Yellow,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 390,
            BaseSpeed = 240,
            BaseDp = 7000,
            SpriteFileName = "Angewomon.png",
            CardId = "ST3-09",
            SignatureWeaponId = "holy_arrow",
            SubSkillIds = new() { "heaven_charm", "saint_air" },
            Description = "아름다운 8장의 날개를 지닌 여성 대천사 디지몬. 성스러운 화살로 번개처럼 악을 꿰뚫습니다.",
            RequiredSignatureLevel = 7,
            EvolutionCandidateIds = new() { "seraphimon", "rose_mon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "lady_devimon",
            Name = "레디데블몬",
            EnglishName = "LadyDevimon",
            Stage = DigimonStage.Ultimate,
            Color = CardColor.Purple,
            Attribute = DigimonAttribute.Virus,
            BaseMaxHp = 400,
            BaseSpeed = 245,
            BaseDp = 7000,
            SpriteFileName = "LadyDevimon.png",
            CardId = "ST6-08",
            SignatureWeaponId = "darkness_wave",
            SubSkillIds = new() { "poison_spear", "dark_slap" },
            Description = "강력한 암흑의 힘을 지닌 여성 타천사 디지몬. 검은 박쥐 파동 '다크니스 웨이브'로 대상을 태워버립니다.",
            RequiredSignatureLevel = 7,
            EvolutionCandidateIds = new() { "piemon", "mugendramon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "lilimon",
            Name = "릴리몬",
            EnglishName = "Lillymon",
            Stage = DigimonStage.Ultimate,
            Color = CardColor.Green,
            Attribute = DigimonAttribute.Data,
            BaseMaxHp = 380,
            BaseSpeed = 250,
            BaseDp = 6000,
            SpriteFileName = "Lilimon.png",
            CardId = "ST4-10",
            SignatureWeaponId = "flower_cannon",
            SubSkillIds = new() { "fairy_vine", "temptation_pollen" },
            Description = "꽃잎 날개를 활짝 펼친 요정형 디지몬. 양손을 모아 화약 가루와 꽃잎 에너지탄을 쏩니다.",
            RequiredSignatureLevel = 7,
            EvolutionCandidateIds = new() { "rose_mon", "hercules_kabuterimon" }
        });

        // ======================== 궁극체 (Mega / Lv.6) ========================

        AddDigimon(new DigimonProfile
        {
            Id = "war_greymon",
            Name = "워그레이몬",
            EnglishName = "WarGreymon",
            Stage = DigimonStage.Mega,
            Color = CardColor.Red,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 680,
            BaseSpeed = 240,
            BaseDp = 12000,
            SpriteFileName = "WarGreymon.png",
            CardId = "ST1-11",
            SignatureWeaponId = "gaia_force",
            SubSkillIds = new() { "dramon_killer", "brave_shield" },
            Description = "초금속 크롬디지조이드 갑옷을 두른 최강의 용전사. 대기 중의 에너지를 집중시켜 초거대 고열 구체 '가이아 포스'를 작렬합니다.",
            RequiredSignatureLevel = 8,
            EvolutionCandidateIds = new() { "omegamon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "metal_garurumon",
            Name = "메탈가루몬",
            EnglishName = "MetalGarurumon",
            Stage = DigimonStage.Mega,
            Color = CardColor.Blue,
            Attribute = DigimonAttribute.Data,
            BaseMaxHp = 630,
            BaseSpeed = 270,
            BaseDp = 11000,
            SpriteFileName = "MetalGarurumon.png",
            CardId = "ST2-11",
            SignatureWeaponId = "cocytus_breath",
            SubSkillIds = new() { "grace_cross_freezer", "garuru_tomahawk" },
            Description = "전신이 메탈화된 맹수형 사이보그. 모든 것을 얼려버리는 절대영도 빔 '코큐토스 브레스'를 방출합니다.",
            RequiredSignatureLevel = 8,
            EvolutionCandidateIds = new() { "omegamon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "hououmon",
            Name = "페닉스몬",
            EnglishName = "Hououmon",
            Stage = DigimonStage.Mega,
            Color = CardColor.Red,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 650,
            BaseSpeed = 270,
            BaseDp = 12000,
            SpriteFileName = "Hououmon.png",
            CardId = "ST1-10",
            SignatureWeaponId = "starlight_explosion",
            SubSkillIds = new() { "crimson_flare", "holy_feather" },
            Description = "4장의 황금 날개를 펼친 전설의 성수형 디지몬. 성스러운 빛의 가루를 흩뿌려 적을 정화합니다.",
            RequiredSignatureLevel = 8,
            EvolutionCandidateIds = new() { "omegamon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "seraphimon",
            Name = "세라피몬",
            EnglishName = "Seraphimon",
            Stage = DigimonStage.Mega,
            Color = CardColor.Yellow,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 660,
            BaseSpeed = 250,
            BaseDp = 12000,
            SpriteFileName = "Seraphimon.png",
            CardId = "ST3-11",
            SignatureWeaponId = "strike_of_the_seven_stars",
            SubSkillIds = new() { "testament", "seven_heavens" },
            Description = "최고위 치천사 디지몬. 7개의 초열 광구 '세븐 즈 헤븐'으로 모든 악을 완전히 소멸시킵니다.",
            RequiredSignatureLevel = 8,
            EvolutionCandidateIds = new() { "omegamon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "hercules_kabuterimon",
            Name = "헤라클레스카부테리몬",
            EnglishName = "HerculesKabuterimon",
            Stage = DigimonStage.Mega,
            Color = CardColor.Green,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 720,
            BaseSpeed = 240,
            BaseDp = 13000,
            SpriteFileName = "HerculesKabuterimon.png",
            CardId = "ST4-13",
            SignatureWeaponId = "giga_blaster",
            SubSkillIds = new() { "high_mega_blaster", "horn_scissor" },
            Description = "카부테리몬과 쿠와가몬의 정점을 합친 황금의 거대 곤충 왕. 초거대 번개 폭풍을 사방으로 방출합니다.",
            RequiredSignatureLevel = 8,
            EvolutionCandidateIds = new() { "omegamon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "piemon",
            Name = "피에몬",
            EnglishName = "Piemon",
            Stage = DigimonStage.Mega,
            Color = CardColor.Purple,
            Attribute = DigimonAttribute.Virus,
            BaseMaxHp = 700,
            BaseSpeed = 260,
            BaseDp = 13000,
            SpriteFileName = "Piemon.png",
            CardId = "BT2-080",
            SignatureWeaponId = "trump_sword",
            SubSkillIds = new() { "ending_snipe", "toy_wonderland" },
            Description = "어둠의 사천왕을 통솔하는 지옥의 광대. 등에 꽂힌 4자루의 검 '트럼프 소드'를 텔레포트 시켜 적을 벱니다.",
            RequiredSignatureLevel = 8,
            EvolutionCandidateIds = new() { "omegamon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "plesiomon",
            Name = "플레시오몬",
            EnglishName = "Plesiomon",
            Stage = DigimonStage.Mega,
            Color = CardColor.Blue,
            Attribute = DigimonAttribute.Data,
            BaseMaxHp = 690,
            BaseSpeed = 230,
            BaseDp = 11000,
            SpriteFileName = "Plesiomon.png",
            CardId = "ST2-10",
            SignatureWeaponId = "sorrow_blue",
            SubSkillIds = new() { "sad_water", "shaking_tail" },
            Description = "환상의 고대 수룡 디지몬. 슬픈 울음소리로 적의 전의를 상실시키고 초음파 해일을 일으킵니다.",
            RequiredSignatureLevel = 8,
            EvolutionCandidateIds = new() { "omegamon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "rose_mon",
            Name = "로제몬",
            EnglishName = "Rosemon",
            Stage = DigimonStage.Mega,
            Color = CardColor.Green,
            Attribute = DigimonAttribute.Data,
            BaseMaxHp = 640,
            BaseSpeed = 265,
            BaseDp = 11000,
            SpriteFileName = "Rosemon.png",
            CardId = "ST4-12",
            SignatureWeaponId = "forbidden_temptation",
            SubSkillIds = new() { "thorn_whip", "beauty_shock" },
            Description = "꽃들의 여왕으로 불리는 요정형 디지몬. 가시 채찍과 장미 꽃잎 폭풍으로 전장을 매혹합니다.",
            RequiredSignatureLevel = 8,
            EvolutionCandidateIds = new() { "omegamon" }
        });

        AddDigimon(new DigimonProfile
        {
            Id = "mugendramon",
            Name = "파워드라몬",
            EnglishName = "Mugendramon",
            Stage = DigimonStage.Mega,
            Color = CardColor.Black,
            Attribute = DigimonAttribute.Virus,
            BaseMaxHp = 800,
            BaseSpeed = 200,
            BaseDp = 14000,
            SpriteFileName = "Mugendramon.png",
            CardId = "ST5-12",
            SignatureWeaponId = "infinity_cannon",
            SubSkillIds = new() { "booster_claw", "catastrophe_d" },
            Description = "모든 사이보그 디지몬의 부품을 집대성한 100% 풀메탈 살육 기계. 2문의 무겐 캐논으로 지평선을 날립니다.",
            RequiredSignatureLevel = 8,
            EvolutionCandidateIds = new() { "omegamon" }
        });

        // ======================== 초궁극체 (Ultra / Lv.7) ========================

        AddDigimon(new DigimonProfile
        {
            Id = "omegamon",
            Name = "오메가몬",
            EnglishName = "Omegamon",
            Stage = DigimonStage.Ultra,
            Color = CardColor.White,
            Attribute = DigimonAttribute.Vaccine,
            BaseMaxHp = 1200,
            BaseSpeed = 290,
            BaseDp = 22000,
            SpriteFileName = "Omegamon.png",
            CardId = "BT1-084",
            SignatureWeaponId = "grey_sword",
            SubSkillIds = new() { "garuru_cannon", "sword_of_ruin" },
            Description = "워그레이몬과 메탈가루몬의 선한 의지가 하나로 융합되어 탄생한 로열 나이츠 성기사. 절대 무적의 힘을 발휘합니다."
        });
    }

    private static void InitializeSkills()
    {
        // ------------------ 메인 시그니처 스킬 ------------------
        AddSkill(new SkillDefinition
        {
            Id = "baby_flame",
            Name = "베이비 플레임",
            CardId = "ST1-03",
            Color = CardColor.Red,
            Type = WeaponType.ProjectileDirect,
            MaxLevel = 8,
            BaseDamage = 38,
            BaseCooldown = 0.9,
            BaseProjectileCount = 1,
            BaseSpeed = 400,
            BaseAreaRadius = 26,
            BasePierce = 1,
            Description = "가장 가까운 적을 향해 고열의 화염구를 발사합니다.",
            IconCardCode = "ST1-03"
        });

        AddSkill(new SkillDefinition
        {
            Id = "petit_fire",
            Name = "쁘띠 파이어",
            CardId = "ST2-03",
            Color = CardColor.Blue,
            Type = WeaponType.OrbitingBarrier,
            MaxLevel = 8,
            BaseDamage = 32,
            BaseCooldown = 0.1,
            BaseProjectileCount = 2,
            BaseSpeed = 3.6,
            BaseAreaRadius = 80,
            BasePierce = 999,
            Description = "플레이어 주위를 빙글빙글 회전하는 푸른 도깨비불 보호막.",
            IconCardCode = "ST2-03"
        });

        AddSkill(new SkillDefinition
        {
            Id = "shadow_wing",
            Name = "섀도우 윙",
            CardId = "ST1-02",
            Color = CardColor.Red,
            Type = WeaponType.PiercingSlice,
            MaxLevel = 8,
            BaseDamage = 52,
            BaseCooldown = 1.3,
            BaseProjectileCount = 1,
            BaseSpeed = 460,
            BaseAreaRadius = 32,
            BasePierce = 4,
            Description = "전방으로 관통하는 화염 날개깃을 던집니다.",
            IconCardCode = "ST1-02"
        });

        AddSkill(new SkillDefinition
        {
            Id = "pico_dart",
            Name = "피코 다트",
            CardId = "ST6-02",
            Color = CardColor.Purple,
            Type = WeaponType.ProjectileDirect,
            MaxLevel = 8,
            BaseDamage = 35,
            BaseCooldown = 0.7,
            BaseProjectileCount = 2,
            BaseSpeed = 450,
            BaseAreaRadius = 24,
            BasePierce = 1,
            Description = "빠른 속도로 회전하는 암흑 독침 다트를 2연사합니다.",
            IconCardCode = "ST6-02"
        });

        AddSkill(new SkillDefinition
        {
            Id = "air_shot",
            Name = "공기팡",
            CardId = "ST3-04",
            Color = CardColor.Yellow,
            Type = WeaponType.ProjectileDirect,
            MaxLevel = 8,
            BaseDamage = 45,
            BaseCooldown = 1.0,
            BaseProjectileCount = 1,
            BaseSpeed = 420,
            BaseAreaRadius = 36,
            BasePierce = 2,
            Description = "압축 공기탄을 발사하여 명중 시 충격파를 일으킵니다.",
            IconCardCode = "ST3-04"
        });

        AddSkill(new SkillDefinition
        {
            Id = "petit_thunder",
            Name = "꼬마 벼락",
            CardId = "ST4-03",
            Color = CardColor.Green,
            Type = WeaponType.LightningStrike,
            MaxLevel = 8,
            BaseDamage = 48,
            BaseCooldown = 1.2,
            BaseProjectileCount = 2,
            BaseSpeed = 999,
            BaseAreaRadius = 55,
            BasePierce = 99,
            Description = "무작위 적의 머리 위에 번개를 내리꽂습니다.",
            IconCardCode = "ST4-03"
        });

        AddSkill(new SkillDefinition
        {
            Id = "surfing_fish",
            Name = "물고기 행진",
            CardId = "ST2-02",
            Color = CardColor.Blue,
            Type = WeaponType.ProjectileDirect,
            MaxLevel = 8,
            BaseDamage = 36,
            BaseCooldown = 0.85,
            BaseProjectileCount = 2,
            BaseSpeed = 380,
            BaseAreaRadius = 28,
            BasePierce = 2,
            Description = "유영하는 물고기 떼를 날려 적들을 밀쳐냅니다.",
            IconCardCode = "ST2-02"
        });

        AddSkill(new SkillDefinition
        {
            Id = "goburi_strike",
            Name = "고블 스트라이크",
            CardId = "ST5-04",
            Color = CardColor.Black,
            Type = WeaponType.PiercingSlice,
            MaxLevel = 8,
            BaseDamage = 58,
            BaseCooldown = 1.1,
            BaseProjectileCount = 1,
            BaseSpeed = 400,
            BaseAreaRadius = 38,
            BasePierce = 3,
            Description = "전방을 향해 곤봉 충격파를 날려 적을 꿰뚫습니다.",
            IconCardCode = "ST5-04"
        });

        AddSkill(new SkillDefinition
        {
            Id = "mega_flame",
            Name = "메가 플레임",
            CardId = "ST1-07",
            Color = CardColor.Red,
            Type = WeaponType.ProjectileDirect,
            MaxLevel = 8,
            BaseDamage = 85,
            BaseCooldown = 1.1,
            BaseProjectileCount = 2,
            BaseSpeed = 430,
            BaseAreaRadius = 45,
            BasePierce = 2,
            Description = "적들을 꿰뚫고 폭발하는 거대 화염 탄환을 부채꼴로 일제 발사합니다.",
            IconCardCode = "ST1-07"
        });

        AddSkill(new SkillDefinition
        {
            Id = "fox_fire",
            Name = "폭스 파이어",
            CardId = "ST2-06",
            Color = CardColor.Blue,
            Type = WeaponType.ProjectileDirect,
            MaxLevel = 8,
            BaseDamage = 50,
            BaseCooldown = 0.75,
            BaseProjectileCount = 3,
            BaseSpeed = 480,
            BaseAreaRadius = 30,
            BasePierce = 1,
            Description = "빠른 속도로 청백색 냉기 불꽃을 3연사합니다.",
            IconCardCode = "ST2-06"
        });

        AddSkill(new SkillDefinition
        {
            Id = "meteor_wing",
            Name = "메테오 윙",
            CardId = "ST1-05",
            Color = CardColor.Red,
            Type = WeaponType.PiercingSlice,
            MaxLevel = 8,
            BaseDamage = 75,
            BaseCooldown = 1.2,
            BaseProjectileCount = 2,
            BaseSpeed = 480,
            BaseAreaRadius = 38,
            BasePierce = 3,
            Description = "거대한 유성 불꽃을 좌우 교차로 투척합니다.",
            IconCardCode = "ST1-05"
        });

        AddSkill(new SkillDefinition
        {
            Id = "death_claw",
            Name = "데스 클로",
            CardId = "ST6-05",
            Color = CardColor.Purple,
            Type = WeaponType.PiercingSlice,
            MaxLevel = 8,
            BaseDamage = 80,
            BaseCooldown = 1.0,
            BaseProjectileCount = 2,
            BaseSpeed = 520,
            BaseAreaRadius = 35,
            BasePierce = 4,
            Description = "길게 뻗어나가는 검은 악마의 손톱으로 적을 할퀴고 관통합니다.",
            IconCardCode = "ST6-05"
        });

        AddSkill(new SkillDefinition
        {
            Id = "heavens_knuckle",
            Name = "헤븐즈 너클",
            CardId = "ST3-05",
            Color = CardColor.Yellow,
            Type = WeaponType.ProjectileDirect,
            MaxLevel = 8,
            BaseDamage = 135,
            BaseCooldown = 1.1,
            BaseProjectileCount = 1,
            BaseSpeed = 460,
            BaseAreaRadius = 55,
            BasePierce = 4,
            Description = "황금의 빛으로 뭉친 거대 주먹 충격파를 발사합니다.",
            IconCardCode = "ST3-05"
        });

        AddSkill(new SkillDefinition
        {
            Id = "mega_blaster",
            Name = "메가 블래스터",
            CardId = "ST4-08",
            Color = CardColor.Green,
            Type = WeaponType.LightningStrike,
            MaxLevel = 8,
            BaseDamage = 95,
            BaseCooldown = 1.3,
            BaseProjectileCount = 3,
            BaseSpeed = 999,
            BaseAreaRadius = 65,
            BasePierce = 99,
            Description = "고압의 전자 벼락을 주변 적 3체에 연속으로 내리꽂습니다.",
            IconCardCode = "ST4-08"
        });

        AddSkill(new SkillDefinition
        {
            Id = "giga_destroyer",
            Name = "기가 디스트로이어",
            CardId = "ST1-09",
            Color = CardColor.Red,
            Type = WeaponType.HomingMissile,
            MaxLevel = 8,
            BaseDamage = 135,
            BaseCooldown = 1.5,
            BaseProjectileCount = 2,
            BaseSpeed = 380,
            BaseAreaRadius = 50,
            BasePierce = 1,
            Description = "가장 위협적인 적을 유도 추적해 대폭발을 일으킵니다.",
            IconCardCode = "ST1-09"
        });

        AddSkill(new SkillDefinition
        {
            Id = "kaiser_nail",
            Name = "카이저 네일",
            CardId = "ST2-08",
            Color = CardColor.Blue,
            Type = WeaponType.RapidMeleeClaw,
            MaxLevel = 8,
            BaseDamage = 110,
            BaseCooldown = 0.95,
            BaseProjectileCount = 1,
            BaseSpeed = 0,
            BaseAreaRadius = 130,
            BasePierce = 999,
            Description = "플레이어 전방위로 칼바람 회전 참격을 연속 난무합니다.",
            IconCardCode = "ST2-08"
        });

        AddSkill(new SkillDefinition
        {
            Id = "gaia_force",
            Name = "테라 광선 (가이아 포스)",
            CardId = "ST1-11",
            Color = CardColor.Red,
            Type = WeaponType.ScreenBomb,
            MaxLevel = 8,
            BaseDamage = 320,
            BaseCooldown = 3.5,
            BaseProjectileCount = 1,
            BaseSpeed = 0,
            BaseAreaRadius = 260,
            BasePierce = 999,
            Description = "대기의 에너지를 압축하여 화면 전체를 휩쓰는 초거대 태양구를 폭발시킵니다.",
            IconCardCode = "ST1-11"
        });

        AddSkill(new SkillDefinition
        {
            Id = "cocytus_breath",
            Name = "코퀴토스 브레스",
            CardId = "ST2-11",
            Color = CardColor.Blue,
            Type = WeaponType.FreezingLaser,
            MaxLevel = 8,
            BaseDamage = 260,
            BaseCooldown = 2.4,
            BaseProjectileCount = 2,
            BaseSpeed = 600,
            BaseAreaRadius = 60,
            BasePierce = 999,
            Description = "전방을 절대영도로 얼려버리는 초대형 냉기 광선을 방사합니다.",
            IconCardCode = "ST2-11"
        });

        AddSkill(new SkillDefinition
        {
            Id = "grey_sword",
            Name = "그레이 소드",
            CardId = "BT1-084",
            Color = CardColor.White,
            Type = WeaponType.ScreenBomb,
            MaxLevel = 8,
            BaseDamage = 500,
            BaseCooldown = 3.0,
            BaseProjectileCount = 1,
            BaseSpeed = 0,
            BaseAreaRadius = 350,
            BasePierce = 999,
            Description = "오메가몬의 성검이 시공간을 베어 화면 내 모든 적을 일도양단합니다.",
            IconCardCode = "BT1-084"
        });

        // ------------------ 서브 액티브 스킬 (드래프트 가능) ------------------
        AddSkill(new SkillDefinition
        {
            Id = "spit_fire",
            Name = "스피트 파이어",
            CardId = "ST1-04",
            Color = CardColor.Red,
            Type = WeaponType.ProjectileDirect,
            MaxLevel = 8,
            BaseDamage = 42,
            BaseCooldown = 0.8,
            BaseProjectileCount = 2,
            BaseSpeed = 440,
            BaseAreaRadius = 25,
            BasePierce = 1,
            Description = "연속으로 날카로운 화염 단발탄을 뿜어냅니다.",
            IconCardCode = "ST1-04"
        });

        AddSkill(new SkillDefinition
        {
            Id = "hammer_spark",
            Name = "해머 스파크",
            CardId = "ST2-13",
            Color = CardColor.Blue,
            Type = WeaponType.LightningStrike,
            MaxLevel = 8,
            BaseDamage = 120,
            BaseCooldown = 1.6,
            BaseProjectileCount = 2,
            BaseSpeed = 999,
            BaseAreaRadius = 75,
            BasePierce = 99,
            Description = "스파크 벼락을 내리쳐 타격 주변 적들에게 연쇄 감전 피해를 줍니다.",
            IconCardCode = "ST2-13"
        });

        AddSkill(new SkillDefinition
        {
            Id = "sorrow_blue",
            Name = "소로우 블루",
            CardId = "ST2-10",
            Color = CardColor.Blue,
            Type = WeaponType.IceChillingField,
            MaxLevel = 8,
            BaseDamage = 35,
            BaseCooldown = 2.0,
            BaseProjectileCount = 1,
            BaseSpeed = 0,
            BaseAreaRadius = 140,
            BasePierce = 999,
            Description = "바닥에 혹한의 눈보라 장판을 생성해 지속 피해와 슬로우를 부여합니다.",
            IconCardCode = "ST2-10"
        });

        AddSkill(new SkillDefinition
        {
            Id = "trident_arm",
            Name = "트라이던트 암",
            CardId = "ST1-09",
            Color = CardColor.Red,
            Type = WeaponType.PiercingSlice,
            MaxLevel = 8,
            BaseDamage = 130,
            BaseCooldown = 1.4,
            BaseProjectileCount = 1,
            BaseSpeed = 550,
            BaseAreaRadius = 45,
            BasePierce = 6,
            Description = "와이어로 연결된 강철의 삼지창 팔을 쏘아보내 적들을 꿰뚫습니다.",
            IconCardCode = "ST1-09"
        });

        AddSkill(new SkillDefinition
        {
            Id = "holy_rod",
            Name = "홀리 로드",
            CardId = "ST3-05",
            Color = CardColor.Yellow,
            Type = WeaponType.RapidMeleeClaw,
            MaxLevel = 8,
            BaseDamage = 85,
            BaseCooldown = 0.95,
            BaseProjectileCount = 1,
            BaseSpeed = 0,
            BaseAreaRadius = 110,
            BasePierce = 999,
            Description = "성스러운 지팡이를 전방위로 회전시켜 주변 적들을 일제히 밀쳐냅니다.",
            IconCardCode = "ST3-05"
        });

        AddSkill(new SkillDefinition
        {
            Id = "dramon_killer",
            Name = "드라몬 킬러",
            CardId = "ST1-11",
            Color = CardColor.Red,
            Type = WeaponType.RapidMeleeClaw,
            MaxLevel = 8,
            BaseDamage = 180,
            BaseCooldown = 1.2,
            BaseProjectileCount = 1,
            BaseSpeed = 0,
            BaseAreaRadius = 150,
            BasePierce = 999,
            Description = "용을 멸하는 양손의 클로를 회전시켜 강력한 참격을 방출합니다.",
            IconCardCode = "ST1-11"
        });

        AddSkill(new SkillDefinition
        {
            Id = "dark_matter",
            Name = "다크 매터",
            CardId = "ST5-06",
            Color = CardColor.Black,
            Type = WeaponType.IceChillingField,
            MaxLevel = 8,
            BaseDamage = 45,
            BaseCooldown = 2.2,
            BaseProjectileCount = 1,
            BaseSpeed = 0,
            BaseAreaRadius = 130,
            BasePierce = 999,
            Description = "바닥에 암흑 중력장을 생성하여 적들을 끌어당기고 지속 피해를 줍니다.",
            IconCardCode = "ST5-06"
        });

        // ------------------ 추가 진화체 시그니처 스킬 ------------------
        AddSkill(new SkillDefinition
        {
            Id = "shadow_wing_ex",
            Name = "섀도우 윙 EX",
            CardId = "ST1-08",
            Color = CardColor.Red,
            Type = WeaponType.PiercingSlice,
            MaxLevel = 8,
            BaseDamage = 130,
            BaseCooldown = 1.2,
            BaseProjectileCount = 2,
            BaseSpeed = 500,
            BaseAreaRadius = 50,
            BasePierce = 999,
            Description = "거대한 화염 불새 환영을 날려 전방의 모든 적을 일소합니다.",
            IconCardCode = "ST1-08"
        });

        AddSkill(new SkillDefinition
        {
            Id = "starlight_explosion",
            Name = "스타라이트 익스플로전",
            CardId = "ST1-10",
            Color = CardColor.Red,
            Type = WeaponType.ScreenBomb,
            MaxLevel = 8,
            BaseDamage = 350,
            BaseCooldown = 3.2,
            BaseProjectileCount = 1,
            BaseSpeed = 0,
            BaseAreaRadius = 320,
            BasePierce = 999,
            Description = "성스러운 황금 별빛 폭풍을 화면 전체에 방출해 악의 기운을 정화합니다.",
            IconCardCode = "ST1-10"
        });

        AddSkill(new SkillDefinition
        {
            Id = "night_raid",
            Name = "나이트 레이드",
            CardId = "BT2-074",
            Color = CardColor.Purple,
            Type = WeaponType.HomingMissile,
            MaxLevel = 8,
            BaseDamage = 110,
            BaseCooldown = 1.3,
            BaseProjectileCount = 4,
            BaseSpeed = 420,
            BaseAreaRadius = 40,
            BasePierce = 2,
            Description = "무수한 붉은 눈의 박쥐 떼를 소환해 적들을 추적 난타합니다.",
            IconCardCode = "BT2-074"
        });

        AddSkill(new SkillDefinition
        {
            Id = "trump_sword",
            Name = "트럼프 소드",
            CardId = "BT2-080",
            Color = CardColor.Purple,
            Type = WeaponType.PiercingSlice,
            MaxLevel = 8,
            BaseDamage = 175,
            BaseCooldown = 1.0,
            BaseProjectileCount = 4,
            BaseSpeed = 620,
            BaseAreaRadius = 42,
            BasePierce = 8,
            Description = "시공을 초월해 순간이동하는 4자루의 마검을 날려 적들을 꿰뚫습니다.",
            IconCardCode = "BT2-080"
        });

        AddSkill(new SkillDefinition
        {
            Id = "heaven_gate",
            Name = "헤븐즈 게이트",
            CardId = "ST3-08",
            Color = CardColor.Yellow,
            Type = WeaponType.IceChillingField,
            MaxLevel = 8,
            BaseDamage = 75,
            BaseCooldown = 2.4,
            BaseProjectileCount = 1,
            BaseSpeed = 0,
            BaseAreaRadius = 180,
            BasePierce = 999,
            Description = "천벌의 아공간 문을 열어 적들을 빨아들이고 영구 흡혈 지속 피해를 줍니다.",
            IconCardCode = "ST3-08"
        });

        AddSkill(new SkillDefinition
        {
            Id = "strike_of_the_seven_stars",
            Name = "세븐즈 헤븐",
            CardId = "ST3-11",
            Color = CardColor.Yellow,
            Type = WeaponType.ScreenBomb,
            MaxLevel = 8,
            BaseDamage = 400,
            BaseCooldown = 3.0,
            BaseProjectileCount = 1,
            BaseSpeed = 0,
            BaseAreaRadius = 340,
            BasePierce = 999,
            Description = "7개의 초고열 성광구를 투하하여 화면 전체를 성스러운 빛으로 불태웁니다.",
            IconCardCode = "ST3-11"
        });

        AddSkill(new SkillDefinition
        {
            Id = "horn_buster",
            Name = "혼 버스터",
            CardId = "ST4-08",
            Color = CardColor.Green,
            Type = WeaponType.LightningStrike,
            MaxLevel = 8,
            BaseDamage = 150,
            BaseCooldown = 1.4,
            BaseProjectileCount = 3,
            BaseSpeed = 999,
            BaseAreaRadius = 80,
            BasePierce = 99,
            Description = "거대한 뿔에서 초고압 입자 전격을 방출해 넓은 범위의 적들을 섬멸합니다.",
            IconCardCode = "ST4-08"
        });

        AddSkill(new SkillDefinition
        {
            Id = "giga_blaster",
            Name = "기가 블래스터",
            CardId = "ST4-11",
            Color = CardColor.Green,
            Type = WeaponType.ScreenBomb,
            MaxLevel = 8,
            BaseDamage = 360,
            BaseCooldown = 3.2,
            BaseProjectileCount = 1,
            BaseSpeed = 0,
            BaseAreaRadius = 300,
            BasePierce = 999,
            Description = "황금빛 초대형 전광 플라즈마 파동포를 작렬시켜 전방위 적을 감전시킵니다.",
            IconCardCode = "ST4-11"
        });

        AddSkill(new SkillDefinition
        {
            Id = "ground_zero",
            Name = "그라운드 제로",
            CardId = "ST1-09",
            Color = CardColor.Black,
            Type = WeaponType.HomingMissile,
            MaxLevel = 8,
            BaseDamage = 200,
            BaseCooldown = 1.8,
            BaseProjectileCount = 1,
            BaseSpeed = 340,
            BaseAreaRadius = 90,
            BasePierce = 1,
            Description = "등에서 발사된 유기 미사일이 적을 추적해 핵폭발 수준의 충격파를 일으킵니다.",
            IconCardCode = "ST1-09"
        });

        AddSkill(new SkillDefinition
        {
            Id = "holy_arrow",
            Name = "홀리 애로우",
            CardId = "ST3-09",
            Color = CardColor.Yellow,
            Type = WeaponType.PiercingSlice,
            MaxLevel = 8,
            BaseDamage = 160,
            BaseCooldown = 1.1,
            BaseProjectileCount = 2,
            BaseSpeed = 650,
            BaseAreaRadius = 45,
            BasePierce = 6,
            Description = "빛의 활시위를 당겨 적들을 꿰뚫는 성스러운 번개 화살을 연사합니다.",
            IconCardCode = "ST3-09"
        });

        AddSkill(new SkillDefinition
        {
            Id = "darkness_wave",
            Name = "다크니스 웨이브",
            CardId = "ST6-08",
            Color = CardColor.Purple,
            Type = WeaponType.PiercingSlice,
            MaxLevel = 8,
            BaseDamage = 150,
            BaseCooldown = 1.3,
            BaseProjectileCount = 3,
            BaseSpeed = 500,
            BaseAreaRadius = 55,
            BasePierce = 5,
            Description = "칠흑의 암흑 파동을 부채꼴로 방출해 적들을 잠식합니다.",
            IconCardCode = "ST6-08"
        });

        AddSkill(new SkillDefinition
        {
            Id = "flower_cannon",
            Name = "플라워 캐논",
            CardId = "ST4-09",
            Color = CardColor.Green,
            Type = WeaponType.ProjectileDirect,
            MaxLevel = 8,
            BaseDamage = 135,
            BaseCooldown = 0.9,
            BaseProjectileCount = 3,
            BaseSpeed = 480,
            BaseAreaRadius = 40,
            BasePierce = 2,
            Description = "꽃잎 포신에서 응축된 자연의 에너지 탄환을 일제 발사합니다.",
            IconCardCode = "ST4-09"
        });

        AddSkill(new SkillDefinition
        {
            Id = "infinity_cannon",
            Name = "무겐 캐논",
            CardId = "ST5-11",
            Color = CardColor.Black,
            Type = WeaponType.FreezingLaser,
            MaxLevel = 8,
            BaseDamage = 280,
            BaseCooldown = 2.5,
            BaseProjectileCount = 2,
            BaseSpeed = 700,
            BaseAreaRadius = 70,
            BasePierce = 999,
            Description = "등 뒤의 거대한 2연장 캐논에서 절대 파괴 에너지 빔을 일제 사격합니다.",
            IconCardCode = "ST5-11"
        });

        AddSkill(new SkillDefinition
        {
            Id = "haouken",
            Name = "패왕권",
            CardId = "ST5-05",
            Color = CardColor.Black,
            Type = WeaponType.RapidMeleeClaw,
            MaxLevel = 8,
            BaseDamage = 125,
            BaseCooldown = 1.0,
            BaseProjectileCount = 1,
            BaseSpeed = 0,
            BaseAreaRadius = 120,
            BasePierce = 999,
            Description = "온몸의 완력을 실어 전방위로 분노의 패왕 주먹 충격을 터뜨립니다.",
            IconCardCode = "ST5-05"
        });

        AddSkill(new SkillDefinition
        {
            Id = "ice_arrow",
            Name = "아이스 애로우",
            CardId = "BT1-034",
            Color = CardColor.Blue,
            Type = WeaponType.PiercingSlice,
            MaxLevel = 8,
            BaseDamage = 85,
            BaseCooldown = 1.0,
            BaseProjectileCount = 2,
            BaseSpeed = 520,
            BaseAreaRadius = 35,
            BasePierce = 3,
            Description = "입에서 절대영도의 얼음 화살을 쏘아 적들을 꿰뚫고 얼려버립니다.",
            IconCardCode = "BT1-034"
        });

        AddSkill(new SkillDefinition
        {
            Id = "hyper_cannon",
            Name = "하이퍼 캐논",
            CardId = "ST5-07",
            Color = CardColor.Black,
            Type = WeaponType.ProjectileDirect,
            MaxLevel = 8,
            BaseDamage = 115,
            BaseCooldown = 1.2,
            BaseProjectileCount = 2,
            BaseSpeed = 460,
            BaseAreaRadius = 45,
            BasePierce = 2,
            Description = "전차 포신에서 고폭 철갑탄을 발사하여 광범위 폭발을 일으킵니다.",
            IconCardCode = "ST5-07"
        });

        AddSkill(new SkillDefinition
        {
            Id = "harpoon_torpedo",
            Name = "해푼 토피도",
            CardId = "ST2-05",
            Color = CardColor.Blue,
            Type = WeaponType.HomingMissile,
            MaxLevel = 8,
            BaseDamage = 100,
            BaseCooldown = 1.4,
            BaseProjectileCount = 2,
            BaseSpeed = 400,
            BaseAreaRadius = 45,
            BasePierce = 1,
            Description = "머리의 뿔에서 어뢰 미사일을 사출하여 적을 추적 폭파합니다.",
            IconCardCode = "ST2-05"
        });

        AddSkill(new SkillDefinition
        {
            Id = "forbidden_temptation",
            Name = "포비든 템테이션",
            CardId = "ST4-10",
            Color = CardColor.Green,
            Type = WeaponType.ScreenBomb,
            MaxLevel = 8,
            BaseDamage = 340,
            BaseCooldown = 3.3,
            BaseProjectileCount = 1,
            BaseSpeed = 0,
            BaseAreaRadius = 290,
            BasePierce = 999,
            Description = "금단의 흑장미 향기와 무수한 장미 꽃잎 폭풍으로 화면 전체를 뒤덮습니다.",
            IconCardCode = "ST4-10"
        });
    }

    private static void InitializePassives()
    {
        AddPassive(new PassiveDefinition
        {
            Id = "taichi",
            Name = "신태일 (용기의 테이머)",
            TamerOrOption = "Tamer",
            CardId = "ST1-12",
            Color = CardColor.Red,
            MaxLevel = 5,
            DamageBonusPerLevel = 0.15,
            ProjectileBonusAtMax = 1,
            Description = "공격력(DP)이 레벨당 +15% 상승하며, 최고 레벨 시 모든 투사체 발사 수가 +1 증가합니다."
        });

        AddPassive(new PassiveDefinition
        {
            Id = "yamato",
            Name = "매튜 (우정의 테이머)",
            TamerOrOption = "Tamer",
            CardId = "ST2-12",
            Color = CardColor.Blue,
            MaxLevel = 5,
            SpeedBonusPerLevel = 0.12,
            CooldownReductionPerLevel = 0.08,
            Description = "이동 속도 +12%, 스킬 쿨타임 -8% 단축."
        });

        AddPassive(new PassiveDefinition
        {
            Id = "digivice",
            Name = "디지바이스 (진화의 빛)",
            TamerOrOption = "Option",
            CardId = "ST1-15",
            Color = CardColor.Red,
            MaxLevel = 5,
            ExpBonusPerLevel = 0.20,
            Description = "경험치 비트 획득량이 레벨당 +20% 증가합니다."
        });

        AddPassive(new PassiveDefinition
        {
            Id = "brave_shield",
            Name = "브레이브 실드",
            TamerOrOption = "Option",
            CardId = "ST1-14",
            Color = CardColor.Red,
            MaxLevel = 5,
            MaxHpBonusPerLevel = 0.20,
            HpRegenPerLevel = 4.0,
            Description = "최대 체력 +20% 증가 및 초당 HP 자연 회복 +4."
        });

        AddPassive(new PassiveDefinition
        {
            Id = "data_magnet",
            Name = "데이터 서치 레이더",
            TamerOrOption = "Option",
            CardId = "ST2-15",
            Color = CardColor.Blue,
            MaxLevel = 5,
            MagnetBonusPerLevel = 0.35,
            Description = "경험치 보석 흡수 반경이 레벨당 +35% 대폭 확장됩니다."
        });

        AddPassive(new PassiveDefinition
        {
            Id = "horn_buster_passive",
            Name = "헤라클레스 갑각",
            TamerOrOption = "Option",
            CardId = "ST4-12",
            Color = CardColor.Green,
            MaxLevel = 5,
            MaxHpBonusPerLevel = 0.15,
            DamageBonusPerLevel = 0.10,
            Description = "단단한 곤충 장갑으로 최대 체력 +15%, 공격력 +10% 상승."
        });

        AddPassive(new PassiveDefinition
        {
            Id = "holy_ring",
            Name = "홀리 링 (신성의 축복)",
            TamerOrOption = "Option",
            CardId = "ST3-12",
            Color = CardColor.Yellow,
            MaxLevel = 5,
            HpRegenPerLevel = 6.0,
            CooldownReductionPerLevel = 0.06,
            Description = "초당 HP 재생 +6 및 스킬 쿨타임 -6% 단축."
        });
    }

    private static void FinalizeSkillStages()
    {
        // 1. 디지몬의 시그니처 및 서브스킬로부터 단계(Stage) 자동 매핑
        foreach (var digi in Digimons.Values)
        {
            if (!string.IsNullOrEmpty(digi.SignatureWeaponId) && Skills.TryGetValue(digi.SignatureWeaponId, out var sigSkill))
            {
                sigSkill.Stage = digi.Stage;
            }

            foreach (var subId in digi.SubSkillIds)
            {
                if (Skills.TryGetValue(subId, out var subSkill))
                {
                    subSkill.Stage = digi.Stage;
                }
            }
        }

        // 2. 명시적 특수 스킬 스테이지 매핑
        if (Skills.TryGetValue("spit_fire", out var s1)) s1.Stage = DigimonStage.Rookie;
        if (Skills.TryGetValue("hammer_spark", out var s2)) s2.Stage = DigimonStage.Champion;
        if (Skills.TryGetValue("holy_rod", out var s3)) s3.Stage = DigimonStage.Champion;
        if (Skills.TryGetValue("dark_matter", out var s4)) s4.Stage = DigimonStage.Champion;
        if (Skills.TryGetValue("ice_arrow", out var s5)) s5.Stage = DigimonStage.Champion;
        if (Skills.TryGetValue("haouken", out var s6)) s6.Stage = DigimonStage.Champion;
        if (Skills.TryGetValue("harpoon_torpedo", out var s7)) s7.Stage = DigimonStage.Champion;
        if (Skills.TryGetValue("sorrow_blue", out var s8)) s8.Stage = DigimonStage.Ultimate;
        if (Skills.TryGetValue("trident_arm", out var s9)) s9.Stage = DigimonStage.Ultimate;
        if (Skills.TryGetValue("hyper_cannon", out var s10)) s10.Stage = DigimonStage.Ultimate;
        if (Skills.TryGetValue("night_raid", out var s11)) s11.Stage = DigimonStage.Ultimate;
        if (Skills.TryGetValue("dramon_killer", out var s12)) s12.Stage = DigimonStage.Mega;
        if (Skills.TryGetValue("trump_sword", out var s13)) s13.Stage = DigimonStage.Mega;
        if (Skills.TryGetValue("grey_sword", out var s14)) s14.Stage = DigimonStage.Ultra;
    }

    private static void AddDigimon(DigimonProfile d) => Digimons[d.Id] = d;
    private static void AddSkill(SkillDefinition s) => Skills[s.Id] = s;
    private static void AddPassive(PassiveDefinition p) => Passives[p.Id] = p;
}
