using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace DigimonSurvivors.Engine;

public enum ParticleType
{
    Spark,          // 타격 스파크 (불꽃/빛)
    DigitalBit,     // 적 처치 시 흩어지는 사각 데이터 조각
    Shockwave,      // 폭발 시 팽창하는 충격파 링
    CyberTrail,     // 대시 잔상 또는 궤적
    FloatingMote,   // 배경 디지털 부유 입자
    TextPop         // 특수 팝업 (CRIT!)
}

public class Particle
{
    public ParticleType Type;
    public Point Position;
    public Vector Velocity;
    public Color Color;
    public double Size;
    public double MaxSize;
    public double Life;
    public double MaxLife;
    public double Rotation;
    public double RotationSpeed;
    public bool IsAlive => Life < MaxLife;
    public double Alpha => Math.Clamp(1.0 - (Life / MaxLife), 0.0, 1.0);
}

public class ParticleSystem
{
    private readonly List<Particle> _particles = new(1000);
    private readonly List<Particle> _ambientMotes = new(60);
    private readonly Random _rnd = new();

    public void InitAmbient(Rect arena)
    {
        _ambientMotes.Clear();
        for (int i = 0; i < 50; i++)
        {
            _ambientMotes.Add(new Particle
            {
                Type = ParticleType.FloatingMote,
                Position = new Point(
                    arena.Left + _rnd.NextDouble() * arena.Width,
                    arena.Top + _rnd.NextDouble() * arena.Height
                ),
                Velocity = new Vector((_rnd.NextDouble() - 0.5) * 15.0, -20.0 - _rnd.NextDouble() * 30.0),
                Color = Color.FromArgb((byte)(40 + _rnd.Next(60)), 0, (byte)(180 + _rnd.Next(75)), 255),
                Size = 2.0 + _rnd.NextDouble() * 3.5,
                Life = 0,
                MaxLife = 999999
            });
        }
    }

    public void AddHitSparks(Point pos, Vector hitDir, Color color, int count = 8)
    {
        for (int i = 0; i < count; i++)
        {
            double angle = Math.Atan2(hitDir.Y, hitDir.X) + (_rnd.NextDouble() - 0.5) * 1.6;
            double speed = 100.0 + _rnd.NextDouble() * 250.0;
            _particles.Add(new Particle
            {
                Type = ParticleType.Spark,
                Position = pos,
                Velocity = new Vector(Math.Cos(angle) * speed, Math.Sin(angle) * speed),
                Color = color,
                Size = 3.5 + _rnd.NextDouble() * 3.0,
                Life = 0,
                MaxLife = 0.25 + _rnd.NextDouble() * 0.2,
                Rotation = _rnd.NextDouble() * Math.PI * 2,
                RotationSpeed = (_rnd.NextDouble() - 0.5) * 20.0
            });
        }
    }

    public void AddDigitalDeathBits(Point pos, Color color, int count = 16)
    {
        for (int i = 0; i < count; i++)
        {
            double angle = _rnd.NextDouble() * Math.PI * 2.0;
            double speed = 80.0 + _rnd.NextDouble() * 180.0;
            _particles.Add(new Particle
            {
                Type = ParticleType.DigitalBit,
                Position = pos,
                Velocity = new Vector(Math.Cos(angle) * speed, Math.Sin(angle) * speed),
                Color = color,
                Size = 4.0 + _rnd.NextDouble() * 5.0,
                Life = 0,
                MaxLife = 0.45 + _rnd.NextDouble() * 0.35,
                Rotation = _rnd.NextDouble() * Math.PI * 2,
                RotationSpeed = (_rnd.NextDouble() - 0.5) * 12.0
            });
        }
    }

    public void AddShockwave(Point pos, double maxRadius, Color color, double duration = 0.4)
    {
        _particles.Add(new Particle
        {
            Type = ParticleType.Shockwave,
            Position = pos,
            Size = 5.0,
            MaxSize = maxRadius,
            Color = color,
            Life = 0,
            MaxLife = duration
        });
    }

    public void AddCyberTrail(Point pos, double size, Color color)
    {
        _particles.Add(new Particle
        {
            Type = ParticleType.CyberTrail,
            Position = pos,
            Velocity = new Vector(0, 0),
            Color = color,
            Size = size,
            Life = 0,
            MaxLife = 0.28
        });
    }

    public void Update(double dt, Rect arena)
    {
        // 1. 일반 파티클 업데이트
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Life += dt;
            if (!p.IsAlive)
            {
                _particles.RemoveAt(i);
                continue;
            }

            p.Position += p.Velocity * dt;
            p.Velocity *= Math.Max(0.0, 1.0 - dt * 3.5); // 감속
            p.Rotation += p.RotationSpeed * dt;

            if (p.Type == ParticleType.Shockwave)
            {
                double progress = p.Life / p.MaxLife;
                p.Size = 5.0 + (p.MaxSize - 5.0) * Math.Sin(progress * Math.PI * 0.5);
            }
        }

        // 2. 배경 부유 데이터 모트 업데이트
        foreach (var m in _ambientMotes)
        {
            m.Position += m.Velocity * dt;
            if (m.Position.Y < arena.Top)
            {
                m.Position.Y = arena.Bottom;
                m.Position.X = arena.Left + _rnd.NextDouble() * arena.Width;
            }
            if (m.Position.X < arena.Left) m.Position.X = arena.Right;
            if (m.Position.X > arena.Right) m.Position.X = arena.Left;
        }
    }

    public void Draw(DrawingContext dc)
    {
        // 1. 부유 데이터 모트 드로잉 (은은한 사각 디지털 픽셀)
        foreach (var m in _ambientMotes)
        {
            var brush = new SolidColorBrush(m.Color);
            brush.Freeze();
            dc.DrawRectangle(brush, null, new Rect(m.Position.X - m.Size / 2, m.Position.Y - m.Size / 2, m.Size, m.Size));
        }

        // 2. 활성 파티클 드로잉
        foreach (var p in _particles)
        {
            if (!p.IsAlive) continue;
            byte alpha = (byte)(p.Alpha * 255);
            var col = Color.FromArgb(alpha, p.Color.R, p.Color.G, p.Color.B);

            switch (p.Type)
            {
                case ParticleType.Spark:
                {
                    var b = new SolidColorBrush(col);
                    b.Freeze();
                    dc.DrawEllipse(b, null, p.Position, p.Size, p.Size);
                    break;
                }
                case ParticleType.DigitalBit:
                {
                    var b = new SolidColorBrush(col);
                    b.Freeze();
                    dc.PushTransform(new RotateTransform(p.Rotation * 180.0 / Math.PI, p.Position.X, p.Position.Y));
                    dc.DrawRectangle(b, null, new Rect(p.Position.X - p.Size / 2, p.Position.Y - p.Size / 2, p.Size, p.Size));
                    dc.Pop();
                    break;
                }
                case ParticleType.Shockwave:
                {
                    var pen = new Pen(new SolidColorBrush(col), 3.0 * (1.0 - p.Life / p.MaxLife));
                    pen.Freeze();
                    dc.DrawEllipse(null, pen, p.Position, p.Size, p.Size);
                    break;
                }
                case ParticleType.CyberTrail:
                {
                    var b = new SolidColorBrush(Color.FromArgb((byte)(alpha * 0.4), p.Color.R, p.Color.G, p.Color.B));
                    b.Freeze();
                    dc.DrawEllipse(b, null, p.Position, p.Size, p.Size);
                    break;
                }
            }
        }
    }

    public void Clear()
    {
        _particles.Clear();
    }
}
