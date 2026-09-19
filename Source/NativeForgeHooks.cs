using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine.EventSystems;

internal static class NativeForgeHooks
{
    private static Harmony harmony;
    internal static bool Installed;
    internal static void Install()
    {
        if(Installed) return;
        harmony=new Harmony("local.sephiria.bodyforge.nativeui");
        try
        {
            Prefix(typeof(UI_CharacterStatusPanel),"OnItemClicked","ItemClick");
            Prefix(typeof(UI_CharacterStatusPanel),"OnSubBagItemClicked","SubBagClick");
            foreach(string name in new[]{"OnBeginDrag","OnDrag","OnEndDrag","OnDrop"})
                Prefix(typeof(UI_SubBagIcon),name,"SubBagInput");
            foreach(string name in new[]{"OnItemLongClicked","HandleDrop","HandleDropSubBag","ActivateSelectedItem"})
                Prefix(typeof(UI_CharacterStatusPanel),name,"PanelInput");
            foreach(string name in new[]{"OnPointerDown","OnPointerUp","OnBeginDrag","OnDrag","OnEndDrag","OnDrop"})
                Prefix(typeof(UI_NewInventoryIcon),name,"IconInput");
            foreach(Type type in new[]{typeof(UI_NewItemPicker),typeof(UI_NewItemPicker_Controller)})
                foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly))
                    if(method.Name.StartsWith("Pick",StringComparison.Ordinal))
                        harmony.Patch(method,prefix:new HarmonyMethod(typeof(NativeForgeHooks),"OtherInput"));
            Prefix(typeof(UI_ItemDropZone),"DropItem","OtherInput");
            Prefix(typeof(UI_ItemDropZone),"DropSubBagItem","OtherInput");
            harmony.Patch(AccessTools.Method(typeof(UI_CharacterStatusPanel),"OnClosed"),
                postfix:new HarmonyMethod(typeof(NativeForgeHooks),"PanelClosed"));
            Installed=true;
        }
        catch { Uninstall(); throw; }
    }
    private static void Prefix(Type type,string method,string patch)
    {
        var original=AccessTools.Method(type,method);
        if(original==null) throw new MissingMethodException(type.Name,method);
        harmony.Patch(original,prefix:new HarmonyMethod(typeof(NativeForgeHooks),patch));
    }
    internal static void Uninstall()
    { Installed=false; if(harmony!=null) harmony.UnpatchSelf(); harmony=null; }
    private static bool ItemClick(UI_CharacterStatusPanel __instance,PointerEventData.InputButton __0,UI_NewInventoryIcon __1)
    {
        var panel=BodyForgePanel.Instance;
        if(panel==null || !panel.InterceptsNative(__instance)) return true;
        panel.HandleNativeClick(__0,__1); return false;
    }
    private static bool PanelInput(UI_CharacterStatusPanel __instance)
    { return BodyForgePanel.Instance==null || !BodyForgePanel.Instance.InterceptsNative(__instance); }
    private static bool SubBagClick(UI_CharacterStatusPanel __instance,PointerEventData.InputButton __0,UI_SubBagIcon __1)
    {
        var panel=BodyForgePanel.Instance;
        if(panel==null || !panel.InterceptsNative(__instance))return true;
        panel.HandleNativeSubBagClick(__0,__1);return false;
    }
    private static bool SubBagInput(UI_SubBagIcon __instance)
    { return BodyForgePanel.Instance==null || !BodyForgePanel.Instance.InterceptsNativeSubBag(__instance); }
    private static bool IconInput(UI_NewInventoryIcon __instance)
    { return BodyForgePanel.Instance==null || !BodyForgePanel.Instance.InterceptsNativeIcon(__instance); }
    private static bool OtherInput()
    { return BodyForgePanel.Instance==null || !BodyForgePanel.Instance.NativeActive; }
    private static void PanelClosed(UI_CharacterStatusPanel __instance)
    {
        var panel=BodyForgePanel.Instance;
        if(panel!=null && panel.InterceptsNative(__instance)) panel.HandleNativeClosed();
    }
}
