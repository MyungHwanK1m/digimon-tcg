namespace DigimonSurvivors.Models;

public enum DigimonStage
{
    Digitama = 2,
    Rookie = 3,      // 성장기 (Lv.3)
    Champion = 4,    // 성숙기 (Lv.4)
    Ultimate = 5,    // 완전체 (Lv.5)
    Mega = 6,        // 궁극체 (Lv.6)
    Ultra = 7        // 초궁극체 (Lv.7)
}

public enum DigimonAttribute
{
    Vaccine,
    Data,
    Virus,
    Free
}

public enum GameState
{
    Title,
    CharacterSelect,
    Playing,
    Paused,
    LevelUp,
    EvolutionCutscene,
    GameOver,
    Victory
}

public enum UpgradeType
{
    Evolution,
    SignatureUpgrade,
    WeaponNew,
    WeaponUpgrade,
    PassiveNew,
    PassiveUpgrade,
    SpecialHeal
}
