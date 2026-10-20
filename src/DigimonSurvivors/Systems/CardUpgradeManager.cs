using System;
using System.Collections.Generic;
using System.Linq;
using DigimonSurvivors.Data;
using DigimonSurvivors.Entities;
using DigimonSurvivors.Models;
using DigimonSurvivors.Skills;
using DigimonTcg.Core.Models;

namespace DigimonSurvivors.Systems;

public class UpgradeOption
{
    public UpgradeType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CardId { get; set; } = string.Empty;
    public CardColor Color { get; set; }
    public string TargetId { get; set; } = string.Empty;
    public int CurrentLevel { get; set; }
    public int NextLevel { get; set; }
}

public class CardUpgradeManager
{
    private readonly Random _rnd = new();

    public List<UpgradeOption> GenerateOptions(PlayerEntity player)
    {
        var options = new List<UpgradeOption>();

        // 1. 고유기술 레벨 체크에 따른 진화(Evolution) 조건 판정
        // 2 & 2-1: 고유기술을 특정 랩까지 올리면 진화 가능 + 랜덤 3장의 진화 후보 카드 제시
        bool canEvolve = player.SignatureWeapon.Level >= player.Profile.RequiredSignatureLevel &&
                         player.Profile.EvolutionCandidateIds.Count > 0;

        if (canEvolve)
        {
            var evoOptions = GenerateEvolutionOptions(player);
            if (evoOptions.Count > 0)
            {
                // 진화 조건 달성 시에는 진화 후보 카드 3장 제시!
                return evoOptions;
            }
        }

        // 2. 고유 기술(Signature) 강화 옵션 (만렙이 아닐 경우)
        if (player.SignatureWeapon.Level < player.SignatureWeapon.Definition.MaxLevel)
        {
            var sig = player.SignatureWeapon;
            int req = player.Profile.RequiredSignatureLevel;
            string evoHint = sig.Level + 1 >= req ? " (다음 레벨 달성 시 진화 해금!)" : $" (Lv.{req} 달성 시 진화 가능)";

            options.Add(new UpgradeOption
            {
                Type = UpgradeType.SignatureUpgrade,
                Title = $"⭐ [고유기 강화] {sig.Definition.Name}",
                Subtitle = $"Lv.{sig.Level} ➔ Lv.{sig.Level + 1}",
                Description = $"데미지 +35% 증가 및 쿨타임 단축!{evoHint}",
                CardId = sig.Definition.CardId,
                Color = sig.Definition.Color,
                TargetId = sig.Definition.Id,
                CurrentLevel = sig.Level,
                NextLevel = sig.Level + 1
            });
        }

        // 3. 보유 중인 서브 액티브 무기 업그레이드 옵션
        foreach (var weapon in player.ActiveWeapons)
        {
            if (weapon.Level < weapon.Definition.MaxLevel)
            {
                options.Add(new UpgradeOption
                {
                    Type = UpgradeType.WeaponUpgrade,
                    Title = $"[서브 무기] {weapon.Definition.Name}",
                    Subtitle = $"Lv.{weapon.Level} ➔ Lv.{weapon.Level + 1}",
                    Description = $"데미지 +35% 증가 및 쿨타임 단축 ({weapon.Definition.Description})",
                    CardId = weapon.Definition.CardId,
                    Color = weapon.Definition.Color,
                    TargetId = weapon.Definition.Id,
                    CurrentLevel = weapon.Level,
                    NextLevel = weapon.Level + 1
                });
            }
        }

        // 4. 신규 서브 액티브 무기 획득 옵션 (2-3: 고유 제외 최대 5개 슬롯 제한)
        // 유저 요청: "서브 무기는 그 랩에 맞는 스킬로 등장하게 해줘"
        if (player.ActiveWeapons.Count < PlayerEntity.MaxActiveSkills)
        {
            var ownedIds = player.Weapons.Select(w => w.Definition.Id).ToHashSet();
            var currentStage = player.Profile.Stage;

            // 현재 디지몬의 진화 단계(Stage) 이하의 스킬만 등장 가능! (성장기일 때 궁극체 스킬 등장 원천 차단)
            // 1순위: 현재 디지몬의 서브 기술 (SubSkillIds)
            // 2순위: 현재 진화 단계와 동일한 스킬 (s.Stage == currentStage)
            // 3순위: 이전 진화 단계 스킬 (s.Stage < currentStage)
            var availableSkills = GameDatabase.Skills.Values
                .Where(s => !ownedIds.Contains(s.Id) &&
                            s.Id != player.SignatureWeapon.Definition.Id &&
                            s.Stage <= currentStage)
                .OrderByDescending(s => player.Profile.SubSkillIds.Contains(s.Id) ? 2 : (s.Stage == currentStage ? 1 : 0))
                .ThenBy(_ => _rnd.Next())
                .ToList();

            foreach (var skill in availableSkills.Take(3))
            {
                bool isSignatureSub = player.Profile.SubSkillIds.Contains(skill.Id);
                string stageTag = skill.Stage switch
                {
                    DigimonStage.Rookie => "[성장기]",
                    DigimonStage.Champion => "[성숙기]",
                    DigimonStage.Ultimate => "[완전체]",
                    DigimonStage.Mega => "[궁극체]",
                    DigimonStage.Ultra => "[초궁극체]",
                    _ => ""
                };

                options.Add(new UpgradeOption
                {
                    Type = UpgradeType.WeaponNew,
                    Title = isSignatureSub ? $"✨ {stageTag} [고유 서브] {skill.Name}" : $"{stageTag} [신규 무기] {skill.Name}",
                    Subtitle = "Lv.0 ➔ Lv.1 (신규 슬롯)",
                    Description = skill.Description,
                    CardId = skill.CardId,
                    Color = skill.Color,
                    TargetId = skill.Id,
                    CurrentLevel = 0,
                    NextLevel = 1
                });
            }
        }

        // 5. 패시브 업그레이드 및 신규 패시브 옵션 (2-3: 최대 5개 슬롯 제한)
        foreach (var (passId, passDef) in GameDatabase.Passives)
        {
            bool alreadyOwned = player.Passives.TryGetValue(passId, out int curLvl);
            if (alreadyOwned && curLvl < passDef.MaxLevel)
            {
                options.Add(new UpgradeOption
                {
                    Type = UpgradeType.PassiveUpgrade,
                    Title = $"[{passDef.TamerOrOption}] {passDef.Name}",
                    Subtitle = $"Lv.{curLvl} ➔ Lv.{curLvl + 1}",
                    Description = passDef.Description,
                    CardId = passDef.CardId,
                    Color = passDef.Color,
                    TargetId = passId,
                    CurrentLevel = curLvl,
                    NextLevel = curLvl + 1
                });
            }
            else if (!alreadyOwned && player.Passives.Count < PlayerEntity.MaxPassives)
            {
                options.Add(new UpgradeOption
                {
                    Type = UpgradeType.PassiveNew,
                    Title = $"[{passDef.TamerOrOption}] {passDef.Name}",
                    Subtitle = "Lv.0 ➔ Lv.1 (신규 장착)",
                    Description = passDef.Description,
                    CardId = passDef.CardId,
                    Color = passDef.Color,
                    TargetId = passId,
                    CurrentLevel = 0,
                    NextLevel = 1
                });
            }
        }

        // 모든 옵션이 마스터 상태일 때 폴백 회복 카드
        if (options.Count == 0)
        {
            options.Add(new UpgradeOption
            {
                Type = UpgradeType.SpecialHeal,
                Title = "[생명력 회복 & 데이터 강화]",
                Subtitle = "HP 100% 전액 회복",
                Description = "최대 체력의 100%를 즉시 회복하고 일시적으로 공격력이 상승합니다.",
                CardId = "ST1-01",
                Color = CardColor.Red,
                TargetId = "heal",
                CurrentLevel = 1,
                NextLevel = 1
            });
        }

        // 3장 랜덤 셔플 선택
        return options.OrderBy(_ => _rnd.Next()).Take(3).ToList();
    }

