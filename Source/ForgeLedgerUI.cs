using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed partial class BodyForgePanel
{
    private float ledgerScrollOffset;
    private bool ledgerDetails;
    private int ledgerScrollEpoch=-1;
    private sealed class LedgerRow
    {
        internal EarnedReward Entry;
        internal RectTransform Rect;
        internal TextMeshProUGUI Label;
        internal Image Background;
        internal int Group,Amount=-1,Batch=-1;
        internal float HighlightUntil;
        internal bool Highlighted;
    }
    private static int LedgerGroup(EarnedReward entry)
    {
        string id=entry.Reward.Metadata.Split('/')[0],category=RewardIconCategory(id);
        string[] categories={"FROST","FLAMESWORD","DARKCLOUD","EMBER","GLACIER","MAGITECH","CURSE","COMPANION","SAVVY","ACADEMY","LAKE","SHADOW","PLANET","FORTUNE","COMET","WEAPON","PARTY"};
        for(int i=0;i<categories.Length;i++)if(category==categories[i])return 3+i;
        string key=entry.Reward.Key??"";
        if(entry.Reward.Kind==6 || key=="FIREDAMAGE"||key=="ICEDAMAGE"||key=="LIGHTNINGDAMAGE")return 2;
        if(entry.Reward.Kind==2||entry.Reward.Kind==3||entry.Reward.Kind==5||
            key=="DAMAGEREDUCTION"||key=="DEFENSE"||key=="EVASION"||key=="HPREGEN"||key=="MPREGEN"||key=="HPSTEAL"||key=="MPSTEAL")return 1;
        if(key=="WEAPONRANGE"||key=="PHYSICALDAMAGE"||key=="BASICATTACKDAMAGE"||key=="SPECIALATTACKDAMAGE"||key=="ATTACKSPEED"||
            key=="CRITICAL"||key=="CRITICALDAMAGERATE"||key=="CRITICALDAMAGEBONUS"||key=="FINALDAMAGE"||key=="ALLDAMAGEBONUS"||key=="TRUEDAMAGE")return 0;
        return LedgerGroupNames.Length-1;
    }
    private static readonly string[] LedgerGroupNames={"通用伤害","生存恢复","元素伤害","专属效果 · 冰霜武具","专属效果 · 太阳剑",
        "专属效果 · 乌云","专属效果 · 余烬","专属效果 · 冰川","专属效果 · 魔法科技","专属效果 · 诅咒","专属效果 · 同伴","专属效果 · 谈判","专属效果 · 学院","专属效果 · 湖泊","专属效果 · 影子","专属效果 · 行星",
        "专属效果 · 命运","专属效果 · 彗星","专属效果 · 锻造","专属效果 · 派对（本人贡献）","其他收益"};
    private void OpenForgeProgress(int unused)
    {
        EnsureForgeProgress();
        // A different reward dictionary lifetime must not inherit an unrelated scroll position.
        if(ledgerScrollEpoch!=progressEpoch) {ledgerScrollOffset=0;ledgerScrollEpoch=progressEpoch;}
        var card=BeginForgeModal("锻体收益",ledgerUserSize.x,ledgerUserSize.y,null);
        modalReflowResize=true;
        var summary=ModalText(card,"",24,64,600,54,19,new Color(1,.87f,.62f));
        var storageStatus=ModalText(card,"",24,100,600,40,15,new Color(.84f,.79f,.72f));
        var details=ModalText(card,"",24,114,600,44,15,new Color(.78f,.73f,.76f));
        summary.enableAutoSizing=false;storageStatus.enableAutoSizing=false;details.enableAutoSizing=false;
        var detailButton=ModalButton(card,"展开详情",620,67,126,32,()=>{ledgerDetails=!ledgerDetails;nextModalRefresh=0;});
        var detailLabel=detailButton.GetComponentInChildren<TextMeshProUGUI>();
        var returnButton=ModalButton(card,"返回",600,504,130,36,OpenForgeHub).GetComponent<RectTransform>();
        returnButton.anchorMin=returnButton.anchorMax=new Vector2(1,0);returnButton.anchoredPosition=new Vector2(-154,52);
        var hint=ModalText(card,"滚轮滚动 · 右下角调整窗口大小",24,510,480,26,14,new Color(.76f,.70f,.72f));
        hint.rectTransform.anchorMin=hint.rectTransform.anchorMax=new Vector2(0,0);hint.rectTransform.anchoredPosition=new Vector2(24,46);
        var frame=Rect("LedgerScroll",card,new Vector2(732,360),new Vector2(24,-128));
        var background=frame.gameObject.AddComponent<Image>();background.color=new Color(.16f,.12f,.19f,.8f);
        var scroll=frame.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;
        scroll.movementType=ScrollRect.MovementType.Clamped;scroll.inertia=false;scroll.scrollSensitivity=36;
        var viewport=Rect("Viewport",frame,new Vector2(710,360),Vector2.zero);
        viewport.gameObject.AddComponent<RectMask2D>();
        var hit=viewport.gameObject.AddComponent<Image>();hit.color=Color.clear;
        var content=Rect("Content",viewport,new Vector2(710,360),Vector2.zero);
        scroll.viewport=viewport;scroll.content=content;
        var barRect=Rect("Scrollbar",frame,new Vector2(14,360),new Vector2(718,0));
        var barImage=barRect.gameObject.AddComponent<Image>();barImage.color=new Color(.30f,.23f,.30f,1);
        var bar=barRect.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;
        var slide=Rect("SlidingArea",barRect,Vector2.zero,Vector2.zero);
        slide.anchorMin=Vector2.zero;slide.anchorMax=Vector2.one;slide.offsetMin=new Vector2(2,2);slide.offsetMax=new Vector2(-2,-2);
        var handle=Rect("Handle",slide,Vector2.zero,Vector2.zero);
        handle.anchorMin=Vector2.zero;handle.anchorMax=Vector2.one;handle.offsetMin=handle.offsetMax=Vector2.zero;
        var handleImage=handle.gameObject.AddComponent<Image>();handleImage.color=new Color(.80f,.63f,.39f,1);
        bar.handleRect=handle;bar.targetGraphic=handleImage;scroll.verticalScrollbar=bar;
        scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.Permanent;
        bool layingOut=false;
        scroll.onValueChanged.AddListener(v=>{if(!layingOut)ledgerScrollOffset=Mathf.Max(0,content.anchoredPosition.y);});
        var rows=new List<LedgerRow>();var headers=new Dictionary<int,TextMeshProUGUI>();
        var knownRows=new HashSet<EarnedReward>();
        bool laidOut=false;ForgeLedgerRefresh lastLayout=default(ForgeLedgerRefresh);
        Action updateHighlights=()=>{
            foreach(var row in rows)
            {
                bool highlight=row.Entry.LastBatch==consumedMaterials && row.Entry.LastAmount>0 && Time.unscaledTime<row.HighlightUntil;
                if(row.Highlighted==highlight)continue;
                row.Highlighted=highlight;
                row.Background.color=highlight?new Color(.38f,.29f,.18f,1):new Color(.23f,.18f,.26f,1);
            }
        };
        var empty=ModalText(content,"尚无已确认的属性收益",12,12,600,40,18,Color.white);
        modalRefresh=()=>{
            updateHighlights();
            var key=new ForgeLedgerRefresh {Width=modalSize.x,Height=modalSize.y,Details=ledgerDetails,Revision=ledgerRevision,
                Consumed=consumedMaterials,Enchants=earnedEnchants,Completed=completedForges,Failed=failedForges,Storage=EarnedStorage(),Message=milestones.Message};
            if(laidOut && lastLayout.Same(key))return;
            LedgerRow anchor=null;float anchorOffset=0;
            if(ledgerScrollOffset>0)foreach(var row in rows)
            {
                float oldTop=-row.Rect.anchoredPosition.y;
                if(oldTop+row.Rect.sizeDelta.y>ledgerScrollOffset){anchor=row;anchorOffset=ledgerScrollOffset-oldTop;break;}
            }
            float w=modalSize.x,h=modalSize.y;
            ForgeLocalizedLabel.Set(summary,"消耗 "+(consumedMaterials-externalForges)+" 件"+(externalForges>0?" · 联动 "+externalForges+" 次":"")+" · 附魔 +"+earnedEnchants+" · 扩容 "+EarnedStorage()+"/"+ForgeBalance.StorageLimit);
            ForgeLocalizedLabel.Set(storageStatus,StorageProgress());
            ForgeLocalizedLabel.Set(detailLabel,ledgerDetails?"收起详情":"展开详情");
            details.gameObject.SetActive(ledgerDetails);
            ForgeLocalizedLabel.Set(details,"完成 "+completedForges+" 次 · 中止 "+failedForges+" 次\n"+(string.IsNullOrEmpty(milestones.Message)?"当前冒险已确认到账；中止前到账的部分保留。":milestones.Message));
            var flow=NativeToolbarLayout.Ledger(h,
                summary.GetPreferredValues(summary.text,w-48,10000).y+4,
                storageStatus.GetPreferredValues(storageStatus.text,w-200,10000).y+4,
                details.GetPreferredValues(details.text,w-48,10000).y+4,ledgerDetails);
            summary.rectTransform.sizeDelta=new Vector2(w-48,flow.SummaryHeight);
            storageStatus.rectTransform.anchoredPosition=new Vector2(24,-flow.StatusTop);
            storageStatus.rectTransform.sizeDelta=new Vector2(w-200,flow.StatusHeight);
            var db=detailButton.GetComponent<RectTransform>();db.anchoredPosition=new Vector2(w-150,-flow.StatusTop);
            details.rectTransform.anchoredPosition=new Vector2(24,-flow.DetailsTop);
            details.rectTransform.sizeDelta=new Vector2(w-48,flow.DetailsHeight);
            hint.rectTransform.sizeDelta=new Vector2(Mathf.Max(80,w-202),28);
            float top=flow.ListTop,viewWidth=w-70,viewHeight=flow.ListHeight;
            frame.anchoredPosition=new Vector2(24,-top);frame.sizeDelta=new Vector2(w-48,viewHeight);
            viewport.sizeDelta=new Vector2(viewWidth,viewHeight);barRect.anchoredPosition=new Vector2(w-64,0);barRect.sizeDelta=new Vector2(14,viewHeight);
            bool added=false;
            foreach(var e in earned.Values)
            {
                if(e.Reward.Kind==7 || !knownRows.Add(e))continue;
                added=true;
                var rect=Rect("Reward",content,Vector2.zero,Vector2.zero);
                var bg=rect.gameObject.AddComponent<Image>();bg.color=new Color(.23f,.18f,.26f,1);
                var icon=ModalIcon(rect,10,14,24);icon.sprite=RewardIcon(e.Reward);icon.enabled=icon.sprite!=null;
                var label=ModalText(rect,"",44,9,260,56,18,new Color(.94f,.91f,.85f));
                label.enableAutoSizing=false;label.fontSize=18;label.textWrappingMode=TextWrappingModes.Normal;
                rows.Add(new LedgerRow{Entry=e,Rect=rect,Label=label,Background=bg,Group=LedgerGroup(e)});
            }
            if(added)rows.Sort((a,b)=>a.Group!=b.Group?a.Group.CompareTo(b.Group):a.Entry.Order.CompareTo(b.Entry.Order));
            empty.gameObject.SetActive(rows.Count==0);empty.rectTransform.sizeDelta=new Vector2(viewWidth-24,48);
            int columns=viewWidth>=650?2:1;
            float cellWidth=(viewWidth-12*(columns-1))/columns,y=0;
            int rowIndex=0;
            while(rowIndex<rows.Count)
            {
                int group=rows[rowIndex].Group;TextMeshProUGUI header;
                if(!headers.TryGetValue(group,out header)){header=ModalText(content,LedgerGroupNames[group],4,0,viewWidth,30,17,new Color(1,.85f,.58f));headers.Add(group,header);}
                header.rectTransform.anchoredPosition=new Vector2(4,-y);header.rectTransform.sizeDelta=new Vector2(viewWidth-8,30);y+=36;
                while(rowIndex<rows.Count && rows[rowIndex].Group==group)
                {
                    int start=rowIndex;float rowHeight=64;
                    for(int c=0;c<columns && rowIndex<rows.Count && rows[rowIndex].Group==group;c++,rowIndex++)
                    {
                        var row=rows[rowIndex];var e=row.Entry;bool latest=e.LastBatch==consumedMaterials && e.LastAmount>0;
                        if(row.Amount!=e.Amount||row.Batch!=e.LastBatch){row.HighlightUntil=latest?Time.unscaledTime+8:0;row.Amount=e.Amount;row.Batch=e.LastBatch;}
                        ForgeLocalizedLabel.Set(row.Label,e.Reward.DisplayTotal(e.Amount)+(latest?"\n本次 "+e.Reward.DisplayAmount(e.LastAmount):""));
                        float textWidth=cellWidth-54;
                        rowHeight=Mathf.Max(rowHeight,row.Label.GetPreferredValues(row.Label.text,textWidth,10000).y+20);
                        row.Rect.anchoredPosition=new Vector2(c*(cellWidth+12),-y);
                        row.Label.rectTransform.sizeDelta=new Vector2(textWidth,rowHeight-18);
                    }
                    for(int i=start;i<rowIndex;i++){rows[i].Rect.sizeDelta=new Vector2(cellWidth,rowHeight);rows[i].Label.rectTransform.sizeDelta=new Vector2(cellWidth-54,rowHeight-18);}
                    y+=rowHeight+8;
                }
                y+=10;
            }
            layingOut=true;
            content.sizeDelta=new Vector2(viewWidth,Mathf.Max(viewHeight,y));
            if(anchor!=null)ledgerScrollOffset=-anchor.Rect.anchoredPosition.y+anchorOffset;
            ledgerScrollOffset=Mathf.Clamp(ledgerScrollOffset,0,Mathf.Max(0,y-viewHeight));
            content.anchoredPosition=new Vector2(0,ledgerScrollOffset);
            scroll.StopMovement();layingOut=false;
            lastLayout=key;laidOut=true;updateHighlights();
        };
        modalValid=()=>Ready() && BodyForgeSettings.Current.Enabled && nativePanel!=null && nativePanel.IsOpened && nativePanel.PlayerAvatar==owner;
        nextModalRefresh=0;TickForgeModal();
    }
}
