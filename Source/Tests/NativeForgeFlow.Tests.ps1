$ErrorActionPreference = 'Stop'
$source=Get-Content (Join-Path $PSScriptRoot '../NativeForgeFlow.cs') -Raw -Encoding UTF8
$stubs=@'
namespace UnityEngine { public static class Time {public static float realtimeSinceStartup=0;} }
namespace UnityEngine.EventSystems { public class PointerEventData { public enum InputButton { Left,Right,Middle } } }
public struct ItemPosition : System.IEquatable<ItemPosition>
{
    public int x;
    public bool Equals(ItemPosition other) { return x==other.x; }
    public override bool Equals(object other) { return other is ItemPosition && Equals((ItemPosition)other); }
    public override int GetHashCode() { return x; }
}
public class NewItemOwnInstance { public int InstanceID,EntityID,Quantity=1; }
public struct ItemMetadata { public int instanceID,entityID,quantity; }
public class UI_SubBagIcon { public GridInventory Inventory; public sbyte X; }
public class GridInventory { public System.Collections.Generic.Dictionary<sbyte,ItemMetadata> subBagMatrix=new System.Collections.Generic.Dictionary<sbyte,ItemMetadata>(); public System.Collections.Generic.Dictionary<ItemPosition,NewItemOwnInstance> inventoryMatrix=new System.Collections.Generic.Dictionary<ItemPosition,NewItemOwnInstance>(); }
public class PlayerAvatar { public GridInventory Inventory=new GridInventory();public bool IsInBattle; }
public class UI_CharacterStatusPanel
{
    public enum EInventoryMode { None,Enchant }
    public bool IsOpened=true; public PlayerAvatar PlayerAvatar; public EInventoryMode InventoryMode;
    public void Open(){IsOpened=true;}
}
public class UI_NewInventoryIcon { public GridInventory Inventory; public NewItemOwnInstance Item; }
public class UI_ShopPanel { public bool IsOpened; }
public class UI_SephiriteRewardPanel { public bool IsOpened; }
public class UI_InventoryViewer { public bool IsOpened; }
public class UI_NewItemPicker { public bool CurrentAny; }
public class UI_NewItemPicker_Controller { public bool CurrentAny; }
public class UI_MessageBox
{
    public bool IsOpened=true; public System.Action yes,no;
    public void ForceClose() { IsOpened=false; if(no!=null) no(); }
}
public class UI_MessageBoxHolder
{
    public UI_MessageBox Box;
    public bool HasOpenedBox { get { return Box!=null && Box.IsOpened; } }
    public UI_MessageBox OpenYesNo(string text,System.Action yes,System.Action no,bool flag)
    { Box=new UI_MessageBox { yes=yes,no=no }; return Box; }
    public void OpenYes(string text,System.Action yes) { }
    public void Confirm() { Box.IsOpened=false; Box.yes(); }
}
public class UIManager
{
    public System.Collections.Generic.List<object> CurrentControlStack=new System.Collections.Generic.List<object>();
    public static UIManager Instance=new UIManager();
    public System.Collections.Generic.Dictionary<System.Type,object> Items=new System.Collections.Generic.Dictionary<System.Type,object>();
    public T GetElement<T>() where T:new()
    { object value; if(!Items.TryGetValue(typeof(T),out value)) { value=new T(); Items[typeof(T)]=value; } return (T)value; }
}
public class DungeonManager { public static DungeonManager Instance=new DungeonManager(); }
internal static class NativeForgeHooks { internal static bool Installed=true; }
internal class BodyForgeSettings { public bool Enabled=true; public static BodyForgeSettings Current=new BodyForgeSettings(); }
internal static class ForgeTransaction {internal static int RewardCount(int rarity){return new[]{0,1,2,3,5}[rarity];}}
public sealed partial class BodyForgePanel
{
    private class ForgeRow { internal ItemPosition Position; internal int SubBag=-1; internal int Instance,Entity,Rarity,Quantity=1,Count=2; internal bool Target,Complimentary=false; internal string Name="道具"; }
    internal static BodyForgePanel Instance;public bool isActiveAndEnabled=true;
    private float nextNativeScan;
    private PlayerAvatar LocalPlayer(){return owner;}
    private void EnsureForgeProgress(){}
    private void TickNativeUI(){Check(nextNativeScan==0,"external entry scans native UI");nativePanel=UIManager.Instance.GetElement<UI_CharacterStatusPanel>();nativePanel.PlayerAvatar=owner;}
    private PlayerAvatar owner=new PlayerAvatar();
    private UI_CharacterStatusPanel nativePanel;
    private bool forgeBlocked,busy;
    private ForgeMilestones milestones=new ForgeMilestones();
    private bool ForgeBusy { get { return busy; } }
    private string forgePoolText="pool",forgeMessage="message",forgeResults="result";
    private int forgeMaterial,forgeTarget,starts,stops;
    private object forgePool;
    private object forgeModal=null;
    private sealed class Recipe { internal string Text="reward"; }
    private Recipe chosenRecipe;
    private int chosenMaterial=-1,chosenTarget=-1;
    private System.Action pickRecipe;
    private void OpenForgeHub() { }
    private void CloseForgeModal() { forgeModal=null; }
    private int resultsShown;
    private void ShowForgeResult() { resultsShown++;Check(forgeResults!=null,"result text exists"); }
    private void ShowForgeConfirmation(ForgeRow material,ForgeRow target,System.Action confirm,System.Action cancel) { forgeModal=new object(); UIManager.Instance.GetElement<UI_MessageBoxHolder>().OpenYesNo("",()=>{CloseForgeModal();confirm();},()=>{CloseForgeModal();cancel();},false); }
    private void ClearRecipeSelection() { chosenRecipe=null;chosenMaterial=chosenTarget=-1; }
    private void OpenRecipeChoices(ForgeRow material,ForgeRow target,System.Action picked,System.Action cancel)
    { pickRecipe=()=>{chosenRecipe=new Recipe();chosenMaterial=material.Instance;chosenTarget=target==null?-1:target.Instance;picked();}; }
    private System.Collections.Generic.Dictionary<int,ForgeRow> rows=new System.Collections.Generic.Dictionary<int,ForgeRow>();
    private bool Ready() { return owner!=null; }
    private void RefreshForgePool() { forgePool=new object(); }
    private void RefreshForgeRows() { }
    private ForgeRow ForgeSelected(int id) { ForgeRow row; return rows.TryGetValue(id,out row)?row:null; }
    private void StartForge() { starts++; busy=true; }
    private void StopForge(string reason) { stops++; busy=false; forgeBlocked=true; }
    private void RecoverForge() { forgeBlocked=false; }
    private static void Check(bool value,string name) { if(!value) throw new System.Exception(name); }
    private static BodyForgePanel Setup()
    {
        UIManager.Instance=new UIManager(); BodyForgeSettings.Current.Enabled=true;
        var p=new BodyForgePanel(); p.nativePanel=new UI_CharacterStatusPanel { PlayerAvatar=p.owner };
        for(int i=1;i<=3;i++)
        {
            var row=new ForgeRow { Instance=i,Entity=100+i,Target=i==2,Position=new ItemPosition { x=i } };
            p.rows.Add(i,row); p.owner.Inventory.inventoryMatrix.Add(row.Position,new NewItemOwnInstance { InstanceID=i,EntityID=row.Entity });
        }
        return p;
    }
    private void Click(int id)
    { HandleNativeClick(UnityEngine.EventSystems.PointerEventData.InputButton.Left,new UI_NewInventoryIcon { Inventory=owner.Inventory,Item=owner.Inventory.inventoryMatrix[rows[id].Position] }); }
    private void SelectPair() { BeginNativeForge(); Click(1); Click(2); Check(starts==0 && nativeStep==NativeStep.Choice,"recipe selection before confirmation");pickRecipe(); }
    public static string RunTests()
    {
        var p=Setup(); p.SelectPair(); var holder=UIManager.Instance.GetElement<UI_MessageBoxHolder>();
        Check(p.starts==0 && p.nativeStep==NativeStep.Confirm,"nothing consumed before confirmation");
        var stale=holder.Box.yes; holder.Confirm(); stale();
        Check(p.starts==1 && p.nativeStep==NativeStep.Running,"double confirm submits once");
        p.nativePanel.IsOpened=false; p.HandleNativeClosed(); p.TickNativeFlow();
        Check(p.busy && p.stops==0 && p.nativeStep==NativeStep.Running,"closing inventory after confirmation preserves settlement");
        p.CancelNative("closed"); stale(); Check(p.starts==1 && p.stops==1,"stale callback after close cannot submit");

        p=Setup(); p.rows[1].Count=0;p.owner.Inventory.inventoryMatrix.Remove(p.rows[2].Position);
        p.BeginNativeForge();p.Click(1);Check(p.nativeStep==NativeStep.Choice && p.forgeTarget==-1,"white material skips target even without another artifact");
        p.pickRecipe();UIManager.Instance.GetElement<UI_MessageBoxHolder>().Confirm(); Check(p.starts==1,"zero-enchant material starts without target");
        p=Setup(); p.SelectPair(); holder=UIManager.Instance.GetElement<UI_MessageBoxHolder>(); stale=holder.Box.yes;
        holder.Box.ForceClose(); stale(); Check(p.starts==0 && !p.NativeActive,"cancelled confirmation cannot consume");
        p=Setup(); p.SelectPair(); p.owner.Inventory.inventoryMatrix[p.rows[1].Position].Quantity=2;
        UIManager.Instance.GetElement<UI_MessageBoxHolder>().Confirm(); Check(p.starts==0,"changed quantity blocks consumption");
        p=Setup(); p.SelectPair(); p.owner.Inventory.inventoryMatrix.Remove(p.rows[2].Position);
        UIManager.Instance.GetElement<UI_MessageBoxHolder>().Confirm(); Check(p.starts==0,"moved target blocks consumption");
        p=Setup(); p.BeginNativeForge(); p.Click(1); p.Click(1); p.Click(3);
        Check(p.nativeStep==NativeStep.Target && p.starts==0,"same material and stone target rejected");
        p.nativePanel.IsOpened=false; p.TickNativeFlow(); Check(!p.NativeActive && p.starts==0,"backpack closure cancels selection");
        p=Setup(); p.SelectPair(); BodyForgeSettings.Current.Enabled=false;
        UIManager.Instance.GetElement<UI_MessageBoxHolder>().Confirm(); Check(p.starts==0,"disabled switch blocks old confirmation");
        p=Setup(); p.SelectPair(); p.nativePanel.PlayerAvatar=new PlayerAvatar();
        UIManager.Instance.GetElement<UI_MessageBoxHolder>().Confirm(); Check(p.starts==0,"changed avatar blocks old confirmation");
        p=Setup(); p.nativePanel.InventoryMode=UI_CharacterStatusPanel.EInventoryMode.Enchant; p.BeginNativeForge();
        Check(!p.NativeActive,"native enchant mode preserved");
        p=Setup(); UIManager.Instance.GetElement<UI_ShopPanel>().IsOpened=true; p.BeginNativeForge();
        Check(!p.NativeActive,"shop mode cannot start");
        p=Setup(); UIManager.Instance.GetElement<UI_SephiriteRewardPanel>().IsOpened=true; p.BeginNativeForge();
        Check(!p.NativeActive,"reward selection cannot start");
        p=Setup(); UIManager.Instance.GetElement<UI_InventoryViewer>().IsOpened=true; p.BeginNativeForge();
        Check(!p.NativeActive,"chest selection cannot start");
        p=Setup(); UIManager.Instance.GetElement<UI_NewItemPicker>().CurrentAny=true; p.BeginNativeForge();
        Check(!p.NativeActive,"picked up item cannot start");
        p=Setup();var sub=p.rows[1];sub.SubBag=0;sub.Count=0;
        p.owner.Inventory.inventoryMatrix.Remove(sub.Position);
        p.owner.Inventory.subBagMatrix[0]=new ItemMetadata {instanceID=1,entityID=sub.Entity,quantity=1};
        p.BeginNativeForge();p.HandleNativeSubBagClick(UnityEngine.EventSystems.PointerEventData.InputButton.Left,new UI_SubBagIcon {Inventory=p.owner.Inventory,X=0});
        Check(p.nativeStep==NativeStep.Choice && p.starts==0,"white subbag material goes to choices without consuming");
        Check(p.NativeRowUnchanged(sub),"subbag snapshot matches");
        p.owner.Inventory.subBagMatrix[0]=new ItemMetadata {instanceID=8,entityID=sub.Entity,quantity=1};
        Check(!p.NativeRowUnchanged(sub),"subbag replacement invalidates choice");p.TickNativeFlow();Check(!p.NativeActive,"subbag change cancels selection");
        p=Setup();sub=p.rows[1];sub.SubBag=0;p.owner.Inventory.inventoryMatrix.Remove(sub.Position);
        p.owner.Inventory.subBagMatrix[0]=new ItemMetadata {instanceID=1,entityID=sub.Entity,quantity=1};
        p.BeginNativeForge();p.HandleNativeSubBagClick(UnityEngine.EventSystems.PointerEventData.InputButton.Left,new UI_SubBagIcon {Inventory=p.owner.Inventory,X=0});
        Check(p.nativeStep==NativeStep.Target,"colored subbag material requires target");p.Click(2);p.pickRecipe();UIManager.Instance.GetElement<UI_MessageBoxHolder>().Confirm();Check(p.starts==1,"subbag material with main target confirms once");
        p=Setup();p.milestones.Choose(10,0,40,0,()=>{});p.BeginNativeForge();
        Check(!p.NativeActive && p.starts==0,"pending expansion blocks entry without consuming");
        p=Setup();p.SelectPair();p.milestones.Choose(10,0,40,0,()=>{});
        UIManager.Instance.GetElement<UI_MessageBoxHolder>().Confirm();
        Check(p.starts==0 && !p.NativeActive && p.resultsShown==0,"expansion arriving during selection cannot replay old success");
        for(int rarity=0;rarity<5;rarity++){
            p=Setup();var panel=UIManager.Instance.GetElement<UI_CharacterStatusPanel>();panel.IsOpened=false;
            Check(p.StartExternalForge(rarity)==null&&panel.IsOpened,"external entry opens native backpack");
            Check(p.nativeMaterialRow.Complimentary&&p.nativeMaterialRow.Instance==-100-rarity&&p.nativeMaterialRow.Count==new[]{0,1,2,3,5}[rarity],"external tiers and persistent candidate keys");
            Check(p.StartExternalForge(rarity)!=null,"repeated external entry rejected while active");
            Check(p.nativeStep==(rarity==0?NativeStep.Choice:NativeStep.Target),"white skips target, colored requires target");
            if(rarity>0)p.Click(2);p.pickRecipe();var h=UIManager.Instance.GetElement<UI_MessageBoxHolder>();var callback=h.Box.yes;h.Confirm();callback();
            Check(p.starts==1,"external confirmation starts once");
            p.CancelNative("stop");Check(!p.NativeActive&&!p.NativeRowUnchanged(new ForgeRow{Complimentary=true}),"external cancel invalidates virtual material");
        }
        p=Setup();Check(p.StartExternalForge(-1)!=null&&p.StartExternalForge(5)!=null&&!p.NativeActive,"invalid external rarity rejected");
        BodyForgeSettings.Current.Enabled=false;Check(p.StartExternalForge(0)!=null,"disabled mod rejects external entry");
        p=Setup();p.forgeBlocked=true;Check(p.StartExternalForge(0)!=null,"failure lock cannot be bypassed");
        p=Setup();UIManager.Instance.GetElement<UI_CharacterStatusPanel>().IsOpened=false;p.owner.IsInBattle=true;Check(p.StartExternalForge(0)!=null,"combat opening blocked");
        p=Setup();UIManager.Instance.GetElement<UI_CharacterStatusPanel>().IsOpened=false;UIManager.Instance.CurrentControlStack.Add(new object());Check(p.StartExternalForge(0)!=null,"other controlled screen not replaced");
        Instance=Setup();ForgeExternalBridge.Install();var entry=System.AppDomain.CurrentDomain.GetData(ForgeExternalBridge.Key) as System.Func<int,string>;
        Check(entry!=null,"optional BCL bridge registered");Instance.isActiveAndEnabled=false;Check(entry(0)!=null,"disabled component bridge rejected");
        ForgeExternalBridge.Uninstall();Check(System.AppDomain.CurrentDomain.GetData(ForgeExternalBridge.Key)==null,"bridge released on unload");
        var replacement=new System.Func<int,string>(r=>null);System.AppDomain.CurrentDomain.SetData(ForgeExternalBridge.Key,replacement);ForgeExternalBridge.Uninstall();Check(object.ReferenceEquals(System.AppDomain.CurrentDomain.GetData(ForgeExternalBridge.Key),replacement),"old bridge cannot clear replacement");System.AppDomain.CurrentDomain.SetData(ForgeExternalBridge.Key,null);
        return "PASS: native and five complimentary tiers, target selection, repeated entry, confirmation, cancellation, API lifecycle and context guards";
    }
}
'@
Add-Type -TypeDefinition ($source+((Get-Content (Join-Path $PSScriptRoot '../ForgeMilestones.cs') -Raw) -replace 'using System;','')+((Get-Content (Join-Path $PSScriptRoot '../ForgeExternal.cs') -Raw) -replace '(?m)^using [^;]+;','')+$stubs) -WarningAction SilentlyContinue
[BodyForgePanel]::RunTests()


