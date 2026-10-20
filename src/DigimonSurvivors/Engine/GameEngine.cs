using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using DigimonSurvivors.Audio;
using DigimonSurvivors.Data;
using DigimonSurvivors.Entities;
using DigimonSurvivors.Models;
using DigimonSurvivors.Skills;
using DigimonSurvivors.Systems;

namespace DigimonSurvivors.Engine;

public class GameEngine
{
    public Rect ArenaBounds { get; } = new(-1500, -1500, 3000, 3000);
    public GameState State { get; set; } = GameState.Title;

    public PlayerEntity? Player { get; private set; }
    public readonly List<EnemyEntity> Enemies = new();
    public readonly List<ProjectileEntity> Projectiles = new();
    public readonly List<AreaEffectEntity> AreaEffects = new();
    public readonly List<ExpGemEntity> ExpGems = new();
    public readonly List<FloatingTextEntity> FloatingTexts = new();
    public readonly List<FieldDropEntity> FieldDrops = new();
    public readonly List<MonolithEntity> Monoliths = new();

    public readonly ParticleSystem Particles = new();
    public readonly WaveSpawner Spawner = new();
    public readonly WeaponSystem Weapons = new();
    public readonly CardUpgradeManager UpgradeManager = new();

    public Point CameraPosition { get; set; } = new(0, 0);
    public double GameTime { get; private set; } = 0.0;
    public string BossWarningText { get; private set; } = string.Empty;
    public double BossWarningTimer { get; private set; } = 0.0;
    public int PendingLevelUps { get; set; } = 0;

    // 화면 흔들림 (Screen Shake / Trauma System)
    public double ScreenShakeTrauma { get; set; } = 0.0;
    public Vector ScreenShakeOffset { get; private set; } = new(0, 0);
    private readonly Random _rnd = new();

    public List<UpgradeOption> CurrentLevelUpOptions { get; private set; } = new();

    // 진화 컷신 정보
    public DigimonProfile? EvolvingFromProfile { get; private set; }
    public DigimonProfile? EvolvingToProfile { get; private set; }
    public double EvolutionCutsceneTimer { get; private set; } = 0.0;
    public double EvolutionTotalDuration = 2.8;

    private double _monolithSpawnTimer = 0.0;

    public event Action? OnLevelUpTriggered;
    public event Action? OnEvolutionTriggered;
    public event Action? OnGameOverTriggered;
    public event Action? OnVictoryTriggered;

    public GameEngine()
    {
        Spawner.OnBossWarning += msg =>
        {
            BossWarningText = msg;
            BossWarningTimer = 3.5;
            AddScreenShake(0.6);
            BgmSystem.PlayBoss();
        };
    }

    public void AddScreenShake(double amount)
    {
        ScreenShakeTrauma = Math.Clamp(ScreenShakeTrauma + amount, 0.0, 1.0);
    }

    public void StartGame(string starterId)
    {
        if (!GameDatabase.Digimons.TryGetValue(starterId, out var profile))
        {
            profile = GameDatabase.Digimons["agumon"];
        }

        Player = new PlayerEntity(profile, new Point(0, 0));
        Enemies.Clear();
        Projectiles.Clear();
        AreaEffects.Clear();
        ExpGems.Clear();
        FloatingTexts.Clear();
        FieldDrops.Clear();
        Monoliths.Clear();
        Particles.Clear();
        Particles.InitAmbient(ArenaBounds);

        // 초기 모놀리스 4개 배치
        SpawnInitialMonoliths();

        Spawner.Reset();
        GameTime = 0.0;
        BossWarningTimer = 0.0;
        BossWarningText = string.Empty;
        ScreenShakeTrauma = 0.0;
        ScreenShakeOffset = new Vector(0, 0);
        PendingLevelUps = 0;
        CameraPosition = Player.Position;
        State = GameState.Playing;

        BgmSystem.PlayCombat();
    }

    private void SpawnInitialMonoliths()
    {
        Point[] positions = {
            new(-240, -180), new(240, -180), new(-240, 180), new(240, 180),
            new(0, -280), new(0, 280)
        };
        foreach (var pos in positions)
        {
            Monoliths.Add(new MonolithEntity { Position = pos });
        }
    }

