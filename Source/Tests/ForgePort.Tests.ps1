$ErrorActionPreference='Stop'
$source=Get-Content (Join-Path $PSScriptRoot '../ForgePort.cs') -Raw -Encoding UTF8
$gate=Get-Content (Join-Path $PSScriptRoot '../ForgeEffectGate.cs') -Raw -Encoding UTF8
$transaction=Get-Content (Join-Path $PSScriptRoot '../ForgeTransaction.cs') -Raw -Encoding UTF8
$fake=@'
namespace UnityEngine {
    public static class Time { public static int frameCount; }
    public static class JsonUtility {public static string ToJson(object value){return "{}";}}
}
namespace Mirror {public static class NetworkClient {public static bool active=true;}}
public class BodyForgeMod {}
public struct ItemPosition {public int x,y;}
public class NewItemOwnInstance {public int InstanceID,Quantity=1,EntityID;public object Charm=new object();}
public struct StoredItem {public int instanceID,quantity;}
public class GridInventory {
    public bool isOwned=true;public int CurrentInventoryStorage=40;
    public System.Collections.Generic.Dictionary<ItemPosition,NewItemOwnInstance> inventoryMatrix=new System.Collections.Generic.Dictionary<ItemPosition,NewItemOwnInstance>();
    public System.Collections.Generic.Dictionary<int,StoredItem> subBagMatrix=new System.Collections.Generic.Dictionary<int,StoredItem>();
    public System.Collections.Generic.List<StoredItem> temporaryInventory=new System.Collections.Generic.List<StoredItem>();
    public event System.Action OnCharmEffectRefreshedForServer,OnCharmEffectRefreshedForClient;
    public int Listeners {get{return (OnCharmEffectRefreshedForServer==null?0:OnCharmEffectRefreshedForServer.GetInvocationList().Length)+(OnCharmEffectRefreshedForClient==null?0:OnCharmEffectRefreshedForClient.GetInvocationList().Length);}}
    public void DecreaseItemQuantity(int x,int y,int n){}
    public int SubBagSends;
    public void DecreaseSubBagItemQuantity(sbyte x,int n){SubBagSends++;}
    public void Enchant(ItemPosition p){}
    public void AddStorage(int n){CurrentInventoryStorage+=n;}
}
public class PlayerAvatar {public GridInventory Inventory=new GridInventory();public bool isServer;public int Sends;public void AddOrphanedStatusInstance(object s){Sends++;}public void CmdAddOrphanedStatusInstance(string s){Sends++;}}
public class DungeonManager {public static DungeonManager Instance=new DungeonManager();public int sessionSerial=1;public string GetGlobalItemStatValue(int id,string key){return "0";}}
public static class StatusDatabase {public static object CreateStatusEntity(string s){return s;}}
internal class ForgeReward {internal string Metadata="HP/1";internal int Kind=0;internal double Delta=1;internal double Read(PlayerAvatar p){return 0;}}
public static class ForgePortTests {
    static void Check(bool value,string message){if(!value)throw new System.Exception(message);}
    static bool Rejects(ForgePort port){try{port.Validate();return false;}catch(System.InvalidOperationException){return true;}}
    public static string Run(){
        foreach(bool server in new[]{false,true}) {
            var player=new PlayerAvatar {isServer=server};var inventory=player.Inventory;
            var position=new ItemPosition();inventory.inventoryMatrix[position]=new NewItemOwnInstance {InstanceID=2};
            var materialPosition=new ItemPosition {x=1};inventory.inventoryMatrix[materialPosition]=new NewItemOwnInstance {InstanceID=1};
            var port=new ForgePort(player,materialPosition,new NewItemOwnInstance {InstanceID=1},position,2,new[]{new ForgeReward()},()=>true,()=>{},()=>{},r=>{});
            port.Validate();port.Consume();port.Enchant();Check(inventory.Listeners==1,"subscribe once per transaction");
            player.Inventory=new GridInventory();Check(Rejects(port),"replacement inventory rejected");
            port.Close();port.Close();Check(inventory.Listeners==0,"close releases captured inventory events after replacement");
            player.Inventory=inventory;DungeonManager.Instance.sessionSerial++;Check(Rejects(port),"session change rejected");
            DungeonManager.Instance=new DungeonManager();Check(Rejects(port),"dungeon replacement rejected");
            var white=new ForgePort(player,position,new NewItemOwnInstance {InstanceID=1},position,-1,new[]{new ForgeReward()},()=>true,()=>{},()=>{},r=>{},false);
            inventory.inventoryMatrix.Clear();white.Validate();
            bool rejected=false;try{white.Enchant();}catch(System.InvalidOperationException){rejected=true;}
            Check(rejected && inventory.Listeners==0,"attribute-only port needs no target and cannot dispatch enchantment");
            bool allowed=true;
            var guarded=new ForgePort(player,position,new NewItemOwnInstance {InstanceID=1},position,-1,new[]{new ForgeReward()},()=>true,()=>{},()=>{},r=>{},false,index=>allowed);
            guarded.Validate();allowed=false;rejected=false;
            try{guarded.ApplyStat(0);}catch(System.InvalidOperationException){rejected=true;}
            Check(rejected&&player.Sends==0,"late eligibility change prevents host and client reward dispatch");
            allowed=true;guarded.ApplyStat(0);Check(player.Sends==1,"eligible reward dispatched exactly once");
            allowed=false;guarded.Validate();Check(player.Sends==1,"receipt validation must not reject own grant reaching cap");guarded.Close();
        }
        foreach(bool server in new[]{false,true})foreach(int quantity in new[]{1,3}) {
            var p=new PlayerAvatar {isServer=server};var inv=p.Inventory;
            inv.subBagMatrix[2]=new StoredItem {instanceID=9,quantity=quantity};
            var port=new ForgePort(p,new ItemPosition(),null,new ItemPosition(),-1,new[]{new ForgeReward()},()=>true,()=>{},()=>{},r=>{},false,null,2,9,quantity,100);
            Check(port.MaterialState()==0,"subbag initial state");port.Consume();Check(inv.SubBagSends==1 && inv.Listeners==1,"native subbag consume and refresh subscription");
            inv.subBagMatrix.Remove(2);inv.subBagMatrix[3]=new StoredItem {instanceID=9,quantity=quantity};Check(port.MaterialState()==-1,"moved subbag item not consumed");
            inv.subBagMatrix.Clear();inv.temporaryInventory.Add(new StoredItem {instanceID=9});Check(port.MaterialState()==-1,"temporary item not consumed");inv.temporaryInventory.Clear();
            inv.inventoryMatrix[new ItemPosition()]=new NewItemOwnInstance {InstanceID=9};Check(port.MaterialState()==-1,"main bag move not consumed");inv.inventoryMatrix.Clear();
            inv.subBagMatrix[2]=new StoredItem {instanceID=10,quantity=1};Check(port.MaterialState()==-1,"replacement rejected");inv.subBagMatrix.Clear();
            if(quantity>1) {Check(port.MaterialState()==-1,"entire stack disappearance rejected");inv.subBagMatrix[2]=new StoredItem {instanceID=9,quantity=quantity-1};}
            Check(port.MaterialState()==1,"exactly one consumed");
            bool rejected=false;try{port.Consume();}catch(System.InvalidOperationException){rejected=true;}Check(rejected && inv.SubBagSends==1,"no second consumption after changed state");
            port.Close();Check(inv.Listeners==0,"subbag cleanup");
        }
        return "PASS: host/client subbag consumption, stack and movement guards, event cleanup, inventory/session/dungeon guards";
    }
}
'@
$output=Join-Path ([System.IO.Path]::GetTempPath()) ('BodyForge-port-test-'+[guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($output) | Out-Null
Add-Type -TypeDefinition ($source+($gate -replace 'using System;','')+($transaction -replace 'using System;','')+$fake) -OutputAssembly (Join-Path $output 'ForgePortTests.dll') -WarningAction SilentlyContinue
Add-Type -Path (Join-Path $output 'ForgePortTests.dll')
[ForgePortTests]::Run()
