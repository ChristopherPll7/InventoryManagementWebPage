# Inventory Management Web

Pruebas de las 13 operaciones, formularios, redirecciones y errores: [cobertura y ejecución](../docs/Frontend-API-Acceptance.md). Usan HTTP local con API y autenticación simuladas.

Interfaz ASP.NET Core MVC con vistas `.cshtml` para la API de Inventory Management. Es un proyecto independiente; consume las 13 rutas de categorías, productos e inventario mediante un `HttpClient` tipado y llamadas asincrónicas. La aplicación escucha por defecto en `http://localhost:5090` y la API en `http://localhost:8080/`.

## Requisitos locales

- Para **desarrollar o publicar**: SDK de .NET 8 o posterior compatible con `net8.0`, acceso a NuGet para restaurar `Microsoft.AspNetCore.Authentication.OpenIdConnect` y una terminal.
- Para **ejecutar la carpeta publicada en Windows x64**: no hace falta SDK ni runtime de .NET. La API y Keycloak deben estar iniciados y accesibles desde ese equipo. El frontend no contiene la API ni su base de datos.
- Iniciar los servicios locales de la API según el README del backend antes de abrir la interfaz. Comprobar `http://localhost:8080/swagger` y el servidor Keycloak local.

### Arrancar las dependencias

En el repositorio local del backend (`InventoryManagement`, hermano de este repositorio), con Docker Desktop iniciado:

```powershell
cd ../InventoryManagement
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
# Completar .env antes del siguiente comando; no sobrescribir un .env existente.
docker compose up -d --build
docker compose ps -a
docker compose logs --tail 80 api keycloak keycloak-init schema-migrate database-init
```

El `.env` del backend requiere las contraseñas de SQL Server y administrador de Keycloak, además de `INVENTORY_DEV_USERNAME` e `INVENTORY_DEV_PASSWORD` (contraseña de desarrollo de al menos 16 caracteres). No copiarlas al frontend. El inicializador conserva contraseñas existentes: cambiar `.env` no rota automáticamente un usuario ya creado.

Verificar SQL saludable y salida 0 de `database-init`, `schema-migrate` y `keycloak-init`; la API espera sus dependencias. Un servicio de inicialización terminado correctamente no debe permanecer ejecutándose. API usa 8080 y Keycloak 8081. No es necesario borrar volúmenes para arrancar. Preparar también hosts y callbacks como se indica abajo.

Comprobar `http://localhost:8080/swagger` y el descubrimiento OIDC de Keycloak. Un GET de catálogo sin token devuelve 401: confirma que el servidor responde, no que las operaciones autenticadas funcionen. Luego iniciar MVC desde su carpeta de código o publicación. Si el puerto 5090 está ocupado, detener solo la instancia propia que lo utiliza o elegir otro puerto y registrar sus callbacks en Keycloak.

## Inicio de sesión en Keycloak

La autoridad local es `http://keycloak.localhost:8081/realms/inventory-management`. El nombre debe resolver tanto para el navegador como para el proceso MVC. En Windows, si no resuelve, abrir como administrador `C:\Windows\System32\drivers\etc\hosts` y agregar una sola entrada, conservando el contenido existente:

```text
127.0.0.1 keycloak.localhost
```

Comprobar desde PowerShell:

```powershell
[System.Net.Dns]::GetHostAddresses('keycloak.localhost')
$oidc = Invoke-RestMethod 'http://keycloak.localhost:8081/realms/inventory-management/.well-known/openid-configuration'
$oidc | Select-Object issuer, authorization_endpoint, token_endpoint, end_session_endpoint
```

El `issuer` debe coincidir exactamente con la autoridad configurada y los endpoints deben usar ese mismo host local. No sustituir solamente `Authentication:Authority` por `localhost` ni desactivar la validación del emisor o audiencia para resolver un fallo de DNS.

Para comprobar el login usar un navegador actual: Keycloak puede emitir cookies `Secure` para el dominio local y un cliente HTTP de consola puede no reenviarlas sobre HTTP aunque el navegador sí las acepte. Esta configuración HTTP es exclusivamente local.

Se reutiliza el cliente público **`inventory-swagger`** con Authorization Code y PKCE `S256`. El cliente `inventory-api` sigue siendo el recurso `bearerOnly` y la audiencia del access token. Los mappers existentes del cliente Swagger agregan audiencia y permisos al access token.

