namespace Dexterity;

public class DefaultEffectService : IEffectService
{
    protected World World { get; }
    private Trigger _trigger = Trigger.Use;
    public DefaultEffectService(World world) => World = world;

    public virtual Result<List<EffectResult>> Attack(int sourceId, int instrumentId, int primaryTargetId, int[]? secondaryTargetIds = null)
        => Action(sourceId, instrumentId, primaryTargetId, secondaryTargetIds, false);

    public virtual Result<List<EffectResult>> Use(int sourceId, int instrumentId, int primaryTargetId, int[]? secondaryTargetIds = null)
        => Action(sourceId, instrumentId, primaryTargetId, secondaryTargetIds, true);

    private Result<List<EffectResult>> Action(int sourceId, int instrumentId, int primaryTargetId, int[]? secondaryTargetIds, bool consume)
    {
        try
        {
            // reject exhausted instruments before calculating or changing gameplay state
            if (World?.Repository == null)
                return EffectRules.Failure<List<EffectResult>>(Result.Fail("A configured world is required.", 400));
            if (CanConsume(instrumentId).Failed(out var eligible))
                return EffectRules.Failure<List<EffectResult>>(eligible);
            if (!eligible.Data)
                return EffectRules.Failure<List<EffectResult>>(Result.Fail("Instrument has no charges to consume.", 409));

            if (Outgoing(sourceId, instrumentId, primaryTargetId, secondaryTargetIds).Failed(out var outgoing))
                return EffectRules.Failure<List<EffectResult>>(outgoing);
            var reports = new List<EffectResult>();
            foreach (var result in outgoing.Data!)
            {
                if (!result.TargetId.HasValue)
                    return EffectRules.Failure<List<EffectResult>>(Result.Fail("Action output must identify a target.", 400));
                if (Incoming(result.TargetId.Value, result).Failed(out var incoming))
                    return EffectRules.Failure<List<EffectResult>>(incoming);
                foreach (var resolved in incoming.Data!)
                {
                    if (!resolved.TargetId.HasValue)
                        return EffectRules.Failure<List<EffectResult>>(Result.Fail("Action output must identify a target.", 400));
                    if (Apply(resolved.TargetId.Value, resolved).Failed(out var applied))
                        return EffectRules.Failure<List<EffectResult>>(applied);
                    reports.Add(EffectRules.Copy(resolved));
                }
            }
            if (consume && Consume(instrumentId).Failed(out var consumed))
                return EffectRules.Failure<List<EffectResult>>(consumed);
            return Result.Ok(reports);
        }
        catch (Exception exception)
        {
            return EffectRules.Failure<List<EffectResult>>(EffectRules.Exception(exception, "Action resolution failed."));
        }
    }

    private Result<List<ModifierInvocation>> Collect(int ownerId, Phase phase, Component? instrument = null, string? typeId = null)
    {
        var candidates = new List<(Component Holder, ModifierData Data, bool Use, int Index)>();
        if (instrument != null)
            candidates.AddRange(instrument.ModifierData.Select((data, i) => (instrument, data, true, i))
                .Where(x => x.data.Scope == Scope.Use && x.data.Phase == phase));
        var holders = EffectRules.Passives(World, ownerId);
        if (instrument != null)
            holders = holders.Concat(EffectRules.Passives(World, instrument.Id));
        foreach (var holder in holders.Distinct())
            candidates.AddRange(holder.ModifierData.Select((data, i) => (holder, data, false, i))
                .Where(x => x.data.Scope == Scope.Passive && x.data.Phase == phase));
        var validator = new WorldBuilder(World);
        var invocations = new List<ModifierInvocation>();
        foreach (var candidate in candidates.Where(x => typeId == null || x.Data.TypeId == typeId))
        {
            if (validator.Validate(candidate.Data).Failed(out var validation))
                return validation;
            invocations.Add(new ModifierInvocation(candidate.Holder, candidate.Data, World.Modifiers.First(x => x.Id == candidate.Data.TypeId), candidate.Use, candidate.Index));
        }
        return Result.Ok(invocations.OrderBy(x => x.Type.Order).ThenBy(x => x.Use ? 0 : 1).ThenBy(x => x.Use ? 0 : x.Holder.Id).ThenBy(x => x.Index).ToList());
    }

