using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Pege.Exceptions;

namespace Pege.Startup
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            int statusCode = 0;
            string title = string.Empty;
            string detail = string.Empty;

            if (exception is UnknownStreamException unknownStreamException)
            {
                statusCode = StatusCodes.Status404NotFound;
                title = "Resource Not Found";
                detail = unknownStreamException.Message;
                
            }
            else if (exception is StreamUnavailableException streamUnavailableException)
            {
                statusCode = StatusCodes.Status503ServiceUnavailable;
                title = "Service Unavailable";
                detail = streamUnavailableException.Message;
            }

            if (statusCode > 0)
            {
                httpContext.Response.StatusCode = statusCode;
                httpContext.Response.ContentType = "application/json";

                // Формируем ответ RFC 7807 (ProblemDetails)
                var problemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = title,
                    Detail = detail
                };

                await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

                return true;
            }

            return false;
        }
    }
}