En la configuración del cliente `inventory-swagger` de Keycloak, **conservar** la URL de Swagger y agregar:

```text
Valid Redirect URIs:
http://localhost:5090/signin-oidc
http://localhost:5090/signout-callback-oidc

Valid Post Logout Redirect URIs (si Keycloak muestra esta opción):
http://localhost:5090/signout-callback-oidc
http://localhost:5090/
```

El navegador irá al login de Keycloak. Iniciar sesión con el usuario de desarrollo de inventario y la contraseña configurada en el backend. Estas credenciales no se guardan ni se leen en MVC. El cliente es público y **no requiere `client secret`**. El acceso del usuario a cada acción depende de sus roles `products.read/write`, `categories.read/write` e `inventory.read/write`.

Si se cambia el host o el puerto de MVC, agregar las URL de retorno correspondientes en el mismo cliente Keycloak. La URL efectiva de redirección se forma con la dirección desde la que se abrió la interfaz y `/signin-oidc`.

## Configuración

`src/InventoryManagement.Web/appsettings.json` contiene valores de desarrollo no secretos:

| Clave | Predeterminado | Uso |
| --- | --- | --- |
| `Api:BaseUrl` | `http://localhost:8080/` | API protegida |
| `Authentication:Authority` | `http://keycloak.localhost:8081/realms/inventory-management` | Emisor y descubrimiento de Keycloak |
| `Authentication:ClientId` | `inventory-swagger` | Cliente público con PKCE |
| `ASPNETCORE_URLS` | `http://localhost:5090` | Dirección local de MVC |
| `AllowedHosts` | `localhost` | Nombres admitidos en el encabezado Host, sin esquema ni puerto |

Las variables de entorno sobrescriben la configuración JSON usando `__` para separar secciones. Ejemplo en PowerShell antes de iniciar el `.exe`:

```powershell
$env:ASPNETCORE_URLS = "http://localhost:5090"
$env:Api__BaseUrl = "http://localhost:8080/"
$env:Authentication__Authority = "http://keycloak.localhost:8081/realms/inventory-management"
```

La interfaz envía el access token como Bearer desde el servidor MVC. El navegador solo mantiene una cookie de sesión protegida. El token no se envía a las vistas ni a JavaScript. Los errores `401` llevan al inicio de sesión; los `403` informan falta de permiso.

`ASPNETCORE_URLS` controla dónde escucha MVC; `AllowedHosts` filtra qué nombres acepta. Cambiar solo el puerto en localhost no requiere cambiar AllowedHosts. Para otro nombre local, resolverlo en hosts, incluirlo explícitamente (separado por punto y coma) y registrar sus callbacks exactos en Keycloak:

```powershell
$env:ASPNETCORE_URLS = 'http://inventario.localhost:5090'
$env:AllowedHosts = 'localhost;inventario.localhost'
```

El archivo hosts necesitaría `127.0.0.1 inventario.localhost`. Keycloak necesitaría los retornos `http://inventario.localhost:5090/signin-oidc` y `http://inventario.localhost:5090/signout-callback-oidc`. Un 400 por Host rechazado se corrige con el nombre explícito; no hace falta usar `AllowedHosts=*`. El host de MVC y la autoridad `keycloak.localhost` son configuraciones diferentes.

## Ejecutar desde el código fuente

Desde esta carpeta `frontend/`:

```bash
dotnet restore InventoryManagement.Web.sln
dotnet run --project ./src/InventoryManagement.Web/InventoryManagement.Web.csproj
```

Abrir `http://localhost:5090`. Los formularios de producto esperan el precio con punto decimal, por ejemplo `150.50`. Los filtros de historial aceptan fecha y hora ISO 8601 con zona, por ejemplo `2026-09-29T12:00:00-06:00`.

## Publicar y entregar para Windows x64

### Fuente NuGet local

`artifacts/nuget-source/` contiene 11 paquetes `.nupkg`: OpenIdConnect 8.0.0, dependencias transitivas y runtimes win-x64 8.0.31, con inventario `SHA256.csv`. Se preparó con SDK 9.0.318. `artifacts/` está excluido de Git; copiar la fuente por separado para reutilizarla en otro equipo. Otro SDK puede necesitar versiones adicionales de runtime.

