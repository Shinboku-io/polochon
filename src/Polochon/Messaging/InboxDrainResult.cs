namespace Polochon.Messaging
{
    /// <summary>
    /// Outcome of one drain of a module's inbox (see <c>ModuleBase.ProcessInboxAsync</c>): how many
    /// integration events were read, and how many of them failed and were routed to the module's
    /// error queue.
    /// </summary>
    /// <param name="Processed">Every message read from the inbox during the drain, successful or not.</param>
    /// <param name="Failed">The messages whose handling threw, routed to the module's error queue.</param>
    public readonly record struct InboxDrainResult(int Processed, int Failed)
    {
        /// <summary>The messages handled without error.</summary>
        public int Succeeded => Processed - Failed;
    }
}
