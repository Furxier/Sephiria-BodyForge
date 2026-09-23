// Value snapshot: comparing an idle ledger does not format text or measure TMP layout.
internal struct ForgeLedgerRefresh
{
    internal float Width,Height;
    internal bool Details;
    internal long Revision;
    internal int Consumed,Enchants,Completed,Failed,Storage;
    internal string Message;
    internal bool Same(ForgeLedgerRefresh other)
    {
        return Width==other.Width && Height==other.Height && Details==other.Details && Revision==other.Revision &&
            Consumed==other.Consumed && Enchants==other.Enchants && Completed==other.Completed && Failed==other.Failed &&
            Storage==other.Storage && Message==other.Message;
    }
}
