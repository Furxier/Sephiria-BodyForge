using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public sealed partial class BodyForgePanel
{
    internal static BodyForgePanel Instance;
    private UI_CharacterStatusPanel nativePanel;
    private RectTransform nativeToolbar;
    private Button nativeButton;
    private TextMeshProUGUI nativeButtonText, nativeHintText;
    private Image nativeBackground;
    private float nextNativeScan;
    private readonly Vector3[] nativeCorners=new Vector3[4];
    private string lastNativeHint, lastNativeButton;
    private bool nativeManuallyPlaced;
    private Vector2 nativePositionRatio;
    private NativeToolbarLayout.Placement nativePlacement;
    private void TickNativeUI()
    {
        if(!NativeForgeHooks.Installed || UIManager.Instance==null) return;
        if(Time.unscaledTime>=nextNativeScan)
        {
            nextNativeScan=Time.unscaledTime+0.5f;
            var panel=UIManager.Instance.GetElement<UI_CharacterStatusPanel>();
            if(panel!=nativePanel)
            {
                HandleNativeClosed();
                if(nativeToolbar!=null) { nativeToolbar.gameObject.SetActive(false); UnityEngine.Object.Destroy(nativeToolbar.gameObject); }
                nativePanel=panel; nativeToolbar=null;
            }
            if(nativePanel!=null && nativePanel.IsOpened && nativeToolbar==null) CreateNativeToolbar();
        }
        if(nativeToolbar==null) return;
        bool visible=nativePanel!=null && nativePanel.IsOpened && Ready() && nativePanel.PlayerAvatar==owner &&
            BodyForgeSettings.Current.Enabled;
        if(nativeToolbar.gameObject.activeSelf!=visible) nativeToolbar.gameObject.SetActive(visible);
        if(!visible) return;
        bool details=NativeActive || forgeBlocked;
        nativeToolbar.sizeDelta=new Vector2(details?NativeToolbarLayout.DetailWidth:NativeToolbarLayout.ButtonWidth,
            details?NativeToolbarLayout.DetailHeight:NativeToolbarLayout.ButtonHeight);
        nativeHintText.gameObject.SetActive(details);
        nativeBackground.enabled=details;
        PositionNativeToolbar();
        nativeButton.interactable=true;
        string button="锻体";
        if(lastNativeButton!=button) { ForgeLocalizedLabel.Set(nativeButtonText,button); lastNativeButton=button; }
        if(lastNativeHint!=nativeHint) { ForgeLocalizedLabel.Set(nativeHintText,nativeHint); lastNativeHint=nativeHint; }
    }
    private void CreateNativeToolbar()
    {
        var parent=nativePanel.transform as RectTransform;
        if(parent==null || nativePanel.inventoryScrollParent==null) return;
        TMP_Text source=nativePanel.selectItemScreenText;
        if(source==null) source=nativePanel.GetComponentInChildren<TMP_Text>(true);
        if(source==null || source.font==null) return;
        nativeToolbar=Rect("BodyForgeNativeToolbar",parent,new Vector2(NativeToolbarLayout.ButtonWidth,NativeToolbarLayout.ButtonHeight),Vector2.zero);
        nativeBackground=nativeToolbar.gameObject.AddComponent<Image>();
        nativeBackground.color=new Color(.09f,.10f,.13f,.98f); nativeBackground.raycastTarget=true;
        ApplyNativeFrame(nativeBackground,false);
        var buttonRect=Rect("ForgeButton",nativeToolbar,new Vector2(NativeToolbarLayout.ButtonWidth,NativeToolbarLayout.ButtonHeight),Vector2.zero);
        var graphic=buttonRect.gameObject.AddComponent<Image>();
        graphic.color=Color.white;
        var draggable=buttonRect.gameObject.AddComponent<ForgeDraggableButton>();
        draggable.Moved=MoveNativeToolbar;
        nativeButton=draggable; nativeButton.targetGraphic=graphic;
        var colors=ColorBlock.defaultColorBlock;
        colors.normalColor=new Color(.24f,.16f,.25f,1);
        colors.highlightedColor=new Color(.36f,.25f,.35f,1);
        colors.selectedColor=colors.highlightedColor;
        colors.pressedColor=new Color(.16f,.10f,.18f,1);
        colors.disabledColor=new Color(.24f,.23f,.20f,1);
        nativeButton.colors=colors;
        // Opaque compact tab: the native toggle sprite has a translucent center.
        var border=buttonRect.gameObject.AddComponent<Outline>();
        border.effectColor=new Color(.53f,.40f,.47f,1);
        border.effectDistance=new Vector2(2,-2);border.useGraphicAlpha=false;
        nativeButton.onClick.AddListener(NativeButtonClicked);
        nativeButtonText=NativeText("Text",buttonRect,new Vector2(NativeToolbarLayout.ButtonWidth-16,NativeToolbarLayout.ButtonHeight-8),new Vector2(8,-4),source);
        nativeButtonText.fontSize=24; nativeButtonText.color=new Color(1,.96f,.83f,1);
        nativeButtonText.alignment=TextAlignmentOptions.Center;
        nativeHintText=NativeText("Instructions",nativeToolbar,new Vector2(268,58),new Vector2(6,-NativeToolbarLayout.ButtonHeight-6),source);
        nativeHintText.alignment=TextAlignmentOptions.TopLeft; nativeHintText.fontSize=14;
        lastNativeButton=null; lastNativeHint=null;
        nativeToolbar.SetAsLastSibling();
    }
    private static RectTransform Rect(string name,RectTransform parent,Vector2 size,Vector2 position)
    {
        var go=new GameObject(name,typeof(RectTransform));
        var rect=(RectTransform)go.transform; rect.SetParent(parent,false);
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
        rect.sizeDelta=size; rect.anchoredPosition=position;
        return rect;
    }
    private static Image NativeFrameSource(bool inventory)
    {
        if(UIManager.Instance==null)return null;
        var panel=UIManager.Instance.GetElement<UI_CharacterStatusPanel>();
        if(panel==null)return null;
        // These exact nodes were verified in the game's CharacterPanel asset.
        string path=inventory?"InentoryParent/Scroll View/Viewport/Content/InventoryZone":
            "InentoryParent/SkillAndCombo/Skill/Toggle/Background";
        var node=panel.transform.Find(path);
        return node==null?null:node.GetComponent<Image>();
    }
    private static bool ApplyNativeFrame(Image target,bool inventory)
    {
        var source=NativeFrameSource(inventory);
        if(source==null || source.sprite==null)return false;
        target.sprite=source.sprite;
        target.type=Image.Type.Sliced;target.fillCenter=true;target.preserveAspect=false;
        // Our overlay canvas has a different scale from the original backpack.
        // Keep one sprite pixel per reference UI unit; resolution scaling follows.
        target.pixelsPerUnitMultiplier=100f/Mathf.Max(1,source.sprite.pixelsPerUnit);
        var color=source.color;color.a=1;target.color=color;
        return true;
    }
    private static void ApplyNativeButton(Button button,Image image)
    {
        if(!ApplyNativeFrame(image,false))return;
        Color normal=image.color;image.color=Color.white;
        var colors=ColorBlock.defaultColorBlock;
        colors.normalColor=normal;
        colors.highlightedColor=Color.Lerp(normal,Color.white,.2f);
        colors.selectedColor=colors.highlightedColor;
        colors.pressedColor=Color.Lerp(normal,Color.black,.25f);
        colors.disabledColor=new Color(normal.r,normal.g,normal.b,.45f);
        button.colors=colors;
    }
    private static TextMeshProUGUI NativeText(string name,RectTransform parent,Vector2 size,Vector2 position,TMP_Text source)
    {
        var rect=Rect(name,parent,size,position);
        // Assign the text reference before the game's font component subscribes
        // in OnEnable. It applies the user's selection and follows font changes.
        rect.gameObject.SetActive(false);
        var label=rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font=source.font; label.fontSharedMaterial=source.fontSharedMaterial;
        label.fontSize=14; var color=source.color; color.a=1; label.color=color; label.richText=false;
        label.raycastTarget=false; label.textWrappingMode=TextWrappingModes.Normal;
        label.overflowMode=TextOverflowModes.Ellipsis;
        var fontChanger=rect.gameObject.AddComponent<UI_LocalizationFontChanger>();
        fontChanger.text=label; fontChanger.style="Default";
        rect.gameObject.SetActive(true);
        return label;
    }
    private void MoveNativeToolbar(Vector2 delta)
    {
        if(nativeToolbar==null || !nativeToolbar.gameObject.activeInHierarchy)return;
        var moved=NativeToolbarLayout.Move(nativePlacement,Screen.width,Screen.height,nativePlacement.X+delta.x,nativePlacement.Top+delta.y);
        nativeManuallyPlaced=true;
        nativePositionRatio=new Vector2(moved.X/Mathf.Max(1,Screen.width),moved.Top/Mathf.Max(1,Screen.height));
        PositionNativeToolbar();
    }
    private void PositionNativeToolbar()
    {
        var reference=nativePanel.inventoryScrollParent;
        var parent=nativeToolbar.parent as RectTransform;
        if(reference==null || parent==null) return;
        var canvas=parent.GetComponentInParent<Canvas>();
        var rootCanvas=canvas==null?null:canvas.rootCanvas;
        Camera camera=rootCanvas==null || rootCanvas.renderMode==RenderMode.ScreenSpaceOverlay?null:rootCanvas.worldCamera;
        reference.GetWorldCorners(nativeCorners);
        float left=float.MaxValue,bottom=float.MaxValue,right=float.MinValue,top=float.MinValue;
        for(int i=0;i<4;i++)
        {
            Vector2 point=RectTransformUtility.WorldToScreenPoint(camera,nativeCorners[i]);
            left=Mathf.Min(left,point.x); right=Mathf.Max(right,point.x);
            bottom=Mathf.Min(bottom,point.y); top=Mathf.Max(top,point.y);
        }
        var placement=NativeToolbarLayout.Calculate(Screen.width,Screen.height,left,bottom,right,top,NativeActive||forgeBlocked);
        if(nativeManuallyPlaced)placement=NativeToolbarLayout.Move(placement,Screen.width,Screen.height,
            nativePositionRatio.x*Screen.width,nativePositionRatio.y*Screen.height);
        nativePlacement=placement;
        Vector2 origin=RectTransformUtility.WorldToScreenPoint(camera,parent.TransformPoint(Vector3.zero));
        float pixelsX=(RectTransformUtility.WorldToScreenPoint(camera,parent.TransformPoint(Vector3.right))-origin).magnitude;
        float pixelsY=(RectTransformUtility.WorldToScreenPoint(camera,parent.TransformPoint(Vector3.up))-origin).magnitude;
        nativeToolbar.localScale=new Vector3(NativeToolbarLayout.LocalScale(pixelsX,placement.Scale),
            NativeToolbarLayout.LocalScale(pixelsY,placement.Scale),1);
        Vector3 world;
        if(RectTransformUtility.ScreenPointToWorldPointInRectangle(parent,new Vector2(placement.X,placement.Top),camera,out world))
            nativeToolbar.position=world;
    }
    private void DisposeNativeUI()
    {
        CloseForgeModal();
        CancelNative("Mod 已卸载");
        if(nativeToolbar!=null) { nativeToolbar.gameObject.SetActive(false); UnityEngine.Object.Destroy(nativeToolbar.gameObject); }
        nativeToolbar=null; nativePanel=null;



        if(Instance==this) Instance=null;
    }
}

