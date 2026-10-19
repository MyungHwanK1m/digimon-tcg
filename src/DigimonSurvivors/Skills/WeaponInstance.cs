using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using DigimonSurvivors.Data;
using DigimonSurvivors.Entities;
using DigimonSurvivors.Models;

namespace DigimonSurvivors.Skills;

public class WeaponInstance
{
    public SkillDefinition Definition { get; }
    public int Level { get; private set; } = 1;
    public double CooldownTimer { get; set; } = 0.0;

    public WeaponInstance(SkillDefinition def, int initialLevel = 1)
    {
        Definition = def;
        Level = initialLevel;
    }

    public void Upgrade()
    {
        if (Level < Definition.MaxLevel)
        {
            Level++;
        }
    }

    public double GetCurrentDamage(double playerDamageMult) =>
        (Definition.BaseDamage + (Level - 1) * (Definition.BaseDamage * 0.35)) * playerDamageMult;

    public double GetCurrentCooldown(double playerCooldownMult) =>
        Math.Max(0.2, (Definition.BaseCooldown * Math.Pow(0.93, Level - 1)) * playerCooldownMult);

    public int GetProjectileCount(int bonusProjectiles) =>
        Definition.BaseProjectileCount + (Level >= 4 ? 1 : 0) + (Level >= 7 ? 1 : 0) + bonusProjectiles;

    public double GetAreaRadius() =>
        Definition.BaseAreaRadius + (Level - 1) * (Definition.BaseAreaRadius * 0.15);

    public int GetPierce() =>
        Definition.BasePierce + (Level >= 3 ? 1 : 0) + (Level >= 6 ? 1 : 0);

    public Color GetColor() => Definition.Color switch
    {
        DigimonTcg.Core.Models.CardColor.Red => Color.FromRgb(255, 80, 20),
        DigimonTcg.Core.Models.CardColor.Blue => Color.FromRgb(30, 160, 255),
        DigimonTcg.Core.Models.CardColor.Yellow => Color.FromRgb(255, 220, 40),
        DigimonTcg.Core.Models.CardColor.Green => Color.FromRgb(40, 220, 80),
        DigimonTcg.Core.Models.CardColor.Black => Color.FromRgb(140, 60, 200),
        DigimonTcg.Core.Models.CardColor.White => Color.FromRgb(255, 255, 255),
        _ => Colors.Orange
    };
}
