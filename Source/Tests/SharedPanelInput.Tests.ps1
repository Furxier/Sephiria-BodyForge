$ErrorActionPreference = 'Stop'
$source=Get-Content (Join-Path $PSScriptRoot '../SharedPanelInput.cs') -Raw
$tests=@'
namespace UnityEngine {
    public enum CursorLockMode { None,Locked,Confined }
    public static class Cursor { public static bool visible; public static CursorLockMode lockState; }
}
public class PlayerInputController
{
    public static PlayerInputController Instance;
    private bool blocked;
    public bool Destroyed;
    public static bool operator ==(PlayerInputController a,PlayerInputController b) {
        bool an=object.ReferenceEquals(a,null)||a.Destroyed,bn=object.ReferenceEquals(b,null)||b.Destroyed;
        return an||bn?an==bn:object.ReferenceEquals(a,b);
    }
    public static bool operator !=(PlayerInputController a,PlayerInputController b){return !(a==b);}
    public override bool Equals(object other){return object.ReferenceEquals(this,other);}
    public override int GetHashCode(){return base.GetHashCode();}
    public int BlockSetterCalls;
    public bool BlockAvatarInput { get { return blocked; } set { blocked=value; BlockSetterCalls++; } }
}
public static class SharedPanelInputTests
{
    private static void Check(bool value,string message) { if(!value) throw new System.Exception(message); }
    public static string Run()
    {
        var a=new SharedPanelInput(); var b=new SharedPanelInput();
        var original=new PlayerInputController(); PlayerInputController.Instance=original;
        UnityEngine.Cursor.visible=false;
        UnityEngine.Cursor.lockState=UnityEngine.CursorLockMode.Locked;
        a.Update(true,true); b.Update(true,true); a.Update(false,false);
        Check(original.BlockAvatarInput && UnityEngine.Cursor.visible,"second panel retains input lease");
        Check(UnityEngine.Cursor.lockState==UnityEngine.CursorLockMode.None,"panel unlocks mouse");
        Check(original.BlockSetterCalls==1,"frame updates do not repeatedly disable avatar input");
        b.Update(false,false);
        Check(!original.BlockAvatarInput && !UnityEngine.Cursor.visible,"original input/cursor restored");
        Check(UnityEngine.Cursor.lockState==UnityEngine.CursorLockMode.Locked,"original cursor lock restored");
        a.Update(true,true); b.Update(true,true); b.Update(false,false); a.Update(false,false);
        Check(!original.BlockAvatarInput && !UnityEngine.Cursor.visible,"reverse closing order");
        original.BlockAvatarInput=true; UnityEngine.Cursor.visible=true;
        a.Update(true,true); a.Update(false,false);
        Check(original.BlockAvatarInput && UnityEngine.Cursor.visible,"preserve preexisting block");
        original.BlockAvatarInput=false;
        a.Update(true,true);
        var replacement=new PlayerInputController(); PlayerInputController.Instance=replacement;
        a.Update(true,true);
        Check(!original.BlockAvatarInput && replacement.BlockAvatarInput,"restore replaced controller");
        a.Update(false,false);
        Check(!replacement.BlockAvatarInput,"release replacement controller");
        a.Update(true,false);
        Check(!replacement.BlockAvatarInput && UnityEngine.Cursor.visible,"menu without avatar uses cursor only");
        a.Update(false,false);
        a.Update(true,true);replacement.Destroyed=true;PlayerInputController.Instance=null;a.Update(false,false);
        var state=(System.Collections.Generic.Dictionary<string,object>)System.AppDomain.CurrentDomain.GetData("LocalSephiriaPanels.InputLease.v1");
        Check(object.ReferenceEquals(state["controller"],null),"destroyed Unity controller wrapper is released");
        return "PASS: independent panel close order, original input/cursor, controller replacement and menu state";
    }
}
'@
Add-Type -TypeDefinition ($source+$tests)
[SharedPanelInputTests]::Run()
