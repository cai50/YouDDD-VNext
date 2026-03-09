# YouZack-VNext"# YouDDD-VNext" 
代码框架从6.0 升级到10.0 
大部分包都升级了

docker文件和Nginx 参考在CommonInitializer下面

流程说明：
1.环境变量配置（配置完，重启vs）
DefaultDB:ConnStr
Server=127.0.0.1,1433;Database=YourDB;User Id=sa;Password=YourStrongPassword!123;TrustServerCertificate=True

2.运行docker在目录下运行 docker-compose up -d
3.连接docker数据库，创建YourDB,创建T_configs

CREATE TABLE [dbo].[T_Configs] (
    [Id]    INT            IDENTITY (1, 1) NOT NULL,
    [Name]  NVARCHAR (128) NOT NULL,
    [Value] NVARCHAR (MAX) NOT NULL
);
SET IDENTITY_INSERT [dbo].[T_Configs] ON
INSERT INTO [dbo].[T_Configs] ([Id], [Name], [Value]) VALUES (1, N'Cors', N'{"Origins":["http://localhost:3000","http://localhost:3001"]}')
INSERT INTO [dbo].[T_Configs] ([Id], [Name], [Value]) VALUES (2, N'FileService:SMB', N'{"WorkingDir":"e:/temp/upload/"}')
INSERT INTO [dbo].[T_Configs] ([Id], [Name], [Value]) VALUES (3, N'FileService:UpYun', N'{"BucketName":"testfileservice","UserName":"test","Password":"mj3YcwN94NnwMhnFdQOn1B0Acoidd016"}')
INSERT INTO [dbo].[T_Configs] ([Id], [Name], [Value]) VALUES (5, N'FileService:Endpoint', N'{"UrlRoot":"http://localhost/FileService"}')
INSERT INTO [dbo].[T_Configs] ([Id], [Name], [Value]) VALUES (6, N'Redis', N'{"ConnStr":"127.0.0.1:46379"}')
INSERT INTO [dbo].[T_Configs] ([Id], [Name], [Value]) VALUES (7, N'RabbitMQ', N'{"HostName":"127.0.0.1","ExchangeName":"youzack_event_bus"}')
INSERT INTO [dbo].[T_Configs] ([Id], [Name], [Value]) VALUES (9, N'ElasticSearch', N'{"Url":"http://elastic:pxAAeyJgStG9eNSYXZNi@localhost:49200/"}')
INSERT INTO [dbo].[T_Configs] ([Id], [Name], [Value]) VALUES (10, N'JWT', N'{"Issuer":"myIssuer","Audience":"myAudience","Key":"afafafdfa23jyuobc@123_9876543210AB","ExpireSeconds":31536000}')
SET IDENTITY_INSERT [dbo].[T_Configs] OFF

4.执行EF 迁移（参考原教材）
5.启动多后端，启动前端。访问成功。