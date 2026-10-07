namespace Polochon.Abstractions.Persistence
{
    /// <summary>
    /// Represents a unit of work that can be used to group multiple repository operations into a single transaction.
    /// </summary>
    public interface IUnitOfWork
    {
        /// <summary>
        /// Commits all changes made within this unit of work.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CommitAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Discards all changes made within this unit of work, without persisting them. A
        /// resource obtained through this unit of work before this call (e.g. a tracked entity)
        /// is not guaranteed to remain valid afterwards - see the implementation's own remarks for
        /// exactly what happens to it.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RollbackAsync(CancellationToken cancellationToken = default);
    }
}
