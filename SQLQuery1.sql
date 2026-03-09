SET IDENTITY_INSERT [dbo].[T_Configs] ON;

INSERT INTO [dbo].[T_Configs] ([Id], [Name], [Value]) VALUES
(1, N'Cors', N'{"Origins":["http://localhost:3000","http://localhost:3001"]}'),
(2, N'FileService:SMB', N'{"WorkingDir":"e:/temp/upload/"}'),
(3, N'FileService:UpYun', N'{"BucketName":"testfileservice","UserName":"test","Password":"mj3YcwN94NnwMhnFdQOn1B0Acoidd016"}'),
(5, N'FileService:Endpoint', N'{"UrlRoot":"http://localhost/FileService"}'),
(6, N'Redis', N'{"ConnStr":"localhost"}'),
(7, N'RabbitMQ', N'{"HostName":"127.0.0.1","ExchangeName":"youzack_event_bus"}'),
(9, N'ElasticSearch', N'{"Url":"http://elastic:pxAAeyJgStG9eNSYXZNi@localhost:9200/"}'),
(10, N'JWT', N'{"Issuer":"myIssuer","Audience":"myAudience","Key":"afafafdfa23jyuobc@123","ExpireSeconds":31536000}');

-- 执行完后记得关闭
SET IDENTITY_INSERT [dbo].[T_Configs] OFF;