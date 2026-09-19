using System;
using System.Collections.Generic;
using UnityEngine;

// BCL-only state shared across the two independent assemblies. The last panel
// restores the original cursor/input state regardless of which panel closes first.
internal sealed class SharedPanelInput
{
    private readonly object token=new object();
    internal void Update(bool open,bool blockAvatar)
    {
        const string key="LocalSephiriaPanels.InputLease.v1";
        var state=AppDomain.CurrentDomain.GetData(key) as Dictionary<string,object>;
        if(state==null)
        {
            state=new Dictionary<string,object>();
            state["holders"]=new Dictionary<object,bool>();
            state["cursor"]=false; state["controller"]=null; state["block"]=false;
            state["cursorLock"]=CursorLockMode.None;
            AppDomain.CurrentDomain.SetData(key,state);
        }
        var holders=(Dictionary<object,bool>)state["holders"];
        int before=holders.Count;
        if(open)
        {
            if(before==0) { state["cursor"]=Cursor.visible; state["cursorLock"]=Cursor.lockState; }
            holders[token]=blockAvatar;
        }
        else holders.Remove(token);
        if(holders.Count>0) { Cursor.visible=true; Cursor.lockState=CursorLockMode.None; }
        else if(before>0)
        {
            Cursor.visible=(bool)state["cursor"];
            if(state.ContainsKey("cursorLock")) Cursor.lockState=(CursorLockMode)state["cursorLock"];
        }

        bool wantsBlock=false;
        foreach(bool value in holders.Values) if(value) { wantsBlock=true; break; }
        var prior=state["controller"] as PlayerInputController;
        var current=PlayerInputController.Instance;
        if(!wantsBlock || prior==null || prior!=current)
        { if(prior!=null)prior.BlockAvatarInput=(bool)state["block"]; state["controller"]=null; }
        if(!wantsBlock || current==null) return;
        if((state["controller"] as PlayerInputController)==null)
        { state["controller"]=current; state["block"]=current.BlockAvatarInput; }
        if(!current.BlockAvatarInput) current.BlockAvatarInput=true;
    }
}
