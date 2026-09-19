$ErrorActionPreference='Stop'
$source=Get-Content (Join-Path $PSScriptRoot '../NativeForgeUI.cs') -Raw -Encoding UTF8
$source=$source.Substring($source.IndexOf('public sealed class ForgeDraggableButton'))
$tests=@'
using UnityEngine;using UnityEngine.UI;using UnityEngine.EventSystems;
namespace UnityEngine {
 public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static Vector2 operator -(Vector2 a,Vector2 b){return new Vector2(a.x-b.x,a.y-b.y);}}
}
namespace UnityEngine.EventSystems {
 public interface IBeginDragHandler {} public interface IDragHandler {} public interface IEndDragHandler {}
 public class PointerEventData {public enum InputButton {Left,Right} public InputButton button;public Vector2 position,pressPosition;public bool eligibleForClick=true;public void Use(){}}
}
namespace UnityEngine.UI {
 public class Button {
  public int Clicks;public virtual void OnPointerDown(PointerEventData e){}public virtual void OnPointerClick(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)Clicks++;}
  protected bool IsActive(){return true;}protected bool IsInteractable(){return true;}
  protected virtual void OnDisable(){}public void Disable(){OnDisable();}
 }
}
public static class DragTests {
 static void Check(bool ok,string why){if(!ok)throw new System.Exception(why);}
 public static string Run(){
  var b=new ForgeDraggableButton();int moved=0;Vector2 total=new Vector2();
  b.Moved=d=>{moved++;total=new Vector2(total.x+d.x,total.y+d.y);};
  var e=new PointerEventData{button=PointerEventData.InputButton.Left,pressPosition=new Vector2(10,10),position=new Vector2(30,40)};
  b.OnPointerDown(e);b.OnPointerClick(e);Check(b.Clicks==1,"normal click preserved");
  b.OnPointerDown(e);b.OnBeginDrag(e);b.OnDrag(e);b.OnPointerClick(e);b.OnEndDrag(e);b.OnPointerClick(e);
  Check(b.Clicks==1&&!e.eligibleForClick,"drag release cannot open panel regardless of callback ordering");
  Check(moved==1&&total.x==20&&total.y==30,"initial threshold movement included in screen delta");
  b.OnPointerDown(e);b.OnPointerClick(e);Check(b.Clicks==2,"next independent click works");
  e.button=PointerEventData.InputButton.Right;b.OnBeginDrag(e);b.OnDrag(e);Check(moved==1,"right button does not drag");
  e.button=PointerEventData.InputButton.Left;b.OnPointerDown(e);b.OnBeginDrag(e);b.Disable();b.OnDrag(e);Check(moved==1,"closing backpack stops drag");
  b.OnPointerDown(e);b.OnBeginDrag(e);b.OnDrag(e);Check(moved==2,"reopening retains move handler");
  return "PASS: unchanged click, drag suppression, next click, initial motion, right button and disable/reopen";
 }
}
'@
Add-Type -TypeDefinition ($tests+$source)
[DragTests]::Run()
