using Microsoft.EntityFrameworkCore;

namespace CommonInitializer
{
    public static class DbContextOptionsBuilderFactory
    {
        public static DbContextOptionsBuilder<TDbContext> Create<TDbContext>()
            where TDbContext : DbContext
        {
            //var connStr = Environment.GetEnvironmentVariable("DefaultDB:ConnStr");
            //var optionsBuilder = new DbContextOptionsBuilder<TDbContext>();
            ////optionsBuilder.UseSqlServer("Data Source=.;Initial Catalog=YouzackVNextDB;User ID=sa;Password=dLLikhQWy5TBz1uM;");
            ////optionsBuilder.UseSqlServer(connStr);
            //return optionsBuilder;


            var connStr = Environment.GetEnvironmentVariable("DefaultDB:ConnStr");
            var optionsBuilder = new DbContextOptionsBuilder<TDbContext>();

            // 1. 定义 MySQL 版本 (自动探测或手动指定)
            // 推荐手动指定，避免程序启动时额外连一次数据库去探测版本，节省那点微小的启动内存
            var serverVersion = new MySqlServerVersion(new Version(8, 0, 31));

            // 2. 切换为 UseMySql
            optionsBuilder.UseMySql(connStr, serverVersion, options =>
            {
                // 针对微服务和低内存环境的优化设置
                //options.EnableRetryOnFailure(3); // 自动重试
                options.CommandTimeout(30);     // 超时时间
            });

            return optionsBuilder;
        }
    }
}
