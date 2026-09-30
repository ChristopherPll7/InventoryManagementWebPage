# Estado de verificación de esta entrega

## Corrección P1 — 30 de septiembre de 2026

Entorno: Windows x64, SDK .NET 9.0.318, proyecto net8.0.

Comprobaciones ejecutadas:

- Descarga de OpenIdConnect 8.0.0, dependencias transitivas y runtimes win-x64 8.0.31.
- Fuente local artifacts/nuget-source: 11 paquetes e inventario SHA256; excluida de Git.
- Restauración usando solo esa fuente y una caché nueva (artifacts/nuget-offline-cache): correcta. Auditoría NuGet desactivada para evitar consultas externas.
- dotnet build InventoryManagement.Web.sln -c Release --no-restore: correcto, 0 errores y 0 advertencias; compilación C# y Razor.
- Publicación del proyecto con -c Release -r win-x64 --self-contained true -o ./artifacts/publish/win-x64 --no-restore: correcta.
- Generados InventoryManagement.Web.exe, hostfxr.dll, hostpolicy.dll y coreclr.dll. runtimeconfig.json declara includedFrameworks .NET y ASP.NET Core 8.0.31.
- Inicio directo del ejecutable en Production con ASPNETCORE_URLS=http://localhost:5091. El puerto 5090 estaba ocupado; no se detuvo el proceso existente.
- GET de /, /css/site.css y /js/site.js en 5091: HTTP 200.
- Inspección de módulos: hostfxr.dll, hostpolicy.dll y coreclr.dll cargados desde la carpeta publicada.

Se corrigió CS8604 en TokenSessionService comprobando explícitamente las propiedades de autenticación y eliminando accesos condicionales innecesarios. La compilación final no muestra advertencias.

El intento de build de solución con RID produjo NETSDK1134; se corrigió la invocación, usando build de solución sin RID y publish del proyecto con RID. No fue un fallo del código fuente.

El primer arranque en 5090 falló por puerto ocupado; al registrar ese fallo se encontró también una restricción del Event Log en el sandbox. El arranque en 5091 fue correcto.

## Límites

No se probó en un equipo limpio sin SDK/runtime. Se verificó que el proceso usa el runtime incluido, pero sigue pendiente la prueba en otro equipo.

En esta fase P1 de publicación no se probaron login/logout de Keycloak, permisos, CRUD, movimientos ni responsive en navegador. La comprobación posterior de autenticación se documenta a continuación. No se realizó auditoría de vulnerabilidades de paquetes.

Entregar toda la carpeta artifacts/publish/win-x64/. API y Keycloak se ejecutan por separado. Los comandos de preparación y publicación están en el README.

## P1: autoridad y autenticación local — 30 de septiembre de 2026

La entrada `127.0.0.1 keycloak.localhost` ya existía en hosts al comenzar esta fase. Se comprobó su resolución a 127.0.0.1; no se modificaron hosts, la autoridad MVC ni la configuración de Keycloak.

Pruebas ejecutadas contra los servicios locales:

- Descubrimiento OIDC: HTTP 200. El issuer coincide con `http://keycloak.localhost:8081/realms/inventory-management`; authorization_endpoint, token_endpoint y end_session_endpoint usan la misma autoridad.
- Chrome headless con perfil aislado: petición real de MVC con Authorization Code, PKCE S256 y callback `http://localhost:5090/signin-oidc`.
- Login con el usuario de desarrollo del entorno local y procesamiento del callback: página MVC autenticada con formulario de logout.
- Consultas autenticadas de `/categories` y `/products` desde MVC: HTTP 200. No se ejecutaron escrituras sobre el catálogo.
- Flujo PKCE adicional con el mismo cliente público inventory-swagger y su callback original `http://localhost:8080/swagger/oauth2-redirect.html`: Keycloak lo acepta y el canje del código con code_verifier, sin client secret, devuelve token. Esto comprueba el callback de Swagger; no se automatizó su botón Authorize.
- El access token de ese flujo tiene el issuer esperado, audiencia inventory-api y los seis permisos categories.read/write, products.read/write e inventory.read/write. Un GET de categorías a la API con ese token devuelve 200. Tokens y contraseñas solo se usaron en memoria, sin incluirlos en informes.
- Logout mediante el formulario MVC con antiforgery: regreso a portada anónima. Un nuevo login presenta el formulario de Keycloak y exige credenciales, confirmando cierre de la sesión SSO de prueba.

