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
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CommitAsync();

        /// <summary>
        /// Rolls back all changes made within this unit of work.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RollbackAsync();
    }
}