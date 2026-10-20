using System;
using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Models;

namespace DigimonTcg.Core.Engine.Effects;

/// <summary>
/// 카드 번호 → 효과 정의. 엔진은 카드 번호로 분기하지 않고 여기서 효과를 찾아 처리한다.
/// 지금은 사람이 쓴 스크립트(BuiltInCardScripts)만 있고, 이후 키워드 사전 · 문장 파서가 같은 형식으로 채운다
/// (docs/card_effect_engine_plan.md).
/// </summary>
public static class CardScripts
{
    private static readonly Lazy<Dictionary<string, CardScript>> Registry = new(() =>
    {
        var builder = new CardScriptBuilder();
        BuiltInCardScripts.Register(builder);
        return builder.Scripts;
    });

    public static CardScript? Get(string cardId) =>
        Registry.Value.TryGetValue(cardId, out var script) ? script : null;

    public static IReadOnlyCollection<CardScript> All => Registry.Value.Values;

    /// <summary>디지몬이 키워드를 가졌는지 (맨 위 카드의 키워드 + 진화원 효과로 얻는 키워드)</summary>
    public static bool HasKeyword(CardInstance digimon, Keyword keyword) =>
        (Get(digimon.TopCard.CardId)?.Keywords.Contains(keyword) ?? false)
        || digimon.EvolutionSources.Any(s => Get(s.CardId)?.InheritedKeywords.Contains(keyword) ?? false);

    /// <summary>옵션 [메인] 효과를 쓸 때 고를 대상 종류</summary>
    public static TargetKind MainTargetOf(CardData card) => Get(card.CardId)?.MainTarget ?? TargetKind.None;

    /// <summary>카드에 적힌 다른 진화 조건을 만족하는지 (예: 오메가몬은 Lv.6 레드 · 블루 디지몬에서 진화)</summary>
    public static bool MeetsAlternativeEvolution(CardData evoCard, CardInstance target) =>
        Get(evoCard.CardId)?.AlternativeEvolution?.Invoke(target) ?? false;
}

/// <summary>카드 스크립트를 읽기 쉽게 등록하는 도구</summary>
public sealed class CardScriptBuilder
{
    internal Dictionary<string, CardScript> Scripts { get; } = new(StringComparer.Ordinal);

    public CardScriptDefinition Card(string cardId)
    {
        if (!Scripts.TryGetValue(cardId, out var script))
            Scripts[cardId] = script = new CardScript(cardId);
        return new CardScriptDefinition(script);
    }
}

public sealed class CardScriptDefinition
{
    private readonly CardScript _script;
    internal CardScriptDefinition(CardScript script) => _script = script;

    public CardScriptDefinition On(EffectTrigger trigger, Action<EffectContext> resolve, Func<EffectContext, bool>? when = null, bool oncePerTurn = false)
    {
        _script.Effects.Add(new CardEffect(trigger, EffectSource.Main, resolve) { Condition = when, OncePerTurn = oncePerTurn });
        return this;
    }

    /// <summary>진화원 효과</summary>
    public CardScriptDefinition Inherited(EffectTrigger trigger, Action<EffectContext> resolve, Func<EffectContext, bool>? when = null, bool oncePerTurn = false)
    {
        _script.Effects.Add(new CardEffect(trigger, EffectSource.Inherited, resolve) { Condition = when, OncePerTurn = oncePerTurn });
        return this;
    }

    public CardScriptDefinition OnPlay(Action<EffectContext> resolve, Func<EffectContext, bool>? when = null) => On(EffectTrigger.OnPlay, resolve, when);
    public CardScriptDefinition WhenDigivolving(Action<EffectContext> resolve, Func<EffectContext, bool>? when = null) => On(EffectTrigger.WhenDigivolving, resolve, when);
    public CardScriptDefinition WhenAttacking(Action<EffectContext> resolve, Func<EffectContext, bool>? when = null, bool oncePerTurn = false) => On(EffectTrigger.WhenAttacking, resolve, when, oncePerTurn);
    public CardScriptDefinition OnDeletion(Action<EffectContext> resolve) => On(EffectTrigger.OnDeletion, resolve);
    public CardScriptDefinition Security(Action<EffectContext> resolve) => On(EffectTrigger.Security, resolve);

    /// <summary>옵션 [메인] 효과</summary>
    public CardScriptDefinition Main(TargetKind target, Action<EffectContext> resolve)
    {
        _script.MainTarget = target;
        return On(EffectTrigger.Main, resolve);
    }

    public CardScriptDefinition Static(StaticStat stat, EffectSource source, Func<StaticContext, int> value)
    {
        _script.Statics.Add(new StaticEffect(stat, source, value));
        return this;
    }

    /// <summary>진화원 효과: [자신의 턴] 조건부 DP +N</summary>
    public CardScriptDefinition InheritedDpOnOwnTurn(int dp, Func<StaticContext, bool> when) =>
        Static(StaticStat.Dp, EffectSource.Inherited, c => c.OwnersTurn && when(c) ? dp : 0);

    public CardScriptDefinition Keyword(Keyword keyword)
    {
        _script.Keywords.Add(keyword);
        return this;
    }

    public CardScriptDefinition InheritedKeyword(Keyword keyword)
    {
        _script.InheritedKeywords.Add(keyword);
        return this;
    }

    public CardScriptDefinition EvolvesFrom(Func<CardInstance, bool> condition)
    {
        _script.AlternativeEvolution = condition;
        return this;
    }

    public CardScriptDefinition StartOfTurnMemory(int amount, Func<PlayerZones, bool> enemyCondition, string reason)
    {
        _script.StartOfTurnMemory = new StartOfTurnMemoryEffect(amount, enemyCondition, reason);
        return this;
    }
}
