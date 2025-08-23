using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure
{
    public static class DI
    {
        public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(configuration.GetConnectionString("DATABASE")));

            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IUserMentionRepository, UserMentionRepository>();
            services.AddScoped<IChatRepository, ChatRepository>();
            services.AddScoped<IChatMentionRepository, ChatMentionRepository>();
            services.AddScoped<IChatMemberRepository, ChatMemberRepository>();
            services.AddScoped<IMentionRepository, MentionRepository>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

            services.AddScoped<IUnitOfWork, UnitOfWork>();

            return services;
        }

        public static IServiceCollection AddExternalServices(this IServiceCollection services)
        {
            services.AddTransient<IEmailSender, EmailSender>();

            return services;
        }
    }
}