    public void TryPlayerDash(Vector inputDir)
    {
        if (State != GameState.Playing || Player == null) return;
        if (Player.TryDash(inputDir))
        {
            if (GameSettings.ParticlesEnabled)
            {
                Particles.AddCyberTrail(Player.Position, Player.Radius * 1.5, Color.FromRgb(0, 229, 255));
            }
            AddScreenShake(0.18);

            // 대시 충격으로 모놀리스 타격
            foreach (var mono in Monoliths)
            {
                if (!mono.IsAlive) continue;
                double dx = mono.Position.X - Player.Position.X;
                double dy = mono.Position.Y - Player.Position.Y;
                double hitDist = mono.Radius + Player.Radius + 35;
                if (dx * dx + dy * dy <= hitDist * hitDist)
                {
                    mono.TakeDamage(60);
                    Particles.AddHitSparks(mono.Position, inputDir * 400, Colors.Cyan, 10);
                    SoundSystem.PlayHit();
                    FloatingTexts.Add(new FloatingTextEntity
                    {
                        Position = mono.Position,
                        Text = "💥 DASH 60",
                        Color = Colors.Gold,
                        Size = 18
                    });
                }
            }
        }
    }

    public void Update(double dt, Vector moveInput)
    {
        // 1. 화면 흔들림 계산
        if (GameSettings.ScreenShakeEnabled && ScreenShakeTrauma > 0)
        {
            ScreenShakeTrauma = Math.Max(0.0, ScreenShakeTrauma - dt * 2.2);
            double shakePower = ScreenShakeTrauma * ScreenShakeTrauma;
            double maxOffset = 18.0;
            double angle = _rnd.NextDouble() * Math.PI * 2;
            ScreenShakeOffset = new Vector(Math.Cos(angle) * maxOffset * shakePower, Math.Sin(angle) * maxOffset * shakePower);
        }
        else
        {
            ScreenShakeOffset = new Vector(0, 0);
        }

        if (BossWarningTimer > 0)
        {
            BossWarningTimer -= dt;
        }

        if (State == GameState.EvolutionCutscene)
        {
            EvolutionCutsceneTimer -= dt;
            if (EvolutionCutsceneTimer <= 0)
            {
                if (PendingLevelUps > 0)
                {
                    TriggerLevelUp();
                }
                else
                {
                    State = GameState.Playing;
                }
                BgmSystem.PlayCombat();
            }
            return;
        }

        if (State != GameState.Playing || Player == null)
        {
            return;
        }

        GameTime += dt;

        // 파티클 업데이트
        Particles.Update(dt, ArenaBounds);

        // 1. 플레이어 업데이트
        Player.Update(dt, moveInput, ArenaBounds);
        if (Player.IsDashing)
        {
            Particles.AddCyberTrail(Player.Position, Player.Radius * 1.3, Color.FromRgb(0, 229, 255));
        }

        // 부드러운 카메라 추적
        double camLerp = Math.Min(1.0, dt * 10.0);
        CameraPosition = new Point(
            CameraPosition.X + (Player.Position.X - CameraPosition.X) * camLerp,
            CameraPosition.Y + (Player.Position.Y - CameraPosition.Y) * camLerp
        );

        // 모놀리스 정기 리스폰 (60초 주기)
        _monolithSpawnTimer += dt;
        if (_monolithSpawnTimer >= 60.0 && Monoliths.Count < 6)
        {
            _monolithSpawnTimer = 0.0;
            double x = ArenaBounds.Left + 200 + _rnd.NextDouble() * (ArenaBounds.Width - 400);
            double y = ArenaBounds.Top + 200 + _rnd.NextDouble() * (ArenaBounds.Height - 400);
            Monoliths.Add(new MonolithEntity { Position = new Point(x, y) });
        }

        foreach (var m in Monoliths) m.Update(dt);

        // 2. 적 스폰
        Spawner.Update(dt, GameTime, Player.Position, Enemies, ArenaBounds);

        // 3. 무기 발사 및 스킬 업데이트
        Weapons.Update(dt, Player, Enemies, Projectiles, AreaEffects, FloatingTexts, Monoliths);

        // 4. 투사체 업데이트 및 충돌 판정
        for (int i = Projectiles.Count - 1; i >= 0; i--)
        {
            var proj = Projectiles[i];
            proj.Update(dt, Player.Position);

            if (!proj.IsAlive)
            {
                Projectiles.RemoveAt(i);
                continue;
            }

            // 고유 스킬 원소별 궤적 파티클 방출
            if (GameSettings.ParticlesEnabled && _rnd.NextDouble() < 0.7)
            {
                Particles.AddSkillTrail(proj.Position, proj.Velocity, proj.SkillId, proj.ParticleColor);
            }

            // 모놀리스와의 충돌 판정
            foreach (var mono in Monoliths)
            {
                if (!mono.IsAlive) continue;
                if (proj.IsOrbiting && mono.HitFlashTimer > 0) continue;

                double dx = mono.Position.X - proj.Position.X;
                double dy = mono.Position.Y - proj.Position.Y;
                double hitDist = mono.Radius + proj.Radius;
                if (dx * dx + dy * dy <= hitDist * hitDist)
                {
                    mono.TakeDamage(proj.Damage);
                    Particles.AddHitSparks(mono.Position, proj.Velocity, Colors.Cyan, 6);
                    SoundSystem.PlayHit();
                    FloatingTexts.Add(new FloatingTextEntity
                    {
                        Position = new Point(mono.Position.X + (_rnd.NextDouble() - 0.5) * 20, mono.Position.Y - 20),
                        Text = $"💎 {(int)proj.Damage}",
                        Color = Colors.Cyan,
                        Size = 16
                    });

                    if (!proj.IsOrbiting)
                    {
                        proj.PierceLeft--;
                        if (proj.PierceLeft <= 0) { proj.IsAlive = false; break; }
                    }
                }
            }

            if (!proj.IsAlive)
            {
                Projectiles.RemoveAt(i);
                continue;
            }

            // 적과의 충돌 판정
            foreach (var enemy in Enemies)
            {
                if (!enemy.IsAlive) continue;

                double dx = enemy.Position.X - proj.Position.X;
                double dy = enemy.Position.Y - proj.Position.Y;
                double distSq = dx * dx + dy * dy;
                double hitDist = enemy.Radius + proj.Radius;

                if (distSq <= hitDist * hitDist)
                {
                    // 크리티컬 판정 (기본 15% 확률)
                    bool isCrit = _rnd.NextDouble() < 0.15;
                    double actualDamage = isCrit ? proj.Damage * 2.0 : proj.Damage;

                    Vector hitDir = proj.Velocity;
                    enemy.TakeDamage(actualDamage, hitDir, knockbackForce: isCrit ? 180.0 : 90.0);
                    Player.RecordDamage(proj.SkillId, actualDamage);

                    // 타격 피드백 & 주스
                    Particles.AddHitSparks(enemy.Position, hitDir, proj.ParticleColor, isCrit ? 12 : 6);
                    if (isCrit)
                    {
                        AddScreenShake(0.12);
                        SoundSystem.PlayCrit();
                    }
                    else
                    {
                        SoundSystem.PlayHit();
                    }

                    FloatingTexts.Add(new FloatingTextEntity
                    {
                        Position = enemy.Position,
                        Text = isCrit ? $"CRIT! {(int)actualDamage}" : $"{(int)actualDamage}",
                        Color = isCrit ? Color.FromRgb(255, 235, 59) : proj.ParticleColor,
                        Size = isCrit ? 22 : (proj.Damage > 100 ? 18 : 15)
                    });

                    // 폭발 투사체인 경우 반경 내 모든 적 스플래시
                    if (proj.ExplodesOnHit && proj.ExplosionRadius > 0)
                    {
                        TriggerExplosion(proj.Position, proj.ExplosionRadius, proj.Damage * 0.8, proj.ParticleColor);
                    }

                    proj.PierceLeft--;
                    if (proj.PierceLeft <= 0)
                    {
                        proj.IsAlive = false;
                        break;
                    }
                }
            }

            if (!proj.IsAlive)
            {
                Projectiles.RemoveAt(i);
            }
        }

        // 5. 지속 장판(AreaEffect) 업데이트 및 적 피격
        for (int i = AreaEffects.Count - 1; i >= 0; i--)
        {
            var area = AreaEffects[i];
            area.Update(dt);

            if (!area.IsAlive)
            {
                AreaEffects.RemoveAt(i);
                continue;
            }

            if (area.TickTimer >= area.TickInterval && area.DamagePerTick > 0)
            {
                area.TickTimer = 0.0;
                foreach (var enemy in Enemies)
                {
                    if (!enemy.IsAlive) continue;
                    double distSq = (enemy.Position.X - area.Position.X) * (enemy.Position.X - area.Position.X) +
                                    (enemy.Position.Y - area.Position.Y) * (enemy.Position.Y - area.Position.Y);
                    if (distSq <= area.Radius * area.Radius)
                    {
                        enemy.TakeDamage(area.DamagePerTick);
                        Player.RecordDamage(area.Name, area.DamagePerTick);
                        if (area.SlowsEnemies)
                        {
                            enemy.ApplySlow(1.0, 0.4);
                        }
                    }
                }

                // 모놀리스에도 장판 지속 대미지 적용
                foreach (var mono in Monoliths)
                {
                    if (!mono.IsAlive) continue;
                    double distSq = (mono.Position.X - area.Position.X) * (mono.Position.X - area.Position.X) +
                                    (mono.Position.Y - area.Position.Y) * (mono.Position.Y - area.Position.Y);
                    double hitDist = area.Radius + mono.Radius;
                    if (distSq <= hitDist * hitDist)
                    {
                        mono.TakeDamage(area.DamagePerTick);
                        Player.RecordDamage(area.Name, area.DamagePerTick);
                        Particles.AddHitSparks(mono.Position, new Vector(0, -1), area.VisualColor, 4);
                        FloatingTexts.Add(new FloatingTextEntity
                        {
                            Position = new Point(mono.Position.X + (_rnd.NextDouble() - 0.5) * 15, mono.Position.Y - 15),
                            Text = $"💎 {(int)area.DamagePerTick}",
                            Color = area.VisualColor,
                            Size = 14
                        });
                    }
                }
            }
        }

        // 6. 적 업데이트 및 플레이어 접촉 공격
        for (int i = Enemies.Count - 1; i >= 0; i--)
        {
            var enemy = Enemies[i];
            if (!enemy.IsAlive)
            {
                // 적 사망 처리 -> 디지털 큐브 비산 + 경험치 보석 드롭
                Player.KillCount++;
                Particles.AddDigitalDeathBits(enemy.Position, enemy.IsBoss ? Colors.Gold : Color.FromRgb(0, 229, 255), enemy.IsBoss ? 35 : 14);
                SpawnExpDrop(enemy.Position, enemy.DropType);

                // 일정 확률로 특수 필드 아이템 드롭 (고기 2.5%, 자석 1.2%, 폭탄 0.8%, 타임프리즈 0.8%)
                TrySpawnFieldDrop(enemy.Position, enemy.IsBoss || enemy.IsMiniBoss);

                // 피에몬(최종 보스) 격파 시 승리!
                if (enemy.IsBoss && enemy.Id == "Piemon")
                {
                    State = GameState.Victory;
                    AddScreenShake(0.8);
                    SoundSystem.PlayLevelUp();
                    OnVictoryTriggered?.Invoke();
                    return;
                }

                Enemies.RemoveAt(i);
                continue;
            }

            enemy.Update(dt, Player.Position, Enemies);

            // 플레이어와 충돌 시 데미지
            double dx = Player.Position.X - enemy.Position.X;
            double dy = Player.Position.Y - enemy.Position.Y;
            double hitDist = Player.Radius + enemy.Radius;

            if (dx * dx + dy * dy <= hitDist * hitDist)
            {
                Player.TakeDamage(enemy.Dp);
                AddScreenShake(0.25);
                Particles.AddHitSparks(Player.Position, new Vector(dx, dy), Colors.Red, 8);

                if (Player.CurrentHp <= 0)
                {
                    State = GameState.GameOver;
                    BgmSystem.Stop();
                    SoundSystem.PlayGameOver();
                    OnGameOverTriggered?.Invoke();
                    return;
                }
            }
        }

        // 7. 모놀리스 파괴된 객체 정리 및 보상 드롭
        for (int i = Monoliths.Count - 1; i >= 0; i--)
        {
            var mono = Monoliths[i];
            if (!mono.IsAlive)
            {
                TriggerMonolithDestroyed(mono.Position);
                Monoliths.RemoveAt(i);
            }
        }

        // 8. 특수 필드 아이템 업데이트 및 획득
        for (int i = FieldDrops.Count - 1; i >= 0; i--)
        {
            var item = FieldDrops[i];
            item.Update(dt, Player.Position, Player.MagnetRange);

            if (item.IsCollected)
            {
                ApplyFieldItem(item.Type, item.Position);
                FieldDrops.RemoveAt(i);
            }
        }

        // 9. 경험치 보석 업데이트 및 흡수
        for (int i = ExpGems.Count - 1; i >= 0; i--)
        {
            var gem = ExpGems[i];
            gem.Update(dt, Player.Position, Player.MagnetRange);

            if (gem.IsCollected)
            {
                SoundSystem.PlayGem();
                int levelUps = Player.AddExp(gem.ExpValue);

                if (gem.Type == ExpGemType.DigiEggBox)
                {
                    levelUps++;
                }

                if (levelUps > 0)
                {
                    PendingLevelUps += levelUps;
                }

                ExpGems.RemoveAt(i);
            }
        }

        // 보석들을 다 먹은 후 대기 레벨업이 있으면 즉시 레벨업 창 오픈
        if (PendingLevelUps > 0 && State == GameState.Playing)
        {
            SoundSystem.PlayLevelUp();
            TriggerLevelUp();
        }

        // 10. 플로팅 텍스트 업데이트
        for (int i = FloatingTexts.Count - 1; i >= 0; i--)
        {
            var ft = FloatingTexts[i];
            ft.Update(dt);
            if (!ft.IsAlive)
            {
                FloatingTexts.RemoveAt(i);
            }
        }
    }

