# Lumina Store (.NET 8)
Requires the .NET 8 SDK.

    cd Api && dotnet run --urls http://localhost:5080     # Swagger at /swagger, health at /health
    cd Web && dotnet run --urls http://localhost:5173     # Blazor WASM frontend

Set a different API address in Web/wwwroot/appsettings.json ("ApiUrl") and CORS origins in Api/appsettings.json.
