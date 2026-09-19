using System;

// Native refresh RPCs are not atomic receipts for client SyncVars. After all
// refresh notifications, require consecutive stable client observations too.
internal sealed class ForgeEffectGate
{
    private int requested, refreshed, lastFrame=-1, stable;
    private double[] previous;
    private bool hasPrevious;
    internal void Request() { requested++; ResetSamples(); }
    internal void Refreshed() { refreshed=Math.Min(requested,refreshed+1); ResetSamples(); }
    private void ResetSamples() { hasPrevious=false; stable=0; lastFrame=-1; }
    internal bool Ready(bool server,int frame,double[] values)
    {
        if(requested==0 || refreshed<requested) return false;
        if(server) return true;
        if(frame==lastFrame) return stable>=2;
        lastFrame=frame;
        bool same=hasPrevious && previous!=null && previous.Length==values.Length;
        for(int i=0;i<values.Length;i++)
        {
            if(double.IsNaN(values[i]) || double.IsInfinity(values[i])) { ResetSamples(); return false; }
            if(same && Math.Abs(values[i]-previous[i])>0.00001) same=false;
        }
        stable=same?stable+1:0;
        if(previous==null || previous.Length!=values.Length)previous=new double[values.Length];
        Array.Copy(values,previous,values.Length);hasPrevious=true;
        return stable>=2;
    }
}
