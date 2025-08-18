namespace WebAPI.Middlerwares
{
    public class JwtCookieMiddleware
    {
        private readonly RequestDelegate _next;

        public JwtCookieMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Cookies.TryGetValue("access_token", out var access_token))
            {
                context.Request.Headers["Authorization"] = "Bearer " + access_token;
            }

            await _next(context);
        }
    }
}
