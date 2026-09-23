using TMPro;
using UnityEngine;

// Attached only to Mod-owned labels. Raw text survives a language switch in either direction.
public sealed class ForgeLocalizedLabel : MonoBehaviour
{
    private TMP_Text label;
    private string source;
    private bool traditional;
    internal static void Set(TMP_Text target,string text)
    {
        var localized=target.GetComponent<ForgeLocalizedLabel>();
        if(localized==null)localized=target.gameObject.AddComponent<ForgeLocalizedLabel>();
        localized.label=target;localized.source=text;localized.Refresh();
    }
    private void Refresh()
    {
        traditional=ForgeLocalization.Traditional;
        if(label!=null)label.text=ForgeLocalization.Convert(source,traditional);
    }
    private void OnEnable(){if(label!=null)Refresh();}
    private void LateUpdate(){if(traditional!=ForgeLocalization.Traditional)Refresh();}
}
