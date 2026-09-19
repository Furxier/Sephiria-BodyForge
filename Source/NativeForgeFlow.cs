using System;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed partial class BodyForgePanel
{
    private enum NativeStep { Idle, Material, Target, Choice, Confirm, Running }
    private NativeStep nativeStep;
    private int nativeGeneration;

    private ForgeRow nativeMaterialRow, nativeTargetRow;
    private string nativeHint="选择材料与目标神器，吞噬前会再次确认。";
    internal bool NativeActive { get { return nativeStep!=NativeStep.Idle; } }
    internal bool InterceptsNative(UI_CharacterStatusPanel panel) { return NativeActive && panel==nativePanel; }
    internal bool InterceptsNativeIcon(UI_NewInventoryIcon icon)
    { return NativeActive && owner!=null && icon!=null && icon.Inventory==owner.Inventory; }
    private bool NativeContextValid()
    {
        if(!NativeForgeHooks.Installed || !BodyForgeSettings.Current.Enabled || !Ready() ||
            nativePanel==null || !nativePanel.IsOpened || nativePanel.PlayerAvatar!=owner ||
            nativePanel.InventoryMode!=UI_CharacterStatusPanel.EInventoryMode.None || DungeonManager.Instance==null) return false;
        var shop=UIManager.Instance.GetElement<UI_ShopPanel>();
        var rewards=UIManager.Instance.GetElement<UI_SephiriteRewardPanel>();
        var chest=UIManager.Instance.GetElement<UI_InventoryViewer>();
        return (shop==null || !shop.IsOpened) && (rewards==null || !rewards.IsOpened) && (chest==null || !chest.IsOpened);
    }
    private bool NativePickerBusy()
    {
        var mouse=UIManager.Instance.GetElement<UI_NewItemPicker>();
        var controller=UIManager.Instance.GetElement<UI_NewItemPicker_Controller>();
        return (mouse!=null && mouse.CurrentAny)||(controller!=null && controller.CurrentAny);
    }
    private void NativeButtonClicked() { OpenForgeHub(); }
    private void BeginNativeForge()
    {
        try
        {
            if(NativeActive) { CancelNative("已取消锻体，已发出的请求仍可能生效"); return; }
            if(forgeBlocked) { RecoverForge(); nativeHint=forgeMessage; return; }
            if(milestones.Settling(Time.realtimeSinceStartup))
            { nativeHint="扩容正在同步，请稍后继续锻体。";forgeMessage=nativeHint;OpenForgeHub();return; }
            if(!NativeContextValid() || ForgeBusy || NativePickerBusy())
            { nativeHint="请关闭其他操作界面，并放下正在拖动的道具。"; return; }
            var holder=UIManager.Instance.GetElement<UI_MessageBoxHolder>();
            if(holder==null || holder.HasOpenedBox) { nativeHint="请先关闭当前确认框。"; return; }
            RefreshForgePool();
            if(forgePool==null) { nativeHint=forgePoolText; return; }
            forgeMaterial=-1; forgeTarget=-1; nativeMaterialRow=null; nativeTargetRow=null;
            nativeGeneration++; nativeStep=NativeStep.Material;
            nativeHint="第 1 步：点击要吞噬的神器或石板。";
        }
        catch(Exception ex) { CancelNative("无法开始锻体："+ex.Message); }
    }
    internal void HandleNativeClick(PointerEventData.InputButton button,UI_NewInventoryIcon icon)
    { HandleNativeItemClick(button,icon==null?null:icon.Inventory,icon==null||icon.Item==null?-1:icon.Item.InstanceID); }
    internal void HandleNativeSubBagClick(PointerEventData.InputButton button,UI_SubBagIcon icon)
    {
        ItemMetadata item;
        int id=icon!=null && icon.Inventory!=null && icon.Inventory.subBagMatrix.TryGetValue(icon.X,out item)?item.instanceID:-1;
        HandleNativeItemClick(button,icon==null?null:icon.Inventory,id);
    }
    internal bool InterceptsNativeSubBag(UI_SubBagIcon icon)
    { return NativeActive && owner!=null && icon!=null && icon.Inventory==owner.Inventory; }
    private void HandleNativeItemClick(PointerEventData.InputButton button,GridInventory inventory,int instance)
    {
        try
        {
            if(!NativeActive) return;
            if(!NativeContextValid()) { CancelNative("背包状态变化，已取消"); return; }
            if(nativeStep==NativeStep.Choice || nativeStep==NativeStep.Confirm || nativeStep==NativeStep.Running) return;
            if(button==PointerEventData.InputButton.Right) { CancelNative("已取消选择，未吞噬材料"); return; }
            if(button!=PointerEventData.InputButton.Left || inventory!=owner.Inventory || instance<0) return;
            RefreshForgeRows();
            var row=ForgeSelected(instance);
            if(row==null || row.Count<0) { nativeHint="请选择主背包或下方副背包中的神器或石板。"; return; }
            if(nativeStep==NativeStep.Material)
            {
                nativeMaterialRow=row; forgeMaterial=row.Instance;
                if(row.Count==0) { nativeTargetRow=null;forgeTarget=-1;ShowNativeConfirmation();return; }
                nativeStep=NativeStep.Target;
                nativeHint="第 2 步：已选材料「"+row.Name+"」，请点击另一件目标神器。";
                return;
            }
            if(!row.Target || row.Instance==forgeMaterial)
            { nativeHint="附魔目标必须是主背包中的另一件神器，不能是材料本身或石板。"; return; }
            nativeTargetRow=row; forgeTarget=row.Instance;
            if(!NativeRowsUnchanged()) { CancelNative("材料或目标已变化，请重新选择"); return; }
            ShowNativeConfirmation();
        }
        catch(Exception ex) { CancelNative("选择失败："+ex.Message); }
    }
    private bool NativeRowUnchanged(ForgeRow row)
    {
        if(row==null || !Ready()) return false;
        if(row.SubBag>=0)
        {
            ItemMetadata item;
            return owner.Inventory.subBagMatrix.TryGetValue((sbyte)row.SubBag,out item) &&
                item.instanceID==row.Instance && item.entityID==row.Entity && item.quantity==row.Quantity;
        }
        NewItemOwnInstance actual;
        return owner.Inventory.inventoryMatrix.TryGetValue(row.Position,out actual) && actual!=null &&
            actual.InstanceID==row.Instance && actual.EntityID==row.Entity && actual.Quantity==row.Quantity;
    }
    private bool NativeRowsUnchanged()
    { return ForgeRowsUnchanged(nativeMaterialRow,nativeTargetRow); }
    private bool ForgeRowsUnchanged(ForgeRow material,ForgeRow target)
    { return NativeRowUnchanged(material) && (material.Count==0 || NativeRowUnchanged(target)); }
    private void ShowNativeConfirmation()
    {
        if(chosenRecipe==null || chosenMaterial!=forgeMaterial || chosenTarget!=forgeTarget)
        {
            nativeStep=NativeStep.Choice; int token=++nativeGeneration;
            OpenRecipeChoices(nativeMaterialRow,nativeTargetRow,()=>{
                if(token!=nativeGeneration || nativeStep!=NativeStep.Choice)return;
                if(!NativeContextValid() || !NativeRowsUnchanged()) {CancelNative("材料或目标变化，未消耗");return;}
                ShowNativeConfirmation();
            },()=>CancelNative("已取消配方选择，未消耗材料"));
            return;
        }
        var holder=UIManager.Instance.GetElement<UI_MessageBoxHolder>();
        if(holder==null || holder.HasOpenedBox) { CancelNative("已有其他确认框，请重新选择"); return; }
        int generation=++nativeGeneration;
        nativeStep=NativeStep.Confirm; nativeHint="请在确认框中核对消耗与奖励。";
        ShowForgeConfirmation(nativeMaterialRow,nativeTargetRow,()=>ConfirmNative(generation),()=>{
            if(generation==nativeGeneration) CancelNative("已取消，未吞噬材料");
        });
    }
    private void ConfirmNative(int generation)
    {
        if(generation!=nativeGeneration || nativeStep!=NativeStep.Confirm) return;

        try
        {
            if(milestones.Settling(Time.realtimeSinceStartup))
            { CancelNative("扩容正在同步，尚未消耗材料，请稍后继续锻体。");forgeMessage=nativeHint;OpenForgeHub();return; }
            if(!NativeContextValid() || !NativeRowsUnchanged() || NativePickerBusy())
            { CancelNative("材料、目标或背包状态已变化，未吞噬"); return; }
            nativeStep=NativeStep.Running;
            forgeResults="";
            StartForge();
            nativeHint=forgeMessage;
            if(!ForgeBusy) FinishNative();
        }
        catch(Exception ex) { CancelNative("锻体已停止："+ex.Message); }
    }
    internal void CancelNative(string reason)
    {
        if(!NativeActive) return;
        bool running=nativeStep==NativeStep.Running;
        nativeStep=NativeStep.Idle; nativeGeneration++;
        CloseForgeModal(); ClearRecipeSelection();

        if(running) StopForge(reason);
        nativeHint=running?forgeMessage:reason;
        nativeMaterialRow=null; nativeTargetRow=null; forgeMaterial=-1; forgeTarget=-1;

    }
    internal void HandleNativeClosed()
    {
        // Confirmed work belongs to the character, not the lifetime of the inventory UI.
        if(nativeStep!=NativeStep.Running) CancelNative("背包已关闭，已取消选择");
    }
    private void FinishNative()
    {
        nativeStep=NativeStep.Idle; nativeGeneration++;
        nativeMaterialRow=null; nativeTargetRow=null; forgeMaterial=-1; forgeTarget=-1;
        nativeHint=forgeMessage;
        var holder=UIManager.Instance==null?null:UIManager.Instance.GetElement<UI_MessageBoxHolder>();
        if(forgeModal==null && nativePanel!=null && nativePanel.IsOpened && holder!=null && !holder.HasOpenedBox) ShowForgeResult();
    }
    private void TickNativeFlow()
    {
        if(!NativeActive) return;
        if(nativeStep==NativeStep.Running)
        {
            nativeHint=forgeMessage;
            if(!ForgeBusy) FinishNative();
            return;
        }
        if(!NativeContextValid()) { CancelNative("背包、角色或开关状态变化"); return; }
        if(nativeStep==NativeStep.Target && !NativeRowUnchanged(nativeMaterialRow))
        { CancelNative("材料已移动或变化，请重新选择"); return; }
        if(nativeStep==NativeStep.Choice && !NativeRowsUnchanged())
        { CancelNative("材料或目标已变化，请重新选择");return; }
        if(nativeStep==NativeStep.Confirm && (forgeModal==null))
        { CancelNative("确认框已关闭，未吞噬材料"); return; }
    }
}