La prueba inicial mediante HttpClient de PowerShell no completó login porque no reenviaba las cookies Secure de Keycloak sobre HTTP local. La prueba posterior con Chrome sí completó el flujo; no se debilitó la configuración de cookies de la aplicación.

El archivo local de realm confirma inventory-api como bearerOnly y los mappers de audiencia/permisos; no se consultó su configuración administrativa en vivo. Quedan pendientes permisos restringidos/403, expiración y renovación de tokens, CRUD, movimientos y responsive. La presencia de permisos en el token no acredita esas pruebas.

## P1: permisos independientes — 30 de septiembre de 2026

Se separaron balance/historial de la consulta opcional de producto y se retiró el GET de producto previo al POST de movimientos. Se agregó Operaciones por UUID, categoría manual al recibir 403 de categorías, edición por reemplazo y confirmación de desactivación por UUID cuando la lectura está prohibida. Las escrituras exitosas sin lectura no redirigen a detalles restringidos.

Validación ejecutada: build Release de C# y Razor con 0 errores y 0 advertencias; 24 comprobaciones de aceptación aprobadas en tests/PermissionAcceptance. Se utilizan los controladores y el cliente HTTP reales con autenticación y API simuladas. Cubren permisos individuales de escritura, inventory.read sin products.read, conflictos, formularios preservados y 403 de escritura para usuarios de solo lectura. No son pruebas contra usuarios restringidos reales de Keycloak ni pruebas de navegador.

Los casos manuales pendientes y las diferencias inevitables frente al SDD (nombre y estado activo no disponibles sin lectura) se documentan en docs/Acceptance-Independent-Permissions.md. No se modificaron roles, usuarios ni datos de la API real durante esta fase.

## P2: timeout y lectura de respuestas

InventoryApiClient utiliza un CancellationTokenSource enlazado al de la solicitud MVC, con el plazo de HttpClient.Timeout, desde el envío hasta terminar la deserialización. La adquisición/renovación del access token sigue siendo una operación separada. Tanto respuestas exitosas como errores 4xx se leen dentro de ese plazo y bajo el mismo manejo de fallos de transporte. La respuesta se libera incluso si el cuerpo falla. La cancelación solicitada por el consumidor se propaga y no se convierte en timeout.

Los 5xx se registran antes de leer el cuerpo, con estado HTTP, método y ruta. Se devuelve un mensaje genérico sin exponer el cuerpo remoto; no se leen cuerpos de 5xx ni de 204. No se agregaron reintentos automáticos.

Se añadieron 13 comprobaciones en tests/PermissionAcceptance/TransportAcceptance.cs: cuerpos bloqueados en 200/400, IOException y HttpRequestException durante lectura de éxito/error, cancelación del consumidor, presupuesto compartido entre cabeceras/cuerpo y POST sin reintento, JSON válido e inválido, conflicto 409 conservado, 500 registrado sin detalles internos y liberación de 204 sin lectura. Son pruebas ejecutadas con streams HTTP controlados, no fallos inducidos en la API real.

## P2: validación y conservación de formularios

