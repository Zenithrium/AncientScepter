﻿using R2API;
using RoR2;
using RoR2.Orbs;
using RoR2.Skills;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Networking;
using static AncientScepter.SkillUtil;

namespace AncientScepter
{
    public class CrocoDisease2 : ScepterSkill
    {
        private static GameObject diseaseWardPrefab;

        public override SkillDef myDef { get; protected set; }

        public override string oldDescToken { get; protected set; }
        public override string newDescToken { get; protected set; }
        public override string overrideStr => "\n<color=#d299ff>SCEPTER: Victims become walking sources of Plague.</color>";

        public override string targetBody => "CrocoBody";
        public override SkillSlot targetSlot => SkillSlot.Special;
        public override int targetVariantIndex => 0;

        internal override void SetupAttributes()
        {
            var oldDef = LegacyResourcesAPI.Load<SkillDef>("SkillDefs/CrocoBody/CrocoDisease");
            myDef = CloneSkillDef(oldDef);

            var nametoken = "ANCIENTSCEPTER_CROCO_DISEASENAME";
            newDescToken = "ANCIENTSCEPTER_CROCO_DISEASEDESC";
            oldDescToken = oldDef.skillDescriptionToken;
            var namestr = "Plague";
            LanguageAPI.Add(nametoken, namestr);

            myDef.skillName = $"{oldDef.skillName}Scepter";
            (myDef as ScriptableObject).name = myDef.skillName;
            myDef.skillNameToken = nametoken;
            myDef.skillDescriptionToken = newDescToken;
            myDef.icon = Assets.SpriteAssets.CrocoDisease2;

            ContentAddition.AddSkillDef(myDef);

            var mshPrefab = Resources.Load<GameObject>("Prefabs/NetworkedObjects/MushroomWard");

            var dwPrefabPrefab = new GameObject("CIDiseaseAuraPrefabPrefab");
            dwPrefabPrefab.AddComponent<TeamFilter>();
            dwPrefabPrefab.AddComponent<MeshFilter>().mesh = mshPrefab.GetComponentInChildren<MeshFilter>().mesh;
            dwPrefabPrefab.AddComponent<MeshRenderer>().material = UnityEngine.Object.Instantiate(mshPrefab.GetComponentInChildren<MeshRenderer>().material);
            dwPrefabPrefab.GetComponent<MeshRenderer>().material.SetVector("_TintColor", new Vector4(1.5f, 0.3f, 1f, 0.3f));
            dwPrefabPrefab.AddComponent<NetworkedBodyAttachment>().forceHostAuthority = true;
            var dw = dwPrefabPrefab.AddComponent<DiseaseWard>();
            dw.rangeIndicator = dwPrefabPrefab.GetComponent<MeshRenderer>().transform;
            dw.interval = 1f;
            diseaseWardPrefab = dwPrefabPrefab.InstantiateClone("AncientScepterDiseaseWardAuraPrefab");
            UnityEngine.Object.Destroy(dwPrefabPrefab);

            if (ModCompat.compatBetterUI)
            {
                BetterUI.ProcCoefficientCatalog.AddSkill(myDef.skillName, BetterUI.ProcCoefficientCatalog.GetProcCoefficientInfo("CrocoDisease"));
            }
        }

        internal override void LoadBehavior()
        {
            On.RoR2.Orbs.LightningOrb.OnArrival += On_LightningOrbArrival;
        }

        internal override void UnloadBehavior()
        {
            On.RoR2.Orbs.LightningOrb.OnArrival -= On_LightningOrbArrival;
        }

        private void On_LightningOrbArrival(On.RoR2.Orbs.LightningOrb.orig_OnArrival orig, LightningOrb self)
        {
            orig(self);
            if (self.lightningType != LightningOrb.LightningType.CrocoDisease || AncientScepterItem.instance.GetCount(self.attacker?.GetComponent<CharacterBody>()) < 1) return;
            if (!self.target || !self.target.healthComponent) return;

            var cpt = self.target.healthComponent.gameObject.GetComponentInChildren<DiseaseWard>()?.gameObject;
            if (!cpt)
            {
                cpt = UnityEngine.Object.Instantiate(diseaseWardPrefab);
                cpt.GetComponent<TeamFilter>().teamIndex = self.target.GetComponent<TeamComponent>()?.teamIndex ?? TeamIndex.Monster;
                cpt.GetComponent<DiseaseWard>().attacker = self.attacker;
                cpt.GetComponent<DiseaseWard>().owner = self.target.healthComponent.gameObject;
                cpt.GetComponent<NetworkedBodyAttachment>().AttachToGameObjectAndSpawn(self.target.healthComponent.gameObject);
            }
            cpt.GetComponent<DiseaseWard>().netDamage = self.damageValue;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
    [RequireComponent(typeof(TeamFilter))]
    public class DiseaseWard : NetworkBehaviour
    {
        private readonly float radius = 10f;

        [SyncVar]
        private float damage;

        public float netDamage
        {
            get { return damage; }
            set { base.SetSyncVar<float>(value, ref damage, 1u); }
        }

        public float interval;
        public Transform rangeIndicator;

        public GameObject attacker;
        public GameObject owner;

        private TeamFilter teamFilter;
        private float rangeIndicatorScaleVelocity;

        private float procStopwatch;
        private float stopwatch;

        public DamageType baseDamageType = DamageType.Generic;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by UnityEngine")]
        private void Awake()
        {
            teamFilter = base.GetComponent<TeamFilter>();
            stopwatch = 5f;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by UnityEngine")]
        private void Update()
        {
            float num = Mathf.SmoothDamp(rangeIndicator.localScale.x, radius * 2f, ref rangeIndicatorScaleVelocity, 0.2f);
            rangeIndicator.localScale = new Vector3(num, num, num);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by UnityEngine")]
        private void FixedUpdate()
        {
            procStopwatch -= Time.fixedDeltaTime;
            if (procStopwatch <= 0f)
            {
                if (NetworkServer.active)
                {
                    procStopwatch = interval;
                    ServerProc();
                }
            }
            stopwatch -= Time.fixedDeltaTime;
            if (stopwatch <= 0f) Destroy(this.gameObject);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by UnityEngine")]
        private void OnDestroy()
        {
            Destroy(rangeIndicator);
        }

        [Server]
        private void ServerProc()
        {
            var tind = teamFilter.teamIndex;
            ReadOnlyCollection<TeamComponent> teamMembers = TeamComponent.GetTeamMembers(tind);
            float sqrad = radius * radius;
            int foundCount = 0;
            foreach (TeamComponent tcpt in teamMembers)
            {
                if (tcpt.body && tcpt.body.gameObject != owner && (tcpt.transform.position - transform.position).sqrMagnitude <= sqrad
                    && tcpt.body.mainHurtBox && tcpt.body.isActiveAndEnabled)
                {
                    OrbManager.instance.AddOrb(new LightningOrb
                    {
                        attacker = attacker,
                        bouncesRemaining = 0,
                        damageColorIndex = DamageColorIndex.Poison,
                        damageType = DamageType.AOE | baseDamageType,
                        damageValue = damage,
                        isCrit = false,
                        lightningType = LightningOrb.LightningType.CrocoDisease,
                        origin = transform.position,
                        procChainMask = default,
                        procCoefficient = 1f,
                        target = tcpt.body.mainHurtBox,
                        teamIndex = teamFilter.teamIndex,
                        bouncedObjects = new List<HealthComponent> { owner.GetComponent<HealthComponent>() }
                    });
                    foundCount++;
                    if (foundCount > 1) break;
                }
            }
        }
    }
}