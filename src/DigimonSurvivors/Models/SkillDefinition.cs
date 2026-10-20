using DigimonTcg.Core.Models;

namespace DigimonSurvivors.Models;

public enum WeaponType
{
    ProjectileDirect,   // 베이비 플레임, 메가 플레임
    OrbitingBarrier,    // 쁘띠 파이어 (주변 회전 보호구체)
    PiercingSlice,      // 섀도우 윙 (관통 깃털/참격)
    HomingMissile,      // 기가 디스트로이어 (유도 미사일 폭격)
    LightningStrike,    // 해머 스파크 (랜덤 번개)
    IceChillingField,   // 소로우 블루 (지속 슬로우 빙결 장판)
    RapidMeleeClaw,     // 카이저 네일 (근접 360도 연속 발톱베기)
    ScreenBomb,         // 가이아 포스 (화면 전체 거대 구체 폭발)
    FreezingLaser       // 코큐토스 브레스 (전방 절대영도 빔)
}

public class SkillDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string JapaneseDubName { get; set; } = string.Empty;
    public string CardId { get; set; } = string.Empty;
    public CardColor Color { get; set; }
    public WeaponType Type { get; set; }
    public int MaxLevel { get; set; } = 8;
    public double BaseDamage { get; set; } = 25.0;
    public double BaseCooldown { get; set; } = 1.2;
    public int BaseProjectileCount { get; set; } = 1;
    public double BaseSpeed { get; set; } = 350.0;
    public double BaseAreaRadius { get; set; } = 25.0;
    public int BasePierce { get; set; } = 1;
    public double BaseDuration { get; set; } = 3.0;
    public string Description { get; set; } = string.Empty;
    public string IconCardCode { get; set; } = string.Empty;
    public DigimonStage Stage { get; set; } = DigimonStage.Rookie;
}

public class PassiveDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TamerOrOption { get; set; } = string.Empty;
    public string CardId { get; set; } = string.Empty;
    public CardColor Color { get; set; }
    public int MaxLevel { get; set; } = 5;
    public string Description { get; set; } = string.Empty;

    // 레벨당 증가량
    public double DamageBonusPerLevel { get; set; } = 0.0;       // 예: +15% 공격력 (신태일)
    public double SpeedBonusPerLevel { get; set; } = 0.0;        // 예: +10% 이동속도 (매튜)
    public double CooldownReductionPerLevel { get; set; } = 0.0; // 예: -8% 쿨타임
    public double MaxHpBonusPerLevel { get; set; } = 0.0;        // 예: +20% 최대체력 (소라)
    public double HpRegenPerLevel { get; set; } = 0.0;           // 예: +1.5 HP/초
    public double MagnetBonusPerLevel { get; set; } = 0.0;       // 예: +30% 자석반경 (정석)
    public double ExpBonusPerLevel { get; set; } = 0.0;          // 예: +20% 경험치 (한솔)
    public int ProjectileBonusAtMax { get; set; } = 0;           // 예: 만렙 시 탄환 +1 (리키)
}