    private RelationKind Relation(int owner, int other) => owner == other ? RelationKind.Self : World.Logic.GetRelationKind(owner, other);
    private EffectContext Context(Phase phase, int owner, EffectResult result, ModifierData data, int other, Trigger trigger) => new()
    {
        Phase = phase, Trigger = trigger, OwnerId = owner, SourceId = result.SourceId, TargetId = result.TargetId,
        InstrumentId = result.InstrumentId, EffectTypeId = data.EffectTypeId, Relation = Relation(owner, other)
    };

    public virtual Result<List<EffectResult>> Outgoing(int sourceId, int instrumentId, int primaryTargetId, int[]? secondaryTargetIds = null)
    {
        // validate every endpoint, even when the action will be empty
        var targets = new[] { primaryTargetId }.Concat(secondaryTargetIds ?? Array.Empty<int>()).Distinct().ToArray();
        foreach (var id in new[] { sourceId, instrumentId }.Concat(targets).Distinct())
        {
            if (World.Repository.Get(id).Failed(out var lookup))
                return EffectRules.Failure(lookup);
            if (lookup.Data!.State != ComponentState.Active)
                return Result.Fail($"Component {id} is Deleted and cannot join a new action.", 409);
        }
        var instrument = World.Repository.Items[instrumentId];
        var category = World.Categories.FirstOrDefault(x => x.Id == instrument.CategoryId);
        var ownership = World.Repository.Relations.FirstOrDefault(x => x.ParentId == sourceId && x.TargetId == instrumentId);
        if (category == null || ownership == null || (category.Equipable && ownership.Type != RelationType.Equipped))
            return Result.Fail("The source must own the instrument and equip it when required.", 409);
        if (CanConsume(instrumentId).Failed(out var consumable))
            return EffectRules.Failure(consumable);
        var results = targets.Select(id => new EffectResult { TargetId = id, SourceId = sourceId, InstrumentId = instrumentId }).ToList();
        if (!consumable.Data)
            return Result.Ok(results);
        if (new TagService(World).GetTags(instrumentId).Failed(out var tags))
            return EffectRules.Failure(tags);
        if (Collect(sourceId, Phase.Outgoing, instrument).Failed(out var collected))
            return EffectRules.Failure(collected);

        try
        {
            // A matching Use modifier authorizes passives, even if its amount is zero.
            var primary = results[0];
            var relation = Relation(sourceId, primaryTargetId);
            if (!collected.Data!.Any(x => x.Use && EffectRules.Matches(x.Data, relation, tags.Data!)))
                return Result.Ok(results);
            foreach (var invocation in collected.Data!)
            {
                if (invocation.Type.Id == "steal")
                    continue; // Steal is deferred until Incoming mitigation.
                if (invocation.Type.Id == "splash")
                {
                    foreach (var secondary in results.Skip(1))
                    {
                        var context = Context(Phase.Outgoing, sourceId, secondary, invocation.Data, secondary.TargetId!.Value, Trigger.Use);
                        if (!EffectRules.Matches(invocation.Data, context.Relation, tags.Data!))
                            continue;
                        context.Distance = World.Logic.GetDistance(primaryTargetId, secondary.TargetId.Value);
                        var splash = invocation.Type.Calculate(EffectRules.Copy(primary), invocation.Data, context, invocation.Holder.Id);
                        secondary.Items.AddRange(splash.Items);
                    }
                    continue;
                }
                var primaryContext = Context(Phase.Outgoing, sourceId, primary, invocation.Data, primaryTargetId, Trigger.Use);
                if (!EffectRules.Matches(invocation.Data, primaryContext.Relation, tags.Data!))
                    continue;
                primary = invocation.Type.Calculate(primary, invocation.Data, primaryContext, invocation.Holder.Id);
                results[0] = primary;
            }
            if (results.Any(x => x.Items.Any(i => !EffectRules.Finite(i.Amount))))
                return Result.Fail("Outgoing calculation produced a non-finite amount.", 400);
            return Result.Ok(results);
        }
        catch (Exception exception)
        {
            return EffectRules.Exception(exception, "Outgoing effect calculation failed.");
        }
    }

