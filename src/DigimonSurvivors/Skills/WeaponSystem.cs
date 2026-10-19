using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using DigimonSurvivors.Audio;
using DigimonSurvivors.Entities;
using DigimonSurvivors.Models;

namespace DigimonSurvivors.Skills;

public class WeaponSystem
{
    private readonly Random _rnd = new();

    public void Update(
        double dt,
        PlayerEntity player,
        List<EnemyEntity> enemies,
        List<ProjectileEntity> projectiles,
        List<AreaEffectEntity> areaEffects,
        List<FloatingTextEntity> floatingTexts)
    {
        // 1. 회전 보호막(Petit Fire 등) 동기화
        SyncOrbitingWeapons(player, projectiles);

        // 2. 무기 쿨타임 및 발사 처리
        foreach (var weapon in player.Weapons)
        {
            if (weapon.Definition.Type == WeaponType.OrbitingBarrier)
            {
                continue; // 상시 유지
            }

            weapon.CooldownTimer += dt;
            double cd = weapon.GetCurrentCooldown(player.CooldownMultiplier);

            if (weapon.CooldownTimer >= cd)
            {
                weapon.CooldownTimer = 0.0;
                FireWeapon(weapon, player, enemies, projectiles, areaEffects, floatingTexts);
            }
        }
    }

    private void SyncOrbitingWeapons(PlayerEntity player, List<ProjectileEntity> projectiles)
    {
        var orbitWeapons = player.Weapons.Where(w => w.Definition.Type == WeaponType.OrbitingBarrier).ToList();
        foreach (var weapon in orbitWeapons)
        {
            int requiredCount = weapon.GetProjectileCount(player.BonusProjectiles);
            var activeOrbs = projectiles.Where(p => p.SkillId == weapon.Definition.Id && p.IsAlive).ToList();

            if (activeOrbs.Count != requiredCount)
            {
                // 제거 후 재배치
                foreach (var orb in activeOrbs) orb.IsAlive = false;

                double angleStep = (Math.PI * 2.0) / requiredCount;
                for (int i = 0; i < requiredCount; i++)
                {
                    double angle = angleStep * i;
                    projectiles.Add(new ProjectileEntity
                    {
                        SkillId = weapon.Definition.Id,
                        Position = new Point(player.Position.X + Math.Cos(angle) * weapon.GetAreaRadius(),
                                             player.Position.Y + Math.Sin(angle) * weapon.GetAreaRadius()),
                        Velocity = new Vector(0, 0),
                        Damage = weapon.GetCurrentDamage(player.DamageMultiplier),
                        PierceLeft = 999999,
                        Radius = 14,
                        ParticleColor = weapon.GetColor(),
                        MaxLifeTime = 99999,
                        IsOrbiting = true,
                        OrbitAngle = angle,
                        OrbitRadius = weapon.GetAreaRadius(),
                        OrbitSpeed = 3.6
                    });
                }
            }
            else
            {
                // 데미지 실시간 동기화
                foreach (var orb in activeOrbs)
                {
                    orb.Damage = weapon.GetCurrentDamage(player.DamageMultiplier);
                }
            }
        }
    }