    private void TriggerMonolithDestroyed(Point pos)
    {
        AddScreenShake(0.4);
        SoundSystem.PlayBomb();
        Particles.AddShockwave(pos, 120.0, Color.FromRgb(0, 229, 255));
        Particles.AddDigitalDeathBits(pos, Color.FromRgb(0, 229, 255), 25);

        // 보석 및 특수 아이템 확정 드롭
        SpawnExpDrop(pos, ExpGemType.LargeBit);
        FieldDrops.Add(new FieldDropEntity
        {
            Position = new Point(pos.X + (_rnd.NextDouble() - 0.5) * 30, pos.Y + (_rnd.NextDouble() - 0.5) * 30),
            Type = _rnd.Next(2) == 0 ? FieldItemType.Meat : FieldItemType.Magnet
        });
    }

    private void TrySpawnFieldDrop(Point pos, bool isBoss)
    {
        if (isBoss)
        {
            // 보스는 고기와 자석 확정 드롭
            FieldDrops.Add(new FieldDropEntity { Position = pos, Type = FieldItemType.Meat });
            FieldDrops.Add(new FieldDropEntity { Position = new Point(pos.X + 25, pos.Y), Type = FieldItemType.Magnet });
            return;
        }

        double roll = _rnd.NextDouble();
        if (roll < 0.025) // 2.5% 고기
        {
            FieldDrops.Add(new FieldDropEntity { Position = pos, Type = FieldItemType.Meat });
        }
        else if (roll < 0.037) // 1.2% 자석
        {
            FieldDrops.Add(new FieldDropEntity { Position = pos, Type = FieldItemType.Magnet });
        }
        else if (roll < 0.045) // 0.8% 폭탄
        {
            FieldDrops.Add(new FieldDropEntity { Position = pos, Type = FieldItemType.Bomb });
        }
        else if (roll < 0.053) // 0.8% 타임 프리즈
        {
            FieldDrops.Add(new FieldDropEntity { Position = pos, Type = FieldItemType.TimeFreeze });
        }
    }

