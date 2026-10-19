using System;
using System.Collections.Generic;
using System.Windows;
using DigimonSurvivors.Audio;
using DigimonSurvivors.Entities;
using DigimonSurvivors.Models;

namespace DigimonSurvivors.Systems;

public class WaveSpawner
{
    private double _spawnTimer = 0.0;
    private double _swarmTimer = 0.0;
    private readonly Random _rnd = new();

    private bool _orgemonSpawned = false;
    private bool _devimonSpawned = false;
    private bool _vamdemonSpawned = false;
    private bool _piemonSpawned = false;

    public event Action<string>? OnBossWarning;

    public void Reset()
    {
        _spawnTimer = 0.0;
        _swarmTimer = 0.0;
        _orgemonSpawned = false;
        _devimonSpawned = false;
        _vamdemonSpawned = false;
        _piemonSpawned = false;
    }

    public void Update(double dt, double gameTime, Point playerPos, List<EnemyEntity> enemies, Rect arenaBounds)
    {
        _spawnTimer += dt;
        _swarmTimer += dt;

        // 1. 일반 웨이브 주기 스폰 (시간 경과에 따라 스폰 간격 단축)
        double interval = Math.Max(0.25, 1.2 - (gameTime / 300.0) * 0.8);
        if (_spawnTimer >= interval)
        {
            _spawnTimer = 0.0;
            int count = 1 + (int)(gameTime / 60.0);
            for (int i = 0; i < count; i++)
            {
                SpawnRegularEnemy(gameTime, playerPos, enemies, arenaBounds);
            }
        }

        // 2. 30초마다 대규모 서클 기습 포위망 (Ambush)
        if (_swarmTimer >= 35.0)
        {
            _swarmTimer = 0.0;
            SpawnAmbushSwarm(gameTime, playerPos, enemies, arenaBounds);
        }

        // 3. 타임라인 보스 출현
        // 2분 30초 (150s) : 우가몬
        if (gameTime >= 150.0 && !_orgemonSpawned)
        {
            _orgemonSpawned = true;
            SpawnBoss("Orgemon", "우가몬", "Orgemon.png", 2200, 30, 105, 36, true, false, ExpGemType.DigiEggBox, playerPos, enemies, arenaBounds);
            OnBossWarning?.Invoke("⚠️ 경고: 악동 우가몬 출현! 거대 패왕권 주의!");
            SoundSystem.PlayBoss();
        }

        // 4분 (240s) : 데블몬
        if (gameTime >= 240.0 && !_devimonSpawned)
        {
            _devimonSpawned = true;
            SpawnBoss("Devimon", "데블몬", "Devimon.png", 5500, 42, 120, 42, true, false, ExpGemType.DigiEggBox, playerPos, enemies, arenaBounds);
            OnBossWarning?.Invoke("⚠️ 경고: 암흑의 지배자 데블몬 출현! 데스 클로 주의!");
            SoundSystem.PlayBoss();
        }

        // 6분 (360s) : 묘티스몬
        if (gameTime >= 360.0 && !_vamdemonSpawned)
        {
            _vamdemonSpawned = true;
            SpawnBoss("Vamdemon", "묘티스몬", "Vamdemon.png", 12000, 55, 130, 48, true, false, ExpGemType.DigiEggBox, playerPos, enemies, arenaBounds);
            OnBossWarning?.Invoke("⚠️ 경고: 흡혈귀왕 묘티스몬 출현! 나이트 레이드 주의!");
            SoundSystem.PlayBoss();
        }

        // 8분 (480s) : 피에몬 (최종 보스)
        if (gameTime >= 480.0 && !_piemonSpawned)
        {
            _piemonSpawned = true;
            SpawnBoss("Piemon", "피에몬", "Piemon.png", 25000, 75, 140, 52, false, true, ExpGemType.DigiEggBox, playerPos, enemies, arenaBounds);
            OnBossWarning?.Invoke("🚨 최종 결전: 어둠의 사천왕 피에몬 출현! 트럼프 소드 격파 시 승리!");
            SoundSystem.PlayBoss();
        }
    }

