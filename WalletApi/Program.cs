var builder = WebApplication.CreateBuilder(args);

// --- Servis kayıtları (DI konteyneri) — Spring'in ApplicationContext'i ---
builder.Services.AddControllers();

// Swagger/OpenAPI: API'yi tarayıcıdan görüp test etmeni sağlar (springdoc karşılığı)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// --- HTTP pipeline (middleware zinciri) — Spring'in Filter zinciri ---
// Swagger UI'ı yalnızca geliştirme ortamında aç
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();     // /swagger/v1/swagger.json (OpenAPI şeması)
    app.UseSwaggerUI();   // /swagger (tarayıcıdaki arayüz)
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
