﻿namespace DocuAurora.API
{
    using System;
    using System.Reflection;

    using DocuAurora.API;
    using DocuAurora.API.Infrastructure;
    using DocuAurora.API.ViewModels.Administration.Users;
    using DocuAurora.Data;
    using DocuAurora.Data.Common;
    using DocuAurora.Data.Common.Repositories;
    using DocuAurora.Data.Models;
    using DocuAurora.Data.Models.MongoDB;
    using DocuAurora.Data.Repositories;
    using DocuAurora.Data.Seeding;
    using DocuAurora.Services.Data;
    using DocuAurora.Services.Mapping;
    using DocuAurora.Services.Messaging;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Microsoft.OpenApi.Models;
    using MongoDB.Driver;
    using Serilog;

    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            AutoMapperConfig.RegisterMappings(typeof(UserViewModel).Assembly, typeof(ApplicationUser).Assembly);

            ConfigureServices(builder.Services, builder.Configuration);

            // Add services to the container.
            var logger = new LoggerConfiguration()
              .ReadFrom.Configuration(builder.Configuration)
              .Enrich.FromLogContext()
              .CreateLogger();
            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(logger);

            var app = builder.Build();
            Configure(app);
            app.Run();

            Log.CloseAndFlush();

        }

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<ApplicationDbContext>(
                options => options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            services.AddDefaultIdentity<ApplicationUser>(IdentityOptionsProvider.GetIdentityOptions)
                .AddRoles<ApplicationRole>().AddEntityFrameworkStores<ApplicationDbContext>();

            // cookie enhancing security by protecting against cross-site scripting (XSS) attacks.
            services.AddSession(options =>
            {
                options.Cookie.HttpOnly = true;
                options.IdleTimeout = new TimeSpan(1, 0, 0, 0);
            });

            services.Configure<CookiePolicyOptions>(
                options =>
                {
                    options.CheckConsentNeeded = context => true;
                    options.MinimumSameSitePolicy = SameSiteMode.None;
                });

            services.AddControllersWithViews().AddRazorRuntimeCompilation();

            services.AddRazorPages();
            services.AddDatabaseDeveloperPageExceptionFilter();

            services.AddControllers().AddNewtonsoftJson();

            services.AddSingleton(configuration);

            // MongoDB
            services.Configure<DocumentStoreDatabaseSettings>(
    configuration.GetSection(nameof(DocumentStoreDatabaseSettings)));
            services.AddSingleton<IDocumentStoreDatabaseSettings>(sp =>
             sp.GetRequiredService<IOptions<DocumentStoreDatabaseSettings>>().Value);
            services.AddSingleton<IMongoClient>(s =>
        new MongoClient(configuration.GetValue<string>("DocumentStoreDatabaseSettings:ConnectionString")));

            // Data repositories
            services.AddScoped(typeof(IDeletableEntityRepository<>), typeof(EfDeletableEntityRepository<>));
            services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
            services.AddScoped<IDbQueryRunner, DbQueryRunner>();
            services.AddScoped<IDocumentService, DocumentService>();

            // Application services
            services.AddTransient<IEmailSender, NullMessageSender>();
            services.AddTransient<IAdminService, AdminService>();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
            });
        }

        private static void Configure(WebApplication app)
        {
            // Seed data on application startup
            using (var serviceScope = app.Services.CreateScope())
            {
                var dbContext = serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                dbContext.Database.Migrate();
                new ApplicationDbContextSeeder().SeedAsync(dbContext, serviceScope.ServiceProvider).GetAwaiter().GetResult();
            }

            //AutoMapperConfig.RegisterMappings(typeof(ErrorViewModel).GetTypeInfo().Assembly);

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseRouting();

            app.UseAuthentication();
            // SERILOG USARNAME INSERTION
            app.UseMiddleware<LogUserNameMiddleware>();
            app.UseAuthorization();

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
                c.RoutePrefix = string.Empty;
            });

            app.MapControllerRoute("areaRoute", "{area:exists}/{controller=Home}/{action=Index}/{id?}");
            app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");


            app.MapRazorPages();
        }
    }
}
