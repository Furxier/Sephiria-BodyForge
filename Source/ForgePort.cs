using System;
using System.IO;
using UnityEngine;

internal sealed class ForgePort : IForgePort
{
    private readonly PlayerAvatar player;
    private readonly ItemPosition materialPosition, targetPosition;
    private readonly int materialID, targetID, quantity;
    private readonly int materialSubBag;
    private readonly ForgeReward[] rewards;
    private readonly string journalPath, transactionID;
    private readonly Func<bool> validPlayer;
    private readonly GridInventory inventory;
    private readonly bool hasTarget;
    private readonly DungeonManager dungeon;
    private readonly int session;
    private readonly double[] samples;
    private readonly ForgeEffectGate effects=new ForgeEffectGate();
    private bool subscribed;
    private readonly bool complimentary;
    private bool complimentaryConsumed;
    private readonly Action onConsume,onEnchant;
    private readonly Action<ForgeReward> onReward;
    private readonly Func<int,bool> rewardAllowed;
    [Serializable] private sealed class Entry
    {
        public string utc, transaction, message;
        public int materialInstance, targetInstance;
    }
    internal ForgePort(PlayerAvatar player,ItemPosition materialPosition,NewItemOwnInstance material,
        ItemPosition targetPosition,int targetID,ForgeReward[] rewards,Func<bool> validPlayer,
        Action onConsume,Action onEnchant,Action<ForgeReward> onReward,bool hasTarget=true,Func<int,bool> rewardAllowed=null,
        int subBag=-1,int subBagInstance=0,int subBagQuantity=0,int subBagEntity=0,bool complimentary=false)
    {
        materialSubBag=subBag;
        this.complimentary=complimentary;
        this.player=player; this.materialPosition=materialPosition; materialID=complimentary?subBagInstance:subBag<0?material.InstanceID:subBagInstance;
        quantity=complimentary?1:subBag<0?material.Quantity:subBagQuantity; this.targetPosition=targetPosition; this.targetID=targetID;
        this.rewards=rewards; this.validPlayer=validPlayer;
        this.hasTarget=hasTarget;
        this.rewardAllowed=rewardAllowed;
        this.onConsume=onConsume;this.onEnchant=onEnchant;this.onReward=onReward;
        inventory=player.Inventory;
        dungeon=DungeonManager.Instance;session=dungeon==null?0:dungeon.sessionSerial;
        samples=new double[rewards.Length];
        transactionID=Guid.NewGuid().ToString("N");
        journalPath=Path.Combine(Path.GetDirectoryName(typeof(BodyForgeMod).Assembly.Location),"forge-history.jsonl");
        string plan="plan complimentary="+complimentary+" materialEntity="+(complimentary?0:subBag<0?material.EntityID:subBagEntity)+" subBag="+subBag+" quantity="+(complimentary?0:1)+" rewards=";
        foreach(var reward in rewards) plan+=reward.Metadata+";";
        Record(plan); // Fail before consuming if the journal cannot be written.
    }
    public void Validate()
    {
        if(!validPlayer() || player==null || !Mirror.NetworkClient.active || player.Inventory==null || player.Inventory!=inventory || !player.Inventory.isOwned)
            throw new InvalidOperationException("角色或连接已变化");
        if(dungeon==null || DungeonManager.Instance!=dungeon || dungeon.sessionSerial!=session)
            throw new InvalidOperationException("地下城或冒险已变化");
        NewItemOwnInstance target;
        if(!hasTarget)return; // Attribute-only forging has no enchantment target.
        if(!player.Inventory.inventoryMatrix.TryGetValue(targetPosition,out target) || target==null ||
            target.InstanceID!=targetID || target.Charm==null)
            throw new InvalidOperationException("目标神器已移动或消失");
    }
    public int MaterialState()
    {
        if(complimentary)return complimentaryConsumed?1:0;
        var inventory=player.Inventory;
        if(materialSubBag>=0)
        {
            foreach(var pair in inventory.inventoryMatrix) if(pair.Value!=null && pair.Value.InstanceID==materialID)return -1;
            foreach(var item in inventory.temporaryInventory)if(item.instanceID==materialID)return -1;
            foreach(var pair in inventory.subBagMatrix)
            {
                if(pair.Value.instanceID!=materialID)continue;
                if(pair.Key!=materialSubBag)return -1;
                return pair.Value.quantity==quantity?0:pair.Value.quantity==quantity-1?1:-1;
            }
            return quantity==1 && !inventory.subBagMatrix.ContainsKey((sbyte)materialSubBag)?1:-1;
        }
        foreach(var pair in inventory.inventoryMatrix)
        {
            var item=pair.Value;
            if(item==null || item.InstanceID!=materialID) continue;
            if(pair.Key.x!=materialPosition.x || pair.Key.y!=materialPosition.y) return -1;
            if(item.Quantity==quantity) return 0;
            return item.Quantity==quantity-1?1:-1;
        }
        foreach(var pair in inventory.subBagMatrix) if(pair.Value.instanceID==materialID) return -1;
        foreach(var item in inventory.temporaryInventory) if(item.instanceID==materialID) return -1;
        NewItemOwnInstance occupant;
        if(inventory.inventoryMatrix.TryGetValue(materialPosition,out occupant) && occupant!=null) return -1;
        return quantity==1?1:-1;
    }
    public int EnchantLevel
    {
        get
        {
            if(DungeonManager.Instance==null) throw new InvalidOperationException("地下城状态不可用");
            int value;
            return int.TryParse(DungeonManager.Instance.GetGlobalItemStatValue(targetID,"Enchant"),out value)?value:0;
        }
    }
    public double StatValue(int index) { return rewards[index].Read(player); }
    public double StatDelta(int index) { return rewards[index].Delta; }
    public void ConfirmedConsume() { onConsume(); }
    public void ConfirmedEnchant() { onEnchant(); }
    public void ConfirmedStat(int index) { onReward(rewards[index]); }
    private void ArmEffects()
    {
        if(!subscribed)
        {
            if(player.isServer) inventory.OnCharmEffectRefreshedForServer+=EffectsRefreshed;
            else inventory.OnCharmEffectRefreshedForClient+=EffectsRefreshed;
            subscribed=true;
        }
        effects.Request();
    }
    private void EffectsRefreshed() { effects.Refreshed(); }
    public bool EffectsReady
    {
        get
        {
            for(int i=0;i<samples.Length;i++) samples[i]=StatValue(i);
            return effects.Ready(player.isServer,Time.frameCount,samples);
        }
    }
    public void Close()
    {
        if(!subscribed) return;
        inventory.OnCharmEffectRefreshedForServer-=EffectsRefreshed;
        inventory.OnCharmEffectRefreshedForClient-=EffectsRefreshed;
        subscribed=false;
    }
    public void Consume()
    {
        if(MaterialState()!=0)throw new InvalidOperationException("材料已移动或数量变化，未发送消耗请求");
        ArmEffects();
        if(complimentary){complimentaryConsumed=true;effects.Refreshed();return;}
        if(materialSubBag>=0)inventory.DecreaseSubBagItemQuantity((sbyte)materialSubBag,1);
        else inventory.DecreaseItemQuantity(materialPosition.x,materialPosition.y,1);
    }
    public void Enchant() { if(!hasTarget)throw new InvalidOperationException("本次锻体没有附魔目标"); ArmEffects(); inventory.Enchant(targetPosition); }
    public void ApplyStat(int index)
    {
        // Enchanting can change equipment-derived limits after the initial selection check.
        // Only check before dispatch, never while confirming our own already-sent reward.
        if(rewardAllowed!=null && !rewardAllowed(index))
            throw new InvalidOperationException("装备刷新后奖励资格或上限已变化，未发送该项属性；已完成部分保留");
        if(rewards[index].Kind==7)
        {
            if(player.Inventory.CurrentInventoryStorage>=120)throw new InvalidOperationException("背包已达 120 格，未执行扩容");
            player.Inventory.AddStorage(1);return;
        }
        if(player.isServer) ForgePermanentStats.Apply(player,rewards[index].Metadata);
        else player.CmdAddOrphanedStatusInstance(rewards[index].Metadata);
    }
    public void Record(string message)
    {
        var entry=new Entry { utc=DateTime.UtcNow.ToString("o"), transaction=transactionID,
            materialInstance=materialID, targetInstance=targetID, message=message };
        File.AppendAllText(journalPath,JsonUtility.ToJson(entry)+Environment.NewLine);
    }
}