    private void SpawnRegularEnemy(double gameTime, Point playerPos, List<EnemyEntity> enemies, Rect arenaBounds)
    {
        Point spawnPos = GetRandomOffscreenPos(playerPos, arenaBounds);

        // 시간대별 적 종류 결정
        if (gameTime < 45.0)
        {
            // 초반: 코로몬 무리
            enemies.Add(new EnemyEntity
            {
                Id = "koromon",
                Name = "코로몬",
                SpriteFileName = "Koromon.png",
                Position = spawnPos,
                MaxHp = 18 + gameTime * 0.4,
                CurrentHp = 18 + gameTime * 0.4,
                Dp = 8,
                BaseSpeed = 95,
                Speed = 95,
                Radius = 18,
                Attribute = DigimonAttribute.Data,
                DropType = ExpGemType.SmallBit
            });
        }
        else if (gameTime < 110.0)
        {
            // 츄몬 or 고블몬
            bool isGoburimon = _rnd.Next(2) == 0;
            enemies.Add(new EnemyEntity
            {
                Id = isGoburimon ? "goburimon" : "tyumon",
                Name = isGoburimon ? "고블몬" : "츄몬",
                SpriteFileName = isGoburimon ? "Goburimon.png" : "Tyumon.png",
                Position = spawnPos,
                MaxHp = isGoburimon ? 45 : 28,
                CurrentHp = isGoburimon ? 45 : 28,
                Dp = isGoburimon ? 14 : 10,
                BaseSpeed = isGoburimon ? 100 : 125,
                Speed = isGoburimon ? 100 : 125,
                Radius = isGoburimon ? 22 : 16,
                Attribute = DigimonAttribute.Virus,
                DropType = isGoburimon ? ExpGemType.MediumBit : ExpGemType.SmallBit
            });
        }
        else if (gameTime < 210.0)
        {
            // 가지몬 or 피코데블몬
            bool isPico = _rnd.Next(3) == 0;
            enemies.Add(new EnemyEntity
            {
                Id = isPico ? "pico_devimon" : "gazimon",
                Name = isPico ? "피코데블몬" : "가지몬",
                SpriteFileName = isPico ? "PicoDevimon.png" : "Gazimon.png",
                Position = spawnPos,
                MaxHp = isPico ? 40 : 70,
                CurrentHp = isPico ? 40 : 70,
                Dp = isPico ? 16 : 18,
                BaseSpeed = isPico ? 145 : 110,
                Speed = isPico ? 145 : 110,
                Radius = isPico ? 18 : 22,
                Attribute = DigimonAttribute.Virus,
                DropType = ExpGemType.MediumBit
            });
        }
        else
        {
            // 후반: 가드로몬(탱커) or 누메몬
            bool isGuard = _rnd.Next(2) == 0;
            enemies.Add(new EnemyEntity
            {
                Id = isGuard ? "guardromon" : "numemon",
                Name = isGuard ? "가드로몬" : "누메몬",
                SpriteFileName = isGuard ? "Guardromon.png" : "Numemon.png",
                Position = spawnPos,
                MaxHp = isGuard ? 220 : 120,
                CurrentHp = isGuard ? 220 : 120,
                Dp = isGuard ? 26 : 18,
                BaseSpeed = isGuard ? 85 : 115,
                Speed = isGuard ? 85 : 115,
                Radius = isGuard ? 26 : 22,
                Attribute = DigimonAttribute.Virus,
                DropType = isGuard ? ExpGemType.LargeBit : ExpGemType.MediumBit
            });
        }
    }

    private void SpawnAmbushSwarm(double gameTime, Point playerPos, List<EnemyEntity> enemies, Rect arenaBounds)
    {
        int circleCount = 12 + (int)(gameTime / 30.0);
        double spawnRadius = 550.0;

        for (int i = 0; i < circleCount; i++)
        {
            double angle = (2.0 * Math.PI / circleCount) * i;
            Point p = new(playerPos.X + Math.Cos(angle) * spawnRadius, playerPos.Y + Math.Sin(angle) * spawnRadius);
            p.X = Math.Clamp(p.X, arenaBounds.Left + 20, arenaBounds.Right - 20);
            p.Y = Math.Clamp(p.Y, arenaBounds.Top + 20, arenaBounds.Bottom - 20);

            enemies.Add(new EnemyEntity
            {
                Id = "tyumon_swarm",
                Name = "츄몬 군단",
                SpriteFileName = "Tyumon.png",
                Position = p,
                MaxHp = 22 + gameTime * 0.3,
                CurrentHp = 22 + gameTime * 0.3,
                Dp = 10,
                BaseSpeed = 130,
                Speed = 130,
                Radius = 16,
                Attribute = DigimonAttribute.Virus,
                DropType = ExpGemType.SmallBit
            });
        }
    }

    private void SpawnBoss(string id, string name, string sprite, double hp, double dp, double speed, double radius, bool isMini, bool isFinal, ExpGemType drop, Point playerPos, List<EnemyEntity> enemies, Rect arenaBounds)
    {
        Point p = GetRandomOffscreenPos(playerPos, arenaBounds);
        enemies.Add(new EnemyEntity
        {
            Id = id,
            Name = name,
            SpriteFileName = sprite,
            Position = p,
            MaxHp = hp,
            CurrentHp = hp,
            Dp = dp,
            BaseSpeed = speed,
            Speed = speed,
            Radius = radius,
            IsMiniBoss = isMini,
            IsBoss = isFinal,
            Attribute = DigimonAttribute.Virus,
            DropType = drop
        });
    }

    private Point GetRandomOffscreenPos(Point center, Rect arenaBounds)
    {
        double angle = _rnd.NextDouble() * Math.PI * 2.0;
        double dist = 600.0 + _rnd.NextDouble() * 150.0;
        double x = center.X + Math.Cos(angle) * dist;
        double y = center.Y + Math.Sin(angle) * dist;

        return new Point(
            Math.Clamp(x, arenaBounds.Left + 30, arenaBounds.Right - 30),
            Math.Clamp(y, arenaBounds.Top + 30, arenaBounds.Bottom - 30)
        );
    }
}