    private void ApplyFieldItem(FieldItemType type, Point pos)
    {
        switch (type)
        {
            case FieldItemType.Meat:
                Player?.HealPercent(0.35);
                SoundSystem.PlayMeat();
                AddScreenShake(0.15);
                FloatingTexts.Add(new FloatingTextEntity
                {
                    Position = pos,
                    Text = "🍖 HP +35%!",
                    Color = Color.FromRgb(76, 175, 80),
                    Size = 20
                });
                break;

            case FieldItemType.Magnet:
                SoundSystem.PlayMeat();
                AddScreenShake(0.2);
                FloatingTexts.Add(new FloatingTextEntity
                {
                    Position = pos,
                    Text = "🧲 DATA MAGNET!",
                    Color = Color.FromRgb(33, 150, 243),
                    Size = 22
                });
                // 모든 경험치 보석 즉시 플레이어 흡수
                if (Player != null)
                {
                    foreach (var gem in ExpGems)
                    {
                        gem.Position = Player.Position;
                    }
                }
                break;

            case FieldItemType.Bomb:
                SoundSystem.PlayBomb();
                AddScreenShake(0.65);
                Particles.AddShockwave(Player?.Position ?? pos, 400.0, Color.FromRgb(255, 87, 34), 0.6);
                FloatingTexts.Add(new FloatingTextEntity
                {
                    Position = pos,
                    Text = "💣 CRASH BOMB!!",
                    Color = Color.FromRgb(255, 87, 34),
                    Size = 26
                });
                // 일반 적 모두 소멸
                foreach (var enemy in Enemies)
                {
                    if (!enemy.IsBoss)
                    {
                        enemy.TakeDamage(99999);
                        Player?.RecordDamage("Bomb", 99999);
                    }
                }
                // 화면 내 모놀리스도 즉시 파괴
                foreach (var mono in Monoliths)
                {
                    if (mono.IsAlive)
                    {
                        mono.TakeDamage(99999);
                    }
                }
                break;

            case FieldItemType.TimeFreeze:
                SoundSystem.PlayFreeze();
                AddScreenShake(0.18);
                FloatingTexts.Add(new FloatingTextEntity
                {
                    Position = pos,
                    Text = "⏱️ TIME FREEZE (4s)!",
                    Color = Color.FromRgb(0, 229, 255),
                    Size = 22
                });
                foreach (var enemy in Enemies)
                {
                    enemy.ApplyFreeze(4.0);
                }
                break;
        }
    }