    private void FireWeapon(
        WeaponInstance weapon,
        PlayerEntity player,
        List<EnemyEntity> enemies,
        List<ProjectileEntity> projectiles,
        List<AreaEffectEntity> areaEffects,
        List<FloatingTextEntity> floatingTexts)
    {
        var aliveEnemies = enemies.Where(e => e.IsAlive).ToList();
        double dmg = weapon.GetCurrentDamage(player.DamageMultiplier);
        int projCount = weapon.GetProjectileCount(player.BonusProjectiles);
        Color color = weapon.GetColor();

        switch (weapon.Definition.Type)
        {
            case WeaponType.ProjectileDirect:
            {
                // 가장 가까운 적 방향으로 발사
                var closest = aliveEnemies
                    .OrderBy(e => (e.Position.X - player.Position.X) * (e.Position.X - player.Position.X) +
                                  (e.Position.Y - player.Position.Y) * (e.Position.Y - player.Position.Y))
                    .Take(projCount)
                    .ToList();

                for (int i = 0; i < projCount; i++)
                {
                    Vector dir;
                    if (i < closest.Count)
                    {
                        dir = new Vector(closest[i].Position.X - player.Position.X, closest[i].Position.Y - player.Position.Y);
                    }
                    else
                    {
                        double spread = (i - (projCount - 1) / 2.0) * 0.2;
                        dir = new Vector(player.FacingDirection, spread);
                    }

                    if (dir.LengthSquared > 0.001) dir.Normalize();
                    else dir = new Vector(player.FacingDirection, 0);

                    // 다탄환 분산
                    if (projCount > 1 && i > 0)
                    {
                        double spreadAngle = (i - (projCount - 1) / 2.0) * 0.18;
                        double cos = Math.Cos(spreadAngle);
                        double sin = Math.Sin(spreadAngle);
                        dir = new Vector(dir.X * cos - dir.Y * sin, dir.X * sin + dir.Y * cos);
                    }

                    projectiles.Add(new ProjectileEntity
                    {
                        SkillId = weapon.Definition.Id,
                        Position = player.Position,
                        Velocity = dir * weapon.Definition.BaseSpeed,
                        Damage = dmg,
                        PierceLeft = weapon.GetPierce(),
                        Radius = weapon.GetAreaRadius(),
                        ParticleColor = color,
                        MaxLifeTime = 2.5,
                        ExplodesOnHit = weapon.Definition.Id == "mega_flame",
                        ExplosionRadius = weapon.Definition.Id == "mega_flame" ? 60.0 : 0.0
                    });
                }
                SoundSystem.PlayShoot();
                break;
            }

            case WeaponType.PiercingSlice: // 섀도우 윙
            {
                // 전방 & 후방으로 관통 블레이드 발사
                for (int i = 0; i < projCount; i++)
                {
                    double dirX = (i % 2 == 0) ? player.FacingDirection : -player.FacingDirection;
                    double spreadY = ((i / 2) - 0.5) * 0.2;
                    Vector dir = new(dirX, spreadY);
                    dir.Normalize();

                    projectiles.Add(new ProjectileEntity
                    {
                        SkillId = weapon.Definition.Id,
                        Position = player.Position,
                        Velocity = dir * weapon.Definition.BaseSpeed,
                        Damage = dmg,
                        PierceLeft = 99999,
                        Radius = weapon.GetAreaRadius(),
                        ParticleColor = color,
                        MaxLifeTime = 3.0
                    });
                }
                SoundSystem.PlayShoot();
                break;
            }

            case WeaponType.HomingMissile: // 기가 디스트로이어
            {
                var highHpTargets = aliveEnemies
                    .OrderByDescending(e => e.CurrentHp)
                    .Take(projCount)
                    .ToList();

                for (int i = 0; i < projCount; i++)
                {
                    EnemyEntity? target = i < highHpTargets.Count ? highHpTargets[i] : null;
                    double angle = (Math.PI * 2.0 / projCount) * i;
                    Vector initialVel = new Vector(Math.Cos(angle), Math.Sin(angle)) * weapon.Definition.BaseSpeed;

                    projectiles.Add(new ProjectileEntity
                    {
                        SkillId = weapon.Definition.Id,
                        Position = player.Position,
                        Velocity = initialVel,
                        Damage = dmg,
                        PierceLeft = 1,
                        Radius = weapon.GetAreaRadius() * 0.4,
                        ParticleColor = color,
                        MaxLifeTime = 4.0,
                        IsHoming = true,
                        HomingTarget = target,
                        ExplodesOnHit = true,
                        ExplosionRadius = weapon.GetAreaRadius()
                    });
                }
                SoundSystem.PlayShoot();
                break;
            }

            case WeaponType.LightningStrike: // 해머 스파크
            {
                if (aliveEnemies.Count == 0) break;

                var randomTargets = aliveEnemies.OrderBy(_ => _rnd.Next()).Take(projCount).ToList();
                foreach (var target in randomTargets)
                {
                    target.TakeDamage(dmg);
                    SoundSystem.PlayHit();

                    floatingTexts.Add(new FloatingTextEntity
                    {
                        Position = target.Position,
                        Text = $"⚡ {(int)dmg}",
                        Color = Color.FromRgb(80, 220, 255),
                        Size = 18
                    });

                    // 번개 이펙트 장판 (짧은 잔상)
                    areaEffects.Add(new AreaEffectEntity
                    {
                        Position = target.Position,
                        Radius = 45,
                        DamagePerTick = 0,
                        TickInterval = 999,
                        Duration = 0.25,
                        VisualColor = Color.FromArgb(200, 100, 220, 255),
                        Name = "Lightning"
                    });
                }
                break;
            }

            case WeaponType.IceChillingField: // 소로우 블루
            {
                for (int i = 0; i < projCount; i++)
                {
                    Point center = player.Position;
                    if (aliveEnemies.Count > 0)
                    {
                        var pick = aliveEnemies[_rnd.Next(aliveEnemies.Count)];
                        center = pick.Position;
                    }

                    areaEffects.Add(new AreaEffectEntity
                    {
                        Position = center,
                        Radius = weapon.GetAreaRadius(),
                        DamagePerTick = dmg * 0.35,
                        TickInterval = 0.5,
                        Duration = weapon.Definition.BaseDuration,
                        VisualColor = Color.FromArgb(120, 40, 140, 255),
                        Name = "BlizzardZone",
                        SlowsEnemies = true
                    });
                }
                break;
            }

            case WeaponType.RapidMeleeClaw: // 카이저 네일 / 그레이 소드
            {
                double radius = weapon.GetAreaRadius();
                int hitCount = 0;

                foreach (var enemy in aliveEnemies)
                {
                    double distSq = (enemy.Position.X - player.Position.X) * (enemy.Position.X - player.Position.X) +
                                    (enemy.Position.Y - player.Position.Y) * (enemy.Position.Y - player.Position.Y);
                    if (distSq <= radius * radius)
                    {
                        enemy.TakeDamage(dmg);
                        hitCount++;
                        floatingTexts.Add(new FloatingTextEntity
                        {
                            Position = enemy.Position,
                            Text = $"⚔️ {(int)dmg}",
                            Color = color,
                            Size = 20
                        });
                    }
                }

                if (hitCount > 0) SoundSystem.PlayHit();

                // 참격 이펙트 장판
                areaEffects.Add(new AreaEffectEntity
                {
                    Position = player.Position,
                    Radius = radius,
                    DamagePerTick = 0,
                    TickInterval = 999,
                    Duration = 0.2,
                    VisualColor = Color.FromArgb(180, color.R, color.G, color.B),
                    Name = "SlashStorm"
                });
                break;
            }

            case WeaponType.ScreenBomb: // 가이아 포스
            {
                // 화면 전체 대폭발
                foreach (var enemy in aliveEnemies)
                {
                    enemy.TakeDamage(dmg);
                    floatingTexts.Add(new FloatingTextEntity
                    {
                        Position = enemy.Position,
                        Text = $"💥 {(int)dmg}",
                        Color = Color.FromRgb(255, 60, 0),
                        Size = 24
                    });
                }
                SoundSystem.PlayEvolution();

                areaEffects.Add(new AreaEffectEntity
                {
                    Position = player.Position,
                    Radius = 380,
                    DamagePerTick = 0,
                    TickInterval = 999,
                    Duration = 0.6,
                    VisualColor = Color.FromArgb(220, 255, 120, 20),
                    Name = "GaiaBurst"
                });
                break;
            }

            case WeaponType.FreezingLaser: // 코큐토스 브레스
            {
                Vector dir = new(player.FacingDirection, 0);
                projectiles.Add(new ProjectileEntity
                {
                    SkillId = weapon.Definition.Id,
                    Position = player.Position,
                    Velocity = dir * weapon.Definition.BaseSpeed,
                    Damage = dmg,
                    PierceLeft = 99999,
                    Radius = weapon.GetAreaRadius(),
                    ParticleColor = Color.FromRgb(100, 240, 255),
                    MaxLifeTime = 1.2
                });
                SoundSystem.PlayShoot();
                break;
            }
        }
    }
}
