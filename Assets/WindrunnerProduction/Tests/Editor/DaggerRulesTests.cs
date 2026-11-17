using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Windrunner.Production.Progression;
using Object = UnityEngine.Object;

namespace Windrunner.Production.Tests
{
    public sealed class DaggerRulesTests
    {
        const string Folder="Assets/WindrunnerFrontend/Daggers/Defaults/";
        static Type Runtime(string name)=>Type.GetType(name+", Assembly-CSharp",true);
        static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method).Invoke(target,args);
        static void Pairs(Object catalog,string field,params Object[] pairs)
        {
            var f=catalog.GetType().GetField(field);
            var array=Array.CreateInstance(f.FieldType.GetElementType(),pairs.Length);
            for(int i=0;i<pairs.Length;i++)array.SetValue(pairs[i],i);
            f.SetValue(catalog,array);
        }

        [Test] public void MissingDefinitionRetainsOwnershipAndCooldownAndRewardsGrantOnce()
        {
            var store=new MemoryProgressionStore();
            var prior=new ProgressionState{playerId="player"};
            prior.rewards.Add(new RewardClaim{id="example-grant",rewardId="example",kind="unlock",contentId="reward.wind",amount=1});
            store.Commit(prior);
            var catalog=Object.Instantiate(AssetDatabase.LoadMainAssetAtPath(Folder+"Arsenal.asset"));
            var pair=Object.Instantiate(AssetDatabase.LoadMainAssetAtPath(Folder+"Farseer's Daggers.asset"));
            var training=AssetDatabase.LoadMainAssetAtPath(Folder+"Training Daggers.asset");
            try
            {
                pair.GetType().GetField("unlockContentId").SetValue(pair,"reward.wind");
                Pairs(catalog,"pairs",training,pair);Pairs(catalog,"startingPairs",training);
                using var service=new ProgressionService(new ProgressionDefinitions(),"player",store);
                var rules=Activator.CreateInstance(Runtime("Windrunner.Daggers.DaggerEquipment"),catalog,service,null);
                int changes=0;service.Changed+=()=>changes++;
                Call(rules,"Synchronize");Assert.Zero(changes);
                Assert.AreEqual(1,service.ReadDaggers().owned.Count(id=>id=="farseer"));
                Assert.IsTrue((bool)Call(rules,"Equip",pair));
                var state=service.ReadDaggers();state.cooldowns.Add(new DaggerCooldown{effectId="second-wind",remaining=17});
                service.SaveDaggers(state);
                Pairs(catalog,"pairs",training,null);
                Assert.IsNull(Call(catalog,"ValidateSetup",true));
                Assert.IsNotNull(Call(catalog,"ValidateSetup",false));
                using var reopened=new ProgressionService(new ProgressionDefinitions(),"player",store);
                var recovered=Activator.CreateInstance(Runtime("Windrunner.Daggers.DaggerEquipment"),catalog,reopened,null);
                Assert.IsEmpty(reopened.EquippedDagger);Assert.IsTrue(reopened.OwnsDagger("farseer"));
                Assert.AreEqual(17,reopened.DaggerCooldownRemaining("second-wind"));
                Assert.IsFalse((bool)Call(recovered,"Equip",pair));
                Pairs(catalog,"pairs",training,pair);Call(recovered,"Synchronize");
                Assert.IsTrue((bool)Call(recovered,"Equip",pair));
                Assert.Zero(reopened.DaggerCooldownRemaining("second-wind"));
                Assert.Zero(reopened.StoredDaggerReserve("farseer"));
            }
            finally{Object.DestroyImmediate(catalog);Object.DestroyImmediate(pair);}
        }

        [Test] public void OnlySelectedRewardSourceChargesAtConfiguredRatio()
        {
            var catalog=Object.Instantiate(AssetDatabase.LoadMainAssetAtPath(Folder+"Arsenal.asset"));
            var pair=Object.Instantiate(AssetDatabase.LoadMainAssetAtPath(Folder+"Farseer's Daggers.asset"));
            var effect=Object.Instantiate(AssetDatabase.LoadMainAssetAtPath(Folder+"Second Wind.asset"));
            var root=new GameObject("Resource test");
            var settings=ScriptableObject.CreateInstance(Runtime("AirflowPrototype.AirflowMovementSettings"));
            try
            {
                var source=Runtime("AirflowPrototype.AirPowerRewardSource");
                effect.GetType().GetField("eligibleSources").SetValue(effect,Enum.Parse(source,"SlingNode"));
                effect.GetType().GetField("storageRatio").SetValue(effect,.5f);
                pair.GetType().GetField("active").SetValue(pair,effect);
                Pairs(catalog,"pairs",pair);Pairs(catalog,"startingPairs",pair);
                catalog.GetType().GetField("initialPair").SetValue(catalog,pair);
                using var service=new ProgressionService(new ProgressionDefinitions(),"player",new MemoryProgressionStore());
                var power=root.AddComponent(Runtime("AirflowPrototype.AirPower"));
                Call(power,"Configure",settings,true);
                var rules=Activator.CreateInstance(Runtime("Windrunner.Daggers.DaggerEquipment"),catalog,service,power);
                Call(rules,"Listen",true);
                void Return(string kind)=>Call(power,"GrantReward",Activator.CreateInstance(Runtime("AirflowPrototype.AirPowerReward"),Enum.Parse(source,kind)),40f);
                Return("AirNode");Assert.Zero(service.StoredDaggerReserve("farseer"));
                Return("SlingNode");Assert.AreEqual(20,service.StoredDaggerReserve("farseer"));
                effect.GetType().GetField("capacity").SetValue(effect,8f);Call(rules,"Synchronize");
                Assert.AreEqual(8,service.StoredDaggerReserve("farseer"));
                Call(rules,"Listen",false);Return("SlingNode");Assert.AreEqual(8,service.StoredDaggerReserve("farseer"));
            }
            finally
            {
                Object.DestroyImmediate(root);Object.DestroyImmediate(settings);Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(pair);Object.DestroyImmediate(effect);
            }
        }

