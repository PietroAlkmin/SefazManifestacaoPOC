using SefazManifestacaoPOC.Services;

var builder = WebApplication.CreateBuilder(args);

// Adicionar serviços
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() 
    { 
        Title = "SEFAZ Manifestação POC", 
        Version = "v1",
        Description = "API para manifestação de ciência da operação (210210) em ResNFe da SEFAZ"
    });
});

// Registrar serviços customizados
builder.Services.AddHttpClient();
builder.Services.AddSingleton<XmlEventoBuilder>();
builder.Services.AddSingleton<XmlSignatureService>();
builder.Services.AddScoped<SefazEventoClient>();

var app = builder.Build();

// Configurar pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SEFAZ Manifestação POC v1");
        c.RoutePrefix = string.Empty; // Swagger na raiz
    });
}

app.UseAuthorization();
app.MapControllers();

app.Run();
