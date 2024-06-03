using System.Xml.Schema;
using MassTransit;
using MassTransit.AzureServiceBusTransport;
using MassTransitDemo;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMvc();

builder.Services.AddMassTransit(x =>
{
    // Must share an ASB, so to avoid stealing messages from other developers, we'll prefix all the queues with the username
    var prefix = builder.Environment.IsDevelopment() ? Environment.UserName + "/" : "";
    
    // Register the consumer in DI
    x.AddConsumer<DeliverReferralConsumer>();
    
    x.UsingAzureServiceBus((context, cfg) =>
    {
        // dotnet user-secrets set SbConnectionString "...."
        // you can find the value in the azure portal, you need a key with Manage rights bc Masstransit will create topics and queues for you
        cfg.Host(builder.Configuration["SbConnectionString"]);
        
        // Retry immediately 3 times
        cfg.UseMessageRetry(r => r.Immediate(3)); // try 3 times immediately
        
        // Then start using Scheduled send to delay retries
        cfg.UseDelayedRedelivery(r => r.Intervals(
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(10),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromHours(1),
            TimeSpan.FromHours(2),
            TimeSpan.FromHours(4),
            TimeSpan.FromHours(8),
            TimeSpan.FromHours(16),
            TimeSpan.FromHours(24),
            TimeSpan.FromHours(24),
            TimeSpan.FromHours(24)));
        
        // Tell mass transit to use ASB as the message scheduler for delayed or scheduled messages
        cfg.UseServiceBusMessageScheduler();
        
        
        // override the default topic name formatter to include our username prefix
        cfg.MessageTopology.SetEntityNameFormatter(
            new PrefixEntityNameFormatter(
                new MessageNameFormatterEntityNameFormatter(new ServiceBusMessageNameFormatter()),
                prefix));


        // automatically wire up the registered consumers
        cfg.ConfigureEndpoints(context);
    });

    // add our prefix to the names of the queues
    x.SetEndpointNameFormatter(new SnakeCaseEndpointNameFormatter(prefix));

    // Enlist in EF transactions to use a transactional outbox to ensure never losing an outgoing message
    x.AddEntityFrameworkOutbox<AppDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
    });
    
    x.AddConfigureEndpointsCallback((context, name, cfg) =>
    {
        // Use the outbox on all outgoing messages
        cfg.UseEntityFrameworkOutbox<AppDbContext>(context);
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    // dotnet user-secrets set ConnectionString "...."
    options.UseSqlServer(builder.Configuration["ConnectionString"])
        .EnableDetailedErrors()
        .EnableSensitiveDataLogging();
});

var app = builder.Build();
app.UseHttpsRedirection();

app.MapControllers();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Real app would use our migration framework, but will have to extract scripts for creating the mass transit outbox tables
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();
}

app.Run();

/*
create table InboxState
   (
       Id                 bigint identity
           constraint PK_InboxState
               primary key,
       MessageId          uniqueidentifier not null,
       ConsumerId         uniqueidentifier not null,
       LockId             uniqueidentifier not null,
       RowVersion         timestamp        null,
       Received           datetime2        not null,
       ReceiveCount       int              not null,
       ExpirationTime     datetime2,
       Consumed           datetime2,
       Delivered          datetime2,
       LastSequenceNumber bigint,
       constraint AK_InboxState_MessageId_ConsumerId
           unique (MessageId, ConsumerId)
   )
   go
   
   create index IX_InboxState_Delivered
       on InboxState (Delivered)
   go
   
   create table OutboxMessage
(
    SequenceNumber     bigint identity
        constraint PK_OutboxMessage
            primary key,
    EnqueueTime        datetime2,
    SentTime           datetime2        not null,
    Headers            nvarchar(max),
    Properties         nvarchar(max),
    InboxMessageId     uniqueidentifier,
    InboxConsumerId    uniqueidentifier,
    OutboxId           uniqueidentifier,
    MessageId          uniqueidentifier not null,
    ContentType        nvarchar(256)    not null,
    MessageType        nvarchar(max)    not null,
    Body               nvarchar(max)    not null,
    ConversationId     uniqueidentifier,
    CorrelationId      uniqueidentifier,
    InitiatorId        uniqueidentifier,
    RequestId          uniqueidentifier,
    SourceAddress      nvarchar(256),
    DestinationAddress nvarchar(256),
    ResponseAddress    nvarchar(256),
    FaultAddress       nvarchar(256),
    ExpirationTime     datetime2
)
go

create index IX_OutboxMessage_EnqueueTime
    on OutboxMessage (EnqueueTime)
go

create index IX_OutboxMessage_ExpirationTime
    on OutboxMessage (ExpirationTime)
go

create unique index IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber
    on OutboxMessage (InboxMessageId, InboxConsumerId, SequenceNumber)
    where [InboxMessageId] IS NOT NULL AND [InboxConsumerId] IS NOT NULL
go

create unique index IX_OutboxMessage_OutboxId_SequenceNumber
    on OutboxMessage (OutboxId, SequenceNumber)
    where [OutboxId] IS NOT NULL
go

create table OutboxState
(
    OutboxId           uniqueidentifier not null
        constraint PK_OutboxState
            primary key,
    LockId             uniqueidentifier not null,
    RowVersion         timestamp        null,
    Created            datetime2        not null,
    Delivered          datetime2,
    LastSequenceNumber bigint
)
go

create index IX_OutboxState_Created
    on OutboxState (Created)
go
*/