- Los límites de nombre, SKU, descripción y motivo se evalúan sobre el valor recortado. Se retiraron los StringLength que rechazaban valores válidos antes de Trim. Los controles usan data-trim-max y validación JavaScript sobre el valor recortado, en lugar de maxlength sobre el texto original. La validación del servidor sigue funcionando sin JavaScript.
- Los UUID vacíos en las rutas de los controladores de catálogo/inventario se rechazan antes de ejecutar la acción. CategoryId vacío sigue rechazándose en ProductForm. Register también protege explícitamente el productId vacío sin consultar la API.
- La cantidad del formulario de inventario se genera con Html.TextBox y ModelState; el formulario independiente usa asp-for. Ambos usan entrada de texto con teclado numérico para que el navegador no borre valores inválidos como texto o decimales. JavaScript valida entero/rango sin modificar el valor intentado; el servidor mantiene el tipo int y Range.
- INVALID_CATEGORY_DESCRIPTION e INVALID_PRODUCT_DESCRIPTION se asocian al campo Description.
- En edición de producto, CATEGORY_NOT_FOUND conserva el formulario y marca CategoryId; PRODUCT_NOT_FOUND mantiene HTTP 404 y permite volver al listado.

Validación ejecutada: build Release de C# y Razor con 0 errores y 0 advertencias; 24 comprobaciones de permisos, 13 de transporte, 16 de formularios y 9 de JavaScript aprobadas (62 en total). Publicación win-x64 self-contained actualizada correctamente.

Las pruebas de formularios ejercitan modelos, controladores, ModelState y el filtro de UUID con API simulada. Las de JavaScript ejecutan site.js con dobles del DOM. No se realizó una prueba visual en navegador ni contra la API real para esta corrección; la compilación Razor y la conservación de ModelState no equivalen a esa prueba.

Comandos adicionales, desde frontend:

```powershell
dotnet run --project ./tests/PermissionAcceptance/PermissionAcceptance.csproj -c Release --no-restore
node ./tests/PermissionAcceptance/form-validation.cjs
```

## P2: paginación, filtros y retorno

- Se valida ModelState de las queries de categorías, productos e inventario antes de llamar a la API. Los filtros inválidos de inventario ya no se sustituyen por valores predeterminados. La vista QueryError muestra los valores intentados y HTTP 400, sin simular una lista vacía ni resultados.
- El límite de navegación solicitado es 1000 (más estricto que las 10000 páginas admitidas por el contrato original). PaginationLimits centraliza esta regla; siguiente respeta tanto ese máximo como TotalPages. El selector interno de categorías conserva el recorrido del contrato de la API.
- Los enlaces entre listado, detalle, edición e inventario transportan la URL de origen. Los retornos y redirecciones solo aceptan destinos locales. Los formularios mantienen ese contexto al guardar o cancelar.
- El historial conserva filtros, página, tamaño y retorno tras un movimiento o conflicto. Los parámetros del historial en POST usan el prefijo history para no confundirse con Type del movimiento. Cambiar filtros desde el formulario GET reinicia página a 1.
- Se añadieron 15 comprobaciones de paginación/navegación en PaginationAcceptance.cs: errores de binding sin llamadas API, límite 1000, última página real, filtros inválidos sin resultados sustitutivos, filtros tras éxito/conflicto, offset codificado y retorno local seguro. Son pruebas de controladores y tag helpers con API controlada; no pruebas completas en navegador.

Resultado final de esta fase: build Release con 0 errores y 0 advertencias; 77 comprobaciones aprobadas (24 permisos, 13 transporte, 16 formularios, 15 paginación/navegación y 9 JavaScript). Publicación win-x64 self-contained completada. Pendiente validación completa en navegador contra la API real.

## P2: contrato de respuesta

El cliente exige exactamente 200 en GET/PUT, 201 en POST y 204 en DELETE para las 13 operaciones. Cualquier otro 2xx produce INVALID_API_RESPONSE (502) antes de leer el cuerpo. Las respuestas fallidas mantienen su manejo de sesión, permisos, negocio y 5xx; no se reintentan escrituras.

Los DTO declaran campos JSON obligatorios para distinguir omisiones de valores legítimos como false o cero. ApiResponseValidator comprueba identificadores no vacíos, nombres/SKU no vacíos, precios/stock válidos, tipo/cantidad de movimientos, fechas obligatorias y estructura de páginas, incluidos elementos nulos. Las páginas vacías y las posteriores al último resultado siguen aceptándose. No se exige correspondencia exacta entre conteos e items por las lecturas concurrentes de la API; el límite del contrato sigue siendo 10000 aunque la navegación UI esté limitada a 1000.

