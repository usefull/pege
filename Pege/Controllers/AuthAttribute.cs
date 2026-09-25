using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Pege.Resource;

namespace Pege.Controllers
{
    /// <summary>
    /// Атрибут авторизации.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class AuthAttribute : Attribute, IAsyncAuthorizationFilter
    {
        /// <summary>
        /// Метод авторизации.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        /// <exception cref="ApplicationException">В случае, если в конфигурации не заданы PassKey и PassWord</exception>
        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();

            var passKey = config["PassKey"];
            var passWord = config["PassWord"];
            if (string.IsNullOrWhiteSpace(passKey) || string.IsNullOrWhiteSpace(passWord))
                throw new ApplicationException(Error.PassKeyAndWordNotSpecified);

            var headers = context.HttpContext.Request.Headers;

            if (!headers.TryGetValue(passKey, out var provided)
                || !string.Equals(provided.ToString(), passWord, StringComparison.Ordinal))
            {
                context.Result = new UnauthorizedResult();
            }

            return Task.CompletedTask;
        }
    }
}