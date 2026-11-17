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
                Assert.AreEqual(17,reopened.DaggerCooldownRemaining("second-wind"));
            }
            finally{Object.DestroyImmediate(catalog);Object.DestroyImmediate(pair);}
        }

        [Test] public void SecondWindUsesLiveCapacityAndPublishesOneRecoveryPerAcceptedPress()
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
                bool Press(bool allowed,int frame)=>(bool)Call(rules,"TryActivate",allowed,frame);
                Assert.IsFalse(Press(true,1));Assert.Zero(service.DaggerCooldownRemaining("second-wind"));
                max.SetValue(settings,79f);Call(power,"Spend",79f);
                int recovered=0;Action observe=()=>recovered++;
                power.GetType().GetEvent("RecoveredFromDepletion").AddEventHandler(power,observe);
                Assert.IsFalse(Press(false,2));Assert.IsTrue(Press(true,2));
                Assert.AreEqual(79f,power.GetType().GetProperty("Current").GetValue(power));Assert.AreEqual(1,recovered);
                Call(rules,"Tick",30f);Call(power,"Spend",79f);
                Assert.IsFalse(Press(true,2));Assert.Zero(service.DaggerCooldownRemaining("second-wind"));
                Assert.IsTrue(Press(true,3));Assert.AreEqual(2,recovered);
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(settings);}
        }
    }
}
