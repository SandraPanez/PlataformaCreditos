## Pasos para correr localmente

1. Clonar el repositorio
2. Instalar .NET 8 SDK
3. Ejecutar migraciones: `dotnet ef database update`
4. Ejecutar el proyecto: `dotnet run`

## Variables de entorno

- `ASPNETCORE_ENVIRONMENT` = `Production`
- `ASPNETCORE_URLS` = `http://0.0.0.0:${PORT}`
- `ConnectionStrings__DefaultConnection` = Cadena de conexión SQLite
- `Redis__ConnectionString` = URL de conexión Redis

## Usuarios de prueba

- **Analista:** analista@banco.com / Analista123!
- **Cliente:** cliente@banco.com / Cliente123!

## URLs

- GitHub: https://github.com/SandraPanez/PlataformaCreditos
- Render: https://plataformacreditos-y2dy.onrender.com