    private Result ValidateResult(int targetId, EffectResult result)
    {
        if (result == null || result.TargetId != targetId || result.Items == null)
            return Result.Fail("Effect result must identify the supplied target and contain an item list.", 400);
        foreach (var id in new[] { targetId, result.SourceId, result.InstrumentId }.Distinct())
            if (World.Repository.Get(id).Failed(out var lookup))
                return EffectRules.Failure(lookup);
        foreach (var item in result.Items)
        {
            if (item == null || !EffectRules.Finite(item.Amount) || !EffectRules.Finite(item.Duration) || item.Duration < 0 ||
                World.EffectTypes.All(x => x.Id != item.EffectTypeId))
                return Result.Fail("Effect items require a known type, finite amount and non-negative finite duration.", 400);
        }
        return Result.Ok();
    }

    public virtual Result<List<EffectResult>> Incoming(int targetId, EffectResult result)
    {
        if (ValidateResult(targetId, result).Failed(out var validation))
            return validation;
        if (new TagService(World).GetTags(result.InstrumentId).Failed(out var tags))
            return EffectRules.Failure(tags);
        if (Collect(targetId, Phase.Incoming).Failed(out var collected))
            return EffectRules.Failure(collected);
        try
        {
            // Never write mitigation into input reports or scheduled timed amounts.
            var resolved = EffectRules.Copy(result);
            foreach (var invocation in collected.Data!)
            {
                var context = Context(Phase.Incoming, targetId, resolved, invocation.Data, result.SourceId, _trigger);
                if (!EffectRules.Matches(invocation.Data, context.Relation, tags.Data!))
                    continue;
                if (!resolved.Items.Any(x => x.Duration == 0 && x.EffectTypeId == invocation.Data.EffectTypeId))
                    continue;
                // Custom Incoming types also see only the immediate portion.
                var immediate = EffectRules.Copy(resolved, resolved.Items.Where(x => x.Duration == 0));
                var changed = invocation.Type.Calculate(immediate, invocation.Data, context, invocation.Holder.Id);
                resolved = EffectRules.Copy(resolved, changed.Items.Concat(resolved.Items.Where(x => x.Duration > 0)));
            }
            // Finalize target amounts before presentation and before calculating steal.
            if (World.Settings.RoundEffectAmounts && RoundAmounts(resolved).Failed(out var rounded))
                return rounded;
            var results = new List<EffectResult> { resolved };
            if (ResolveSteal(targetId, resolved, tags.Data!).Failed(out var stolen))
                return EffectRules.Failure(stolen);
            results.AddRange(stolen.Data!);
            if (results.SelectMany(x => x.Items).Any(x => !EffectRules.Finite(x.Amount)))
                return Result.Fail("Incoming calculation produced a non-finite amount.", 400);
            if (World.Settings.RoundEffectAmounts)
                foreach (var recipient in stolen.Data!)
                    if (RoundAmounts(recipient).Failed(out var rounding))
                        return rounding;
            return Result.Ok(results);
        }
        catch (Exception exception)
        {
            return EffectRules.Exception(exception, "Incoming effect calculation failed.");
        }
    }

