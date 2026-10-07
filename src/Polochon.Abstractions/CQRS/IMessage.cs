namespace Polochon.Abstractions.CQRS
{
    /// <summary>
    /// Base message interface for CQRS pattern.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public interface IMessage<out TResponse>;

    /// <summary>
    /// Base message interface for commands and notifications that don't return a value.
    /// </summary>
    public interface IMessage : IMessage<Unit>;

    /// <summary>
    /// Unit type representing no return value.
    /// </summary>
    public readonly struct Unit
    {
        /// <summary>
        /// The singleton instance of the Unit type.
        /// </summary>
        public static readonly Unit Value = default;
    }
}