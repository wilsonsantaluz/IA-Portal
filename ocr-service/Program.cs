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

app.MapGet("/", () => Results.Ok("OCR Service up!"));

app.MapPost("/ocr", async Task<IResult> (
    HttpRequest request,
    CancellationToken ct) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest("Content-Type deve ser multipart/form-data.");

    var form = await request.ReadFormAsync(ct);
    var file = form.Files.Count > 0 ? form.Files[0] : null;

    if (file is null)
        return Results.BadRequest("Arquivo não enviado.");

    if (file.Length == 0)
        return Results.BadRequest("Arquivo vazio.");

    // Salvar arquivo temporário
    var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    await using (var stream = File.Create(tempFile))
    {
        await file.CopyToAsync(stream, ct);
    }

    // Detectar extensão
    var ext = Path.GetExtension(file.FileName).ToLower();
    List<string> images = new();

    // =======================================
    // PDF → PNG usando POPPLER (pdftoppm)
    // =======================================
    if (ext == ".pdf")
    {
        string outPrefix = tempFile + "_page";
        string cmd = $"pdftoppm -png -r 300 \"{tempFile}\" \"{outPrefix}\"";

        RunBash(cmd);

        images = Directory.GetFiles(Path.GetDirectoryName(tempFile)!,
                Path.GetFileName(tempFile) + "_page*.png")
            .OrderBy(f => f)
            .ToList();
    }
    else
    {
        // imagem normal
        images.Add(tempFile);
    }

    // ================================
    // OCR via CLI (SEM TesseractEngine)
    // ================================
    var sb = new System.Text.StringBuilder();

    foreach (var img in images)
    {
        string text = RunTesseract(img);
        sb.AppendLine(text);
    }

    // Limpar arquivos temporários
    try
    {
        File.Delete(tempFile);
        foreach (var i in images)
            if (File.Exists(i)) File.Delete(i);
    }
    catch { }

    return Results.Ok(new OcrResponse(sb.ToString()));
});

app.Run();

static string RunTesseract(string imagePath)
{
    var psi = new System.Diagnostics.ProcessStartInfo
    {
        FileName = "bash",
        Arguments = $"-lc \"tesseract '{imagePath}' stdout -l por+eng\"",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };

    var proc = System.Diagnostics.Process.Start(psi)!;

    string output = proc.StandardOutput.ReadToEnd();
    string error = proc.StandardError.ReadToEnd();
    proc.WaitForExit();

    if (!string.IsNullOrWhiteSpace(error))
        Console.WriteLine("TESSERACT ERROR: " + error);

    return output;
}

static void RunBash(string cmd)
{
    var psi = new System.Diagnostics.ProcessStartInfo
    {
        FileName = "bash",
        Arguments = $"-lc \"{cmd}\"",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };

    var proc = System.Diagnostics.Process.Start(psi)!;
    proc.WaitForExit();
}

public record OcrResponse(string Texto);