    private List<UpgradeOption> GenerateEvolutionOptions(PlayerEntity player)
    {
        var evoList = new List<UpgradeOption>();
        var candidates = player.Profile.EvolutionCandidateIds;

        var availableProfiles = candidates
            .Where(id => GameDatabase.Digimons.ContainsKey(id))
            .Select(id => GameDatabase.Digimons[id])
            .OrderBy(_ => _rnd.Next())
            .Take(3)
            .ToList();

        // 후보가 3개 미만이면 같은 단계의 다른 디지몬에서 보충
        if (availableProfiles.Count < 3)
        {
            var nextStage = (DigimonStage)((int)player.Profile.Stage + 1);
            var others = GameDatabase.Digimons.Values
                .Where(d => d.Stage == nextStage && !availableProfiles.Any(p => p.Id == d.Id))
                .OrderBy(_ => _rnd.Next())
                .Take(3 - availableProfiles.Count);
            availableProfiles.AddRange(others);
        }

        foreach (var evo in availableProfiles)
        {
            string stageName = evo.Stage switch
            {
                DigimonStage.Champion => "성숙기 / Lv.4",
                DigimonStage.Ultimate => "완전체 / Lv.5",
                DigimonStage.Mega => "궁극체 / Lv.6",
                DigimonStage.Ultra => "초궁극체 / Lv.7",
                _ => "진화"
            };

            string sigName = GameDatabase.Skills.TryGetValue(evo.SignatureWeaponId, out var s) ? s.Name : "필살기";

            evoList.Add(new UpgradeOption
            {
                Type = UpgradeType.Evolution,
                Title = $"🧬 [진화] {evo.Name} ({stageName})",
                Subtitle = $"{player.Profile.Name} ➔ {evo.Name}",
                Description = $"체력 전액 회복! 고유기가 '{sigName}'(Lv.{player.SignatureWeapon.Level})으로 레벨과 능력치가 온전히 계승되며 기본 스탯이 대폭 강화됩니다.",
                CardId = evo.CardId,
                Color = evo.Color,
                TargetId = evo.Id,
                CurrentLevel = (int)player.Profile.Stage,
                NextLevel = (int)evo.Stage
            });
        }

        return evoList;
    }

