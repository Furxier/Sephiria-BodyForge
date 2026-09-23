using UnityEngine;

public sealed partial class BodyForgePanel
{
    private ForgeReward milestoneReward;
    private int milestoneDismissedAt=-1;
    private void ResetMilestoneChoice(){milestoneReward=null;milestoneDismissedAt=-1;}
    private ForgeReward MilestoneReward(int choice)
    {return choice==0?ForgeReward.Storage():ForgeReward.Parse(choice==1?"IGNORE_DEFENSE/3":"HP_STEAL/10");}
    private bool MilestoneContextFree()
    {
        var holder=UIManager.Instance==null?null:UIManager.Instance.GetElement<UI_MessageBoxHolder>();
        return holder!=null && !holder.HasOpenedBox && !NativePickerBusy();
    }
    private bool CanChooseMilestone()
    {return Ready() && BodyForgeSettings.Current.Enabled && !ForgeBusy && !NativeActive && !forgeBlocked && !milestones.Pending && !milestones.Ambiguous && milestones.Claimed<ForgeMilestones.Entitled(earnedEnchants);}
    private void PollMilestoneChoice()
    {
        if(milestones.Pending && milestoneReward!=null)
            milestones.Poll(milestoneReward.Read(owner),Time.realtimeSinceStartup,()=>RecordReward(milestoneReward));
        if(CanChooseMilestone() && forgeModal==null && nativePanel!=null && nativePanel.IsOpened &&
            nativePanel.PlayerAvatar==owner && MilestoneContextFree() && milestoneDismissedAt!=earnedEnchants)
            OpenMilestoneChoices();
    }
    private void OpenMilestoneChoices()
    {
        if(!CanChooseMilestone() || !MilestoneContextFree())return;
        int epoch=progressEpoch;
        var card=BeginForgeModal("附魔里程碑 · 三选一",744,330,()=>{milestoneDismissedAt=earnedEnchants;});
        ModalText(card,"累计附魔 +"+earnedEnchants+" · 待领取 "+(ForgeMilestones.Entitled(earnedEnchants)-milestones.Claimed)+" 次\n每累计获得 10 级附魔，选择一项永久奖励。",22,64,700,58,17,Color.white);
        for(int i=0;i<3;i++)
        {
            int choice=i;var reward=MilestoneReward(choice);
            float x=22+i*238;
            var tile=Rect("MilestoneChoice",card,new Vector2(222,144),new Vector2(x,-134));
            PanelGraphic(tile,new Color(.24f,.19f,.28f,1));
            ModalReward(tile,reward,14,16,194, 60,20);
            bool available=choice==0?EarnedStorage()<10 && owner.Inventory.CurrentInventoryStorage<120:choice!=1 || reward.Read(owner)+3<=100;
            ModalButton(tile,available?"选择":"已达上限",12,94,198,36,()=>{
                if(epoch!=progressEpoch || !CanChooseMilestone() || !MilestoneContextFree())return;
                var current=reward.Read(owner);
                milestoneReward=reward;
                if(!milestones.Choose(earnedEnchants,choice,current,Time.realtimeSinceStartup,()=>{
                    if(choice==0)owner.Inventory.AddStorage(1);
                    else if(owner.isServer)ForgePermanentStats.Apply(owner,reward.Metadata);
                    else owner.CmdAddOrphanedStatusInstance(reward.Metadata);
                }))return;
                CloseForgeModal();
            }).interactable=available;
        }
        ModalText(card,"背包奖励最多 10 格；关闭后保留领取资格，可从锻体入口继续领取。",22,288,700,24,15,new Color(.87f,.82f,.73f));
        modalValid=()=>epoch==progressEpoch && CanChooseMilestone() && nativePanel!=null && nativePanel.IsOpened;
    }
}
