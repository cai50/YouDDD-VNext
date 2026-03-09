using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Zack.EventBus
{
    // 在 7.0+ 中，建议将此类设为异步管理类
    class RabbitMQConnection : IDisposable
    {
        private readonly IConnectionFactory _connectionFactory;
        private IConnection? _connection;
        private bool _disposed;
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1); // 替代 lock，支持异步等待

        public RabbitMQConnection(IConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public bool IsConnected => _connection is { IsOpen: true } && !_disposed;

        // 7.0+ 中 IModel 已更名为 IChannel
        public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                // 尝试自动重连
                await TryConnectAsync(cancellationToken);
            }

            if (!IsConnected)
                throw new InvalidOperationException("No RabbitMQ connections are available.");

            // 异步创建通道
            return await _connection!.CreateChannelAsync(cancellationToken: cancellationToken);
        }

        public async Task<bool> TryConnectAsync(CancellationToken cancellationToken = default)
        {
            // 使用 SemaphoreSlim 保证异步环境下的线程安全
            await _connectionLock.WaitAsync(cancellationToken);
            try
            {
                if (IsConnected) return true;

                // 7.0+ 核心变化：异步连接
                _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

                if (IsConnected)
                {
                    // 订阅异步事件
                    _connection.ConnectionShutdownAsync += OnConnectionShutdownAsync;
                    _connection.CallbackExceptionAsync += OnCallbackExceptionAsync;
                    _connection.ConnectionBlockedAsync += OnConnectionBlockedAsync;

                    return true;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        // 异步事件处理程序
        private Task OnConnectionBlockedAsync(object sender, ConnectionBlockedEventArgs e)
        {
            if (_disposed) return Task.CompletedTask;
            return TryConnectAsync();
        }

        private Task OnCallbackExceptionAsync(object sender, CallbackExceptionEventArgs e)
        {
            if (_disposed) return Task.CompletedTask;
            return TryConnectAsync();
        }

        private Task OnConnectionShutdownAsync(object sender, ShutdownEventArgs reason)
        {
            if (_disposed) return Task.CompletedTask;
            // 7.0+ 内部已有更好的重连机制，但手动触发仍可保留
            return TryConnectAsync();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                // 生产环境建议使用异步 DisposeAsync，这里演示同步释放
                _connection?.Dispose();
                _connectionLock.Dispose();
            }
            catch (Exception)
            {
                // 忽略释放异常
            }
        }
    }
}