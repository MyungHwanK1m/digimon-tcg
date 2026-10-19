using System;
using System.Collections.Generic;
using System.Windows;
using DigimonSurvivors.Audio;
using DigimonSurvivors.Data;
using DigimonSurvivors.Models;
using DigimonSurvivors.Skills;

namespace DigimonSurvivors.Entities;

public class PlayerEntity
{
    public DigimonProfile Profile { get; private set; }
    public Point Position;
    public double FacingDirection = 1.0; // 1 = right, -1 = left
    public double Radius = 24.0;

    public int Level = 1;
    public int CurrentExp = 0;
    public int NextExp = 30;

    public double CurrentHp;
    public double MaxHp;
    public double Dp;
    public double Speed;
    public double MagnetRange = 130.0;
    public double DamageMultiplier = 1.0;
    public double CooldownMultiplier = 1.0;
    public double ExpMultiplier = 1.0;
    public double HpRegen = 0.0;
    public int BonusProjectiles = 0;

    public double InvincibleTimer = 0.0;
    public double WalkCycleTimer = 0.0;
    public bool IsMoving = false;
    public int KillCount = 0;
    public double SurvivalTime = 0.0;

    // 대시 (회피기)
    public double DashCooldownMax => Math.Max(0.8, 2.0 * CooldownMultiplier);
    public double DashCooldownTimer = 0.0;
    public double DashDurationTimer = 0.0;
    public bool IsDashing => DashDurationTimer > 0;
    public Vector DashDirection = new(1, 0);

    // 상세 전투 통계 (스킬별 가한 데미지 추적)
    public readonly Dictionary<string, double> SkillDamageStats = new();
    public double TotalDamageDealt = 0.0;

    // 고유 기술 (Signature Innate Skill - 진화 시 교체됨)
    public WeaponInstance SignatureWeapon { get; private set; }

    // 서브 액티브 스킬 (최대 5개)
    public const int MaxActiveSkills = 5;
    public readonly List<WeaponInstance> ActiveWeapons = new();

    // 패시브 스킬 (최대 5개)
    public const int MaxPassives = 5;
    public readonly Dictionary<string, int> Passives = new(); // passiveId -> level

    // 전체 무기 리스트 (고유기 + 서브 액티브)
    public List<WeaponInstance> Weapons
    {
        get
        {
            var list = new List<WeaponInstance>(ActiveWeapons.Count + 1);
            if (SignatureWeapon != null) list.Add(SignatureWeapon);
            list.AddRange(ActiveWeapons);
            return list;
        }
    }

    public PlayerEntity(DigimonProfile startingProfile, Point startPos)
    {
        Profile = startingProfile;
        Position = startPos;
        RecalculateStats(fullHeal: true);

        // 기본 고유 시그니처 무기 지급
        if (!string.IsNullOrEmpty(Profile.SignatureWeaponId) && GameDatabase.Skills.TryGetValue(Profile.SignatureWeaponId, out var sigSkill))
        {
            SignatureWeapon = new WeaponInstance(sigSkill, 1);
        }
        else
        {
            SignatureWeapon = new WeaponInstance(GameDatabase.Skills["baby_flame"], 1);
        }
    }

    public void RecalculateStats(bool fullHeal = false)
    {
        double baseMaxHp = Profile.BaseMaxHp;
        double baseSpeed = Profile.BaseSpeed;
        double baseDp = Profile.BaseDp;

        double damageBonus = 0.0;
        double speedBonus = 0.0;
        double cdrBonus = 0.0;
        double hpBonus = 0.0;
        double regenBonus = 0.0;
        double magnetBonus = 0.0;
        double expBonus = 0.0;
        int projectileBonus = 0;

        foreach (var (passId, passLevel) in Passives)
        {
            if (GameDatabase.Passives.TryGetValue(passId, out var pDef))
            {
                damageBonus += pDef.DamageBonusPerLevel * passLevel;
                speedBonus += pDef.SpeedBonusPerLevel * passLevel;
                cdrBonus += pDef.CooldownReductionPerLevel * passLevel;
                hpBonus += pDef.MaxHpBonusPerLevel * passLevel;
                regenBonus += pDef.HpRegenPerLevel * passLevel;
                magnetBonus += pDef.MagnetBonusPerLevel * passLevel;
                if (passLevel >= pDef.MaxLevel)
                {
                    projectileBonus += pDef.ProjectileBonusAtMax;
                }
            }
        }

        MaxHp = baseMaxHp * (1.0 + hpBonus);
        Speed = baseSpeed * (1.0 + speedBonus);
        Dp = baseDp * (1.0 + damageBonus);
        DamageMultiplier = 1.0 + damageBonus;
        CooldownMultiplier = Math.Max(0.4, 1.0 - cdrBonus);
        ExpMultiplier = 1.0 + expBonus;
        HpRegen = regenBonus;
        MagnetRange = 130.0 * (1.0 + magnetBonus);
        BonusProjectiles = projectileBonus;

        if (fullHeal || CurrentHp <= 0)
        {
            CurrentHp = MaxHp;
        }
        else
        {
            CurrentHp = Math.Min(CurrentHp, MaxHp);
        }
    }

