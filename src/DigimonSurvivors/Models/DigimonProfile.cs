using System.Collections.Generic;
using DigimonTcg.Core.Models;

namespace DigimonSurvivors.Models;

public class DigimonProfile
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string EnglishName { get; set; } = string.Empty;
    public DigimonStage Stage { get; set; }
    public CardColor Color { get; set; }
    public DigimonAttribute Attribute { get; set; }
    public int BaseMaxHp { get; set; } = 100;
    public double BaseSpeed { get; set; } = 180.0;
    public int BaseDp { get; set; } = 1000;
    public string SpriteFileName { get; set; } = string.Empty;
    public string CardId { get; set; } = string.Empty;

    // 고유 기술 (Main Signature Skill)
    public string SignatureWeaponId { get; set; } = string.Empty;

    // 디지몬의 서브 기술 목록 (Active Sub-Skills)
    public List<string> SubSkillIds { get; set; } = new();

    public string Description { get; set; } = string.Empty;

    // 진화 조건 및 후보군 (랜덤 3개 카드 선택)
    public int RequiredSignatureLevel { get; set; } = 3;
    public List<string> EvolutionCandidateIds { get; set; } = new();
}
