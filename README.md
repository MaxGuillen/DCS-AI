# GCI local para DCS — MVP

Lee el `stats.json` de LotATC y le pide a un modelo local (Ollama) que
responda como un controlador GCI. Todo corre en tu PC, sin internet ni
costos de API.

## 1. Activar el export de LotATC

Editá tu `config.lua` (o `config.custom.lua`) de LotATC:

```lua
dump_json_stats = true,
dump_json_filename = "stats.json",
```

Reiniciá el servidor de LotATC / la misión.

## 2. Instalar Ollama

Descargalo de https://ollama.com, instalalo, y bajá un modelo:

```
ollama pull llama3.1:8b
```

Con 8GB+ de VRAM esto corre bien en paralelo con DCS.

## 3. Ajustar la configuración del proyecto

Abrí `Program.cs` y editá estas dos líneas con tu ruta real:

```csharp
private const string StatsJsonPath = @"C:\Users\TU_USUARIO\Saved Games\DCS\Mods\services\LotAtc\stats.json";
```

Buscá dónde quedó realmente el archivo una vez que actives el export —
la ruta exacta depende de tu instancia de DCS (Saved Games puede tener
un sufijo tipo `DCS.openbeta`, etc).

## 4. Correrlo

Necesitás el [.NET SDK 8](https://dotnet.microsoft.com/download) instalado.

```
cd GciLocal
dotnet run
```

Te va a ir imprimiendo en consola lo que "diría" el GCI cada vez que
detecta un cambio en el archivo.

## Qué falta (próximos pasos)

- **Texto a voz**: conectar la respuesta a Piper para que se escuche,
  no solo se lea en consola.
- **Salida por radio**: pasarle el audio generado a
  `DCS-SR-ExternalAudio.exe` para que salga por una frecuencia de SRS.
- **Parseo más preciso**: una vez que veas la estructura real de tu
  `stats.json`, se puede filtrar antes de mandarlo al modelo (por
  ejemplo, ignorar contactos muy lejos) en vez de mandar el archivo
  entero.
