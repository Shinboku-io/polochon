namespace Polochon.Abstractions.CQRS
{
    public interface IMessage<out TResponse>;

    public interface IMessage : IMessage<Unit>;

    public readonly struct Unit
    {
        public static readonly Unit Value = default;
    }
}