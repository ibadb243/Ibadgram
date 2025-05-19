
using System.Net;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace WebAPI.Middlerwares
{
    public class CustomExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;

        public CustomExceptionHandlerMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await _ExceptionHandler(context, ex);
            }
        }

        private async Task _ExceptionHandler(HttpContext context, Exception exception)
        {
            HttpStatusCode code = HttpStatusCode.InternalServerError;
            string message = string.Empty;

            switch (exception)
            {
                default:
                    message = "Something get wrong";
                    break;
            }

            context.Response.StatusCode = (int)code;
            await context.Response.WriteAsJsonAsync(new { error = message });
        }
    }
}
