using Microsoft.AspNetCore.SignalR;
using websocket_service.Service;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddSignalR();
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IRabbitMQService, RabbitMQService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins(
            "http://localhost:3000",      // React default port
            "http://localhost:5173",      // Vite default port
            "http://localhost:5174",       // Alternative Vite port
            "http://frontend"
        )
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials()
        .SetIsOriginAllowedToAllowWildcardSubdomains();

    });
});

var app = builder.Build();
app.UseCors("AllowReactApp");

app.MapHub<ChatHub>("/chathub");
app.Services.GetRequiredService<IRabbitMQService>();
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