    private void TriggerExplosion(Point center, double radius, double damage, Color color)
    {
        AddScreenShake(0.25);
        Particles.AddShockwave(center, radius, color, 0.35);

        AreaEffects.Add(new AreaEffectEntity
        {
            Position = center,
            Radius = radius,
            DamagePerTick = 0,
            TickInterval = 999,
            Duration = 0.25,
            VisualColor = Color.FromArgb(160, color.R, color.G, color.B),
            Name = "Blast"
        });

        foreach (var enemy in Enemies)
        {
            if (!enemy.IsAlive) continue;
            double dx = enemy.Position.X - center.X;
            double dy = enemy.Position.Y - center.Y;
            if (dx * dx + dy * dy <= radius * radius)
            {
                enemy.TakeDamage(damage, new Vector(dx, dy), 160.0);
                Player?.RecordDamage("Explosion", damage);
                FloatingTexts.Add(new FloatingTextEntity
                {
                    Position = enemy.Position,
                    Text = $"💥 {(int)damage}",
                    Color = color,
                    Size = 18
                });
            }
        }

        foreach (var mono in Monoliths)
        {
            if (!mono.IsAlive) continue;
            double dx = mono.Position.X - center.X;
            double dy = mono.Position.Y - center.Y;
            double hitDist = radius + mono.Radius;
            if (dx * dx + dy * dy <= hitDist * hitDist)
            {
                mono.TakeDamage(damage);
                Player?.RecordDamage("Explosion", damage);
                Particles.AddHitSparks(mono.Position, new Vector(dx, dy), color, 8);
                FloatingTexts.Add(new FloatingTextEntity
                {
                    Position = new Point(mono.Position.X, mono.Position.Y - 20),
                    Text = $"💥 {(int)damage}",
                    Color = color,
                    Size = 18
                });
            }
        }
    }