    private Result<List<EffectResult>> ResolveSteal(int targetId, EffectResult result, List<string> tags)
    {
        var instrument = World.Repository.Items[result.InstrumentId];
        if (Collect(result.SourceId, Phase.Outgoing, instrument, "steal").Failed(out var collected))
            return EffectRules.Failure(collected);
        var recipient = new EffectResult { TargetId = result.SourceId, SourceId = result.SourceId, InstrumentId = result.InstrumentId };
        var target = World.Repository.Items[targetId];
        // Damage types sharing a stat also share its available Current budget.
        var damageByType = result.Items.Where(x => x.Duration == 0).GroupBy(x => x.EffectTypeId)
            .ToDictionary(x => x.Key, x => Math.Max(0, -x.Sum(i => i.Amount)));
        var damageByStat = damageByType.GroupBy(x => World.EffectTypes.First(t => t.Id == x.Key).StatTypeId)
            .ToDictionary(x => x.Key, x => x.Sum(i => i.Value));
        foreach (var invocation in collected.Data!)
        {
            var context = Context(Phase.Outgoing, result.SourceId, result, invocation.Data, targetId, _trigger);
            if (!EffectRules.Matches(invocation.Data, context.Relation, tags))
                continue;
            int effectId = invocation.Data.EffectTypeId!.Value;
            int statId = World.EffectTypes.First(x => x.Id == effectId).StatTypeId;
            if (!target.Stats.TryGetValue(statId, out var stat))
                continue;
            if (!damageByType.TryGetValue(effectId, out var raw) || raw == 0)
                continue;
            var damage = raw * Math.Min(1, Math.Max(0, stat.Current) / damageByStat[statId]);
            if (damage == 0)
                continue;
            var basis = EffectRules.Copy(result, new[] { new EffectResultItem { EffectTypeId = effectId, Amount = -damage, SourceId = result.SourceId, InstrumentId = result.InstrumentId } });
            var steal = invocation.Type.Calculate(basis, invocation.Data, context, invocation.Holder.Id);
            recipient.Items.AddRange(steal.Items);
        }
        return Result.Ok(recipient.Items.Count == 0 ? new List<EffectResult>() : new List<EffectResult> { recipient });
    }

    public virtual Result Apply(int targetId, EffectResult result)
    {
        if (ValidateResult(targetId, result).Failed(out var validation))
            return validation;
        var target = World.Repository.Items[targetId];
        // Aggregate all effect types for each stat before optional rounding and clamping.
        var changes = result.Items.Where(x => x.Duration == 0).GroupBy(x => World.EffectTypes.First(t => t.Id == x.EffectTypeId).StatTypeId)
            .ToDictionary(x => x.Key, x => x.Sum(i => i.Amount));
        if (changes.Values.Any(x => !EffectRules.Finite(x)))
            return Result.Fail("Effect totals must be finite.", 400);
        if (World.Settings.RoundEffectAmounts && RoundAmounts(result).Failed(out var rounded))
            return rounded;
        foreach (var pair in changes)
            if (target.Stats.TryGetValue(pair.Key, out var stat))
                stat.Current = EffectRules.Clamp(stat.Current + (World.Settings.RoundEffectAmounts ? Math.Round(pair.Value) : pair.Value), stat.ModifiedMax);
        var timed = result.Items.Where(x => x.Duration > 0).ToArray();
        if (timed.Length > 0)
            target.Effects.Add(EffectRules.Copy(result, timed));
        return Result.Ok();
    }

    /// <summary>Writes whole immediate contributions while preserving Math.Round of each stat's combined amount and all provenance.</summary>
    private Result RoundAmounts(EffectResult result)
    {
        var groups = result.Items.Select((item, index) => (Item: item, Index: index))
            .Where(x => x.Item.Duration == 0)
            .GroupBy(x => World.EffectTypes.First(t => t.Id == x.Item.EffectTypeId).StatTypeId).ToArray();
        if (groups.Any(group => !EffectRules.Finite(group.Sum(x => x.Item.Amount))))
            return Result.Fail("Effect totals must be finite.", 400);
        foreach (var group in groups)
        {
            // Round cumulative totals so individual reports sum to the applied stat delta.
            double total = 0, previous = 0;
            foreach (var entry in group)
            {
                total += entry.Item.Amount;
                var rounded = Math.Round(total);
                result.Items[entry.Index] = entry.Item with { Amount = rounded - previous };
                previous = rounded;
            }
        }
        return Result.Ok();
    }