    public void Evolve(DigimonProfile nextProfile)
    {
        int preservedLevel = SignatureWeapon?.Level ?? 1;
        Profile = nextProfile;
        RecalculateStats(fullHeal: true);

        // 2-2. 진화 시 기존 고유 기술의 레벨과 성장 능력치를 그대로 계승
        if (!string.IsNullOrEmpty(Profile.SignatureWeaponId) && GameDatabase.Skills.TryGetValue(Profile.SignatureWeaponId, out var newSkill))
        {
            SignatureWeapon = new WeaponInstance(newSkill, Math.Clamp(preservedLevel, 1, newSkill.MaxLevel));
        }
    }

    public int AddExp(int amount)
    {
        int gained = Math.Max(1, (int)(amount * ExpMultiplier));
        CurrentExp += gained;
        int leveledUpCount = 0;

        // 3. 자석으로 많은 보석을 먹어도 초과 경험치가 버려지지 않고 연속 레벨업 처리!
        while (CurrentExp >= NextExp)
        {
            CurrentExp -= NextExp;
            Level++;
            NextExp = (int)(30 + Level * 20 + Math.Pow(Level, 1.6) * 12);
            leveledUpCount++;
        }
        return leveledUpCount;
    }

    public void Heal(double amount)
    {
        CurrentHp = Math.Min(MaxHp, CurrentHp + amount);
    }

    public void HealPercent(double ratio)
    {
        Heal(MaxHp * ratio);
    }

    public void RecordDamage(string skillName, double damage)
    {
        TotalDamageDealt += damage;
        if (!SkillDamageStats.ContainsKey(skillName))
        {
            SkillDamageStats[skillName] = 0;
        }
        SkillDamageStats[skillName] += damage;
    }

    public bool TryDash(Vector input)
    {
        if (DashCooldownTimer > 0) return false;

        if (input.LengthSquared > 0.01)
        {
            input.Normalize();
            DashDirection = input;
        }
        else
        {
            DashDirection = new Vector(FacingDirection, 0);
        }

        DashDurationTimer = 0.22;
        DashCooldownTimer = DashCooldownMax;
        InvincibleTimer = 0.35;
        SoundSystem.PlayDash();
        return true;
    }

    public void TakeDamage(double dmg)
    {
        if (InvincibleTimer > 0 || IsDashing) return;
        CurrentHp -= dmg;
        InvincibleTimer = 0.45;
        if (CurrentHp < 0) CurrentHp = 0;
    }

    public void Update(double dt, Vector moveInput, Rect arenaBounds)
    {
        SurvivalTime += dt;

        if (InvincibleTimer > 0)
        {
            InvincibleTimer -= dt;
        }

        if (DashCooldownTimer > 0)
        {
            DashCooldownTimer -= dt;
        }

        if (HpRegen > 0 && CurrentHp < MaxHp)
        {
            CurrentHp = Math.Min(MaxHp, CurrentHp + HpRegen * dt);
        }

        if (IsDashing)
        {
            DashDurationTimer -= dt;
            Position += DashDirection * (Speed * 3.6) * dt;
            IsMoving = true;
        }
        else if (moveInput.LengthSquared > 0)
        {
            IsMoving = true;
            WalkCycleTimer += dt * 8.0;
            moveInput.Normalize();
            Position += moveInput * Speed * dt;

            if (moveInput.X > 0.05) FacingDirection = 1.0;
            else if (moveInput.X < -0.05) FacingDirection = -1.0;
        }
        else
        {
            IsMoving = false;
        }

        Position.X = Math.Clamp(Position.X, arenaBounds.Left + Radius, arenaBounds.Right - Radius);
        Position.Y = Math.Clamp(Position.Y, arenaBounds.Top + Radius, arenaBounds.Bottom - Radius);
    }
}
