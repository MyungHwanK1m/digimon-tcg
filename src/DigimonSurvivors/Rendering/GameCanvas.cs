using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DigimonSurvivors.Engine;
using DigimonSurvivors.Entities;
using DigimonSurvivors.Models;

namespace DigimonSurvivors.Rendering;

public class GameCanvas : FrameworkElement
{
    private readonly Typeface _fontMain = new(new FontFamily("Segoe UI, Malgun Gothic"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    private readonly Typeface _fontMono = new(new FontFamily("Consolas, Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    public GameEngine? Engine { get; set; }

    public GameCanvas()
    {
        ClipToBounds = true;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0 || Engine == null) return;

        // 1. 배경 어두운 디지털 월드 심연
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(8, 12, 22)), null, new Rect(0, 0, w, h));

        if (Engine.Player == null) return;

        // 카메라 변환 (플레이어 중심 + 화면 흔들림 오프셋)
        Point cam = new(
            Engine.CameraPosition.X + Engine.ScreenShakeOffset.X,
            Engine.CameraPosition.Y + Engine.ScreenShakeOffset.Y
        );
        double halfW = w / 2.0;
        double halfH = h / 2.0;

        dc.PushTransform(new TranslateTransform(halfW - cam.X, halfH - cam.Y));

        // 2. 살아있는 디지털 서킷 및 그리드 배경
        DrawDigitalCircuits(dc, Engine.ArenaBounds, cam, w, h, Engine.GameTime);

        // 3. 아레나 외벽 일렉트릭 네온 배리어
        DrawArenaBarrier(dc, Engine.ArenaBounds, Engine.GameTime);

        // 4. 파괴 가능한 디지털 데이터 모놀리스
        foreach (var mono in Engine.Monoliths)
        {
            DrawMonolith(dc, mono);
        }

        // 5. 특수 필드 아이템 (고기, 자석, 폭탄, 타임프리즈)
        foreach (var drop in Engine.FieldDrops)
        {
            DrawFieldDrop(dc, drop);
        }

        // 6. 지속 장판 (Area Effects)
        foreach (var area in Engine.AreaEffects)
        {
            if (!area.IsAlive) continue;

            string? skillKey = area.Name switch
            {
                "BlizzardZone" => "sorrow_blue",
                "Lightning" => "hammer_spark",
                "GaiaBurst" => "gaia_force",
                "SlashStorm" => "kaiser_nail",
                "HolyZone" => "holy_rod",
                "DarkZone" => "dark_matter",
                "HeavenGate" => "heaven_gate",
                "StarlightZone" => "starlight_explosion",
                "SevenStarsZone" => "strike_of_the_seven_stars",
                "GigaBlastZone" => "giga_blaster",
                "ForbiddenZone" => "forbidden_temptation",
                _ => null
            };

            var skillImg = skillKey != null ? AssetManager.GetSkillSprite(skillKey) : null;
            if (skillImg != null)
            {
                double sz = area.Radius * 2.0;
                dc.DrawImage(skillImg, new Rect(area.Position.X - sz / 2.0, area.Position.Y - sz / 2.0, sz, sz));
            }
            else
            {
                var brush = new RadialGradientBrush(area.VisualColor, Color.FromArgb(0, area.VisualColor.R, area.VisualColor.G, area.VisualColor.B));
                dc.DrawEllipse(brush, null, area.Position, area.Radius, area.Radius);
            }
        }

        // 7. 경험치 보석 (Exp Gems)
        foreach (var gem in Engine.ExpGems)
        {
            DrawExpGem(dc, gem);
        }

        // 8. 파티클 이펙트 (Hit Sparks, Digital Bits, Trails, Shockwaves)
        if (GameSettings.ParticlesEnabled)
        {
            Engine.Particles.Draw(dc);
        }

        // 9. 적 (Enemies)
        foreach (var enemy in Engine.Enemies)
        {
            if (!enemy.IsAlive) continue;
            DrawEnemy(dc, enemy);
        }

        // 10. 플레이어 (Player)
        DrawPlayer(dc, Engine.Player);

        // 11. 투사체 (Projectiles)
        foreach (var proj in Engine.Projectiles)
        {
            if (!proj.IsAlive) continue;
            DrawProjectile(dc, proj);
        }

        // 12. 플로팅 데미지 & 알림 텍스트
        if (GameSettings.DamageNumbersEnabled)
        {
            foreach (var ft in Engine.FloatingTexts)
            {
                if (!ft.IsAlive) continue;
                byte alpha = (byte)(ft.Alpha * 255);
                var color = Color.FromArgb(alpha, ft.Color.R, ft.Color.G, ft.Color.B);
                var text = new FormattedText(ft.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMain, ft.Size, new SolidColorBrush(color), 1.0);
                dc.DrawText(text, new Point(ft.Position.X - text.Width / 2.0, ft.Position.Y));
            }
        }

        dc.Pop(); // 월드 변환 복원

        // ========================== 13. UI HUD & 오버레이 (스크린 고정 좌표) ==========================
        DrawHud(dc, w, h, Engine);

        // 보스 알람 경보 화면 테두리 비네트
        if (Engine.BossWarningTimer > 0)
        {
            DrawBossAlarmVignette(dc, w, h, Engine.BossWarningTimer);
        }
    }

    #region 디지털 월드 배경 & 아레나 배리어
    private void DrawDigitalCircuits(DrawingContext dc, Rect arena, Point cam, double viewW, double viewH, double time)
    {
        double step = 80.0;
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(24, 0, 160, 255)), 1.0);
        gridPen.Freeze();

