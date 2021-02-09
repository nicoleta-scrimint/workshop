﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Messages;
using NServiceBus;
using NServiceBus.Logging;

class AddItemHandler : IHandleMessages<AddItem>
{
    OrderRepository orderRepository;

    public AddItemHandler(OrderRepository orderRepository)
    {
        this.orderRepository = orderRepository;
    }

    public async Task Handle(AddItem message, 
        IMessageHandlerContext context)
    {
        log.Info($"Adding item {message.Filling}.");

        var order = await orderRepository.Load(message.OrderId);

        var line = new OrderLine(message.Filling);
        order.Lines.Add(line);

        await context.PublishImmediately(
            new ItemAdded(message.OrderId, message.Filling));

        await orderRepository.Store(order);

        await Task.Delay(TimeSpan.FromSeconds(40));

        log.Info($"Item {message.Filling} added.");
    }

    static readonly ILog log = LogManager.GetLogger<AddItemHandler>();
}