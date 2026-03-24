using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Serilog;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;
using StackExchange.Redis;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Data.SqlClient;
using Zack.ASPNETCore;
using Zack.Commons;
using Zack.Commons.JsonConverters;
using Zack.EventBus;
using Zack.JWT;

namespace CommonInitializer
{
    public static class WebApplicationBuilderExtensions
    {
        public static void ConfigureDbConfiguration(this WebApplicationBuilder builder)
        {
            //builder.Host.ConfigureAppConfiguration((hostCtx, configBuilder) =>
            //{
            //    //不能使用ConfigureAppConfiguration中的configBuilder去读取配置，否则就循环调用了，因此这里直接自己去读取配置文件
            //    //var configRoot = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            //    //string connStr = configRoot.GetValue<string>("DefaultDB:ConnStr");
            //    string connStr = builder.Configuration.GetValue<string>("DefaultDB:ConnStr");
            //    configBuilder.AddDbConfiguration(() => new SqlConnection(connStr),tableName:"T_Docker_Congigs",reloadOnChange: true, reloadInterval: TimeSpan.FromSeconds(5));
            //});
#pragma warning disable ASP0013 // Suggest switching from using Configure methods to WebApplicationBuilder.Configuration
            builder.Host.ConfigureAppConfiguration((hostCtx, configBuilder) =>
            {
                // 1. 获取当前环境（Development / Production 等）
                var env = hostCtx.HostingEnvironment;

                // 2. 从当前已加载的配置中获取连接字符串
                // 注意：在 ConfigureAppConfiguration 中，builder.Configuration 已经包含了
                // appsettings.json 和 环境变量（如 DefaultDB__ConnStr）
                string connStr = builder.Configuration.GetValue<string>("DefaultDB:ConnStr");

                if (string.IsNullOrEmpty(connStr))
                {
                    // 防御性处理：如果没拿到连接字符串，记录日志或跳过 DB 配置加载
                    // 避免报 "The ConnectionString property has not been initialized"
                    return;
                }

                //if (env.IsDevelopment())
                //{
                //    // --- 开发环境：使用默认设置 ---
                //    configBuilder.AddDbConfiguration(
                //        () => new SqlConnection(connStr),
                //        reloadOnChange: true,
                //        reloadInterval: TimeSpan.FromSeconds(5));
                //}
                //else
                //{
                //    // --- 非开发环境（如 Production）：使用指定的表名 ---
                //    configBuilder.AddDbConfiguration(
                //        () => new SqlConnection(connStr),
                //        tableName: "T_Docker_Configs", // 指定生产环境配置表
                //        reloadOnChange: true,
                //        reloadInterval: TimeSpan.FromSeconds(5));
                //}

                if (env.IsDevelopment())
                {
                    configBuilder.AddDbConfiguration(
                        () => new MySqlConnection(connStr), // 换成 MySqlConnection
                        reloadOnChange: true,
                        reloadInterval: TimeSpan.FromSeconds(5));
                }
                else
                {
                    configBuilder.AddDbConfiguration(
                        () => new MySqlConnection(connStr), // 换成 MySqlConnection
                        tableName: "T_Docker_Configs",
                        reloadOnChange: true,
                        reloadInterval: TimeSpan.FromSeconds(5));
                }
            });
#pragma warning restore ASP0013 // Suggest switching from using Configure methods to WebApplicationBuilder.Configuration
        }

        public static void ConfigureExtraServices(this WebApplicationBuilder builder, InitializerOptions initOptions)
        {
            IServiceCollection services = builder.Services;
            IConfiguration configuration = builder.Configuration;
            var assemblies = ReflectionHelper.GetAllReferencedAssemblies();
            services.RunModuleInitializers(assemblies);
            services.AddAllDbContexts(ctx =>
            {
                //连接字符串如果放到appsettings.json中，会有泄密的风险
                //如果放到UserSecrets中，每个项目都要配置，很麻烦
                //因此这里推荐放到环境变量中。
                string connStr = configuration.GetValue<string>("DefaultDB:ConnStr");

                // 1. 定义 MySQL 版本 (自动探测或手动指定)
                // 推荐手动指定，避免程序启动时额外连一次数据库去探测版本，节省那点微小的启动内存
                var serverVersion = new MySqlServerVersion(new Version(8, 0, 31));

                // 2. 切换为 UseMySql
                ctx.UseMySql(connStr, serverVersion, options =>
                {
                    // 针对微服务和低内存环境的优化设置
                    //options.EnableRetryOnFailure(5); // 自动重试
                    options.CommandTimeout(30);     // 超时时间
                });
            }, assemblies);

            //开始:Authentication,Authorization
            //只要需要校验Authentication报文头的地方（非IdentityService.WebAPI项目）也需要启用这些
            //IdentityService项目还需要启用AddIdentityCore
            builder.Services.AddAuthorization();
            builder.Services.AddAuthentication();
            JWTOptions jwtOpt = configuration.GetSection("JWT").Get<JWTOptions>();
            builder.Services.AddJWTAuthentication(jwtOpt);
            //启用Swagger中的【Authorize】按钮。这样就不用每个项目的AddSwaggerGen中单独配置了
            builder.Services.Configure<SwaggerGenOptions>(c =>
            {
                c.AddAuthenticationHeader();
            });
            

            //结束:Authentication,Authorization

            //services.AddMediatR(assemblies);
            builder.Services.AddMediatR(cfg => {
                cfg.RegisterServicesFromAssemblies(assemblies.ToArray());
            });

            //现在不用手动AddMVC了，因此把文档中的services.AddMvc(options =>{})改写成Configure<MvcOptions>(options=> {})这个问题很多都类似
            services.Configure<MvcOptions>(options =>
            {
                options.Filters.Add<UnitOfWorkFilter>();
            });
            services.Configure<JsonOptions>(options =>
            {
                //设置时间格式。而非“2008-08-08T08:08:08”这样的格式
                options.JsonSerializerOptions.Converters.Add(new DateTimeJsonConverter("yyyy-MM-dd HH:mm:ss"));
            });

            services.AddCors(options =>
                {
                    //更好的在Program.cs中用绑定方式读取配置的方法：https://github.com/dotnet/aspnetcore/issues/21491
                    //不过比较麻烦。
                    var corsOpt = configuration.GetSection("Cors").Get<CorsSettings>();
                    string[] urls = corsOpt.Origins;
                    options.AddDefaultPolicy(builder => builder.WithOrigins(urls)
                            .AllowAnyMethod().AllowAnyHeader().AllowCredentials());
                }
            );
            services.AddLogging(builder =>
            {
                Log.Logger = new LoggerConfiguration()
                   // .MinimumLevel.Information().Enrich.FromLogContext()
                   .WriteTo.Console()
                   .WriteTo.File(initOptions.LogFilePath)
                   .CreateLogger();
                builder.AddSerilog();
            });
            services.AddValidatorsFromAssemblies(assemblies);
            services.AddFluentValidationAutoValidation();
            services.Configure<JWTOptions>(configuration.GetSection("JWT"));
            services.Configure<IntegrationEventRabbitMQOptions>(configuration.GetSection("RabbitMQ"));
            services.AddEventBus(initOptions.EventBusQueueName, assemblies);

            //Redis的配置
            string redisConnStr = configuration.GetValue<string>("Redis:ConnStr");
            IConnectionMultiplexer redisConnMultiplexer = ConnectionMultiplexer.Connect(redisConnStr);
            services.AddSingleton(typeof(IConnectionMultiplexer), redisConnMultiplexer);
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.All;
            });
        }
    }
}
