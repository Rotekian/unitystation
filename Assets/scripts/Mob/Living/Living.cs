﻿using NPC;
using PlayGroup;
using System;
using System.Collections;
using System.Collections.Generic;
using UI;
using UnityEngine;
using UnityEngine.Networking;

public class Living : Mob
{
    // Inspector Properties
    public int InitialMaxHealth = 100;

    #region living_defines.dm

    //Maximum health that should be possible.
    public int maxHealth = 100;
    //A mob's health
    public int health = 100;
    //Brutal damage caused by brute force (punching, being clubbed by a toolbox ect... this also accounts for pressure damage)
    private int bruteLoss = 0;
    //Oxygen depravation damage (no air in lungs)
    private int oxyLoss = 0;
    //Toxic damage caused by being poisoned or radiated
    private int toxLoss = 0;
    //Burn damage caused by being way too hot, too cold or burnt.
    private int fireLoss = 0;
    //Damage caused by being cloned or ejected from the cloner early. slimes also deal cloneloss damage to victims
    private int cloneLoss = 0;
    //'Retardation' damage caused by someone hitting you in the head with a bible or being infected with brainrot.
    private int brainLoss = 0;
    //Stamina damage, or exhaustion. You recover it slowly naturally, and are stunned if it gets too high. Holodeck and hallucinations deal this.
    private int staminaLoss = 0;
    // Butcher drops - see var/list/butcher_results = null
    public List<GameObject> butcherResults = new List<GameObject>();

    #endregion

    // Use this for initialization
    void Start () {
        maxHealth = InitialMaxHealth;
        UpdateHealth();
	}
	
	// Update is called once per frame
	void Update () {

    }

    void OnMouseDown()
    {
        if (UIManager.Hands.CurrentSlot.Item != null && PlayerManager.PlayerInReach(transform))
        {
            if (UIManager.Hands.CurrentSlot.Item.GetComponent<ItemAttributes>().type == ItemType.Knife)
            {
                if (mobStat != MobConsciousStat.DEAD)
                {
                    PlayerManager.LocalPlayerScript.playerNetworkActions.CmdAttackNpc(this.gameObject);
                } else
                {
                    // TODO, please read onClick item_attack.dm for how harvest() is normally handled
                    PlayerManager.LocalPlayerScript.playerNetworkActions.CmdHarvestNpc(this.gameObject);
                }
            }
        }
    }

    #region unityhelpers

    [ClientRpc]
    public void RpcReceiveDamage()
    {
        // TODO read from items damage values etc
        ApplyDamage(50, DamageType.BRUTE, BodyPartType.CHEST);
    }

    [ClientRpc]
    public void RpcHarvest()
    {
        // This needs to all be moved, see onclick item_attack.dm for harvest() method handling
        Harvest();
    }

    #endregion

    #region Living.dm Functions

    // see living.dm InCritical
    public bool InCritical()
    {
        return (health < 0 && health > -95 && mobStat == MobConsciousStat.UNCONSCIOUS);
    }


    // see living.dm updateHealth
    public virtual void UpdateHealth()
    {
        if ((StatusFlags & MobStatusFlag.GODMODE) != 0)
            return;

        health = maxHealth - oxyLoss - toxLoss - fireLoss - bruteLoss - cloneLoss;
        Debug.Log(this.gameObject.name + " is now Health: " + health);
        UpdateStat();
    }

    // see living.dm /mob/living/proc/harvest(mob/living/user)
    public virtual void Harvest()
    {
        foreach(GameObject harvestPrefab in butcherResults)
        {
            // TODO This needs some rework
            GameObject harvest = Instantiate(harvestPrefab, transform.position, Quaternion.identity) as GameObject;
            NetworkServer.Spawn(harvest);
        }
        Gib(false, false, true);
    }

    #endregion

    #region damage_procs.dm functions

    // see damage_procs.dm /mob/living/proc/apply_damage
    public virtual int ApplyDamage(int damage = 0, string damagetype = DamageType.BRUTE, BodyPartType bodyparttype = BodyPartType.NULL, int blocked = 0)
    {
        // TODO Blocking
        if (damage == 0)
            return 0;

        switch (damagetype)
        {
            case DamageType.BRUTE:
                AdjustBruteLoss(damage);
                break;
        }

        return 1;
    }

    // see damage_procs.dm /mob/living/proc/getBruteLoss
    public virtual int GetBruteLoss()
    {
        return bruteLoss;
    }

    // see damage_procs.dm /mob/living/proc/adjustBruteLoss
    public virtual int AdjustBruteLoss(int amount, bool updatingHealth = true, bool forced = false)
    {
        if (!forced && ((StatusFlags & MobStatusFlag.GODMODE) != 0))
            return 0;

        bruteLoss = DMMath.Clamp((bruteLoss + amount), 0, maxHealth * 2);

        if (updatingHealth)
            UpdateHealth();

        return amount;
    }

    // see damage_procs.dm /mob/living/proc/getOxyLoss
    public virtual int GetOxyLoss()
    {
        return oxyLoss;
    }

    // see damage_procs.dm /mob/living/proc/getToxLoss
    public virtual int GetToxLoss()
    {
        return toxLoss;
    }

    // see damage_procs.dm /mob/living/proc/getFireLoss
    public virtual int GetFireLoss()
    {
        return fireLoss;
    }

    // see damage_procs.dm /mob/living/proc/adjustFireLoss
    public virtual int AdjustFireLoss(int amount, bool updatingHealth = true, bool forced = false)
    {
        if (!forced && ((StatusFlags & MobStatusFlag.GODMODE) != 0))
            return 0;

        fireLoss = DMMath.Clamp((fireLoss + amount), 0, maxHealth * 2);

        if (updatingHealth)
            UpdateHealth();

        return amount;
    }

    // see damage_procs.dm /mob/living/proc/getCloneLoss
    public virtual int GetCloneLoss()
    {
        return cloneLoss;
    }

    // see damage_procs.dm /mob/living/proc/getBrainLoss
    public virtual int GetBrainLoss()
    {
        return brainLoss;
    }

    // see damage_procs.dm /mob/living/proc/getStaminaLoss
    public virtual int GetStaminaLoss()
    {
        return staminaLoss;
    }

    // see damage_procs.dm /mob/living/proc/take_overall_damage
    public virtual void TakeOverallDamage(int brute, int burn, bool updatingHealth)
    {
        AdjustBruteLoss(brute, false);  //zero as argument for no instant health update
        AdjustFireLoss(burn, false);
        if (updatingHealth)
            UpdateHealth();
    }

    #endregion

    #region death.dm

    // see living death.dm /mob/living/death(gibbed)
    public override void Death(bool gibbed)
    {
        mobStat = MobConsciousStat.DEAD;
        paralysis =  false;
        
    }

    // see living death.dm /mob/living/gib(no_brain, no_organs, no_bodyparts)
    public virtual void Gib(bool no_brain, bool no_organs, bool no_bodyparts)
    {
        if (mobStat != MobConsciousStat.DEAD)
            Death(true);

        GibAnimation();

        NetworkServer.Destroy(this.gameObject);
    }

    // see living death.dm /mob/living/proc/gib_animation()
    public virtual void GibAnimation()
    {
        return;
    }

    // See living death.dm /mob/living/proc/spawn_gibs()
    public virtual void SpawnGibs()
    {
        GibAnimation();
    }

    #endregion
}
