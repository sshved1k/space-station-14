using Content.Shared.Changeling;
using Content.Shared.Chemistry.Components;
using Content.Shared.Cuffs.Components;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs;
using Content.Shared.Store.Components;
using Content.Shared.Popups;
using Content.Shared.Damage;
using Robust.Shared.Prototypes;
using Content.Shared.Damage.Prototypes;
using Content.Server.Objectives.Components;
using Content.Server.Light.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Server.Flash.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Stealth.Components;
using Content.Shared.Damage.Components;
using Content.Server.Radio.Components;
using Content.Shared._Sunrise.CollectiveMind;
using Content.Shared._RMC14.Xenonids.Screech;
using Content.Shared.Coordinates;

namespace Content.Server.Changeling;

public sealed partial class ChangelingSystem : EntitySystem
{
    public void SubscribeAbilities()
    {
        SubscribeLocalEvent<ChangelingComponent, OpenEvolutionMenuEvent>(OnOpenEvolutionMenu);
        SubscribeLocalEvent<ChangelingComponent, AbsorbDNAEvent>(OnAbsorb);
        SubscribeLocalEvent<ChangelingComponent, AbsorbDNADoAfterEvent>(OnAbsorbDoAfter);
        SubscribeLocalEvent<ChangelingComponent, StingExtractDNAEvent>(OnStingExtractDNA);
        SubscribeLocalEvent<ChangelingComponent, ChangelingTransformCycleEvent>(OnTransformCycle);
        SubscribeLocalEvent<ChangelingComponent, ChangelingTransformEvent>(OnTransform);
        SubscribeLocalEvent<ChangelingComponent, EnterStasisEvent>(OnEnterStasis);
        SubscribeLocalEvent<ChangelingComponent, ExitStasisEvent>(OnExitStasis);

        SubscribeLocalEvent<ChangelingComponent, ToggleArmbladeEvent>(OnToggleArmblade);
        SubscribeLocalEvent<ChangelingComponent, CreateBoneShardEvent>(OnCreateBoneShard);
        SubscribeLocalEvent<ChangelingComponent, ToggleChitinousArmorEvent>(OnToggleArmor);
        SubscribeLocalEvent<ChangelingComponent, ToggleOrganicShieldEvent>(OnToggleShield);
        SubscribeLocalEvent<ChangelingComponent, ShriekDissonantEvent>(OnShriekDissonant);
        SubscribeLocalEvent<ChangelingComponent, ShriekResonantEvent>(OnShriekResonant);
        SubscribeLocalEvent<ChangelingComponent, ToggleStrainedMusclesEvent>(OnToggleStrainedMuscles);

        SubscribeLocalEvent<ChangelingComponent, StingBlindEvent>(OnStingBlind);
        SubscribeLocalEvent<ChangelingComponent, StingCryoEvent>(OnStingCryo);
        SubscribeLocalEvent<ChangelingComponent, StingLethargicEvent>(OnStingLethargic);
        SubscribeLocalEvent<ChangelingComponent, StingMuteEvent>(OnStingMute);
        SubscribeLocalEvent<ChangelingComponent, StingTransformEvent>(OnStingTransform);
        SubscribeLocalEvent<ChangelingComponent, StingFakeArmbladeEvent>(OnStingFakeArmblade);

        SubscribeLocalEvent<ChangelingComponent, ActionAnatomicPanaceaEvent>(OnAnatomicPanacea);
        SubscribeLocalEvent<ChangelingComponent, ActionAugmentedEyesightEvent>(OnAugmentedEyesight);
        SubscribeLocalEvent<ChangelingComponent, ActionBiodegradeEvent>(OnBiodegrade);
        SubscribeLocalEvent<ChangelingComponent, ActionChameleonSkinEvent>(OnChameleonSkin);
        SubscribeLocalEvent<ChangelingComponent, ActionEphedrineOverdoseEvent>(OnEphedrineOverdose);
        SubscribeLocalEvent<ChangelingComponent, ActionFleshmendEvent>(OnHealUltraSwag);
        SubscribeLocalEvent<ChangelingComponent, ActionLastResortEvent>(OnLastResort);
        SubscribeLocalEvent<ChangelingComponent, ActionLesserFormEvent>(OnLesserForm);
        SubscribeLocalEvent<ChangelingComponent, ActionSpacesuitEvent>(OnSpacesuit);
        SubscribeLocalEvent<ChangelingComponent, ActionHivemindAccessEvent>(OnHivemindAccess);
    }

