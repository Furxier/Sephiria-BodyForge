$ErrorActionPreference = 'Stop'
$source=Get-Content (Join-Path $PSScriptRoot '../NativeToolbarLayout.cs') -Raw
$tests=@'
public static class NativeToolbarLayoutTests
{
    private static void Check(bool value,string message) { if(!value) throw new System.Exception(message); }
    public static string Run()
    {
        foreach(float parentPixels in new[]{.5f,1f,2f,4f,8f})
        {
            float local=NativeToolbarLayout.LocalScale(parentPixels,1);
            Check(System.Math.Abs(96*local*parentPixels-96)<.001,"parent scaling must not enlarge the button");
        }
        float[,] screens={{1280,720},{1920,1080},{1875,1327},{2560,1440},{3440,1440},{3840,2160}};
        for(int i=0;i<screens.GetLength(0);i++)
        foreach(bool details in new[]{false,true})
        {
            float width=screens[i,0],height=screens[i,1];
            var p=NativeToolbarLayout.Calculate(width,height,width*.28f,height*.1f,width*.82f,height*.9f,details);
            Check(p.X>=8 && p.X+p.Width<=width-7.99f && p.Top<=height-7.99f && p.Top-p.Height>=7.99f,"onscreen bounds");
            float scale=NativeToolbarLayout.ScreenScale(width,height);
            Check(System.Math.Abs(p.Width-(details?260:96)*scale)<.001,"toolbar follows resolution scale");
            float modal=NativeToolbarLayout.ModalScale(width,height,744,440);
            Check(modal*744<=width-24 && modal*440<=height-24,"modal stays within screen");
            Check(System.Math.Abs(modal-scale)<.001,"modal and toolbar use same readable scale");
            Check(p.X>=width*.82f || p.X+p.Width<=width*.28f || p.Top-p.Height>=height*.9f || p.Top<=height*.1f,"outside inventory when space exists");
        }
        var screenshot=NativeToolbarLayout.Calculate(1875,1327,530,60,1580,1210,true);
        Check(NativeToolbarLayout.ModalScale(3840,2160,744,440)==2,"4K scales text and cards to twice 1080p pixels");
        Check(screenshot.X>=1580 && screenshot.Width<280 && screenshot.Height<112,"reported screenshot: panel beside inventory");
        var below=NativeToolbarLayout.Calculate(1280,720,8,200,1272,712,true);
        Check(below.Top<200,"use bottom gap when sides and top are full");
        foreach(bool details in new[]{false,true})
        {
            var original=NativeToolbarLayout.Calculate(1920,1080,300,100,1400,950,details);
            var moved=NativeToolbarLayout.Move(original,1920,1080,100000,-100000);
            Check(moved.X+moved.Width<=1912 && moved.Top-moved.Height>=8,"dragged toolbar stays on screen including hint");
            Check(moved.Width==original.Width && moved.Height==original.Height && moved.Scale==original.Scale,"drag never changes visual dimensions");
            moved=NativeToolbarLayout.Move(original,1920,1080,400,800);
            Check(moved.X==400 && moved.Top==800,"manual position overrides automatic placement");
        }
        for(int i=0;i<screens.GetLength(0);i++)foreach(float cardWidth in new[]{540f,560f,620f,744f})
        {
            float w=screens[i,0],h=screens[i,1],user=1;
            float bigger=NativeToolbarLayout.ResizeModal(w,h,cardWidth,440,user,80,-60);
            Check(bigger>user,"bottom-right drag enlarges");
            float smaller=NativeToolbarLayout.ResizeModal(w,h,cardWidth,440,bigger,-80,60);
            Check(System.Math.Abs(smaller-user)<.001,"reverse drag restores scale before clamping");
            float max=NativeToolbarLayout.ResizeModal(w,h,cardWidth,440,user,100000,-100000);
            float effective=NativeToolbarLayout.ModalScale(w,h,cardWidth,440,max);
            Check(effective*cardWidth<=w-23.99f && effective*440<=h-23.99f,"oversized drag stays onscreen");
            float reduced=NativeToolbarLayout.ResizeModal(w,h,cardWidth,440,max,-10,10);
            Check(reduced<max,"drag reverses immediately at size limit");
            float min=NativeToolbarLayout.ResizeModal(w,h,cardWidth,440,user,-100000,100000);
            Check(System.Math.Abs(min-.65f)<.001,"minimum readable scale");
        }
        foreach(float height in new[]{420f,560f,900f})foreach(float summary in new[]{24f,50f,120f})
        foreach(float status in new[]{22f,44f,90f})foreach(float detail in new[]{22f,70f,300f})foreach(bool expanded in new[]{false,true})
        {
            var flow=NativeToolbarLayout.Ledger(height,summary,status,detail,expanded);
            Check(flow.StatusTop>=64+flow.SummaryHeight+8,"statistics cannot overlap status line");
            Check(flow.DetailsTop>=flow.StatusTop+flow.StatusHeight+8,"details cannot overlap progress or toggle");
            Check(flow.ListTop>=(expanded?flow.DetailsTop+flow.DetailsHeight:flow.DetailsTop),"scroll starts below header text");
            Check(flow.ListHeight>=80 && flow.ListTop+flow.ListHeight<=height-68,"scroll and footer stay separate with usable viewport");
        }
        return "PASS: screen bounds, drag sizing, ledger wrapping and footer separation";
    }
}
'@
Add-Type -TypeDefinition ($source+$tests)
[NativeToolbarLayoutTests]::Run()