CreatedId.Location conserva el encabezado de altas como URI opcional. No sustituye al id, no forma parte del JSON ni se sigue mediante otra llamada HTTP. Se permite URI relativa/absoluta; si falta o no puede interpretarse queda null. La navegación existente sigue construyéndose con el id validado.

Se incorporaron 42 casos en ResponseContractAcceptance.cs: las 13 operaciones con estado correcto e incorrecto, cuerpos incompletos, UUID vacío, omisiones numéricas, stock negativo, páginas inválidas/vacías/fuera de rango y Location ausente/relativo/externo sin seguimiento. Se ejecutan contra respuestas HTTP simuladas; no acreditan pruebas en navegador contra la API real.

Resultado final de contrato: build Release con 0 errores y 0 advertencias; 119 comprobaciones aprobadas (110 .NET y 9 JavaScript), incluidas las 42 nuevas de contrato. Publicación win-x64 self-contained actualizada correctamente. No se ejecutaron estas respuestas anómalas contra la API real.

## Corrección de URL del formulario de movimientos

Se corrigió el atributo action de Inventory/Movement.cshtml: Razor interpretaba la secuencia adyacente movements@Context.Request.QueryString como texto literal, provocando un POST a una ruta inexistente. Ahora toda la URL se construye mediante una expresión Razor explícita, conservando UUID y query. Se revisaron las otras vistas de inventario: el formulario de Details ya utiliza una variable para su URL. No se ejecutó un movimiento real ni se verificó el envío en navegador en esta corrección.

## Nombre informativo al registrar movimientos

El formulario independiente muestra nombre y UUID por separado. El nombre se consulta con products.read o, en su ausencia, con inventory.read. Sin lectura o ante un error de esa consulta opcional, muestra «Nombre no disponible» y mantiene el formulario. El POST sigue usando exclusivamente productId como identificador; el nombre no se toma de campos enviados por el usuario. La validación y los errores recuperables reconstruyen los datos informativos sin perder ModelState.

Se ejecutaron correctamente 148 comprobaciones .NET, incluidas cuatro nuevas sobre nombre con permisos de lectura independientes, conservación del UUID y ausencia de POST con cantidad inválida. Se conservan los casos de solo escritura sin consultas previas. No se verificó este cambio en navegador.

## Accesos integrados por UUID — 30 de septiembre de 2026

Se eliminó /workspace y el cliente dejó de llamar a los dos endpoints /lookup. Categorías y Productos conservan sus listados con permiso de lectura; sin lectura muestran un formulario de UUID y operaciones según permisos, sin consultar el catálogo. El servidor valida formato, UUID distinto de cero y operación permitida antes de construir una ruta local. Las operaciones reales mantienen la autorización de la API. El detalle de producto permite registrar movimientos con inventory.write aunque no exista inventory.read. Se actualizaron navegación, cancelaciones y retornos después de escrituras sin lectura.

Validación ejecutada: build Release sin errores ni advertencias; 144 comprobaciones .NET (110 anteriores y 34 de acceso independiente por UUID) y 9 JavaScript aprobadas. Los casos nuevos cubren las seis operaciones con permisos independientes, ausencia de llamadas de búsqueda/listado, UUID inválidos/vacíos conservando formulario, recurso alterado en la entrada, permisos insuficientes y operación desconocida. Son pruebas con API/autenticación simuladas; no se ejecutó navegador ni usuarios restringidos reales en esta fase. Las pruebas de navegador de P3 siguientes corresponden a la versión anterior, incluida la pantalla ahora retirada.

Publicación win-x64 self-contained completada y InventoryManagement.Web.exe comprobado en artifacts/publish/win-x64. El primer intento sin restore falló con NETSDK1047 por faltar el destino win-x64 en project.assets.json; se restauró correctamente desde la fuente NuGet local y se repitió publish con éxito. No se arrancó el ejecutable en esta fase.

