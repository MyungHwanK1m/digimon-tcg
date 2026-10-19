using System;
using System.Collections.Generic;
using System.Windows;
using DigimonSurvivors.Models;

namespace DigimonSurvivors.Entities;

public class EnemyEntity
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string SpriteFileName = string.Empty;
    public Point Position;
    public Vector Velocity;
    public Vector KnockbackVelocity = new(0, 0);
    public double CurrentHp;
    public double MaxHp;
    public double Dp = 10.0;
    public double Speed = 100.0;
    public double BaseSpeed = 100.0;
    public double Radius = 20.0;
    public bool IsBoss = false;
    public bool IsMiniBoss = false;
    public DigimonAttribute Attribute = DigimonAttribute.Virus;
    public double HitFlashTimer = 0.0;
    public double SlowTimer = 0.0;
    public double FreezeTimer = 0.0;
    public bool IsAlive = true;
    public ExpGemType DropType = ExpGemType.SmallBit;

    public void TakeDamage(double dmg, Vector? hitDirection = null, double knockbackForce = 120.0)
    {
        CurrentHp -= dmg;
        HitFlashTimer = 0.12;

        if (hitDirection.HasValue && !IsBoss)
        {
            Vector dir = hitDirection.Value;
            if (dir.LengthSquared > 0.001)
            {
                dir.Normalize();
                double resistance = IsMiniBoss ? 0.35 : 1.0;
                KnockbackVelocity = dir * (knockbackForce * resistance);
            }
        }

        if (CurrentHp <= 0)
        {
            CurrentHp = 0;
            IsAlive = false;
        }
    }

    public void ApplySlow(double duration, double slowPercent = 0.5)
    {
        SlowTimer = Math.Max(SlowTimer, duration);
        Speed = BaseSpeed * (1.0 - slowPercent);
    }

    public void ApplyFreeze(double duration)
    {
        FreezeTimer = Math.Max(FreezeTimer, duration);
    }

    public void Update(double dt, Point playerPos, List<EnemyEntity> nearbyEnemies)
    {
        if (HitFlashTimer > 0)
        {
            HitFlashTimer -= dt;
        }

        if (FreezeTimer > 0)
        {
            FreezeTimer -= dt;
            return; // 완전히 정지
        }

        // 넉백 감쇠
        if (KnockbackVelocity.LengthSquared > 1.0)
        {
            Position += KnockbackVelocity * dt;
            KnockbackVelocity *= Math.Max(0.0, 1.0 - dt * 10.0);
        }

        if (SlowTimer > 0)
        {
            SlowTimer -= dt;
            if (SlowTimer <= 0)
            {
                Speed = BaseSpeed;
            }
        }

        // 플레이어 추적
        var toPlayer = new Vector(playerPos.X - Position.X, playerPos.Y - Position.Y);
        double dist = toPlayer.Length;
        if (dist > 1.0)
        {
            toPlayer.Normalize();
        }

        // 몬스터 간 상호 분리 (겹침 방지)
        Vector separation = new(0, 0);
        int neighbors = 0;
        double repulseDist = Radius * 2.0;

        foreach (var other in nearbyEnemies)
        {
            if (ReferenceEquals(this, other) || !other.IsAlive) continue;
            double dx = Position.X - other.Position.X;
            double dy = Position.Y - other.Position.Y;
            double dSq = dx * dx + dy * dy;
            if (dSq < repulseDist * repulseDist && dSq > 0.001)
            {
                double d = Math.Sqrt(dSq);
                separation += new Vector(dx / d, dy / d) * (repulseDist - d);
                neighbors++;
            }
        }

        if (neighbors > 0)
        {
            separation /= neighbors;
        }

        Vector finalMove = toPlayer * Speed + separation * (Speed * 0.4);
        if (finalMove.Length > Speed * 1.5)
        {
            finalMove.Normalize();
            finalMove *= Speed * 1.5;
        }

        Position += finalMove * dt;
    }
}