    public virtual Result<List<EffectResult>> Tick(int targetId, double elapsed)
    {
        if (!EffectRules.Finite(elapsed) || elapsed < 0)
            return Result.Fail("Elapsed time must be finite and non-negative.", 400);
        if (World.Repository.Get(targetId).Failed(out var lookup))
            return EffectRules.Failure(lookup);
        var reports = new List<EffectResult>();
        if (elapsed == 0)
            return Result.Ok(reports);
        var target = lookup.Data!;
        var previousTrigger = _trigger;
        try
        {
            _trigger = Trigger.Tick;
            // Resolve applications separately; only group the reports after mitigation.
            var remaining = elapsed;
            while (remaining > 0)
            {
                if (!target.Effects.Any(x => x.Items.Any(i => i.Duration > 0)))
                {
                    Countdown(target, remaining);
                    break;
                }
                var step = Math.Min(1, remaining);
                foreach (var stored in target.Effects.ToArray())
                {
                    var active = stored.Items.Where(x => x.Duration > 0).Select(x => x with { Duration = 0 }).ToArray();
                    if (active.Length == 0)
                        continue;
                    if (Incoming(targetId, EffectRules.Copy(stored, active)).Failed(out var incoming))
                        return EffectRules.Failure(incoming);
                    foreach (var resolved in incoming.Data!)
                    {
                        if (!resolved.TargetId.HasValue)
                            return Result.Fail("Tick output must identify a target.", 400);
                        if (Apply(resolved.TargetId.Value, resolved).Failed(out var applied))
                            return applied;
                        if (resolved.Items.Count > 0)
                            reports.Add(resolved);
                    }
                }
                foreach (var stored in target.Effects)
                    stored.Items = stored.Items.Where(x => x.Duration > step).Select(x => x with { Duration = x.Duration - step }).ToList();
                target.Effects.RemoveAll(x => x.Items.Count == 0);
                Countdown(target, step);
                remaining -= step;
            }
            if (World.Repository.Cleanup().Failed(out var cleaned))
                return cleaned;
            var grouped = reports.GroupBy(x => (x.TargetId, x.SourceId, x.InstrumentId))
                .Select(group => EffectRules.Copy(group.First(), group.SelectMany(x => x.Items))).ToList();
            return Result.Ok(grouped);
        }
        catch (Exception exception)
        {
            return EffectRules.Exception(exception, "Timed effect resolution failed.");
        }
        finally
        {
            _trigger = previousTrigger;
        }
    }

    private void Countdown(Component target, double elapsed)
    {
        foreach (var pair in target.Stats)
            if (World.Stats.Any(x => x.Id == pair.Key && x.Type == StatKind.Counter))
                pair.Value.Current = Math.Max(0, pair.Value.Current - elapsed);
    }

    public virtual Result Recalculate(int componentId) => StatRecalculation.Apply(World, componentId);

    public virtual Result<bool> CanConsume(int instrumentId)
    {
        if (World.Repository.Get(instrumentId).Failed(out var lookup))
            return EffectRules.Failure(lookup);
        var instrument = lookup.Data!;
        var category = World.Categories.FirstOrDefault(x => x.Id == instrument.CategoryId);
        if (instrument.State != ComponentState.Active || category == null || category.Usable == Usable.No)
            return Result.Fail("Instrument must be Active and usable.", 409);
        if (category.Usable != Usable.Charges)
            return Result.Ok(true);
        if (!World.ChargesStatId.HasValue || !instrument.Stats.TryGetValue(World.ChargesStatId.Value, out var charges) ||
            !World.Stats.Any(x => x.Id == World.ChargesStatId && x.Type == StatKind.Pool) || !EffectRules.Finite(charges.Current))
            return Result.Fail("Instrument lacks a valid charges Pool.", 400);
        return Result.Ok(charges.Current > 0);
    }

    public virtual Result Consume(int instrumentId)
    {
        if (CanConsume(instrumentId).Failed(out var consumable))
            return EffectRules.Failure(consumable);
        if (!consumable.Data)
            return Result.Fail("Instrument has no charges to consume.", 409);
        var instrument = World.Repository.Items[instrumentId];
        var category = World.Categories.First(x => x.Id == instrument.CategoryId);
        if (category.Usable == Usable.Eternal)
            return Result.Ok();
        if (category.Usable == Usable.Charges)
        {
            var charges = instrument.Stats[World.ChargesStatId!.Value];
            charges.Current = Math.Max(0, charges.Current - 1);
            if (charges.Current > 0)
            {
                return Result.Ok();
            }
        }
        if (World.Repository.Delete(instrumentId).Failed(out var deleted))
            return deleted;
        return Result.Ok();
    }
}
