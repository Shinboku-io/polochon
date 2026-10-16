namespace Polochon.Messaging
{
    /// <summary>
    /// How an <see cref="InboxProcessor"/> decides when to read its module's inbox.
    /// </summary>
    public enum InboxProcessingMode
    {
        /// <summary>
        /// Reads the inbox on a fixed clock (<see cref="InboxProcessingOptions.Clock"/>). Required
        /// when messages can reach the inbox from outside this process - e.g. a module split into
        /// its own deployment, fed through a persistent inbox by other processes' event buses.
        /// </summary>
        Polling = 0,

        /// <summary>
        /// Reads the inbox only when this process's own <see cref="EventBus"/> delivers a message
        /// into it, plus once at startup to pick up anything a previous run left behind. For a
        /// module hosted in the same process as every module that publishes to it (a modular
        /// monolith): the inbox - even a persistent one - is never read while nothing is delivered,
        /// so an idle module costs no queue or database reads.
        /// </summary>
        InProcess = 1,
    }

    /// <summary>
    /// Configures an <see cref="InboxProcessor"/> - see <c>WithInboxProcessing(InboxProcessingOptions)</c>.
    /// </summary>
    /// <example>
    /// A module of a modular monolith with an Azure Storage Queue inbox, read only on delivery,
    /// with a safety read after 5 idle minutes:
    /// <code>
    /// services.AddModule&lt;SalesModule&gt;()
    ///     .WithAzureQueueInbox(inboxOptions)
    ///     .WithInboxProcessing(new InboxProcessingOptions
    ///     {
    ///         Mode = InboxProcessingMode.InProcess,
    ///         IdleSweep = TimeSpan.FromMinutes(5),
    ///     });
    /// </code>
    /// </example>
    public sealed class InboxProcessingOptions
    {
        /// <summary>
        /// When to read the inbox. Defaults to <see cref="InboxProcessingMode.Polling"/>, the
        /// behavior of <c>WithInboxProcessing(TimeSpan?)</c>.
        /// </summary>
        public InboxProcessingMode Mode { get; set; } = InboxProcessingMode.Polling;

        /// <summary>
        /// How often to read the inbox in <see cref="InboxProcessingMode.Polling"/> mode. Ignored in
        /// <see cref="InboxProcessingMode.InProcess"/> mode. Defaults to <see cref="InboxProcessor.DefaultClock"/>.
        /// </summary>
        public TimeSpan Clock { get; set; } = InboxProcessor.DefaultClock;

        /// <summary>
        /// In <see cref="InboxProcessingMode.InProcess"/> mode, also reads the inbox once it has
        /// been idle (no delivery) for this long; <c>null</c> (the default) never reads it without a
        /// delivery. Ignored in <see cref="InboxProcessingMode.Polling"/> mode.
        /// </summary>
        /// <remarks>
        /// A safety net for messages that become readable without a new delivery - e.g. one a
        /// persistent inbox makes visible again after a processing attempt was interrupted - which
        /// otherwise wait for the next delivery or restart. Choose it long: it only bounds how late
        /// such a message is processed.
        /// </remarks>
        public TimeSpan? IdleSweep { get; set; }

        /// <summary>
        /// Throws if these options cannot drive an <see cref="InboxProcessor"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><see cref="Mode"/> is not a defined value, <see cref="Clock"/> is not positive in <see cref="InboxProcessingMode.Polling"/> mode, or <see cref="IdleSweep"/> is set but not positive in <see cref="InboxProcessingMode.InProcess"/> mode.</exception>
        internal void Validate()
        {
            switch (Mode)
            {
                case InboxProcessingMode.Polling:
                    ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Clock, TimeSpan.Zero, nameof(Clock));
                    break;
                case InboxProcessingMode.InProcess:
                    if (IdleSweep is { } idleSweep)
                    {
                        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(idleSweep, TimeSpan.Zero, nameof(IdleSweep));
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Mode), Mode, "Unknown inbox processing mode.");
            }
        }
    }
}