        double startX = Math.Max(arena.Left, Math.Floor((cam.X - viewW / 2.0) / step) * step);
        double endX = Math.Min(arena.Right, Math.Ceiling((cam.X + viewW / 2.0) / step) * step);
        double startY = Math.Max(arena.Top, Math.Floor((cam.Y - viewH / 2.0) / step) * step);
        double endY = Math.Min(arena.Bottom, Math.Ceiling((cam.Y + viewH / 2.0) / step) * step);

        for (double x = startX; x <= endX; x += step)
        {
            dc.DrawLine(gridPen, new Point(x, arena.Top), new Point(x, arena.Bottom));
        }
        for (double y = startY; y <= endY; y += step)
        {
            dc.DrawLine(gridPen, new Point(arena.Left, y), new Point(arena.Right, y));
        }

        // 회로를 따라 이동하는 빛의 펄스 노드 점
        var pulseBrush = new SolidColorBrush(Color.FromArgb(80, 0, 229, 255));
        pulseBrush.Freeze();
        double pulseCycle = (time * 120.0) % (step * 4.0);

        for (double x = startX; x <= endX; x += step * 3.0)
        {
            double py = arena.Top + pulseCycle;
            if (py >= startY && py <= endY)
            {
                dc.DrawEllipse(pulseBrush, null, new Point(x, py), 3.0, 3.0);
            }
        }
    }

    private void DrawArenaBarrier(DrawingContext dc, Rect arena, double time)
    {
        byte pulseAlpha = (byte)(140 + Math.Sin(time * 6.0) * 60);
        var glowPen = new Pen(new SolidColorBrush(Color.FromArgb(pulseAlpha, 0, 229, 255)), 4.0);
        glowPen.Freeze();
        dc.DrawRectangle(null, glowPen, arena);

        var innerPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(pulseAlpha * 0.4), 0, 255, 200)), 12.0);
        innerPen.Freeze();
        dc.DrawRectangle(null, innerPen, arena);
    }
    #endregion

    #region 모놀리스 & 특수 필드 드롭 아이템
    private void DrawMonolith(DrawingContext dc, MonolithEntity mono)
    {
        double pulse = Math.Sin(mono.PulseTimer * 4.0) * 0.15 + 0.85;
        var baseBrush = new SolidColorBrush(Color.FromArgb((byte)(pulse * 180), 0, 180, 255));
        var glowBrush = new RadialGradientBrush(Color.FromArgb(90, 0, 229, 255), Color.FromArgb(0, 0, 229, 255));

        dc.DrawEllipse(glowBrush, null, mono.Position, mono.Radius * 1.8, mono.Radius * 1.8);

        // 다면체 크리스탈 오벨리스크 드로잉
        double hw = mono.Width / 2.0;
        double hh = mono.Height / 2.0;
        var geo = new PathGeometry();
        var fig = new PathFigure { StartPoint = new Point(mono.Position.X, mono.Position.Y - hh), IsClosed = true };
        fig.Segments.Add(new LineSegment(new Point(mono.Position.X + hw, mono.Position.Y), true));
        fig.Segments.Add(new LineSegment(new Point(mono.Position.X, mono.Position.Y + hh), true));
        fig.Segments.Add(new LineSegment(new Point(mono.Position.X - hw, mono.Position.Y), true));
        geo.Figures.Add(fig);

        var pen = new Pen(mono.HitFlashTimer > 0 ? Brushes.White : new SolidColorBrush(Color.FromRgb(0, 240, 255)), 2.0);
        dc.DrawGeometry(baseBrush, pen, geo);

        // 체력바 (손상 시)
        if (mono.CurrentHp < mono.MaxHp)
        {
            double barW = 44;
            double barH = 5;
            double barX = mono.Position.X - barW / 2;
            double barY = mono.Position.Y - hh - 12;
            double ratio = Math.Clamp(mono.CurrentHp / mono.MaxHp, 0.0, 1.0);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(180, 20, 20, 20)), null, new Rect(barX, barY, barW, barH));
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0, 229, 255)), null, new Rect(barX, barY, barW * ratio, barH));
        }
    }

    private void DrawFieldDrop(DrawingContext dc, FieldDropEntity drop)
    {
        double bobbing = Math.Sin(drop.FloatTimer * 6.0) * 4.0;
        Point pos = new(drop.Position.X, drop.Position.Y + bobbing);

        Color mainCol = drop.Type switch
        {
            FieldItemType.Meat => Color.FromRgb(255, 82, 82),
            FieldItemType.Magnet => Color.FromRgb(33, 150, 243),
            FieldItemType.Bomb => Color.FromRgb(255, 110, 64),
            FieldItemType.TimeFreeze => Color.FromRgb(0, 229, 255),
            _ => Colors.White
        };

        // 회전 후광
        var glow = new RadialGradientBrush(mainCol, Color.FromArgb(0, mainCol.R, mainCol.G, mainCol.B));
        dc.DrawEllipse(glow, null, pos, drop.Radius * 1.6, drop.Radius * 1.6);

        // 아이콘 뱃지
        string iconStr = drop.Type switch
        {
            FieldItemType.Meat => "🍖",
            FieldItemType.Magnet => "🧲",
            FieldItemType.Bomb => "💣",
            FieldItemType.TimeFreeze => "⏱️",
            _ => "✨"
        };

        var text = new FormattedText(iconStr, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMain, 20, Brushes.White, 1.0);
        dc.DrawText(text, new Point(pos.X - text.Width / 2.0, pos.Y - text.Height / 2.0));
    }
    #endregion

    #region 엔티티 드로잉 (적, 플레이어, 투사체, 보석)
    private void DrawExpGem(DrawingContext dc, ExpGemEntity gem)
    {
        Color c = gem.Type switch
        {
            ExpGemType.SmallBit => Color.FromRgb(40, 200, 255),
            ExpGemType.MediumBit => Color.FromRgb(40, 255, 120),
            ExpGemType.LargeBit => Color.FromRgb(255, 215, 0),
            ExpGemType.DigiEggBox => Color.FromRgb(255, 80, 200),
            _ => Colors.Cyan
        };

        double size = gem.Type switch
        {
            ExpGemType.SmallBit => 6.0,
            ExpGemType.MediumBit => 9.0,
            ExpGemType.LargeBit => 13.0,
            ExpGemType.DigiEggBox => 20.0,
            _ => 7.0
        };

        double pulse = 1.0 + Math.Sin(gem.SparkleTimer * 8.0) * 0.15;
        double s = size * pulse;

        var brush = new SolidColorBrush(c);
        var pen = new Pen(new SolidColorBrush(Colors.White), 1.0);

        var geo = new PathGeometry();
        var fig = new PathFigure { StartPoint = new Point(gem.Position.X, gem.Position.Y - s), IsClosed = true };
        fig.Segments.Add(new LineSegment(new Point(gem.Position.X + s, gem.Position.Y), true));
        fig.Segments.Add(new LineSegment(new Point(gem.Position.X, gem.Position.Y + s), true));
        fig.Segments.Add(new LineSegment(new Point(gem.Position.X - s, gem.Position.Y), true));
        geo.Figures.Add(fig);

        dc.DrawGeometry(brush, pen, geo);
    }

    private void DrawEnemy(DrawingContext dc, EnemyEntity enemy)
    {
        // 그림자
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), null,
            new Point(enemy.Position.X, enemy.Position.Y + enemy.Radius * 0.8), enemy.Radius * 0.9, enemy.Radius * 0.4);

        // 보스/미니보스 전용 회전 오라
        if (enemy.IsBoss || enemy.IsMiniBoss)
        {
            Color auraCol = enemy.IsBoss ? Color.FromRgb(255, 30, 30) : Color.FromRgb(180, 40, 255);
            var auraBrush = new RadialGradientBrush(Color.FromArgb(120, auraCol.R, auraCol.G, auraCol.B), Color.FromArgb(0, auraCol.R, auraCol.G, auraCol.B));
            dc.DrawEllipse(auraBrush, null, enemy.Position, enemy.Radius * 1.7, enemy.Radius * 1.7);
        }

        var sprite = AssetManager.GetSprite(enemy.SpriteFileName);
        double drawSize = enemy.Radius * 2.8;
        Rect drawRect = new(enemy.Position.X - drawSize / 2.0, enemy.Position.Y - drawSize / 2.0, drawSize, drawSize);

        if (sprite != null)
        {
            if (enemy.HitFlashTimer > 0)
            {
                // 피격 시 강력한 발광 실루엣
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(190, 255, 80, 80)), null, enemy.Position, enemy.Radius * 1.4, enemy.Radius * 1.4);
            }

            dc.DrawImage(sprite, drawRect);

            // 빙결 상태 얼음 오버레이
            if (enemy.FreezeTimer > 0)
            {
                var iceBrush = new SolidColorBrush(Color.FromArgb(140, 0, 229, 255));
                dc.DrawEllipse(iceBrush, new Pen(Brushes.White, 2.0), enemy.Position, enemy.Radius * 1.1, enemy.Radius * 1.1);
            }
        }
        else
        {
            var fallbackBrush = new SolidColorBrush(enemy.IsBoss ? Color.FromRgb(220, 40, 40) : Color.FromRgb(160, 40, 200));
            dc.DrawEllipse(fallbackBrush, new Pen(Brushes.White, 2.0), enemy.Position, enemy.Radius, enemy.Radius);
        }

        // HP 바 (피격되었거나 보스인 경우 표시)
        if (enemy.CurrentHp < enemy.MaxHp || enemy.IsBoss || enemy.IsMiniBoss)
        {
            double barW = enemy.Radius * 2.4;
            double barH = enemy.IsBoss ? 8.0 : 4.5;
            double barX = enemy.Position.X - barW / 2.0;
            double barY = enemy.Position.Y - enemy.Radius - 14.0;
            double hpRatio = Math.Clamp(enemy.CurrentHp / enemy.MaxHp, 0.0, 1.0);

            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(180, 20, 20, 20)), null, new Rect(barX, barY, barW, barH));
            dc.DrawRectangle(new SolidColorBrush(enemy.IsBoss ? Color.FromRgb(255, 60, 60) : Color.FromRgb(255, 180, 0)), null, new Rect(barX, barY, barW * hpRatio, barH));

            if (enemy.IsBoss || enemy.IsMiniBoss)
            {
                var nameText = new FormattedText(enemy.Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMain, 12, Brushes.White, 1.0);
                dc.DrawText(nameText, new Point(enemy.Position.X - nameText.Width / 2.0, barY - 16.0));
            }
        }
    }

    private void DrawPlayer(DrawingContext dc, PlayerEntity player)
    {
        // 그림자
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)), null,
            new Point(player.Position.X, player.Position.Y + player.Radius * 0.85), player.Radius * 1.1, player.Radius * 0.45);

        // 대시 쿨타임 인디케이터 (플레이어 발밑 사이버 링)
        double cdRatio = Math.Clamp(1.0 - (player.DashCooldownTimer / player.DashCooldownMax), 0.0, 1.0);
        Color ringCol = cdRatio >= 1.0 ? Color.FromArgb(180, 0, 229, 255) : Color.FromArgb(90, 100, 120, 140);
        var ringPen = new Pen(new SolidColorBrush(ringCol), 2.0);
        ringPen.Freeze();
        dc.DrawEllipse(null, ringPen, player.Position, player.Radius * 1.25, player.Radius * 0.7);

        double bobbing = player.IsMoving ? Math.Sin(player.WalkCycleTimer) * 4.0 : 0.0;
        var sprite = AssetManager.GetSprite(player.Profile.SpriteFileName);
        double drawSize = player.Radius * 3.2;
        Rect drawRect = new(player.Position.X - drawSize / 2.0, player.Position.Y - drawSize / 2.0 + bobbing, drawSize, drawSize);

        // 무적 시간 깜빡임
        if (player.InvincibleTimer > 0 && ((int)(player.InvincibleTimer * 20.0) % 2 == 0))
        {
            // Blink skip
        }
        else
        {
            if (sprite != null)
            {
                if (player.FacingDirection < 0)
                {
                    dc.PushTransform(new ScaleTransform(-1, 1, player.Position.X, player.Position.Y));
                }

                dc.DrawImage(sprite, drawRect);

                if (player.FacingDirection < 0)
                {
                    dc.Pop();
                }
            }
            else
            {
                dc.DrawEllipse(Brushes.OrangeRed, new Pen(Brushes.White, 3.0), player.Position, player.Radius, player.Radius);
            }
        }

        // 플레이어 머리 위 레벨 뱃지
        string badgeText = $"Lv.{player.Level} {player.Profile.Name}";
        var fmtBadge = new FormattedText(badgeText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMain, 12, Brushes.Gold, 1.0);
        double badgeY = player.Position.Y - player.Radius - 18.0 + bobbing;
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), null,
            new Rect(player.Position.X - fmtBadge.Width / 2.0 - 4, badgeY - 2, fmtBadge.Width + 8, fmtBadge.Height + 4));
        dc.DrawText(fmtBadge, new Point(player.Position.X - fmtBadge.Width / 2.0, badgeY));
    }

    private void DrawProjectile(DrawingContext dc, ProjectileEntity proj)
    {
        var skillSprite = AssetManager.GetSkillSprite(proj.SkillId);
        if (skillSprite != null)
        {
            double drawSize = Math.Max(proj.Radius * 2.8, 36.0);
            double angleDeg = 0;
            if (proj.Velocity.LengthSquared > 0.01)
            {
                angleDeg = Math.Atan2(proj.Velocity.Y, proj.Velocity.X) * 180.0 / Math.PI;
            }

            dc.PushTransform(new RotateTransform(angleDeg, proj.Position.X, proj.Position.Y));
            dc.DrawImage(skillSprite, new Rect(proj.Position.X - drawSize / 2.0, proj.Position.Y - drawSize / 2.0, drawSize, drawSize));
            dc.Pop();
        }
        else
        {
            var brush = new SolidColorBrush(proj.ParticleColor);
            var glowBrush = new RadialGradientBrush(proj.ParticleColor, Color.FromArgb(0, proj.ParticleColor.R, proj.ParticleColor.G, proj.ParticleColor.B));

            dc.DrawEllipse(glowBrush, null, proj.Position, proj.Radius * 1.8, proj.Radius * 1.8);
            dc.DrawEllipse(new SolidColorBrush(Colors.White), null, proj.Position, proj.Radius * 0.5, proj.Radius * 0.5);
            dc.DrawEllipse(brush, new Pen(Brushes.White, 1.0), proj.Position, proj.Radius, proj.Radius);
        }
    }
    #endregion

    #region UI HUD & 홀로그램 미니맵 레이더
    private void DrawHud(DrawingContext dc, double viewW, double viewH, GameEngine engine)
    {
        var player = engine.Player;
        if (player == null) return;

        // 1. 상단 경험치 바 (전폭)
        double expBarH = 14.0;
        double expRatio = Math.Clamp((double)player.CurrentExp / player.NextExp, 0.0, 1.0);

        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(200, 15, 20, 35)), null, new Rect(0, 0, viewW, expBarH));
        var expGradient = new LinearGradientBrush(Color.FromRgb(0, 190, 255), Color.FromRgb(40, 255, 180), 0.0);
        dc.DrawRectangle(expGradient, null, new Rect(0, 0, viewW * expRatio, expBarH));

        var expText = new FormattedText($"LV {player.Level}  ({player.CurrentExp} / {player.NextExp})",
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMono, 11, Brushes.White, 1.0);
        dc.DrawText(expText, new Point(viewW / 2.0 - expText.Width / 2.0, 1));

        // 2. 좌상단 플레이어 프로필 패널
        double panelX = 20;
        double panelY = 24;
        double panelW = 280;
        double panelH = 75;

        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(210, 12, 18, 30)),
            new Pen(new SolidColorBrush(Color.FromArgb(100, 0, 180, 255)), 1.5),
            new Rect(panelX, panelY, panelW, panelH), 8, 8);

        // 디지몬 초상화
        var portrait = AssetManager.GetSprite(player.Profile.SpriteFileName);
        Rect portraitRect = new(panelX + 8, panelY + 8, 58, 58);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), null, portraitRect, 6, 6);
        if (portrait != null)
        {
            dc.DrawImage(portrait, portraitRect);
        }

        string stageLabel = player.Profile.Stage switch
        {
            DigimonStage.Rookie => "성장기",
            DigimonStage.Champion => "성숙기",
            DigimonStage.Ultimate => "완전체",
            DigimonStage.Mega => "궁극체",
            DigimonStage.Ultra => "초궁극체",
            _ => "디지몬"
        };

        var nameText = new FormattedText($"{player.Profile.Name} [{stageLabel}]",
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMain, 14, Brushes.Gold, 1.0);
        dc.DrawText(nameText, new Point(panelX + 74, panelY + 10));

        // HP 게이지 바
        double hpW = 190;
        double hpH = 12;
        double hpX = panelX + 74;
        double hpY = panelY + 34;
        double hpRatio = Math.Clamp(player.CurrentHp / player.MaxHp, 0.0, 1.0);

        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(180, 30, 30, 30)), null, new Rect(hpX, hpY, hpW, hpH), 4, 4);
        var hpBrush = new LinearGradientBrush(Color.FromRgb(255, 60, 60), Color.FromRgb(255, 140, 20), 0.0);
        dc.DrawRoundedRectangle(hpBrush, null, new Rect(hpX, hpY, hpW * hpRatio, hpH), 4, 4);

        var hpNumText = new FormattedText($"{(int)player.CurrentHp} / {(int)player.MaxHp}",
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMono, 10, Brushes.White, 1.0);
        dc.DrawText(hpNumText, new Point(hpX + hpW / 2.0 - hpNumText.Width / 2.0, hpY - 1));

        // DP 및 SPD 표시
        var dpText = new FormattedText($"⚔️ DP: {(int)player.Dp}   ⚡ SPD: {(int)player.Speed}",
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMain, 11, Brushes.LightSkyBlue, 1.0);
        dc.DrawText(dpText, new Point(panelX + 74, panelY + 52));

        // 3. 우상단 시간 & 킬 카운트
        int minutes = (int)(engine.GameTime / 60.0);
        int seconds = (int)(engine.GameTime % 60.0);
        string timeStr = $"⏱️ {minutes:00}:{seconds:00}";

        var timeText = new FormattedText(timeStr, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMono, 24, Brushes.White, 1.0);
        dc.DrawText(timeText, new Point(viewW - timeText.Width - 170, 24));

        var killText = new FormattedText($"💀 {player.KillCount}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMono, 16, Brushes.OrangeRed, 1.0);
        dc.DrawText(killText, new Point(viewW - killText.Width - 170, 56));

        // 4. 우상단 홀로그램 미니맵 레이더
        if (GameSettings.MinimapEnabled)
        {
            DrawMinimapRadar(dc, viewW - 140, 22, 120, engine);
        }

        // 5. 상단 보스 경고 배너
        if (engine.BossWarningTimer > 0 && !string.IsNullOrEmpty(engine.BossWarningText))
        {
            double warnW = 540;
            double warnH = 42;
            double warnX = viewW / 2.0 - warnW / 2.0;
            double warnY = 24;

            double pulse = Math.Sin(engine.BossWarningTimer * 12.0) * 0.15 + 0.85;
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb((byte)(pulse * 230), 220, 20, 20)),
                new Pen(Brushes.Yellow, 2.5), new Rect(warnX, warnY, warnW, warnH), 6, 6);

            var warnFormatted = new FormattedText(engine.BossWarningText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMain, 15, Brushes.White, 1.0);
            dc.DrawText(warnFormatted, new Point(viewW / 2.0 - warnFormatted.Width / 2.0, warnY + warnH / 2.0 - warnFormatted.Height / 2.0));
        }

        // 6. 좌하단 보유 무기 및 대시(SPACE) 상태 바
        double slotX = 20;
        double slotY = viewH - 58;
        double slotSize = 46;
        double slotGap = 8;

        // 대시 슬롯
        Rect dashRect = new(slotX, slotY, slotSize * 1.5, slotSize);
        double dashRatio = Math.Clamp(1.0 - (player.DashCooldownTimer / player.DashCooldownMax), 0.0, 1.0);
        bool dashReady = dashRatio >= 1.0;

        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(220, 15, 24, 40)),
            new Pen(new SolidColorBrush(dashReady ? Color.FromRgb(0, 229, 255) : Color.FromRgb(80, 90, 100)), 2.0),
            dashRect, 6, 6);

        // 대시 프로그레스 채우기
        if (!dashReady)
        {
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(90, 0, 180, 255)), null,
                new Rect(dashRect.X + 2, dashRect.Bottom - (slotSize - 4) * dashRatio, dashRect.Width - 4, (slotSize - 4) * dashRatio), 4, 4);
        }

        var dashText = new FormattedText(dashReady ? "⚡ DASH\n[SPACE]" : $"DASH\n{player.DashCooldownTimer:0.0}s",
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMain, 10, dashReady ? Brushes.Cyan : Brushes.Gray, 1.0)
        {
            TextAlignment = TextAlignment.Center
        };
        dc.DrawText(dashText, new Point(dashRect.X + dashRect.Width / 2.0, dashRect.Y + 8));

        // 고유 시그니처 무기 슬롯
        double wpnStartX = dashRect.Right + 12;
        var sigWpn = player.SignatureWeapon;
        if (sigWpn != null)
        {
            Rect sigRect = new(wpnStartX, slotY, slotSize, slotSize);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(230, 35, 25, 10)),
                new Pen(Brushes.Gold, 2.5), sigRect, 6, 6);

            var cardImg = AssetManager.GetCardImage(sigWpn.Definition.IconCardCode) ?? AssetManager.GetSkillSprite(sigWpn.Definition.Id);
            if (cardImg != null)
            {
                dc.DrawImage(cardImg, new Rect(sigRect.X + 2, sigRect.Y + 2, slotSize - 4, slotSize - 4));
            }

            var sigBadge = new FormattedText("고유", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMain, 9, Brushes.Gold, 1.0);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(220, 0, 0, 0)), null, new Rect(sigRect.X + 2, sigRect.Y + 2, sigBadge.Width + 4, sigBadge.Height + 2));
            dc.DrawText(sigBadge, new Point(sigRect.X + 4, sigRect.Y + 3));

            var lvlText = new FormattedText($"Lv{sigWpn.Level}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMono, 10, Brushes.Yellow, 1.0);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)), null,
                new Rect(sigRect.Right - lvlText.Width - 2, sigRect.Bottom - lvlText.Height - 1, lvlText.Width + 2, lvlText.Height + 1));
            dc.DrawText(lvlText, new Point(sigRect.Right - lvlText.Width - 1, sigRect.Bottom - lvlText.Height));

            wpnStartX = sigRect.Right + slotGap;
        }

        // 서브 액티브 무기 슬롯들 (최대 5개)
        for (int i = 0; i < PlayerEntity.MaxActiveSkills; i++)
        {
            Rect slotRect = new(wpnStartX + i * (slotSize + slotGap), slotY, slotSize, slotSize);
            if (i < player.ActiveWeapons.Count)
            {
                var wpn = player.ActiveWeapons[i];
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(200, 20, 25, 40)),
                    new Pen(new SolidColorBrush(wpn.GetColor()), 1.5), slotRect, 6, 6);

                var cardImg = AssetManager.GetCardImage(wpn.Definition.IconCardCode) ?? AssetManager.GetSkillSprite(wpn.Definition.Id);
                if (cardImg != null)
                {
                    dc.DrawImage(cardImg, new Rect(slotRect.X + 2, slotRect.Y + 2, slotSize - 4, slotSize - 4));
                }

                var lvlText = new FormattedText($"Lv{wpn.Level}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMono, 10, Brushes.Yellow, 1.0);
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)), null,
                    new Rect(slotRect.Right - lvlText.Width - 2, slotRect.Bottom - lvlText.Height - 1, lvlText.Width + 2, lvlText.Height + 1));
                dc.DrawText(lvlText, new Point(slotRect.Right - lvlText.Width - 1, slotRect.Bottom - lvlText.Height));
            }
            else
            {
                // 빈 슬롯
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(80, 15, 20, 30)),
                    new Pen(new SolidColorBrush(Color.FromArgb(60, 100, 120, 140)), 1.0), slotRect, 6, 6);
            }
        }
    }

    private void DrawMinimapRadar(DrawingContext dc, double x, double y, double size, GameEngine engine)
    {
        double radius = size / 2.0;
        Point center = new(x + radius, y + radius);

        // 레이더 배경
        var radarBg = new RadialGradientBrush(Color.FromArgb(220, 10, 20, 35), Color.FromArgb(240, 5, 12, 22));
        dc.DrawEllipse(radarBg, new Pen(new SolidColorBrush(Color.FromArgb(120, 0, 229, 255)), 1.5), center, radius, radius);

        // 십자선
        var linePen = new Pen(new SolidColorBrush(Color.FromArgb(40, 0, 229, 255)), 1.0);
        dc.DrawLine(linePen, new Point(center.X - radius, center.Y), new Point(center.X + radius, center.Y));
        dc.DrawLine(linePen, new Point(center.X, center.Y - radius), new Point(center.X, center.Y + radius));

        if (engine.Player == null) return;

        double radarRange = 1200.0; // 레이더 감지 반경
        double scale = (radius - 6.0) / radarRange;

        // 적 표시
        var enemyBrush = new SolidColorBrush(Color.FromRgb(255, 60, 60));
        var bossBrush = new SolidColorBrush(Color.FromRgb(255, 215, 0));

        foreach (var e in engine.Enemies)
        {
            if (!e.IsAlive) continue;
            double dx = e.Position.X - engine.Player.Position.X;
            double dy = e.Position.Y - engine.Player.Position.Y;
            if (dx * dx + dy * dy <= radarRange * radarRange)
            {
                Point dot = new(center.X + dx * scale, center.Y + dy * scale);
                if (e.IsBoss || e.IsMiniBoss)
                {
                    dc.DrawEllipse(bossBrush, new Pen(Brushes.White, 1.0), dot, 4.0, 4.0);
                }
                else
                {
                    dc.DrawEllipse(enemyBrush, null, dot, 2.0, 2.0);
                }
            }
        }

        // 필드 드롭 아이템 표시
        var meatBrush = new SolidColorBrush(Color.FromRgb(76, 175, 80));
        foreach (var d in engine.FieldDrops)
        {
            double dx = d.Position.X - engine.Player.Position.X;
            double dy = d.Position.Y - engine.Player.Position.Y;
            if (dx * dx + dy * dy <= radarRange * radarRange)
            {
                dc.DrawEllipse(meatBrush, null, new Point(center.X + dx * scale, center.Y + dy * scale), 3.0, 3.0);
            }
        }

        // 중앙 플레이어 (청록색)
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0, 229, 255)), new Pen(Brushes.White, 1.5), center, 3.5, 3.5);
    }

    private void DrawBossAlarmVignette(DrawingContext dc, double w, double h, double timer)
    {
        double pulse = (Math.Sin(timer * 14.0) + 1.0) * 0.5;
        byte alpha = (byte)(pulse * 100);

        var borderBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops = new GradientStopCollection
            {
                new(Color.FromArgb(alpha, 255, 0, 0), 0.0),
                new(Color.FromArgb(0, 255, 0, 0), 0.15),
                new(Color.FromArgb(0, 255, 0, 0), 0.85),
                new(Color.FromArgb(alpha, 255, 0, 0), 1.0)
            }
        };

        dc.DrawRectangle(borderBrush, null, new Rect(0, 0, w, h));
    }
    #endregion
}
