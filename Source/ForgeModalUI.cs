using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;

public sealed partial class BodyForgePanel
{
    private GameObject forgeModal;
    private RectTransform modalCard;
    private TMP_Text modalFont;
    private Action modalCancel;
    private Func<bool> modalValid;
    private Action modalRefresh;
    private float nextModalRefresh;
    private int modalVersion;
    private Vector2 modalSize;
    private float modalUserScale=1;
    private bool modalReflowResize;
    private Vector2 ledgerUserSize=new Vector2(780,560);
    private TMP_Text FindNativeFont()
    {
        if(UIManager.Instance==null)return null;
        var panel=UIManager.Instance.GetElement<UI_CharacterStatusPanel>();
        if(panel==null)return null;
        var font=panel.selectItemScreenText;
        return font!=null?font:panel.GetComponentInChildren<TMP_Text>(true);
    }
    private RectTransform BeginForgeModal(string title,float width,float height,Action cancel)
    {
        CloseForgeModal();
        modalFont=FindNativeFont();
        if(modalFont==null || modalFont.font==null)throw new InvalidOperationException("原生界面尚未准备好，请打开背包后再试");
        forgeModal=new GameObject("BodyForgeDialog",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
        var canvas=forgeModal.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30000;
        var root=(RectTransform)forgeModal.transform;
        var shade=Rect("Shade",root,Vector2.zero,Vector2.zero);
        shade.anchorMin=Vector2.zero;shade.anchorMax=Vector2.one;shade.offsetMin=shade.offsetMax=Vector2.zero;
        shade.gameObject.AddComponent<Image>().color=new Color(0,0,0,.52f);
        modalSize=new Vector2(width,height);
        modalCard=Rect("Dialog",root,modalSize,Vector2.zero);
        modalCard.anchorMin=modalCard.anchorMax=modalCard.pivot=new Vector2(.5f,.5f);
        PanelGraphic(modalCard,new Color(.18f,.14f,.21f,1));
        // Keep the title inside the backpack frame's inner border. Centering the
        // line also accommodates the selected game's font ascent/descent.
        var heading=ModalText(modalCard,title,28,22,width-98,32,20,new Color(1,.87f,.62f));
        heading.textWrappingMode=TextWrappingModes.NoWrap;
        heading.alignment=TextAlignmentOptions.MidlineLeft;
        heading.rectTransform.anchorMax=new Vector2(1,1);
        heading.rectTransform.sizeDelta=new Vector2(-98,32);
        modalCancel=cancel;
        var close=ModalButton(modalCard,"×",width-54,22,30,30,CancelForgeModal).GetComponent<RectTransform>();
        close.anchorMin=close.anchorMax=new Vector2(1,1);close.anchoredPosition=new Vector2(-54,-22);
        AddResizeGrip(width,height);
        TickForgeModal();UpdateInputBlock();return modalCard;
    }
    private void AddResizeGrip(float width,float height)
    {
        var rect=Rect("ResizeGrip",modalCard,new Vector2(22,22),new Vector2(width-24,-(height-24)));
        rect.anchorMin=rect.anchorMax=new Vector2(1,0);rect.anchoredPosition=new Vector2(-24,24);
        var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.40f,.30f,.23f,.95f);
        for(int i=0;i<2;i++)
        {
            var line=Rect("GripLine",rect,new Vector2(2,10+i*5),new Vector2(12+i*5,-(6+i*2)));
            line.localEulerAngles=new Vector3(0,0,-45);
            var ink=line.gameObject.AddComponent<Image>();ink.color=new Color(1,.87f,.62f);ink.raycastTarget=false;
        }
        int version=modalVersion;
        rect.gameObject.AddComponent<ForgeResizeGrip>().Changed=delta=>{
            if(forgeModal==null || modalCard==null || version!=modalVersion)return;
            if(modalReflowResize)
            {
                float scale=NativeToolbarLayout.ScreenScale(Screen.width,Screen.height);
                ledgerUserSize=new Vector2(Mathf.Clamp(modalSize.x+2*delta.x/scale,420,(Screen.width-24)/scale),
                    Mathf.Clamp(modalSize.y-2*delta.y/scale,420,(Screen.height-24)/scale));
                nextModalRefresh=0;return;
            }
            modalUserScale=NativeToolbarLayout.ResizeModal(Screen.width,Screen.height,modalSize.x,modalSize.y,modalUserScale,delta.x,delta.y);
            modalCard.localScale=Vector3.one*NativeToolbarLayout.ModalScale(Screen.width,Screen.height,modalSize.x,modalSize.y,modalUserScale);
        };
    }
    private void PanelGraphic(RectTransform rect,Color fallback)
    {
        var image=rect.gameObject.AddComponent<Image>();
        image.color=fallback;
        if(ApplyNativeFrame(image,rect==modalCard))return;
        var outline=rect.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.55f,.43f,.45f,1);outline.effectDistance=new Vector2(2,-2);
    }
    private TextMeshProUGUI ModalText(RectTransform parent,string text,float x,float y,float width,float height,float size,Color color)
    {
        var label=NativeText("Label",parent,new Vector2(width,height),new Vector2(x,-y),modalFont);
        // Font selection may change ascent/descent and line height substantially.
        // Fixed point sizes with Ellipsis can hide even the first line entirely.
        label.margin=Vector4.zero;
        label.fontSizeMin=Mathf.Min(12,size);label.fontSizeMax=size;
        label.enableAutoSizing=true;
        label.text=text;label.fontSize=size;label.color=color;label.alignment=TextAlignmentOptions.TopLeft;
        return label;
    }
    private Button ModalButton(RectTransform parent,string text,float x,float y,float width,float height,Action action)
    {
        var rect=Rect("Action",parent,new Vector2(width,height),new Vector2(x,-y));
        var image=rect.gameObject.AddComponent<Image>(); image.color=Color.white;
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
        var colors=ColorBlock.defaultColorBlock;
        colors.normalColor=new Color(.31f,.25f,.19f,1);colors.highlightedColor=new Color(.48f,.37f,.23f,1);
        colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(.22f,.17f,.13f,1);colors.disabledColor=new Color(.23f,.21f,.25f,1);button.colors=colors;
        ApplyNativeButton(button,image);
        var label=ModalText(rect,text,6,3,width-12,height-6,18,new Color(1,.94f,.77f));label.alignment=TextAlignmentOptions.Center;
        int version=modalVersion;
        button.onClick.AddListener(()=>{
            if(forgeModal==null || version!=modalVersion)return;
            try {action();}catch(Exception ex){forgeMessage="操作未完成："+ex.Message;CancelForgeModal();}
        });
        return button;
    }
    private void CloseForgeModal()
    {
        modalVersion++;modalCancel=null;modalValid=null;modalRefresh=null;modalReflowResize=false;
        if(forgeModal!=null){forgeModal.SetActive(false);UnityEngine.Object.Destroy(forgeModal);}
        forgeModal=null;modalCard=null;modalFont=null;
        UpdateInputBlock();
    }
    private void CancelForgeModal()
    { var cancel=modalCancel;CloseForgeModal();if(cancel!=null)cancel(); }
    private void TickForgeModal()
    {
        if(forgeModal==null)return;
        if(modalValid!=null && !modalValid()) { CancelForgeModal();return; }
        if(Keyboard.current!=null && Keyboard.current[Key.Escape].wasPressedThisFrame)
        {CancelForgeModal();return;}
        if(modalReflowResize)
        {
            float scale=NativeToolbarLayout.ScreenScale(Screen.width,Screen.height);
            modalSize=new Vector2(Mathf.Min(ledgerUserSize.x,(Screen.width-24)/scale),Mathf.Min(ledgerUserSize.y,(Screen.height-24)/scale));
            modalCard.sizeDelta=modalSize;modalCard.localScale=Vector3.one*scale;
        }
        else modalCard.localScale=Vector3.one*NativeToolbarLayout.ModalScale(Screen.width,Screen.height,modalSize.x,modalSize.y,modalUserScale);
        if(modalRefresh!=null && Time.unscaledTime>=nextModalRefresh)
        {nextModalRefresh=Time.unscaledTime+.25f;modalRefresh();}
    }
    private void OpenForgeHub()
    {
        EnsureForgeProgress();
        var card=BeginForgeModal("锻体",540,300,null);
        ModalText(card,NativeActive?nativeHint:forgeMessage,22,65,496,80,18,Color.white);
        var start=ModalButton(card,NativeActive?"停止／取消本次":forgeBlocked?"解除锁定":"开始锻体",22,160,240,40,()=>{
            CloseForgeModal();
            if(NativeActive)CancelNative("已手动停止，已发送请求仍可能生效");
            else if(forgeBlocked){RecoverForge();OpenForgeHub();}
            else {BeginNativeForge();}
        });
        start.interactable=NativeActive || forgeBlocked || (NativeContextValid() && !ForgeBusy);
        ModalButton(card,"查看锻体收益",278,160,240,40,()=>OpenForgeProgress(0));
        ModalText(card,"白色材料直接选配方，其他材料需选附魔目标。\n重掷次数按材料计算，取消重开不会恢复。",22,216,496,62,16,new Color(.87f,.82f,.73f));
        modalValid=()=>Ready() && BodyForgeSettings.Current.Enabled && nativePanel!=null && nativePanel.IsOpened && nativePanel.PlayerAvatar==owner;
    }
    private void OpenRecipeChoices(ForgeRow material,ForgeRow target,Action picked,Action cancel)
    {
        var cards=RecipesFor(material);
        var card=BeginForgeModal("选择锻体配方",744,440,cancel);
        string targetText=material.Count==0?"\n仅获得属性奖励，无需附魔目标。":"    目标："+target.Name+"\n目标附魔 +"+material.Count+"；选择一份配方，获得其中全部奖励。";
        ModalText(card,"吞噬："+material.Name+" ×1"+targetText,22,60,700,60,17,Color.white);
        for(int i=0;i<cards.Length;i++)
        {
            var recipe=cards[i];float x=22+i*238;
            var tile=Rect("Recipe",card,new Vector2(222,198),new Vector2(x,-130));
            PanelGraphic(tile,new Color(.24f,.19f,.28f,1));
            var heading=ModalText(tile,string.IsNullOrEmpty(recipe.Title)?"配方 "+(i+1):recipe.Title,14,12,194,30,18,new Color(1,.88f,.58f));
            heading.textWrappingMode=TextWrappingModes.NoWrap;
            heading.alignment=TextAlignmentOptions.MidlineLeft;
            for(int r=0;r<recipe.Rewards.Length;r++)
                ModalReward(tile,recipe.Rewards[r],14,50+r*44,194,44,18);
            bool usable=RecipeAvailable(recipe);
            ModalButton(tile,usable?"选择":"奖励已达上限",12,148,198,36,()=>{
                if(!ForgeRowsUnchanged(material,target) || !RecipeAvailable(recipe))
                {forgeMessage="材料、目标或奖励资格已变化，未消耗";CancelForgeModal();return;}
                chosenRecipe=recipe;chosenMaterial=material.Instance;chosenTarget=material.Count==0?-1:target.Instance;
                CloseForgeModal();picked();
            }).interactable=usable;
        }
        int remaining=RerollsLeft(material);
        ModalButton(card,"重掷全部（剩余 "+remaining+" 次）",22,344,300,38,()=>{
            if(!ForgeRowsUnchanged(material,target)){CancelForgeModal();return;}
            if(RerollRecipes(material))OpenRecipeChoices(material,target,picked,cancel);
        }).interactable=remaining>0;
        ModalText(card,"按首次生成时的连击抽取；重掷不更新连击，取消不恢复次数。\n每消耗一件材料，下一件重新读取连击和重掷次数。",22,388,700,44,15,new Color(.86f,.82f,.75f));
        modalValid=()=>Ready() && ForgeRowsUnchanged(material,target) && BodyForgeSettings.Current.Enabled;
    }
    private void OpenStandaloneRecipes(ForgeRow material,ForgeRow target)
    {
        OpenRecipeChoices(material,target,()=>ShowForgeConfirmation(material,target,StartForge,ClearRecipeSelection),ClearRecipeSelection);
    }
    private void ShowForgeConfirmation(ForgeRow material,ForgeRow target,Action confirm,Action cancel)
    {
        var card=BeginForgeModal("确认锻体",560,390,cancel);
        ModalText(card,"消耗  "+material.Name+" ×1",24,66,512,48,18,new Color(.91f,.83f,.76f));
        ModalText(card,material.Count==0?"白色材料：仅获取所选属性，不附魔。":"附魔  "+target.Name+" +"+material.Count,24,118,512,48,18,new Color(1,.86f,.60f));
        var rewards=Rect("Rewards",card,new Vector2(512,98),new Vector2(24,-172));
        PanelGraphic(rewards,new Color(.24f,.19f,.28f,1));
        for(int r=0;r<chosenRecipe.Rewards.Length;r++)
            ModalReward(rewards,chosenRecipe.Rewards[r],14,14+r*36,484,36,20);
        ModalText(card,"材料将被消耗，原有属性与羁绊随之移除。",24,286,512,30,16,new Color(.79f,.69f,.68f));
        modalValid=()=>Ready() && ForgeRowsUnchanged(material,target) && BodyForgeSettings.Current.Enabled;
        ModalButton(card,"取消",24,330,160,38,CancelForgeModal);
        ModalButton(card,"确认锻体",200,330,336,38,()=>{
            if(!ForgeRowsUnchanged(material,target) || !RecipeAvailable(chosenRecipe)){CancelForgeModal();return;}
            CloseForgeModal();confirm();
        });
    }
    private void ShowForgeResult()
    {
        var card=BeginForgeModal("锻体结果",560,350,null);
        ModalText(card,forgeMessage,24,64,512,76,18,new Color(1,.86f,.60f));
        ModalText(card,forgeResults,24,146,512,116,18,new Color(.92f,.89f,.85f));
        ModalButton(card,"查看收益",24,290,244,38,()=>OpenForgeProgress(0));
        ModalButton(card,"继续锻体",284,290,252,38,()=>{
            CloseForgeModal();
            if(forgeBlocked)
            {
                RecoverForge();
                if(forgeBlocked){OpenForgeHub();return;}
            }
            BeginNativeForge();
            if(!NativeActive){forgeMessage=nativeHint;OpenForgeHub();}
        });
        modalValid=()=>Ready() && BodyForgeSettings.Current.Enabled && nativePanel!=null && nativePanel.IsOpened && nativePanel.PlayerAvatar==owner;
    }
    private static Sprite RewardIcon(ForgeReward reward)
    {
        if(reward==null)return null;
        string keyword="InventorySlot",category="";
        if(reward.Kind!=7)
        {
            var status=StatusDatabase.GetStatusEntity(reward.Metadata.Split('/')[0]);
            if(status==null)return null;
            keyword=status.statKeyword;
            category=RewardIconCategory(status.id);
        }
        var entity=string.IsNullOrEmpty(keyword)?null:KeywordDatabase.GetEntity(keyword);
        var sprite=entity==null?null:entity.keywordImage!=null?entity.keywordImage:entity.bigKeywordSprite;
        if(sprite!=null)return sprite;
        // Some native specialized keywords contain text only. Use their actual
        // synergy badge instead of leaving an empty icon or inventing a sprite.
        var combo=string.IsNullOrEmpty(category)?null:ItemDatabase.FindItemCategory(category);
        return combo==null?null:combo.categoryIcon;
    }
    private static string RewardIconCategory(string id)
    {
        if(string.IsNullOrEmpty(id))return "";
        if(id.StartsWith("FROST_RELIC_",StringComparison.Ordinal)||id.StartsWith("CHARGING_CHARM_",StringComparison.Ordinal))return "FROST";
        if(id.StartsWith("FLAME_SWORD_",StringComparison.Ordinal))return "FLAMESWORD";
        if(id.StartsWith("DARK_CLOUD_",StringComparison.Ordinal)||id=="MIN_DARK_CLOUD")return "DARKCLOUD";
        if(id.StartsWith("BURN_",StringComparison.Ordinal))return "EMBER";
        if(id.StartsWith("FREEZE_",StringComparison.Ordinal)||id=="FROSTBITE_DAMAGE")return "GLACIER";
        if(id.StartsWith("ELECTRIC_",StringComparison.Ordinal))return "MAGITECH";
        if(id.StartsWith("DEBUFF_",StringComparison.Ordinal))return "CURSE";
        if(id.StartsWith("FOLLOWER_",StringComparison.Ordinal))return "COMPANION";
        if(id=="NEGOTIATION"||id=="LEAF_DROP")return "SAVVY";
        if(id.StartsWith("MAGIC_",StringComparison.Ordinal))return "ACADEMY";
        if(id=="MP_REGEN_MULTIPLE"||id=="FINAL_MP")return "LAKE";
        if(id=="DASH_COUNT")return "SHADOW";
        return "";
    }
    private Image ModalIcon(RectTransform parent,float x,float y,float size)
    {
        var rect=Rect("AttributeIcon",parent,new Vector2(size,size),new Vector2(x,-y));
        var image=rect.gameObject.AddComponent<Image>();
        image.preserveAspect=true;image.raycastTarget=false;image.color=Color.white;
        return image;
    }
    private void ModalReward(RectTransform parent,ForgeReward reward,float x,float y,float width,float height,float size)
    {
        var sprite=RewardIcon(reward);float inset=sprite==null?0:30;
        if(sprite!=null){var icon=ModalIcon(parent,x,y,24);icon.sprite=sprite;}
        ModalText(parent,reward.Description,x+inset,y,width-inset,height,size,new Color(.94f,.91f,.85f));
    }

}

public sealed class ForgeResizeGrip : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    internal Action<Vector2> Changed;
    private bool dragging;
    private Vector2 previous;
    public void OnBeginDrag(PointerEventData data)
    {
        if(data.button!=PointerEventData.InputButton.Left)return;
        dragging=true;previous=data.position;data.Use();
    }
    public void OnDrag(PointerEventData data)
    {
        if(!dragging || data.button!=PointerEventData.InputButton.Left)return;
        var delta=data.position-previous;previous=data.position;
        if(Changed!=null)Changed(delta);data.Use();
    }
    public void OnEndDrag(PointerEventData data) { dragging=false;data.Use(); }
    private void OnDisable() { dragging=false;Changed=null; }
}

