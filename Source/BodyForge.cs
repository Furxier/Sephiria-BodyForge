using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;


[Serializable]
internal sealed class BodyForgeSettings
{
    public bool Enabled=true;
    public bool EnableSpecializedRewards=true;
    internal static BodyForgeSettings Current=new BodyForgeSettings();
    private static string ConfigPath
    { get { return Path.Combine(Path.GetDirectoryName(typeof(BodyForgeMod).Assembly.Location),"config.json"); } }
    internal static void Load()
    {
        if(!File.Exists(ConfigPath)) { Save(true); return; }
        var settings=JsonUtility.FromJson<BodyForgeSettings>(File.ReadAllText(ConfigPath));
        if(settings==null) throw new InvalidOperationException("锻体配置无效");
        // Unity deserialization does not reliably run field initializers for missing fields.
        if(!Regex.IsMatch(File.ReadAllText(ConfigPath),"\"EnableSpecializedRewards\"\\s*:"))settings.EnableSpecializedRewards=true;
        Current=settings;
    }
    internal static void Save(bool enabled)
    {
        var settings=new BodyForgeSettings { Enabled=enabled,EnableSpecializedRewards=Current.EnableSpecializedRewards };
        File.WriteAllText(ConfigPath,JsonUtility.ToJson(settings,true));
        Current=settings;
    }
}

public sealed class BodyForgeMod : HorayModBase
{
    private GameObject host;
    private const string LoaderKey="Local.Sephiria.BodyForge.ActiveLoader";
    protected override void OnModLoaded()
    {
        if(host!=null) return;
        foreach(var existing in Resources.FindObjectsOfTypeAll<GameObject>())
            if(existing!=null && existing.name=="BodyForgeHost")
            {
                existing.SendMessage("ShutdownPanel",SendMessageOptions.DontRequireReceiver);
                existing.SetActive(false); UnityEngine.Object.Destroy(existing);
            }
        AppDomain.CurrentDomain.SetData(LoaderKey,this);
        try { BodyForgeSettings.Load(); }
        catch(Exception ex)
        {
            BodyForgeSettings.Current.Enabled=false;
            Debug.LogError("[BodyForge] 配置加载失败，已关闭锻体："+ex.Message);
        }
        host=new GameObject("BodyForgeHost");
        UnityEngine.Object.DontDestroyOnLoad(host); host.hideFlags=HideFlags.HideAndDontSave;
        host.AddComponent<BodyForgePanel>();
        try { NativeForgeHooks.Install(); }
        catch(Exception ex) { Debug.LogError("[BodyForge] 原生背包入口安装失败："+ex); }
        Debug.Log("[BodyForge] 独立锻体 Mod 已加载，从背包进入；启用="+BodyForgeSettings.Current.Enabled);
    }
    protected override void OnModUnloaded()
    {
        if(!object.ReferenceEquals(AppDomain.CurrentDomain.GetData(LoaderKey),this)) return;
        if(BodyForgePanel.Instance!=null) BodyForgePanel.Instance.CancelNative("Mod 已卸载");
        if(host!=null) { host.SendMessage("ShutdownPanel",SendMessageOptions.DontRequireReceiver); host.SetActive(false); UnityEngine.Object.Destroy(host); }
        host=null;
        NativeForgeHooks.Uninstall();
        AppDomain.CurrentDomain.SetData(LoaderKey,null);
    }
}

public sealed partial class BodyForgePanel : MonoBehaviour
{
    private PlayerAvatar owner;
    private readonly System.Random random=new System.Random();
    private readonly Dictionary<int,string> names=new Dictionary<int,string>();
    private readonly SharedPanelInput inputLease=new SharedPanelInput();
    private PlayerAvatar LocalPlayer()
    {
        var identity=Mirror.NetworkClient.localPlayer;
        if(identity==null) return null;
        var spawner=identity.GetComponent<PlayerSpawner>();
        return spawner==null?identity.GetComponent<PlayerAvatar>():spawner.PlayerAvatar;
    }
    private bool Ready() { return owner!=null && owner.Inventory!=null && owner.Inventory.isOwned; }
    private void Awake() { Instance=this; }
    private void Update()
    {
        try
        {
            var player=LocalPlayer();
            if(player!=owner)
            {
                CancelNative("角色已切换");
                CloseForgeModal(); ClearRecipeSelection();
                StopForge("角色已切换"); owner=player;
                forgeMaterial=-1; forgeTarget=-1; forgeBlocked=false;
            }
            EnsureForgeProgress();
            TickForgeModal();
            if(!BodyForgeSettings.Current.Enabled) StopForge("锻体已关闭");
            TickNativeFlow();
            TickForge();
            TickMilestones();
            TickNativeFlow();
            TickNativeUI();
        }
        catch(Exception ex) { CloseForgeModal(); CancelNative("操作异常："+ex.Message); StopForge("操作异常："+ex.Message); forgeMessage="操作异常："+ex.Message; }
    }
    private void UpdateInputBlock()
    {
        inputLease.Update(forgeModal!=null,owner!=null && owner.localDataStorage!=null);
    }
    private void LateUpdate() { UpdateInputBlock(); }
    private void ShutdownPanel() { DisposeNativeUI(); StopForge("Mod 已卸载"); inputLease.Update(false,false); }
    private void OnDisable() { ShutdownPanel(); }
    private void OnDestroy() { DisposeNativeUI(); inputLease.Update(false,false); }
    private string Name(ItemEntity item)
    {
        string name;
        if(!names.TryGetValue(item.id,out name))
        { name=Regex.Replace(item.Name??"未知道具","<[^>]*>",""); names[item.id]=name; }
        return name;
    }
    private static Color ItemNameColor(ItemEntity item) { return ItemDatabase.GetColorViaItemRarity(item.rarity); }
    private static int EnchantLevel(int instance)
    {
        int value;
        return DungeonManager.Instance!=null && int.TryParse(DungeonManager.Instance.GetGlobalItemStatValue(instance,"Enchant"),out value)?value:0;
    }
}
