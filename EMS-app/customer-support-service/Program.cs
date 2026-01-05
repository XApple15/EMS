using customer_support_service.Service;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

builder.Services.AddSingleton<ISupportAgent, SupportAgent>();
builder.Services.AddSingleton<IRabbitMQSupportService, RabbitMQSupportService>();


builder.Services.Configure<RabbitMQSettings>(options =>
{
    options.AdminChatQueue = "admin_chat_messages";
});

// Register Admin Chat services
builder.Services.AddSingleton<IAdminChatPublisher, AdminChatPublisher>();
builder.Services.AddSingleton<IAdminChatService, AdminChatService>();
var app = builder.Build();

app.Services.GetRequiredService<IRabbitMQSupportService>();


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
