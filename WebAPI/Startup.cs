using Application;
using Microsoft.AspNetCore.Builder;
using Persistence;
using Serilog;
using Services;
using WebAPI.Extensions;
using WebAPI.Middlerwares;

namespace WebAPI
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSerilog();

            services.AddApplication();
            services.AddPersistence(_configuration);
            services.AddExternalServices();

            services.AddJwtAuthentication(_configuration);

            services.AddControllers();
            services.AddEndpointsApiExplorer();

            services.AddCors(options =>
            {
                options
                    .AddPolicy("ALL", policy =>
                    {
                        policy.AllowAnyHeader();
                        policy.AllowAnyMethod();
                        policy.AllowAnyOrigin();
                    });
            });

            services.AddSwagger();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseCors("ALL");

            app.UseRouting();
            app.UseHttpsRedirection();

            app.UseMiddleware<JwtCookieMiddleware>();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
