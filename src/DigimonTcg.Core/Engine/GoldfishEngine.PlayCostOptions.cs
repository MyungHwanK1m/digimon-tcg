using System;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;

namespace DigimonTcg.Core.Engine;

/// <summary>
/// 등장할 때 고르는 코스트 감소 ("When this card would be played, by deleting 1 of your Digimon, reduce the play cost by 4.").
/// 카드가 패를 떠난 뒤 · 필드에 놓이기 전에, 낼 수 있는 비용마다 낼지 묻고(플레이어 — AI는 추천대로) 내면 코스트를 줄인다.
/// </summary>
public partial class GoldfishEngine
{
    private int ApplyPlayCostOptions(PlayerZones owner, CardData card, int cost)
    {
        var options = CardScripts.For(card)?.PlayCostOptions;
        if (options == null || options.Count == 0)
            return cost;
        foreach (var option in options)
        {
            if (cost <= 0)
                break;
            var ctx = new EffectContext(this, owner, null, null, null) { Card = card };
            if (!option.CanPay(ctx)
                || !AskYesNo(owner, null, $"[{card.Name}] {option.Describe} 등장 코스트를 {option.Reduce} 줄일까요?", option.Recommend(ctx)))
                continue;
            option.Pay(ctx);
            int reduced = Math.Min(option.Reduce, cost);
            cost -= reduced;
            EffectLog(owner, $"[{card.Name}] 비용을 내고 등장 코스트 -{reduced} (코스트 {cost})");
        }
        return cost;
    }
}
