namespace Polochon.Abstractions.CQRS
{

    /// <summary>
    /// Marker for an in-process notification. Multiple handlers per notification are allowed.
    /// In-process only - see the multi-instance section of the article for the ceiling.
    /// </summary>
    public interface INotification;
}