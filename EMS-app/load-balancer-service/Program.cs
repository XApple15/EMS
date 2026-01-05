using load_balancer_service.Configuration;
using load_balancer_service.Infrastructure.Messaging;
using load_balancer_service.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure RabbitMQ
builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton<IRabbitMqConnectionFactory, RabbitMqConnectionFactory>();

// Configure Load Balancer
builder.Services.Configure<LoadBalancerSettings>(builder.Configuration.GetSection("LoadBalancer"));

// Register replica selectors
builder.Services.AddSingleton<ConsistentHashingSelector>();
builder.Services.AddSingleton<LoadBasedSelector>();
builder.Services.AddSingleton<WeightedRoundRobinSelector>();
builder.Services.AddSingleton<ReplicaSelectorFactory>();

// Register hosted service
builder.Services.AddHostedService<LoadBalancingHostedService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();
