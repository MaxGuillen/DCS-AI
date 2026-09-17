using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GciLocal;

// ------------------------------------------------------------------
// GCI local para DCS
//
// Loop:
//   1. Lee el archivo stats.json que genera LotATC (dump_json_stats = true)
//   2. Arma un prompt con esa info + instrucciones de "hablar como GCI"
//   3. Se lo manda a un modelo corriendo en Ollama (local, sin internet)
//   4. Imprime la respuesta en consola
//
// Todavia NO incluye texto-a-voz ni conexion a SRS: eso va en el
// proximo paso una vez que confirmemos que esta parte anda bien.
// ------------------------------------------------------------------

internal class Program
{
    private static readonly HttpClient Http = new();
    private static readonly AppSettings Settings = LoadSettings();

    private static async Task Main()
    {
        Console.WriteLine("GCI local iniciado. Ctrl+C para salir.");
        Console.WriteLine($"Leyendo: {Settings.StatsJsonPath}");
        Console.WriteLine($"Modelo:  {Settings.Model}");
        Console.WriteLine();

        string? ultimoContenido = null;

        while (true)
        {
            try
            {
                if (!File.Exists(Settings.StatsJsonPath))
                {
                    Console.WriteLine("[!] No se encuentra stats.json todavia. " +
                                      "Revisa que LotATC este corriendo y que " +
                                      "dump_json_stats este en true.");
                }
                else
                {
                    string contenido = LeerStatsJsonSeguro(Settings.StatsJsonPath, Settings.MaxJsonCharacters);

                    // Evita gastar tokens si el archivo no cambio desde la ultima vez
                    if (contenido != ultimoContenido && !string.IsNullOrWhiteSpace(contenido))
                    {
                        ultimoContenido = contenido;

                        string prompt = ArmarPrompt(contenido);
                        string respuesta = await ConsultarOllama(prompt);

                        Console.WriteLine($"[GCI] {respuesta.Trim()}");
                        Console.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[error] {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromSeconds(Settings.IntervalSeconds));
        }
    }

    private static AppSettings LoadSettings()
    {
        string appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        if (!File.Exists(appSettingsPath))
        {
            throw new FileNotFoundException(
                "No se encontró appsettings.json junto al ejecutable. Copialo al directorio de salida o al directorio del proyecto.",
                appSettingsPath);
        }

        string json = File.ReadAllText(appSettingsPath);
        var settingsFile = JsonSerializer.Deserialize<SettingsFile>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        var settings = settingsFile?.GciLocal;

        if (settings is null)
        {
            throw new InvalidOperationException("El archivo appsettings.json no contiene una configuración válida.");
        }

        return settings;
    }

    // LotATC puede estar escribiendo el archivo justo cuando lo leemos,
    // asi que abrimos con FileShare.ReadWrite para no chocar con eso.
    private static string LeerStatsJsonSeguro(string path, int maxCharacters)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        string contenido = reader.ReadToEnd();

        if (contenido.Length > maxCharacters)
        {
            contenido = contenido[..maxCharacters];
        }

        return contenido;
    }

    private static string ArmarPrompt(string statsJson)
    {
        return $"""
            Sos un controlador GCI (Ground Control Intercept) de DCS World.
            Te paso un extracto del JSON con la situacion tactica actual
            (contactos de radar, posiciones, coalicion, etc).

            Reglas:
            - Respondé en español, corto (2-3 frases maximo).
            - Usá formato de llamada real de radio: rumbo, distancia, altitud
              cuando la info este disponible.
            - Priorizá el contacto mas relevante o amenazante si hay varios.
            - Si no hay contactos relevantes, decilo en una sola frase.
            - No expliques el JSON ni menciones que es un JSON.

            Datos:
            {statsJson}
            """;
    }

    private static async Task<string> ConsultarOllama(string prompt)
    {
        var body = new OllamaRequest
        {
            Model = Settings.Model,
            Prompt = prompt,
            Stream = false
        };

        HttpResponseMessage resp = await Http.PostAsJsonAsync(Settings.OllamaUrl, body);
        resp.EnsureSuccessStatusCode();

        var data = await resp.Content.ReadFromJsonAsync<OllamaResponse>();
        return data?.Response ?? "(sin respuesta del modelo)";
    }

    private class AppSettings
    {
        [JsonPropertyName("StatsJsonPath")]
        public string StatsJsonPath { get; set; } = "";

        [JsonPropertyName("IntervalSeconds")]
        public int IntervalSeconds { get; set; } = 15;

        [JsonPropertyName("OllamaUrl")]
        public string OllamaUrl { get; set; } = "http://localhost:11434/api/generate";

        [JsonPropertyName("Model")]
        public string Model { get; set; } = "llama3.1:8b";

        [JsonPropertyName("MaxJsonCharacters")]
        public int MaxJsonCharacters { get; set; } = 6000;
    }

    private class SettingsFile
    {
        public AppSettings? GciLocal { get; set; }
    }

    private class OllamaRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = "";

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    private class OllamaResponse
    {
        [JsonPropertyName("response")]
        public string? Response { get; set; }
    }
}
