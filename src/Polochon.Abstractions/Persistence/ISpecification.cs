using Polochon.Abstractions.Domain;

namespace Polochon.Abstractions.Persistence
{
    /// <summary>
    /// Describes a set of criteria used to select aggregate roots from a repository.
    /// </summary>
    /// <typeparam name="T">The type of the aggregate root the specification applies to.</typeparam>
    /// <typeparam name="TIdentifier">The type of the aggregate root's unique identifier.</typeparam>
    public interface ISpecification<T, TIdentifier> : IRepositoryQuery<T, T>
        where T : IAggregateRoot<TIdentifier>
        where TIdentifier : notnull
    {
    }
}