    public void ApplyUpgrade(PlayerEntity player, UpgradeOption option)
    {
        switch (option.Type)
        {
            case UpgradeType.Evolution:
                if (GameDatabase.Digimons.TryGetValue(option.TargetId, out var nextProfile))
                {
                    // 2-2: 진화 시 진화한 디지몬의 기술로 고유기 교체
                    player.Evolve(nextProfile);
                }
                break;

            case UpgradeType.SignatureUpgrade:
                player.SignatureWeapon.Upgrade();
                break;

            case UpgradeType.WeaponNew:
                if (GameDatabase.Skills.TryGetValue(option.TargetId, out var newSkill))
                {
                    if (player.ActiveWeapons.Count < PlayerEntity.MaxActiveSkills)
                    {
                        player.ActiveWeapons.Add(new WeaponInstance(newSkill, 1));
                    }
                }
                break;

            case UpgradeType.WeaponUpgrade:
                var existingWpn = player.ActiveWeapons.Find(w => w.Definition.Id == option.TargetId);
                existingWpn?.Upgrade();
                break;

            case UpgradeType.PassiveNew:
                if (player.Passives.Count < PlayerEntity.MaxPassives)
                {
                    player.Passives[option.TargetId] = 1;
                    player.RecalculateStats();
                }
                break;

            case UpgradeType.PassiveUpgrade:
                if (player.Passives.ContainsKey(option.TargetId))
                {
                    player.Passives[option.TargetId]++;
                    player.RecalculateStats();
                }
                break;

            case UpgradeType.SpecialHeal:
                player.HealPercent(1.0);
                break;
        }
    }
}
