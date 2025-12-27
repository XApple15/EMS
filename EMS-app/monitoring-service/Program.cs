using Microsoft.EntityFrameworkCore;
using monitoring_service.BackgroundServices;
using monitoring_service.Data;
using monitoring_service.Infrastructure.Messaging;
using monitoring_service.Model;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton<IRabbitMqConnectionFactory, RabbitMqConnectionFactory>();
builder.Services.AddSingleton<IEventConsumer, RabbitMqEventConsumer>();
builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();


builder.Services.AddHostedService<SimulatorDataConsumerService>();
builder.Services.AddHostedService<DeviceCreatedConsumerService>();





builder.Services.AddDbContext<MonitorDbUtils>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ConsumptionDBconn")));


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