Para compilar sin internet se necesita previamente el SDK compatible y sus packs de referencia/targeting para net8.0 y el apphost win-x64, además de la fuente NuGet. La fuente de 11 paquetes se verificó en este equipo; no garantiza restauración en un Windows limpio con packs distintos. Preparar la fuente usando el SDK del equipo de compilación y añadir cualquier paquete de referencia/host requerido que falte. Si aparece NU1101, obtener ese paquete y sus dependencias desde un equipo con conectividad antes de repetir el restore local; `--ignore-failed-sources` no reemplaza un paquete ausente. Comparar los hashes con SHA256.csv tras copiar. Para ejecutar el frontend publicado no se necesita fuente NuGet, SDK ni Node.js: se entrega toda la carpeta publicada.

Para preparar la fuente con acceso a NuGet, desde `frontend/`:

```powershell
dotnet restore InventoryManagement.Web.sln -r win-x64 -p:SelfContained=true --packages ./artifacts/nuget-cache --source https://api.nuget.org/v3/index.json -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw 'Falló la restauración.' }
New-Item -ItemType Directory -Path ./artifacts/nuget-source -Force | Out-Null
Get-ChildItem ./artifacts/nuget-cache -Recurse -Filter *.nupkg | Copy-Item -Destination ./artifacts/nuget-source
Get-ChildItem ./artifacts/nuget-source -Filter *.nupkg | Get-FileHash -Algorithm SHA256 | Select-Object @{Name='Package';Expression={Split-Path $_.Path -Leaf}},Hash | Export-Csv ./artifacts/nuget-source/SHA256.csv -NoTypeInformation
```

Para restaurar, compilar y publicar solo con esa fuente local:

```powershell
dotnet restore InventoryManagement.Web.sln -r win-x64 -p:SelfContained=true --packages ./artifacts/nuget-offline-cache --source ./artifacts/nuget-source -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw 'Falló la restauración local.' }
dotnet build InventoryManagement.Web.sln -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación.' }
dotnet publish ./src/InventoryManagement.Web/InventoryManagement.Web.csproj -c Release -r win-x64 --self-contained true -o ./artifacts/publish/win-x64 --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Falló la publicación.' }
```

`NuGetAudit=false` evita consultas externas; estos comandos no realizan una auditoría de vulnerabilidades. El RID se especifica al publicar el proyecto, no al compilar la solución.

### Publicación habitual

Desde `frontend/` ejecutar el comando requerido:

```bash
dotnet publish ./src/InventoryManagement.Web/InventoryManagement.Web.csproj -c Release -r win-x64 --self-contained true -o ./artifacts/publish/win-x64
```

Entregar **toda** la carpeta `artifacts/publish/win-x64/`, no únicamente el `.exe`. En Windows, con API y Keycloak disponibles, ejecutar `InventoryManagement.Web.exe` desde esa carpeta y abrir `http://localhost:5090`. La publicación self contained incluye el runtime necesario para este frontend; no publica ni modifica la API.

## Estructura

```text
frontend/
  InventoryManagement.Web.sln
  src/InventoryManagement.Web/
    Controllers/     acciones MVC y confirmaciones
    Models/          formularios y DTO del contrato
    Services/        HttpClient, errores y permisos
    Views/           vistas Razor
    wwwroot/         CSS y JavaScript locales
  artifacts/publish/win-x64/   salida de publicación
```

## Comportamiento de la interfaz

### Permisos independientes

**Categorías y Productos** integran los accesos según permisos. Con lectura, se selecciona el registro desde el listado y su UUID se transmite automáticamente. Sin lectura del catálogo, la sección muestra un campo UUID y las operaciones autorizadas, sin consultar el listado. Productos también admite usuarios con solo inventory.read o inventory.write. Un UUID mal formado o vacío se rechaza conservando el formulario. El identificador debe obtenerse por un canal autorizado.

Un usuario con solo escritura puede crear, reemplazar los campos de un registro, confirmar su desactivación por UUID o registrar movimientos. Si no puede leer categorías, introduce el UUID de la categoría al guardar el producto. La API valida cada operación; un 403 de escritura sigue mostrándose como error. El detalle de producto ofrece registrar movimientos con inventory.write sin exigir inventory.read. Se retiró la pantalla /workspace y las búsquedas /lookup por SKU o nombre: el cliente conserva las 13 operaciones del contrato original.

