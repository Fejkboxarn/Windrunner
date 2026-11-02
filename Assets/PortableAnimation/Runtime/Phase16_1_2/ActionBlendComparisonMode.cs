namespace PortableAnimation
{
    /// <summary>Comparison modes use the same action clips and the same grouped job.
    /// Only per-region weights differ. RawFullBody is for diagnosis, not gameplay.</summary>
    public enum ActionBlendComparisonMode
    {
        Contextual = 0,
        FixedPolicy = 1,
        RawFullBody = 2
    }
}
