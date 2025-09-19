using cas_cdrrestore_service.Helpers;
using cas_cdrrestore_service.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<RestoreService>();
builder.Services.AddScoped<TarHelper>();
builder.Services.AddScoped<GoogleClient>();
builder.Services.AddHttpClient("default")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
