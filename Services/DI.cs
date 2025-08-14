using Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services
{
    public static class DI
    {
        public static IServiceCollection AddExternalServices(this IServiceCollection services)
        {
            services.AddTransient<IEmailSender, EmailSender>();

            return services;
        }
    }
}