    private void SpawnExpDrop(Point pos, ExpGemType type)
    {
        int exp = type switch
        {
            ExpGemType.SmallBit => 10,
            ExpGemType.MediumBit => 40,
            ExpGemType.LargeBit => 150,
            ExpGemType.DigiEggBox => 500,
            _ => 10
        };

        ExpGems.Add(new ExpGemEntity
        {
            Position = pos,
            Type = type,
            ExpValue = exp
        });
    }

    private void TriggerLevelUp()
    {
        if (Player == null) return;
        CurrentLevelUpOptions = UpgradeManager.GenerateOptions(Player);
        State = GameState.LevelUp;
        OnLevelUpTriggered?.Invoke();
    }

    public void SelectUpgrade(UpgradeOption option)
    {
        if (Player == null) return;

        bool isEvolution = option.Type == UpgradeType.Evolution;
        var prevProfile = Player.Profile;

        UpgradeManager.ApplyUpgrade(Player, option);
        PendingLevelUps = Math.Max(0, PendingLevelUps - 1);

        if (isEvolution)
        {
            EvolvingFromProfile = prevProfile;
            EvolvingToProfile = Player.Profile;
            EvolutionCutsceneTimer = 2.8;
            State = GameState.EvolutionCutscene;
            AddScreenShake(0.5);
            SoundSystem.PlayEvolution();
            OnEvolutionTriggered?.Invoke();
        }
        else if (PendingLevelUps > 0)
        {
            // 남은 대기 레벨업이 있으면 즉시 다음 카드 선택창 오픈!
            TriggerLevelUp();
        }
        else
        {
            State = GameState.Playing;
        }
    }
}
