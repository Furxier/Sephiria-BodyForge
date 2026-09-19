using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class ForgeTheme : IDisposable
{
    internal readonly GUIStyle Window, Card, Label, Button, Field, Badge;
    private readonly List<Texture2D> textures = new List<Texture2D>();
    private readonly Font font;
    internal ForgeTheme()
    {
        font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 16);
        if (font != null) font.hideFlags = HideFlags.HideAndDontSave;
        Window = Style(GUI.skin.window, new Color(.08f,.09f,.11f,.99f));
        Card = Style(GUI.skin.box, new Color(.12f,.13f,.16f,.98f));
        Label = new GUIStyle(GUI.skin.label); Label.font = font; Label.fontSize = 15;
        Label.normal.textColor = new Color(.91f,.92f,.94f); Label.wordWrap = true; Label.richText = false;
        Button = Style(GUI.skin.button, new Color(.16f,.18f,.22f));
        Button.hover.background = Texture(new Color(.23f,.27f,.33f));
        Button.active.background = Texture(new Color(.36f,.30f,.17f));
        Button.alignment = TextAnchor.MiddleLeft; Button.padding = new RectOffset(9,7,2,2);
        Field = Style(GUI.skin.textField, new Color(.06f,.07f,.09f));
        Badge = Style(GUI.skin.label,new Color(.08f,.09f,.11f));
        Badge.fontSize=11; Badge.alignment=TextAnchor.MiddleCenter; Badge.padding=new RectOffset(0,0,0,0);
    }
    private GUIStyle Style(GUIStyle basis, Color color)
    {
        var style = new GUIStyle(basis); style.font = font; style.fontSize = 15; style.richText = false;
        style.normal.background = Texture(color); style.normal.textColor = Color.white;
        style.border = new RectOffset(8,8,8,8); return style;
    }
    private Texture2D Texture(Color color)
    {
        var texture = new Texture2D(20,20,TextureFormat.RGBA32,false);
        texture.hideFlags = HideFlags.HideAndDontSave; texture.wrapMode = TextureWrapMode.Clamp;
        for (int y=0;y<20;y++) for(int x=0;x<20;x++)
        {
            float dx = Mathf.Max(7-x, x-12), dy = Mathf.Max(7-y,y-12);
            float distance = Mathf.Sqrt(Mathf.Max(0,dx)*Mathf.Max(0,dx)+Mathf.Max(0,dy)*Mathf.Max(0,dy));
            var pixel = color; pixel.a *= Mathf.Clamp01(7.5f-distance); texture.SetPixel(x,y,pixel);
        }
        texture.Apply(); textures.Add(texture); return texture;
    }
    public void Dispose() { foreach (var texture in textures) UnityEngine.Object.Destroy(texture); if(font != null) UnityEngine.Object.Destroy(font); }
}
