namespace Solana.Unity.Dex.Test.Orca.Utils 
{
    public enum StandardErrorType
    {
        RawConstraint = 0x7d3,
        AccountOwnedByWrongProgram = 0xbbf,
        HasOneConstraint = 0x7d1,
        AddressConstraint = 0x7dc,
        OwnerMismatch = 0x4,
        SeedsConstraint = 0x7d6
    }
}