    #region Basic Abilities

    private void OnOpenEvolutionMenu(EntityUid uid, ChangelingComponent comp, ref OpenEvolutionMenuEvent args)
    {
        if (!TryComp<StoreComponent>(uid, out var store))
            return;

        _store.ToggleUi(uid, uid, store);
    }

    private void OnAbsorb(EntityUid uid, ChangelingComponent comp, ref AbsorbDNAEvent args)
    {
        var target = args.Target;

        if (!IsIncapacitated(target))
        {
            _popup.PopupEntity(Loc.GetString("changeling-absorb-fail-incapacitated"), uid, uid);
            return;
        }
        if (HasComp<AbsorbedComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("changeling-absorb-fail-absorbed"), uid, uid);
            return;
        }
        if (!HasComp<AbsorbableComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("changeling-absorb-fail-unabsorbable"), uid, uid);
            return;
        }

        if (!TryUseAbility(uid, comp, args))
            return;

        var popupOthers = Loc.GetString("changeling-absorb-start", ("user", Identity.Entity(uid, EntityManager)), ("target", Identity.Entity(target, EntityManager)));
        _popup.PopupEntity(popupOthers, uid, PopupType.LargeCaution);
        PlayMeatySound(uid, comp);
        var dargs = new DoAfterArgs(EntityManager, uid, TimeSpan.FromSeconds(15), new AbsorbDNADoAfterEvent(), uid, target)
        {
            DistanceThreshold = 1.5f,
            BreakOnDamage = true,
            BreakOnHandChange = false,
            BreakOnMove = true,
            BreakOnWeightlessMove = true,
            AttemptFrequency = AttemptFrequency.StartAndEnd
        };
        _doAfter.TryStartDoAfter(dargs);
    }
    public ProtoId<DamageGroupPrototype> AbsorbedDamageGroup = "Genetic";
    private void OnAbsorbDoAfter(EntityUid uid, ChangelingComponent comp, ref AbsorbDNADoAfterEvent args)
    {
        if (args.Args.Target == null)
            return;

        var target = args.Args.Target.Value;

        if (args.Cancelled || !IsIncapacitated(target) || HasComp<AbsorbedComponent>(target))
            return;

        PlayMeatySound(args.User, comp);

        UpdateBiomass(uid, comp, comp.MaxBiomass - comp.TotalAbsorbedEntities);

        var dmg = new DamageSpecifier(_proto.Index(AbsorbedDamageGroup), 180);
        _damage.TryChangeDamage(target, dmg, false, false);
        _blood.ChangeBloodReagent(target, "FerrochromicAcid");
        _blood.SpillAllSolutions(target);

        EnsureComp<AbsorbedComponent>(target);

        var popup = Loc.GetString("changeling-absorb-end-self-ling");
        var bonusChemicals = 0f;
        var bonusEvolutionPoints = 0f;
        if (TryComp<ChangelingComponent>(target, out var targetComp))
        {
            bonusChemicals += targetComp.MaxChemicals / 2;
            bonusEvolutionPoints += 10;
            comp.MaxBiomass += targetComp.MaxBiomass / 2;
        }
        else
        {
            popup = Loc.GetString("changeling-absorb-end-self");
            bonusChemicals += 10;
            bonusEvolutionPoints += 2;
        }
        TryStealDNA(uid, target, comp, true);
        comp.TotalAbsorbedEntities++;

        _popup.PopupEntity(popup, args.User, args.User);
        comp.MaxChemicals += bonusChemicals;

        if (TryComp<StoreComponent>(args.User, out var store))
        {
            _store.TryAddCurrency(new Dictionary<string, FixedPoint2> { { "EvolutionPoint", bonusEvolutionPoints } }, args.User, store);
            _store.UpdateUserInterface(args.User, args.User, store);
        }

        if (_mind.TryGetMind(uid, out var mindId, out var mind))
            if (_mind.TryGetObjectiveComp<AbsorbConditionComponent>(mindId, out var objective, mind))
                objective.Absorbed += 1;
    }

    private void OnStingExtractDNA(EntityUid uid, ChangelingComponent comp, ref StingExtractDNAEvent args)
    {
        if (!TrySting(uid, comp, args, true))
            return;

        var target = args.Target;
        if (!TryStealDNA(uid, target, comp, true))
        {
            _popup.PopupEntity(Loc.GetString("changeling-sting-extract-fail"), uid, uid);
            // royal cashback
            comp.Chemicals += Comp<ChangelingActionComponent>(args.Action).ChemicalCost;
        }
        else _popup.PopupEntity(Loc.GetString("changeling-sting", ("target", Identity.Entity(target, EntityManager))), uid, uid);
    }

    private void OnTransformCycle(EntityUid uid, ChangelingComponent comp, ref ChangelingTransformCycleEvent args)
    {
        comp.AbsorbedDNAIndex += 1;
        if (comp.AbsorbedDNAIndex >= comp.MaxAbsorbedDNA || comp.AbsorbedDNAIndex >= comp.AbsorbedDNA.Count)
            comp.AbsorbedDNAIndex = 0;

        if (comp.AbsorbedDNA.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("changeling-transform-cycle-empty"), uid, uid);
            return;
        }

        var selected = comp.AbsorbedDNA.ToArray()[comp.AbsorbedDNAIndex];
        comp.SelectedForm = selected;
        _popup.PopupEntity(Loc.GetString("changeling-transform-cycle", ("target", selected.Name)), uid, uid);
    }
    private void OnTransform(EntityUid uid, ChangelingComponent comp, ref ChangelingTransformEvent args)
    {
        if (!TryUseAbility(uid, comp, args))
            return;

        if (!TryTransform(uid, comp))
            comp.Chemicals += Comp<ChangelingActionComponent>(args.Action).ChemicalCost;
    }

    private void OnEnterStasis(EntityUid uid, ChangelingComponent comp, ref EnterStasisEvent args)
    {
        if (comp.IsInStasis || HasComp<AbsorbedComponent>(uid))
        {
            _popup.PopupEntity(Loc.GetString("changeling-stasis-enter-fail"), uid, uid);
            return;
        }

        if (!TryUseAbility(uid, comp, args))
            return;

        comp.Chemicals = 0f;

        ToggleChameleonSkin(uid, comp, false);

        if (_mobState.IsAlive(uid))
        {
            // fake our death
            var othersMessage = Loc.GetString("suicide-command-default-text-others", ("name", uid));
            _popup.PopupEntity(othersMessage, uid, Robust.Shared.Player.Filter.PvsExcept(uid), true);

            var selfMessage = Loc.GetString("changeling-stasis-enter");
            _popup.PopupEntity(selfMessage, uid, uid);
        }

        if (!_mobState.IsDead(uid))
            _mobState.ChangeMobState(uid, MobState.Dead);

        comp.IsInStasis = true;

        args.Handled = true;
    }
    private void OnExitStasis(EntityUid uid, ChangelingComponent comp, ref ExitStasisEvent args)
    {
        if (!comp.IsInStasis)
        {
            _popup.PopupEntity(Loc.GetString("changeling-stasis-exit-fail"), uid, uid);
            return;
        }
        if (HasComp<AbsorbedComponent>(uid))
        {
            _popup.PopupEntity(Loc.GetString("changeling-stasis-exit-fail-dead"), uid, uid);
            return;
        }

        if (!TryUseAbility(uid, comp, args))
            return;

        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return;

        // heal of everything
        _damage.SetAllDamage(uid, damageable, 0);
        _mobState.ChangeMobState(uid, MobState.Alive);
        _blood.TryModifyBloodLevel(uid, 1000);
        _blood.TryModifyBleedAmount(uid, -1000);

        _popup.PopupEntity(Loc.GetString("changeling-stasis-exit"), uid, uid);

        // Sunrise edit start
        StartScreech(uid);
        // Sunrise edit end

        comp.IsInStasis = false;

        args.Handled = true;
    }

    #endregion

    #region Combat Abilities

    private void OnToggleArmblade(EntityUid uid, ChangelingComponent comp, ref ToggleArmbladeEvent args)
    {
        if (!TryUseAbility(uid, comp, args))
            return;

        if (!TryToggleItem(uid, ArmbladePrototype, comp))
            return;

        PlayMeatySound(uid, comp);
    }
    private void OnCreateBoneShard(EntityUid uid, ChangelingComponent comp, ref CreateBoneShardEvent args)
    {
        if (!TryUseAbility(uid, comp, args))
            return;

        var star = Spawn(BoneShardPrototype, Transform(uid).Coordinates);
        _hands.TryPickupAnyHand(uid, star);

        PlayMeatySound(uid, comp);
    }
    private void OnToggleArmor(EntityUid uid, ChangelingComponent comp, ref ToggleChitinousArmorEvent args)
    {
        if (!TryUseAbility(uid, comp, args))
            return;

        if (!TryToggleItem(uid, ArmorPrototype, comp, "outerClothing")
        || !TryToggleItem(uid, ArmorHelmetPrototype, comp, "head"))
        {
            _popup.PopupEntity(Loc.GetString("changeling-equip-armor-fail"), uid, uid);
            comp.Chemicals += Comp<ChangelingActionComponent>(args.Action).ChemicalCost;
            return;
        }

        PlayMeatySound(uid, comp);
    }
    private void OnToggleShield(EntityUid uid, ChangelingComponent comp, ref ToggleOrganicShieldEvent args)
    {
        if (!TryUseAbility(uid, comp, args))
            return;

        if (!TryToggleItem(uid, ShieldPrototype, comp))
            return;

        PlayMeatySound(uid, comp);
    }
    private void OnShriekDissonant(EntityUid uid, ChangelingComponent comp, ref ShriekDissonantEvent args)
    {
        if (!TryUseAbility(uid, comp, args))
            return;

        DoScreech(uid, comp);

        var pos = _transform.GetMapCoordinates(uid);
        var power = comp.ShriekPower;
        _emp.EmpPulse(pos, power, 5000f, power * 2);
    }
    private void OnShriekResonant(EntityUid uid, ChangelingComponent comp, ref ShriekResonantEvent args)
    {
        if (!TryUseAbility(uid, comp, args))
            return;

        DoScreech(uid, comp);

        var power = comp.ShriekPower;
        _flash.FlashArea(uid, uid, power, power * 2f * 1000f);

        var lookup = _lookup.GetEntitiesInRange(uid, power);
        var lights = GetEntityQuery<PoweredLightComponent>();

        foreach (var ent in lookup)
            if (lights.HasComponent(ent))
                _light.TryDestroyBulb(ent);
    }
    private void OnToggleStrainedMuscles(EntityUid uid, ChangelingComponent comp, ref ToggleStrainedMusclesEvent args)
    {
        if (!TryUseAbility(uid, comp, args) || _gravity.IsWeightless(uid))
            return;

        ToggleStrainedMuscles(uid, comp);
    }
    private void ToggleStrainedMuscles(EntityUid uid, ChangelingComponent comp)
    {
        if (!comp.StrainedMusclesActive)
        {
            _speed.ChangeBaseSpeed(uid, 4f, 7.5f, 20f);
            _popup.PopupEntity(Loc.GetString("changeling-muscles-start"), uid, uid);
            comp.StrainedMusclesActive = true;
        }
        else
        {
            _speed.ChangeBaseSpeed(uid, 2.5f, 4.5f, 20f);
            _popup.PopupEntity(Loc.GetString("changeling-muscles-end"), uid, uid);
            comp.StrainedMusclesActive = false;
        }

        PlayMeatySound(uid, comp);
    }

    #endregion

    #region Stings

    private void OnStingBlind(EntityUid uid, ChangelingComponent comp, ref StingBlindEvent args)
    {
        if (!TrySting(uid, comp, args))
            return;

        var target = args.Target;
        if (!TryComp<BlindableComponent>(target, out var blindable) || blindable.IsBlind)
            return;

        _blindable.AdjustEyeDamage((target, blindable), 2);
        var timeSpan = TimeSpan.FromSeconds(5f);
        _statusEffect.TryAddStatusEffect(target, TemporaryBlindnessSystem.BlindingStatusEffect, timeSpan, false, TemporaryBlindnessSystem.BlindingStatusEffect);
    }
    private void OnStingCryo(EntityUid uid, ChangelingComponent comp, ref StingCryoEvent args)
    {
        var reagents = new List<(string, FixedPoint2)>()
        {
            ("Fresium", 20f),
            ("ChloralHydrate", 10f)
        };

        if (!TryReagentSting(uid, comp, args, reagents))
            return;
    }
    private void OnStingLethargic(EntityUid uid, ChangelingComponent comp, ref StingLethargicEvent args)
    {
        var reagents = new List<(string, FixedPoint2)>()
        {
            ("Impedrezene", 10f),
            ("MuteToxin", 5f)
        };

        if (!TryReagentSting(uid, comp, args, reagents))
            return;
    }
    private void OnStingMute(EntityUid uid, ChangelingComponent comp, ref StingMuteEvent args)
    {
        var reagents = new List<(string, FixedPoint2)>()
        {
            ("MuteToxin", 15f)
        };

        if (!TryReagentSting(uid, comp, args, reagents))
            return;
    }
    private void OnStingTransform(EntityUid uid, ChangelingComponent comp, ref StingTransformEvent args)
    {
        if (!TrySting(uid, comp, args, true))
            return;

        var target = args.Target;
        if (!TryTransform(target, comp, true, true))
            comp.Chemicals += Comp<ChangelingActionComponent>(args.Action).ChemicalCost;
    }
    private void OnStingFakeArmblade(EntityUid uid, ChangelingComponent comp, ref StingFakeArmbladeEvent args)
    {
        if (!TrySting(uid, comp, args))
            return;

        var target = args.Target;
        var fakeArmblade = EntityManager.SpawnEntity(FakeArmbladePrototype, Transform(target).Coordinates);
        if (!_hands.TryPickupAnyHand(target, fakeArmblade))
        {
            QueueDel(fakeArmblade);
            comp.Chemicals += Comp<ChangelingActionComponent>(args.Action).ChemicalCost;
            _popup.PopupEntity(Loc.GetString("changeling-sting-fail-simplemob"), uid, uid);
            return;
        }

        PlayMeatySound(target, comp);
    }

    #endregion

    #region Utilities

    private void OnAnatomicPanacea(EntityUid uid, ChangelingComponent comp, ref ActionAnatomicPanaceaEvent args)
    {
        if (!TryUseAbility(uid, comp, args))
            return;

        var reagents = new List<(string, FixedPoint2)>()
        {
            ("Diphenhydramine", 5f),
            ("Arithrazine", 5f),
            ("Ethylredoxrazine", 5f)
        };
        if (TryInjectReagents(uid, reagents))
            _popup.PopupEntity(Loc.GetString("changeling-panacea"), uid, uid);
        else return;
        PlayMeatySound(uid, comp);
    }

    private void OnAugmentedEyesight(EntityUid uid, ChangelingComponent comp, ref ActionAugmentedEyesightEvent args)
    {
        if (!TryUseAbility(uid, comp, args))
            return;

        if (HasComp<FlashImmunityComponent>(uid))
        {
            _popup.PopupEntity(Loc.GetString("changeling-passive-active"), uid, uid);
            return;
        }

        EnsureComp<FlashImmunityComponent>(uid);
        _popup.PopupEntity(Loc.GetString("changeling-passive-activate"), uid, uid);
    }

    private void OnBiodegrade(EntityUid uid, ChangelingComponent comp, ref ActionBiodegradeEvent args)
    {
        if (!TryUseAbility(uid, comp, args))
            return;

        if (TryComp<CuffableComponent>(uid, out var cuffs) && cuffs.Container.ContainedEntities.Count > 0)
        {
            var cuff = cuffs.LastAddedCuffs;

            _cuffs.Uncuff(uid, cuffs.LastAddedCuffs, cuff);
            QueueDel(cuff);
        }

        var soln = new Solution();
        soln.AddReagent("PolytrinicAcid", 10f);

        if (_pull.IsPulled(uid))
        {
            var puller = Comp<PullableComponent>(uid).Puller;
            if (puller != null)
            {
                _puddle.TrySplashSpillAt((EntityUid) puller, Transform((EntityUid) puller).Coordinates, soln, out _);
                return;
            }
        }
        _puddle.TrySplashSpillAt(uid, Transform(uid).Coordinates, soln, out _);
    }

    private void OnChameleonSkin(EntityUid uid, ChangelingComponent comp, ref ActionChameleonSkinEvent args)
    {
        if (!TryUseAbility(uid, comp, args))
            return;

        if (comp.StealthEnabled && HasComp<StealthComponent>(uid) && HasComp<StealthOnMoveComponent>(uid))
            ToggleChameleonSkin(uid, comp, false);
        else
            ToggleChameleonSkin(uid, comp, true);
    }

    public void ToggleChameleonSkin(EntityUid uid, ChangelingComponent comp, bool toState)
    {
        if (!toState)
        {
            RemComp<StealthComponent>(uid);
            RemComp<StealthOnMoveComponent>(uid);
            _popup.PopupEntity(Loc.GetString("changeling-chameleon-end"), uid, uid);
            comp.StealthEnabled = false;
        }
        else
        {
            EnsureComp<StealthComponent>(uid);
            var stealthonmove = EnsureComp<StealthOnMoveComponent>(uid);
            stealthonmove.PassiveVisibilityRate = -0.37f;
            _popup.PopupEntity(Loc.GetString("changeling-chameleon-start"), uid, uid);
            comp.StealthEnabled = true;
        }
    }

    private void OnEphedrineOverdose(EntityUid uid, ChangelingComponent comp, ref ActionEphedrineOverdoseEvent args)
    {
        if (!TryUseAbility(uid, comp, args))
            return;

        var stam = EnsureComp<StaminaComponent>(uid);
        stam.StaminaDamage = 0;

        var reagents = new List<(string, FixedPoint2)>()
        {
            ("Ephedrine", 15f)
        };
        if (TryInjectReagents(uid, reagents))
            _popup.PopupEntity(Loc.GetString("changeling-inject"), uid, uid);
        else
        {
            _popup.PopupEntity(Loc.GetString("changeling-inject-fail"), uid, uid);
            return;
        }
    }
    // john space made me do this
    private void OnHealUltraSwag(EntityUid uid, ChangelingComponent comp, ref ActionFleshmendEvent args)
    {
        if (!TryUseAbility(uid, comp, args))
            return;

        var reagents = new List<(string, FixedPoint2)>()
        {
            ("Ichor", 10f),
            ("TranexamicAcid", 5f)
        };
        if (TryInjectReagents(uid, reagents))
            _popup.PopupEntity(Loc.GetString("changeling-fleshmend"), uid, uid);
        else
            return;
        PlayMeatySound(uid, comp);
    }
    public void OnLastResort(EntityUid uid, ChangelingComponent comp, ref ActionLastResortEvent args)
    {
        if (!TryUseAbility(uid, comp, args))
            return;

        // todo: implement
    }
    public void OnLesserForm(EntityUid uid, ChangelingComponent comp, ref ActionLesserFormEvent args)
    {
        if (!TryUseAbility(uid, comp, args))
            return;

        var newUid = TransformEntity(uid, protoId: "MobMonkey", comp: comp);
        if (newUid == null)
        {
            comp.Chemicals += Comp<ChangelingActionComponent>(args.Action).ChemicalCost;
            return;
        }

        PlayMeatySound((EntityUid) newUid, comp);
        var loc = Loc.GetString("changeling-transform-others", ("user", Identity.Entity((EntityUid) newUid, EntityManager)));
        _popup.PopupEntity(loc, (EntityUid) newUid, PopupType.LargeCaution);

        comp.IsInLesserForm = true;
    }
    public void OnSpacesuit(EntityUid uid, ChangelingComponent comp, ref ActionSpacesuitEvent args)
    {
        if (!TryUseAbility(uid, comp, args))
            return;

        if (!TryToggleItem(uid, SpacesuitPrototype, comp, "outerClothing")
        || !TryToggleItem(uid, SpacesuitHelmetPrototype, comp, "head"))
        {
            _popup.PopupEntity(Loc.GetString("changeling-equip-armor-fail"), uid, uid);
            comp.Chemicals += Comp<ChangelingActionComponent>(args.Action).ChemicalCost;
            return;
        }

        PlayMeatySound(uid, comp);
    }
    public void OnHivemindAccess(EntityUid uid, ChangelingComponent comp, ref ActionHivemindAccessEvent args)
    {
        if (!TryUseAbility(uid, comp, args))
            return;

        if (HasComp<HivemindComponent>(uid))
        {
            _popup.PopupEntity(Loc.GetString("changeling-passive-active"), uid, uid);
            return;
        }

        EnsureComp<HivemindComponent>(uid);
        var collectiveMindComponent = EnsureComp<CollectiveMindComponent>(uid);
        if (!collectiveMindComponent.Minds.Contains("Changeling"))
            collectiveMindComponent.Minds.Add("Changeling");

        _popup.PopupEntity(Loc.GetString("changeling-hivemind-start"), uid, uid);
    }

    #endregion

    // Sunrise edit start
    private void StartScreech(EntityUid uid, XenoScreechComponent? component = null, bool playSound = true)
    {
        if (!Resolve(uid, ref component))
            return;

        if (playSound)
            _audio.PlayPvs(component.Sound, uid);

        SpawnAttachedTo(component.Effect, uid.ToCoordinates());
    }
    // Sunrise edit end
}
