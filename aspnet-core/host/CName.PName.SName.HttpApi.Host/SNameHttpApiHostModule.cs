using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using CName.PName.SName.Data;
using CName.PName.SName.EntityFrameworkCore;
using CName.PName.SName.MultiTenancy;
using IdentityModel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using Volo.Abp;
using Volo.Abp.AspNetCore.ExceptionHandling;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.MultiTenancy;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.Guids;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.Reflection;
using Volo.Abp.Security.Claims;
using Volo.Abp.Timing;
using Volo.Abp.VirtualFileSystem;

namespace CName.PName.SName
{
    [DependsOn(
        typeof(SNameApplicationModule),
        typeof(SNameEntityFrameworkCoreModule),
        typeof(SNameHttpApiModule),
        typeof(AbpAspNetCoreMvcUiMultiTenancyModule),
        typeof(AbpAutofacModule),
        typeof(AbpCachingStackExchangeRedisModule),
        typeof(AbpEntityFrameworkCorePostgreSqlModule),
        typeof(AbpAuditLoggingEntityFrameworkCoreModule),
        typeof(AbpPermissionManagementEntityFrameworkCoreModule),
        typeof(AbpAspNetCoreSerilogModule))]

    // typeof(AbpSettingManagementEntityFrameworkCoreModule), // micro service should not use settings in main db
    public class SNameHttpApiHostModule : AbpModule
    {
        private const string DefaultCorsPolicyName = "Default";

        private const string PathBase = "SName";

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var hostingEnvironment = context.Services.GetHostingEnvironment();
            var configuration = context.Services.GetConfiguration();

            Configure<AbpClockOptions>(opt => opt.Kind = DateTimeKind.Utc);

            Configure<AbpAspNetCoreMvcOptions>(options =>
            {
                options
                    .ConventionalControllers
                    .Create(typeof(SNameApplicationModule).Assembly);
            });

            Configure<AbpDbContextOptions>(options =>
            {
                options.UseNpgsql();
                // options.UseNpgsql(opt => opt.EnableRetryOnFailure());
            });

            Configure<AbpSequentialGuidGeneratorOptions>(options =>
            {
                options.DefaultSequentialGuidType = SequentialGuidType.SequentialAsString;
            });

            Configure<AbpMultiTenancyOptions>(options =>
            {
                options.IsEnabled = MultiTenancyConsts.IsEnabled;
            });
#if DEBUG
            if (hostingEnvironment.IsDevelopment())
            {
                Configure<AbpVirtualFileSystemOptions>(options =>
                {
                    options.FileSets.ReplaceEmbeddedByPhysical<SNameDomainSharedModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}src{0}CName.PName.SName.Domain.Shared", Path.DirectorySeparatorChar)));
                    options.FileSets.ReplaceEmbeddedByPhysical<SNameDomainModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}src{0}CName.PName.SName.Domain", Path.DirectorySeparatorChar)));
                    options.FileSets.ReplaceEmbeddedByPhysical<SNameApplicationContractsModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}src{0}CName.PName.SName.Application.Contracts", Path.DirectorySeparatorChar)));
                    options.FileSets.ReplaceEmbeddedByPhysical<SNameApplicationModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}src{0}CName.PName.SName.Application", Path.DirectorySeparatorChar)));
                });
            }