Balance e historial no necesitan products.read. Sin ese permiso, el estado activo puede ser desconocido; la API lo comprueba al registrar. Con estado inactivo conocido o tras PRODUCT_INACTIVE, se bloquea el formulario. Sin inventory.read, el resultado del movimiento vuelve al formulario y no intenta cargar balance/historial.

Una edición sin lectura reemplaza todos los campos editables: el usuario debe indicar conscientemente la descripción que desea conservar o vaciar. Después de una escritura sin permiso de lectura, Operaciones muestra la confirmación y el identificador, sin redirigir a un detalle prohibido.

Casos y límites de aceptación: [permisos independientes](../docs/Acceptance-Independent-Permissions.md). Para ejecutar las comprobaciones con API y autenticación simuladas:

```powershell
dotnet restore ./tests/PermissionAcceptance/PermissionAcceptance.csproj --source ./artifacts/nuget-source --packages ./artifacts/nuget-offline-cache -p:NuGetAudit=false
dotnet run --project ./tests/PermissionAcceptance/PermissionAcceptance.csproj -c Release --no-restore
```

### Catálogos e inventario

El cliente exige 200 en GET/PUT, 201 en POST y 204 en DELETE. Un 2xx distinto o un DTO incompleto/inválido produce un error controlado 502 (INVALID_API_RESPONSE), sin reintentar la operación ni mostrar éxito. Los campos de respuesta son obligatorios, aunque los campos nullable pueden contener null; los campos desconocidos adicionales se toleran. Se comprueban UUID, nombres, tipos de movimiento, cantidades, stock, precios y estructura de páginas. Las páginas vacías fuera de rango siguen siendo válidas y no se asume una instantánea exacta de los totales.

Las altas de catálogo conservan `Location` en `CreatedId.Location` como URI opcional, relativa o absoluta. La navegación sigue utilizando el ID; no se sigue ese encabezado ni se envían tokens a su destino. Si falta o no es una URI válida, el metadato queda vacío.

La interfaz limita la navegación a páginas 1–1000 y tamaños 1–100. Este límite de página sigue la corrección solicitada y es más estricto que el máximo 10000 de la API/SDD original; el recorrido interno de categorías para el selector conserva el contrato de la API. El paginador no ofrece siguiente después de 1000 ni después de la última página real.

Una query inválida muestra un formulario de corrección con los valores introducidos y sin resultados: no se consultan filtros predeterminados como sustitución. Los enlaces de retorno conservan página/tamaño y los filtros del historial se mantienen al paginar y al registrar movimientos. Los destinos de retorno se validan como URL locales.

Los límites de texto se aplican después de recortar espacios externos. Los errores de descripción se muestran en su campo; una categoría inexistente al editar producto permite corregir la selección sin perder el formulario. Las cantidades inválidas conservan el texto introducido y los UUID vacíos se rechazan antes de llamar a la API.

El ejecutable de aceptación incluye 16 comprobaciones de formularios además de permisos y transporte. Para las 9 comprobaciones de validación JavaScript (requiere Node.js únicamente para ejecutar estas pruebas):

```powershell
node ./tests/PermissionAcceptance/form-validation.cjs
```

Cada llamada HTTP a la API tiene un plazo de 20 segundos que incluye cabeceras, lectura del cuerpo y deserialización (la obtención/renovación de sesión es una operación separada). Los fallos de transporte durante el cuerpo y el timeout se muestran como error recuperable. La cancelación de la petición MVC se propaga sin registrarse como timeout. No se reintentan escrituras automáticamente.

Las respuestas HTTP 5xx se registran en el servidor con estado, método y ruta. Su cuerpo no se lee ni se muestra: la UI recibe un mensaje genérico. Los errores de negocio 4xx conservan su contrato. El ejecutable de aceptación anterior incluye también 13 comprobaciones de transporte con cuerpos controlados, además de las 24 de permisos.

- Listas de categorías y productos paginadas; registros inactivos siguen visibles.
- Altas, edición completa y desactivación con confirmación. Las desactivaciones no eliminan físicamente ni reactivan registros.
- Selección de categorías activas recorriendo todas las páginas del catálogo.
- Existencia e historial paginado por producto; filtros por tipo y fechas con offset.
- Registro de entradas y salidas; el stock se consulta otra vez tras un resultado exitoso. Los conflictos de stock se muestran sin simular una operación aplicada.
- Validación HTML y del servidor, protección antifalsificación en formularios, mensajes de carga, estados vacíos y errores recuperables.