        [Test] public void OldSlotWithOnlyCooldownLoadsEmptyAndPreservesEquippedPair()
        {
            var loadout=new DaggerLoadout{initialized=true,equipped="farseer"};
            loadout.owned.Add("farseer");
            loadout.cooldowns.Add(new DaggerCooldown{effectId="second-wind",remaining=28f});
            string legacy=ProgressionCodec.Encode(new ProgressionState{playerId="player",daggers=loadout})
                .Replace(",\"reserves\":[]","");
            Assert.That(legacy,Does.Not.Contain("\"reserves\""));
            var store=new MemoryProgressionStore();store.Commit(ProgressionCodec.Decode(legacy));
            using var service=new ProgressionService(new ProgressionDefinitions(),"player",store);
            var catalog=AssetDatabase.LoadMainAssetAtPath(Folder+"Arsenal.asset");
            Activator.CreateInstance(Runtime("Windrunner.Daggers.DaggerEquipment"),catalog,service,null);
            Assert.AreEqual("farseer",store.Load().daggers.equipped);
            Assert.Zero(service.StoredDaggerReserve("farseer"));Assert.Zero(store.Load().daggers.Remaining("second-wind"));
        }

        [Test] public void RewardOverflowAndReleaseConserveEnergyAtLiveCapacity()
        {
            var root=new GameObject("Rule test player");
            var settings=ScriptableObject.CreateInstance(Runtime("AirflowPrototype.AirflowMovementSettings"));
            try
            {
                var power=root.AddComponent(Runtime("AirflowPrototype.AirPower"));
                var max=settings.GetType().GetField("maxAirPower");max.SetValue(settings,41f);
                Call(power,"Configure",settings,true);
                using var service=new ProgressionService(new ProgressionDefinitions(),"player",new MemoryProgressionStore());
                var catalog=AssetDatabase.LoadMainAssetAtPath(Folder+"Arsenal.asset");
                var rules=Activator.CreateInstance(Runtime("Windrunner.Daggers.DaggerEquipment"),catalog,service,power);
                Call(rules,"Equip",AssetDatabase.LoadMainAssetAtPath(Folder+"Farseer's Daggers.asset"));
                Call(rules,"Listen",true);Call(rules,"Listen",true);
                float Stored()=>service.StoredDaggerReserve("farseer");
                bool Press(bool allowed,int frame)=>(bool)Call(rules,"TryActivate",allowed,frame);
                object Reward(string source)=>Activator.CreateInstance(Runtime("AirflowPrototype.AirPowerReward"),
                    Enum.Parse(Runtime("AirflowPrototype.AirPowerRewardSource"),source));
                void Grant(object reward,float amount)=>Call(power,"GrantReward",reward,amount);
                Assert.IsFalse(Press(true,1));
                Call(power,"Spend",20f);
                var ticket=Reward("AirNode");
                Action<float,float> replay=(current,maximum)=>Grant(ticket,50);
                var changed=power.GetType().GetEvent("Changed");
                changed.AddEventHandler(power,replay);
                try { Grant(ticket,50); } finally { changed.RemoveEventHandler(power,replay); }
                Assert.AreEqual(30,Stored());Grant(ticket,50);Assert.AreEqual(30,Stored());
                Assert.IsFalse(Press(true,1));Assert.AreEqual(30,Stored());
                max.SetValue(settings,79f);
                Assert.IsFalse(Press(false,2));Assert.IsTrue(Press(true,2));
                Assert.AreEqual(71f,power.GetType().GetProperty("Current").GetValue(power));
                Assert.Zero(Stored());Call(power,"Refill");Call(power,"Restore",100f);Assert.Zero(Stored());
                Grant(Reward("SlingNode"),120);Assert.AreEqual(50,Stored());
                Call(power,"Spend",10f);Assert.IsFalse(Press(true,2));Assert.IsTrue(Press(true,3));
                Assert.AreEqual(79f,power.GetType().GetProperty("Current").GetValue(power));Assert.Zero(Stored());
                Call(rules,"Equip",AssetDatabase.LoadMainAssetAtPath(Folder+"Training Daggers.asset"));
                Grant(Reward("AirNode"),100);Assert.Zero(Stored());
                var far=AssetDatabase.LoadMainAssetAtPath(Folder+"Farseer's Daggers.asset");Call(rules,"Equip",far);
                Grant(Reward("AirNode"),33);Assert.AreEqual(33,Stored());
                var oversized=service.ReadDaggers();oversized.SetStored("farseer",88);service.SaveDaggers(oversized);
                Call(rules,"Synchronize");Assert.AreEqual(50,Stored());
                Call(rules,"Listen",false);
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(settings);}
        }
    }
}