#endif
            context.Services.AddSwaggerGen(
                options =>
                {
                    options.SwaggerDoc("v1", new OpenApiInfo { Title = "SName API", Version = "v1" });
                    options.DocInclusionPredicate((docName, description) => true);
                    // options.CustomSchemaIds(type => type.FullName);
                    options.ResolveConflictingActions(re => re.First());
                    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, typeof(SNameApplicationContractsModule).Assembly.GetName().Name + ".xml"));
                    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, typeof(SNameApplicationModule).Assembly.GetName().Name + ".xml"));
                    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, typeof(SNameDomainSharedModule).Assembly.GetName().Name + ".xml"));

                    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.OAuth2,
                        Flows = new OpenApiOAuthFlows
                        {
                            ClientCredentials = new OpenApiOAuthFlow
                            {
                                AuthorizationUrl = new Uri(configuration["AuthServer:Authority"]),
                                Scopes = new Dictionary<string, string>
                                {
                                    { "SName.all", "SName" } // scopes
                                },
                                TokenUrl = new Uri($"{configuration["AuthServer:Authority"]}/connect/token")
                            }
                        },
                        Scheme = JwtBearerDefaults.AuthenticationScheme,
                        Name = "Authorization",
                        In = ParameterLocation.Header,
                        OpenIdConnectUrl = new Uri($"{configuration["AuthServer:Authority"]}/.well-known/openid-configuration")
                    });

                    options.AddSecurityRequirement(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oauth2" }
                            },
                            new[] { "SName.all" } // scopes
                        }
                    });
                });
            context.Services.AddSwaggerGenNewtonsoftSupport();

            Configure<AbpLocalizationOptions>(options =>
            {
                // options.Languages.Add(new LanguageInfo("cs", "cs", "Čeština"));
                options.Languages.Add(new LanguageInfo("en", "en", "English"));

                // options.Languages.Add(new LanguageInfo("pt-BR", "pt-BR", "Português"));
                // options.Languages.Add(new LanguageInfo("ru", "ru", "Русский"));
                // options.Languages.Add(new LanguageInfo("tr", "tr", "Türkçe"));
                options.Languages.Add(new LanguageInfo("zh-Hans", "zh-Hans", "简体中文"));

                // options.Languages.Add(new LanguageInfo("zh-Hant", "zh-Hant", "繁體中文"));
            });

            // Updates AbpClaimTypes to be compatible with identity server claims.
            AbpClaimTypes.UserId = JwtClaimTypes.Subject;
            AbpClaimTypes.UserName = JwtClaimTypes.Name;
            AbpClaimTypes.Role = JwtClaimTypes.Role;
            AbpClaimTypes.Email = JwtClaimTypes.Email;

            context.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
               .AddJwtBearer(options =>
               {
                   options.Authority = configuration["AuthServer:Authority"];
                   options.Audience = configuration["AuthServer:Audience"];
                   options.RequireHttpsMetadata = false;
                   options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(1);
               });

            Configure<AbpDistributedCacheOptions>(options =>
            {
                options.KeyPrefix = "SName:";
            });

            if (!hostingEnvironment.IsDevelopment())
            {
                var redis = ConnectionMultiplexer.Connect(configuration["Redis:Configuration"]);
                context.Services
                    .AddDataProtection()
                    .PersistKeysToStackExchangeRedis(redis, "SName-Protection-Keys");
            }

            context.Services.AddCors(options =>
            {
                options.AddPolicy(DefaultCorsPolicyName, builder =>
                {
                    builder
                        .WithOrigins(
                            configuration["App:CorsOrigins"]
                                .Split(",", StringSplitOptions.RemoveEmptyEntries)
                                .Select(o => o.RemovePostFix("/"))
                                .ToArray())
                        .WithAbpExposedHeaders()
                        .SetIsOriginAllowedToAllowWildcardSubdomains()
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });

            if (hostingEnvironment.IsDevelopment())
            {
                // for Auto-Migrate
                context.Services.AddAbpDbContext<SNameHttpApiHostMigrationsDbContext>();
            }

            Configure<AbpExceptionHttpStatusCodeOptions>(opt =>
            {
                // opt.Map(SNameErrorCodes.DynamicApplications.ManagementPermissionsNotGranted, HttpStatusCode.Forbidden);
                var codes = ReflectionHelper.GetPublicConstantsRecursively(typeof(SNameErrorCodes)).ToList();

                codes.Remove(SNameErrorCodes.GroupName);

                // codes.Remove(SNameErrorCodes.DynamicApplications.ManagementPermissionsNotGranted);
                foreach (var code in codes)
                {
                    opt.Map(code, HttpStatusCode.BadRequest);
                }
            });
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            var app = context.GetApplicationBuilder();
            var env = context.GetEnvironment();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseErrorPage();
                app.UseHsts();
            }

            app.UsePathBase($"/{PathBase}");
            app.UseHttpsRedirection();
            app.UseCorrelationId();
            app.UseVirtualFiles();
            app.UseRouting();
            app.UseCors(DefaultCorsPolicyName);
            app.UseAuthentication();
            app.UseAbpClaimsMap();
            if (MultiTenancyConsts.IsEnabled)
            {
#pragma warning disable CS0162 // 检测到无法访问的代码
                app.UseMultiTenancy();
#pragma warning restore CS0162 // 检测到无法访问的代码
            }

            app.UseAbpRequestLocalization();
            app.UseAuthorization();
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint($"/{PathBase}/swagger/v1/swagger.json", "Support APP API");
            });
            app.UseAuditing();
            app.UseAbpSerilogEnrichers();
            app.UseConfiguredEndpoints();
        }

        public override void OnPostApplicationInitialization(ApplicationInitializationContext context)
        {
            var hostEnv = context.GetEnvironment();
            if (hostEnv.IsDevelopment())
            {
                // for Auto-Migrate
                context.ServiceProvider.GetRequiredService<SNameDbMigrationService>().MigrateAsync().Wait();
            }
        }
    }
}
