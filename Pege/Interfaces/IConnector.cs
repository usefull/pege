namespace Pege.Interfaces
{
    internal interface IConnector
    {
        Task ConsumeAsync(IStream stream, HttpRequest httpRequest, HttpResponse httpResponse, CancellationToken cancellationToken);
    }
}
