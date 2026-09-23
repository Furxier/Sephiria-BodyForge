using System;

internal static class NativeToolbarLayout
{
    internal const float ButtonWidth=96,ButtonHeight=44,DetailWidth=280,DetailHeight=112;
    internal struct LedgerFlow { internal float SummaryHeight,StatusTop,StatusHeight,DetailsTop,DetailsHeight,ListTop,ListHeight; }
    internal static LedgerFlow Ledger(float height,float summary,float status,float details,bool expanded)
    {
        var layout=new LedgerFlow();
        layout.SummaryHeight=Clamp(summary,26,60);
        layout.StatusTop=64+layout.SummaryHeight+8;
        layout.StatusHeight=Clamp(status,32,48);
        layout.DetailsTop=layout.StatusTop+layout.StatusHeight+8;
        layout.DetailsHeight=expanded?Clamp(details,24,Math.Max(24,height-layout.DetailsTop-160)):0;
        layout.ListTop=expanded?layout.DetailsTop+layout.DetailsHeight+12:layout.DetailsTop+4;
        layout.ListHeight=Math.Max(1,height-layout.ListTop-68);
        return layout;
    }
    internal struct Placement { internal float X,Top,Scale,Width,Height; }
    private static float Clamp(float value,float min,float max) { return Math.Max(min,Math.Min(value,max)); }
    internal static float ScreenScale(float width,float height)
    { return Math.Max(.5f,Math.Min(width/1920f,height/1080f)); }
    internal static float ModalScale(float width,float height,float cardWidth,float cardHeight,float userScale=1)
    { return Math.Max(.01f,Math.Min(ScreenScale(width,height)*Clamp(userScale,.65f,2.5f),Math.Min((width-24)/cardWidth,(height-24)/cardHeight))); }
    internal static float ResizeModal(float width,float height,float cardWidth,float cardHeight,float userScale,float dx,float dy)
    {
        float basis=ScreenScale(width,height);
        // The dialog stays centered, so each corner moves by half the size change.
        float delta=2*(dx*cardWidth-dy*cardHeight)/(cardWidth*cardWidth+cardHeight*cardHeight);
        float wanted=ModalScale(width,height,cardWidth,cardHeight,userScale)+delta;
        float maximum=Math.Max(.01f,Math.Min(basis*2.5f,Math.Min((width-24)/cardWidth,(height-24)/cardHeight)));
        return Clamp(wanted,Math.Min(basis*.65f,maximum),maximum)/basis;
    }
    internal static Placement Calculate(float screenWidth,float screenHeight,float left,float bottom,float right,float top,bool details)
    {
        const float gap=8;
        float scale=Math.Max(.85f,ScreenScale(screenWidth,screenHeight));
        scale=Math.Min(scale,Math.Min(Math.Max(1,screenWidth-16)/DetailWidth,Math.Max(1,screenHeight-16)/DetailHeight));
        float width=(details?DetailWidth:ButtonWidth)*scale, height=(details?DetailHeight:ButtonHeight)*scale;
        float x,y=top;
        if(right+gap+width<=screenWidth-gap) x=right+gap;
        else if(left-gap-width>=gap) x=left-gap-width;
        else if(top+gap+height<=screenHeight-gap) { x=right-width; y=top+gap+height; }
        else if(bottom-gap-height>=gap) { x=right-width; y=bottom-gap; }
        else { x=screenWidth-gap-width; y=screenHeight-gap; }
        return new Placement { X=Clamp(x,gap,Math.Max(gap,screenWidth-gap-width)),
            Top=Clamp(y,height+gap,Math.Max(height+gap,screenHeight-gap)),Scale=scale,Width=width,Height=height };
    }
    internal static float LocalScale(float pixelsPerParentUnit,float desiredPixelScale)
    {
        if(float.IsNaN(pixelsPerParentUnit)||float.IsInfinity(pixelsPerParentUnit)||pixelsPerParentUnit<.0001f) return 1;
        return desiredPixelScale/pixelsPerParentUnit;
    }
    internal static Placement Move(Placement placement,float screenWidth,float screenHeight,float x,float top)
    {
        placement.X=Clamp(x,8,Math.Max(8,screenWidth-8-placement.Width));
        placement.Top=Clamp(top,placement.Height+8,Math.Max(placement.Height+8,screenHeight-8));
        return placement;
    }
}

