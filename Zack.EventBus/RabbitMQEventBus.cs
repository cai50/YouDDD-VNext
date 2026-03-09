using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Zack.EventBus;

class RabbitMQEventBus : IEventBus, IDisposable
{
    private IChannel _consumerChannel; // 旧版是 IModel
    private readonly string _exchangeName;
    private string _queueName;
    private readonly RabbitMQConnection _persistentConnection;
    private readonly SubscriptionsManager _subsManager;
    private readonly IServiceScope _serviceScope;
    private readonly IServiceProvider _serviceProvider;

    public RabbitMQEventBus(RabbitMQConnection persistentConnection,
        IServiceScopeFactory serviceProviderFactory, string exchangeName, string queueName)
    {
        this._persistentConnection = persistentConnection ?? throw new ArgumentNullException(nameof(persistentConnection));
        this._subsManager = new SubscriptionsManager();
        this._exchangeName = exchangeName;
        this._queueName = queueName;

        this._serviceScope = serviceProviderFactory.CreateScope();
        this._serviceProvider = _serviceScope.ServiceProvider;

        // 注意：在构造函数中调用异步方法是危险的。
        // 在 7.0+ 中建议通过一个异步的 Init 方法来初始化。这里为了兼容你的逻辑，使用 Task.Run 同步化获取。
        this._consumerChannel = Task.Run(() => CreateConsumerChannelAsync()).GetAwaiter().GetResult();
        this._subsManager.OnEventRemoved += async (s, e) => await SubsManager_OnEventRemoved(s, e);
    }

    private async Task SubsManager_OnEventRemoved(object? sender, string eventName)
    {
        if (!_persistentConnection.IsConnected)
        {
            await _persistentConnection.TryConnectAsync();
        }

        // 异步创建临时 Channel
        using var channel = await _persistentConnection.CreateChannelAsync();
        await channel.QueueUnbindAsync(queue: _queueName,
            exchange: _exchangeName,
            routingKey: eventName);

        if (_subsManager.IsEmpty)
        {
            _queueName = string.Empty;
            await _consumerChannel.CloseAsync(); // 异步关闭
        }
    }

    public async Task PublishAsync(string eventName, object? eventData)
    {
        if (!_persistentConnection.IsConnected)
        {
            await _persistentConnection.TryConnectAsync();
        }

        using var channel = await _persistentConnection.CreateChannelAsync();

        // 声明交换机 (异步)
        await channel.ExchangeDeclareAsync(exchange: _exchangeName, type: ExchangeType.Direct);

        byte[] body;
        if (eventData == null)
        {
            body = Array.Empty<byte>();
        }
        else
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            body = JsonSerializer.SerializeToUtf8Bytes(eventData, eventData.GetType(), options);
        }

        // 7.0+ 设置消息持久化的新方式
        var properties = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent // 使用枚举代替数字 2
        };

        // 异步发布消息
        await channel.BasicPublishAsync(
            exchange: _exchangeName,
            routingKey: eventName,
            mandatory: true,
            basicProperties: properties,
            body: body);
    }

    // 适配原有接口（如果 IEventBus 还是同步的，建议也改为异步）
    public void Publish(string eventName, object? eventData)
    {
        Task.Run(() => PublishAsync(eventName, eventData)).Wait();
    }

    public void Subscribe(string eventName, Type handlerType)
    {
        CheckHandlerType(handlerType);
        // 内部涉及网络操作，在 7.0 中建议全异步，此处使用 Task.Run 同步化
        Task.Run(async () => {
            await DoInternalSubscriptionAsync(eventName);
            _subsManager.AddSubscription(eventName, handlerType);
            await StartBasicConsumeAsync();
        }).Wait();
    }

    private async Task DoInternalSubscriptionAsync(string eventName)
    {
        var containsKey = _subsManager.HasSubscriptionsForEvent(eventName);
        if (!containsKey)
        {
            if (!_persistentConnection.IsConnected)
            {
                await _persistentConnection.TryConnectAsync();
            }
            await _consumerChannel.QueueBindAsync(queue: _queueName,
                                            exchange: _exchangeName,
                                            routingKey: eventName);
        }
    }

    private void CheckHandlerType(Type handlerType)
    {
        if (!typeof(IIntegrationEventHandler).IsAssignableFrom(handlerType))
        {
            throw new ArgumentException($"{handlerType} doesn't inherit from IIntegrationEventHandler", nameof(handlerType));
        }
    }

    public void Unsubscribe(string eventName, Type handlerType)
    {
        CheckHandlerType(handlerType);
        _subsManager.RemoveSubscription(eventName, handlerType);
    }

    public void Dispose()
    {
        _consumerChannel?.Dispose();
        _subsManager.Clear();
        _persistentConnection.Dispose();
        _serviceScope.Dispose();
    }

    private async Task StartBasicConsumeAsync()
    {
        if (_consumerChannel != null)
        {
            // 7.0+ 默认就是异步消费，AsyncEventingBasicConsumer 依然可用
            var consumer = new AsyncEventingBasicConsumer(_consumerChannel);
            consumer.ReceivedAsync += Consumer_Received; // 事件名变为 ReceivedAsync

            await _consumerChannel.BasicConsumeAsync(
                queue: _queueName,
                autoAck: false,
                consumer: consumer);
        }
    }

    private async Task Consumer_Received(object sender, BasicDeliverEventArgs eventArgs)
    {
        var eventName = eventArgs.RoutingKey;
        var message = Encoding.UTF8.GetString(eventArgs.Body.Span);
        try
        {
            await ProcessEvent(eventName, message);
            // 异步确认
            await _consumerChannel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            Debug.Fail(ex.ToString());
        }
    }

    private async Task<IChannel> CreateConsumerChannelAsync()
    {
        if (!_persistentConnection.IsConnected)
        {
            await _persistentConnection.TryConnectAsync();
        }

        var channel = await _persistentConnection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(exchange: _exchangeName, type: ExchangeType.Direct);

        await channel.QueueDeclareAsync(queue: _queueName,
                                  durable: true,
                                  exclusive: false,
                                  autoDelete: false,
                                  arguments: null);

        // 7.0+ 事件改名为 CallbackExceptionAsync
        channel.CallbackExceptionAsync += (sender, ea) =>
        {
            Debug.Fail(ea.ToString());
            return Task.CompletedTask;
        };

        return channel;
    }

    private async Task ProcessEvent(string eventName, string message)
    {
        if (_subsManager.HasSubscriptionsForEvent(eventName))
        {
            var subscriptions = _subsManager.GetHandlersForEvent(eventName);
            foreach (var subscription in subscriptions)
            {
                using var scope = this._serviceProvider.CreateScope();
                if (scope.ServiceProvider.GetService(subscription) is IIntegrationEventHandler handler)
                {
                    await handler.Handle(eventName, message);
                }
                else
                {
                    throw new ApplicationException($"无法创建{subscription}类型的服务");
                }
            }
        }
    }
}