## P3: carga, accesibilidad y documentación operativa — 30 de septiembre de 2026

El indicador incluye texto visible y una región persistente role=status, aria-live=polite y aria-atomic=true. Formularios y regiones de navegación reciben aria-busy; los formularios muestran también el mensaje junto a la operación. Se detectan cambios de query además de cambios de ruta. pageshow limpia el estado al volver desde la caché de navegación. Los enlaces internos de ancla no activan la carga.

Se ajustaron colores de texto, foco de teclado, bordes de campos y adaptación de contenido largo. Las tablas conservan roles y cabeceras accesibles al convertirse en tarjetas móviles. Se respeta prefers-reduced-motion.

Pruebas ejecutadas en Chrome headless real mediante CDP, con perfil aislado y MVC temporal en localhost:5092, usando API y Keycloak locales existentes:

- Teclado: Tab alcanza el enlace para saltar al contenido; Enter lleva el foco a main. En alta de categoría, Tab pasa de nombre a descripción y existe indicación visual de foco.
- Carga: clic de enlace que cambia únicamente la query activa texto y aria-busy; envío de formulario válido activa mensaje local y deshabilita el botón. Se interceptaron navegación/envío para inspeccionar el estado transitorio sin escribir datos. Se comprobó la presencia de status en el árbol de accesibilidad y la limpieza mediante pageshow simulado.
- Contraste de texto calculado sobre estilos del navegador: 184 elementos en portada, categorías, productos, alta de categoría, alta de producto y operaciones por UUID. Tras corregir los fallos detectados, ninguno queda por debajo de 4.5:1 para texto normal o 3:1 para texto grande en esa muestra. No es una auditoría exhaustiva de todos los estados, fondos y componentes.
- Responsive: esas seis rutas a 320, 375, 768 y 1440 píxeles (24 combinaciones), sin desbordamiento horizontal del documento. Los listados se sometieron a textos largos inyectados solo en el DOM; se inspeccionó una captura de productos a 375 píxeles. No se modificaron registros de la API.

Una comprobación adicional de detalle/inventario no terminó: agotó la espera de la página y el proceso registró errores del proveedor Windows EventLog. No se atribuye aprobación a esas vistas. Tampoco se probó un lector de pantalla real, todos los recorridos con teclado, todos los estados de error ni usuarios con permisos restringidos en navegador. Las comprobaciones anteriores son una muestra, no una certificación de accesibilidad. El inicio y cierre de sesión completos constan en la fase P1; no se repitieron completos en el puerto temporal.

Validación de regresión: build Release limpio, 0 errores y 0 advertencias; 110 comprobaciones .NET y 9 JavaScript aprobadas. Un intento previo de build falló por DLL bloqueada por el servidor temporal; se detuvo y se repitió correctamente. Publicación win-x64 self-contained completada y ejecutable generado. No se volvió a arrancar el ejecutable publicado en esta fase; las pruebas de Chrome usaron el build Release.

README ampliado con resolución de keycloak.localhost y comprobación del descubrimiento/issuer, diferencia entre AllowedHosts y direcciones de escucha, callbacks al cambiar host, arranque y diagnóstico de dependencias Docker y requisitos de NuGet offline. Los comandos de arranque se contrastaron con los archivos locales del backend; no se recrearon sus contenedores ni volúmenes. La fuente offline no acredita una instalación en un equipo limpio: deben estar disponibles también SDK y packs requeridos. No se modificó hosts ni la configuración de Keycloak.

## Suite HTTP/Razor de las 13 operaciones

409 comprobaciones aprobadas: 148 .NET anteriores, 252 HTTP/Razor nuevas y 9 JavaScript. Cubren las 13 operaciones con respuestas exitosas y 400/401/403/404/409/500, formularios renderizados, destinos, query y antiforgery. API y autenticacion simuladas; sin navegador ni escrituras reales. Detalle: docs/Frontend-API-Acceptance.md. No se modifico codigo de produccion.