// Preserve Button visuals and normal click/keyboard behavior; only a real drag
// suppresses the pointer click. The next press restores normal clicking.
public sealed class ForgeDraggableButton : Button, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    internal System.Action<Vector2> Moved;
    private bool dragging,suppressClick;
    private Vector2 previous;
    public override void OnPointerDown(PointerEventData data)
    {
        if(data.button==PointerEventData.InputButton.Left){dragging=false;suppressClick=false;}
        base.OnPointerDown(data);
    }
    public override void OnPointerClick(PointerEventData data)
    { if(!suppressClick)base.OnPointerClick(data); }
    public void OnBeginDrag(PointerEventData data)
    {
        if(data.button!=PointerEventData.InputButton.Left || !IsActive() || !IsInteractable())return;
        dragging=true;suppressClick=true;previous=data.pressPosition;data.eligibleForClick=false;data.Use();
    }
    public void OnDrag(PointerEventData data)
    {
        if(!dragging || data.button!=PointerEventData.InputButton.Left)return;
        var delta=data.position-previous;previous=data.position;
        if(Moved!=null)Moved(delta);data.eligibleForClick=false;data.Use();
    }
    public void OnEndDrag(PointerEventData data)
    {dragging=false;if(suppressClick)data.eligibleForClick=false;data.Use();}
    protected override void OnDisable()
    {dragging=false;suppressClick=true;base.OnDisable();}
}
