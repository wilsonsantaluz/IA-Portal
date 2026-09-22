using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => Results.Ok("ML Service up!"));

// Endpoint ML
app.MapPost("/predict", ([FromBody] PredictRequest req) =>
{
    var previsoes = new List<Dictionary<string, object>>();

    for (int i = 0; i < req.MesesProjecao; i++)
    {
        previsoes.Add(new Dictionary<string, object>
        {
            ["mesOffset"] = i,
            ["valorPrevisto"] = 100000 - i * 5000,
            ["riscoInadimplencia"] = 0.10 + i * 0.02
        });
    }

    return Results.Ok(new PredictResponse(previsoes));
});

app.Run();


// -------------------------------------------------------
// RECORDS DEVEM FICAR DEPOIS DO app.Run()
// (programa top-level já terminou)
// -------------------------------------------------------

public record PredictRequest(string CarteiraId, int MesesProjecao = 6);

public record PredictResponse(List<Dictionary<string, object>> Previsoes);
