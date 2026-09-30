# Especificación SDD de la interfaz MVC para Inventory Management

**Proyecto:** interfaz web de la prueba técnica Inventory Management  
**Versión:** 1.2  
**Fecha:** 29 de septiembre de 2026  
**Estado:** especificación previa a la implementación  
**Contrato de referencia:** `guia-api-para-sdd.md`, aportado para esta fase

## 1. Propósito

Este documento define el comportamiento esperado de una interfaz web desarrollada con ASP.NET Core MVC y vistas `.cshtml` que consume la API de Inventory Management. Sirve como contrato de trabajo antes de escribir la interfaz: establece qué podrá hacer una persona, qué endpoint atenderá cada acción, cómo se mostrarán los resultados y errores, y cuándo se considerará terminada la entrega.

La URL base de la API en el entorno local será `http://localhost:8080/`. La interfaz será un proyecto independiente y se entregará también como ejecutable de Windows publicado en modo *self contained* para `win-x64`.

## 2. Alcance y decisiones del producto

- La interfaz tendrá tres secciones principales: **Categorías**, **Productos** e **Inventario**.
- La aplicación MVC renderizará las vistas `.cshtml` y consumirá la API desde el servidor mediante `HttpClient` y operaciones asincrónicas.
- La API será la fuente de verdad. La interfaz no accederá directamente a la base de datos ni modificará existencias sin llamar a los endpoints indicados.
- Se implementarán las operaciones cubiertas por los 13 endpoints enumerados en la sección 4.
- El diseño visual seguirá una dirección **Mid century modern**, con una presentación clara y usable en escritorio y móvil.
- La guía adjunta define el contrato HTTP, los permisos y las validaciones de esta versión. Los ejemplos JSON de la guía son ilustrativos; la interfaz no dependerá de los UUID ni de los registros de ejemplo.

## 3. Estructura de navegación

| Pantalla | Propósito | Acciones visibles |
| --- | --- | --- |
| Inicio | Orientar al usuario y dar acceso a las tres secciones | Ir a categorías, productos o inventario |
| Categorías | Consultar y administrar categorías | Listar con paginación, ver detalle, crear, editar y desactivar |
| Productos | Consultar y administrar productos | Listar con paginación, ver detalle, crear, editar y desactivar |
| Inventario de producto | Consultar existencias e historial | Ver existencia actual, filtrar y paginar movimientos, registrar entrada o salida |

Las listas mostrarán activos e inactivos con una etiqueta de estado. La acción visible será **Desactivar**, pues DELETE realiza una baja lógica. No habrá acción de reactivación. Se pedirá confirmación con el nombre del registro; los registros desactivados seguirán consultables. Un listado sin datos mostrará un estado vacío y una acción útil.

## 4. Matriz de trazabilidad de endpoints

Todas las rutas de API de esta tabla se resolverán respecto de `http://localhost:8080/`.

| Método y endpoint de API | Acción en la interfaz | Resultado esperado en la pantalla |
| --- | --- | --- |
| `GET /api/categories?page={page}&pageSize={pageSize}` | Abrir y paginar categorías | Mostrar `items`, totales y navegación; incluye inactivas |
| `GET /api/categories/{id}` | Abrir detalle o edición de categoría | Mostrar los datos de la categoría seleccionada |
| `POST /api/categories` | Enviar formulario de alta | Leer `id` del 201 y consultar o abrir el nuevo detalle |
| `PUT /api/categories/{id}` | Guardar cambios | Leer el `CategoryDto` del 200 y mostrar datos actualizados |
| `DELETE /api/categories/{id}` | Confirmar desactivación | Interpretar 204 sin cuerpo y actualizar estado |
| `GET /api/products?page={page}&pageSize={pageSize}` | Abrir y paginar productos | Mostrar `items`, totales, stock y estados |
| `GET /api/products/{id}` | Abrir detalle o edición de producto | Mostrar los datos del producto seleccionado |
| `POST /api/products` | Enviar formulario de alta | Leer `id` del 201 y consultar o abrir el nuevo detalle |
| `PUT /api/products/{id}` | Guardar cambios | Leer el `ProductDto` del 200 y mostrar datos actualizados |
| `DELETE /api/products/{id}` | Confirmar desactivación | Interpretar 204 sin cuerpo y actualizar estado |
| `GET /api/inventory/products/{productId}` | Abrir inventario de un producto | Mostrar la existencia actual informada por la API |
| `GET /api/inventory/products/{productId}/movements` con parámetros opcionales | Consultar historial | Mostrar movimientos filtrados, `totalCount` y paginación |
| `POST /api/inventory/movements` | Registrar entrada o salida | Leer `movementId` y `currentStock` del 201; volver a consultar balance e historial |

La interfaz no ofrecerá editar ni eliminar movimientos: el requerimiento solo proporciona un endpoint para registrarlos y endpoints para consultarlos.

## 5. Flujos funcionales

### 5.1 Categorías

1. El listado solicita `GET /api/categories?page=1&pageSize=20` por defecto y consume `items`, `page`, `pageSize`, `totalCount` y `totalPages`. Presenta nombre, descripción y estado. Usa anterior/siguiente sin exceder `totalPages`; conserva la página al regresar de un detalle cuando sea posible.
2. Cada registro permite abrir detalle mediante `GET /api/categories/{id}`; muestra fechas de creación y modificación cuando existan.
3. Crear exige `name` y permite `description`. El 201 devuelve `{ "id": "..." }`, no la categoría completa; con ese ID se abre el detalle o se hace una nueva consulta.
4. Editar carga el detalle y envía `name` y `description` en `PUT`. La descripción omitida se convierte en null, por lo que el formulario debe enviar conscientemente el valor que se desea conservar o vaciar. El 200 devuelve el DTO actualizado. Editar una categoría inactiva no la reactiva.
5. Desactivar pide confirmación antes de DELETE; tras el 204 sin cuerpo vuelve a consultar el registro o listado. Si hay productos activos vinculados, muestra `CATEGORY_IN_USE` y conserva la categoría activa. Repetir la desactivación puede devolver 204.
6. Los nombres de categorías inactivas continúan reservados: `DUPLICATE_CATEGORY_NAME` se presenta como conflicto y no como oportunidad de reutilizar el nombre.

### 5.2 Productos

1. El listado usa `GET /api/products?page=1&pageSize=20` y representa `PagedResult<ProductDto>`: nombre, SKU, precio, categoría, `currentStock` y `isActive`. No enviará filtros de categoría, SKU, estado u orden que la API no implementa; el orden de la API es nombre e ID ascendentes.
2. El detalle usa `GET /api/products/{id}` y ofrece acceso al inventario del producto. `ProductDto` no incluye fechas de creación o modificación; la UI no las inventa.
3. Crear y editar solicitan `name`, `description`, `sku`, `price` y `categoryId`. El selector usa `GET /api/categories` paginado, recorre las páginas necesarias y ofrece solo categorías activas para una nueva asignación; no limita las opciones a las primeras 20. Si la categoría actual de una edición ya está inactiva, la vista muestra su valor y explica que la API exige una categoría activa para guardar.
4. El 201 de creación contiene solo `{ "id": "..." }`; el 200 de edición contiene `ProductDto`. El SKU devuelto puede estar normalizado a mayúsculas. PUT envía todos los campos editables, aunque solo se cambie uno; omitir `description` la convierte en null.
5. `id`, `isActive` y `currentStock` son de lectura. Para cambiar stock se usa un movimiento, nunca PUT de producto.
6. Desactivar requiere confirmación; el 204 no tiene cuerpo. El producto permanece visible, conserva stock e historial y ya no admite movimientos. El SKU sigue reservado y no se ofrece reactivación.

### 5.3 Inventario

1. Desde el detalle se consulta `GET /api/inventory/products/{productId}` y se muestran `sku`, `name` y `currentStock`, incluso para un producto inactivo.
2. El historial muestra `type`, `quantity`, `reason` y `createdAt`, con la respuesta paginada de `GET /api/inventory/products/{productId}/movements`. Se permiten filtros opcionales de tipo (`Entry` o `Exit`), fecha inicial y final inclusivas, página y tamaño. El orden fijo es fecha de creación descendente e ID descendente. Cambiar filtros reinicia a la primera página; se mantienen al navegar páginas.
3. Las fechas se enviarán como instantes ISO 8601 con `Z` u offset. La interfaz no debe tratar una fecha sola como día completo sin construir explícitamente el intervalo; los parámetros se codifican al construir la URL.
4. El formulario muestra el producto, tipo de movimiento, cantidad entera y motivo opcional; envía `productId`, `type` como cadena `Entry` o `Exit`, `quantity` y `reason`. Se deshabilita para productos inactivos, cuyo balance e historial siguen consultables.
5. La existencia visible es informativa; la API resuelve concurrencia y stock al registrar. En un 201 se lee `movementId` y `currentStock`, y se consultan nuevamente el balance y el historial. En 409 `INSUFFICIENT_STOCK` o `INVENTORY_STOCK_OVERFLOW` no se altera la cifra local como si se hubiera aplicado el movimiento.
6. Un historial sin movimientos muestra estado vacío con `totalCount=0` y `totalPages=0`. Una página fuera del conjunto puede contener `items=[]` con un `totalCount` mayor que cero; se ofrece volver a una página válida sin confundirlo con ausencia de movimientos.

## 6. Contrato de interacción de la aplicación MVC

Las siguientes rutas son propuestas para la **interfaz**, no son endpoints adicionales de la API. Pueden variar sus nombres internos sin alterar el comportamiento especificado.

| Ruta MVC propuesta | Vista o acción |
| --- | --- |
| `/` | Inicio |
| `/categories` | Listado |
| `/categories/create` | Formulario de creación |
| `/categories/{id}` | Detalle |
| `/categories/{id}/edit` | Formulario de edición |
| `/products` | Listado |
| `/products/create` | Formulario de creación |
| `/products/{id}` | Detalle |
| `/products/{id}/edit` | Formulario de edición |
| `/inventory/products/{productId}` | Existencia, historial y formulario de movimiento |

Los formularios HTML enviarán sus datos al proyecto MVC. Las acciones MVC llamarán a la API y, al completar una escritura exitosa, redirigirán a una vista GET para evitar reenvíos accidentales al actualizar el navegador. Las operaciones de escritura tendrán protección antifalsificación de solicitudes. La vista nunca incluirá secretos de conexión o tokens en el HTML.

## 7. Integración técnica

| Aspecto | Comportamiento especificado |
| --- | --- |
| Cliente HTTP | Registrar un cliente tipado mediante `IHttpClientFactory`, con métodos específicos para categorías, productos e inventario. La API se invoca desde el servidor MVC, no directamente desde JavaScript en el navegador. |
| Configuración | Valor local inicial `http://localhost:8080/`, modificable mediante configuración de la aplicación o variable de entorno. |
| Asincronía | Todas las llamadas HTTP de controladores y servicios utilizarán `async`/`await` y propagarán cancelación cuando aplique. |
| Modelos | Separar modelos de formulario, solicitudes y respuestas; representar `Guid`/UUID, `decimal` para precio y `DateTimeOffset` para fechas. Los DTO de respuesta están en la sección 7.1. |
| Serialización | Enviar `application/json` con propiedades `camelCase`; los tipos de movimiento son las cadenas `Entry` y `Exit`, nunca valores numéricos. No esperar envolturas `Result<T>`. |
| Resultado HTTP | Distinguir 201 `{id}` de creación de catálogo, 201 `{movementId,currentStock}` de inventario, 200 con DTO o página y 204 sin cuerpo. Leer `Location` de altas de catálogo solo como dato adicional. |
| Errores | Leer `{code,message}` cuando exista; distinguir fallo de transporte de respuesta HTTP. Mostrar mensajes útiles sin exponer detalles internos. |
| Autenticación | MVC reutilizará el cliente público `inventory-swagger` de Keycloak mediante Authorization Code con PKCE `S256`. Mantendrá sesión local mediante cookie protegida y enviará el access token como Bearer en cada llamada a la API. `inventory-api` seguirá siendo el recurso protegido `bearerOnly`. |
| Disponibilidad | Si la API no responde o se agota el tiempo de espera, mostrar un estado recuperable con opción de reintento. |

### 7.1 Mapeo de datos de respuesta

| Modelo de API | Campos consumidos por la interfaz |
| --- | --- |
| `CreatedId` | `id: Guid`; tras POST de categoría o producto. |
| `CategoryDto` | `id`, `name`, `description`, `isActive`, `createdAt`, `updatedAt`. |
| `ProductDto` | `id`, `name`, `description`, `sku`, `price`, `categoryId`, `isActive`, `currentStock`. |
| `ProductInventoryDto` | `productId`, `sku`, `name`, `currentStock`. |
| `InventoryMovementDto` | `id`, `productId`, `type`, `quantity`, `reason`, `createdAt`. |
| `InventoryMovementResult` | `movementId`, `currentStock`; tras POST de movimiento. |
| `PagedResult<T>` | `items`, `page`, `pageSize`, `totalCount`, `totalPages`; `T` es categoría, producto o movimiento. |
| `ApiError` | `code`, `message`; sin `isSuccess`, `value` ni `error`. |

Las listas de catálogo y el historial usan `page=1&pageSize=20` por defecto. Los límites admitidos son página **1 a 10000** y tamaño **1 a 100**. Las categorías y productos se ordenan por nombre ascendente, con ID como desempate; la UI no promete búsqueda ni filtros no soportados. El historial se ordena de más reciente a más antiguo. La paginación por desplazamiento no representa necesariamente una instantánea si otros usuarios agregan datos entre páginas.

### 7.2 Cliente Keycloak, sesión y permisos

- Se reutilizará `inventory-swagger`, que en la configuración local es un cliente **público** con Authorization Code y PKCE `S256`. Se agregará `http://localhost:5090/signin-oidc` a sus `redirectUris` permitidas, conservando la redirección de Swagger. Para cierre de sesión se configurará la URL de retorno `http://localhost:5090/signout-callback-oidc` según las opciones de Keycloak. No se creará un cliente adicional en esta fase.
- `inventory-api` tiene `bearerOnly: true`: es la audiencia de los tokens dirigidos a la API, no el cliente que realiza el inicio de sesión web. Los mappers existentes de `inventory-swagger` agregan la audiencia `inventory-api` y los roles del usuario en el claim `permissions` del **access token**. El flujo de Swagger seguirá disponible.
- El usuario de desarrollo creado para inventario puede iniciar sesión en la página de Keycloak con las credenciales definidas mediante `INVENTORY_DEV_USERNAME` e `INVENTORY_DEV_PASSWORD`. Esas variables no se incorporarán a MVC ni se usarán para autenticar automáticamente. El administrador de Keycloak y el usuario de inventario son identidades distintas.
- MVC guardará de forma protegida su sesión local y los tokens que necesite. El navegador recibirá una cookie de sesión; las vistas y el JavaScript no recibirán el access token. Al llamar a la API, MVC enviará **el access token**, nunca el ID token. No se requiere `client secret` porque se reutiliza un cliente público con PKCE.
- La sesión web tendrá una duración propuesta de 30 minutos de inactividad, renovable con actividad. La vigencia del access token se comprobará por separado: si hay renovación disponible se hará en el servidor; en caso contrario se solicitará un nuevo inicio de sesión. Al salir se limpiará la sesión local y se completará el cierre de sesión de Keycloak.
- Cada GET de categorías, productos o inventario requiere respectivamente `categories.read`, `products.read` o `inventory.read`. POST, PUT y DELETE de catálogo requieren su permiso `.write`; el POST de movimientos requiere `inventory.write`. Los permisos de lectura y escritura son independientes.
- La UI podrá ocultar o deshabilitar acciones según permisos conocidos, pero tratará el **403 de la API** como decisión definitiva. Si el token falta o expiró y la API devuelve **401**, dirigirá al flujo de inicio de sesión y conservará una ruta de retorno segura. No repetirá automáticamente un POST/PUT/DELETE cuya ejecución sea incierta.
- El README indicará las redirecciones que deben agregarse al cliente existente, sin quitar las de Swagger. Cambiar el puerto o el host local exige actualizar esas URL permitidas en Keycloak.

### 7.3 Estados HTTP y errores de negocio

| Estado o código | Respuesta de la UI |
| --- | --- |
| `400 INVALID_REQUEST` y códigos `INVALID_*` de campos | Conservar formulario, asociar al campo cuando sea posible y mostrar el mensaje general. |
| `400 INVALID_PAGINATION` / `INVALID_DATE_RANGE` | Mantener filtros visibles y señalar el rango o la paginación que debe corregirse. |
| `401 UNAUTHORIZED` | Solicitar inicio de sesión o renovación controlada de sesión. |
| `403 FORBIDDEN` | Indicar falta de permiso; no insistir automáticamente. |
| `404 CATEGORY_NOT_FOUND` / `PRODUCT_NOT_FOUND` | Mostrar registro no disponible y volver al listado apropiado. |
| `409 DUPLICATE_CATEGORY_NAME` / `DUPLICATE_PRODUCT_SKU` | Marcar nombre o SKU y permitir corrección. |
| `409 CATEGORY_INACTIVE` / `CATEGORY_IN_USE` | Explicar que la categoría no admite asignación o desactivación en su estado actual. |
| `409 PRODUCT_INACTIVE` | Bloquear nuevos movimientos y permitir consultar stock e historial. |
| `409 INSUFFICIENT_STOCK` / `INVENTORY_STOCK_OVERFLOW` | Mostrar conflicto y consultar stock de nuevo antes de otro intento. |
| `500 INTERNAL_ERROR` o fallo de red | Mostrar un mensaje recuperable y registrar detalle técnico en el servidor, sin exponerlo en pantalla. |

El texto de la API puede estar en inglés. La interfaz mostrará etiquetas y explicaciones en español a partir del `code` estable; podrá incluir el `message` de la API como detalle legible. No se asumirá que una solicitud con varias reglas incumplidas devuelve todos los errores a la vez.

## 8. Validación y estados de interfaz

### 8.1 Validación de formularios

- Mostrar etiquetas, campos obligatorios y mensajes de validación junto al control correspondiente.
- Categoría: `name` obligatorio, recortado y de hasta 150 caracteres; `description` opcional, hasta 500. El nombre debe ser único, con decisión final de la API.
- Producto: `name` obligatorio hasta 150; `description` opcional hasta 1000; `sku` obligatorio hasta 80 y único; `price` decimal de 0 a 9999999999999999.99, representable sin redondear a más de dos decimales; `categoryId` UUID no vacío de una categoría activa. Usar entrada decimal que no convierta el precio a `double`.
- Movimiento: `productId` UUID no vacío, `type` exactamente `Entry` o `Exit`, `quantity` entero entre 1 y 2147483647 y `reason` opcional hasta 500 caracteres. Una salida mayor que el stock visible puede señalarse de inmediato, pero la API toma la decisión definitiva bajo concurrencia.
- Historial: `page` de 1 a 10000, `pageSize` de 1 a 100, `type` opcional `Entry`/`Exit`, fechas ISO 8601 con offset o `Z` y `endDate >= startDate` cuando se proveen ambas.
- Los límites de longitud se aplican después de recortar espacios externos, como indica la guía. `description` y `reason` solo con espacios pueden convertirse en cadena vacía; campos opcionales omitidos se representan como null. Los formularios de edición deben preservar o vaciar explícitamente estos valores.
- Conservar la información introducida cuando la API rechace un envío corregible.
- Aplicar la misma regla en el servidor MVC; la validación del navegador es apoyo visual y no sustituye la validación de la API.
- Evitar envíos duplicados mientras una acción está en curso mediante un estado de envío visible. La API sigue siendo responsable de sus reglas de concurrencia y consistencia.

### 8.2 Estados observables

| Estado | Respuesta de la interfaz |
| --- | --- |
| Carga inicial | Indicador y texto de carga dentro de la región solicitada; mantener orientación y navegación visibles. |
| Lista vacía | Mensaje claro; en catálogos, acción de crear cuando se tenga permiso. En historial, explicar que no hay movimientos para los filtros actuales. |
| Envío en curso | Deshabilitar temporalmente el botón enviado e indicar que se está procesando. |
| Éxito | Aviso breve con el resultado real; mostrar datos actualizados tras la redirección o nueva consulta. |
| Error de validación | Marcar campos afectados y conservar entradas corregibles. |
| Recurso inexistente | Explicar que el elemento ya no está disponible y ofrecer regreso al listado. |
| Conflicto | Mostrar el mensaje de negocio recibido o una explicación clara y permitir corrección o reintento apropiado. |
| Sin autorización | Distinguir 401 por sesión y 403 por permiso; no confundirlos con lista vacía. |
| API no disponible | Mostrar error de conexión y acción de reintento sin simular datos. |

La mejora visual de carga en la navegación y los envíos podrá utilizar JavaScript ligero; las operaciones principales deberán conservar una ruta funcional mediante formularios y renderizado MVC.

## 9. Dirección visual Mid century modern

La interfaz combinará formas geométricas simples, superficies cálidas, tipografía expresiva para títulos y controles sobrios. La estética estará al servicio de la lectura de tablas, formularios y existencias.

| Elemento | Decisión visual propuesta |
| --- | --- |
| Fondo | Marfil cálido `#F4F0E8` |
| Texto principal | Verde carbón `#253B35` |
| Superficies | Crema claro `#FFFCF6` |
| Acción principal | Terracota `#B85C38` |
| Acentos | Mostaza `#D3A143` y verde oliva `#718267` |
| Títulos | Tipografía serif con personalidad, con alternativa local legible |
| Cuerpo y controles | Sans serif clara, con tamaños cómodos y cifras fáciles de comparar |
| Componentes | Tarjetas con bordes discretos, esquinas suaves, etiquetas consistentes y divisores ligeros |

Las formas circulares o arcos se usarán como acentos decorativos discretos. Se mantendrá contraste suficiente, foco visible de teclado, etiquetas asociadas a controles y mensajes que no dependan exclusivamente del color. En móvil, las tablas podrán transformarse en filas apiladas sin perder acciones ni datos principales. No se dependerá de fuentes o recursos remotos para que el ejecutable conserve su presentación en un equipo sin acceso a internet.

## 10. Entrega y ejecución en Windows

MVC escuchará por defecto solo en `http://localhost:5090`, separado de la API en `http://localhost:8080/`. El puerto podrá sobrescribirse con `ASPNETCORE_URLS`; cambiar el host o puerto requiere ajustar las URL de retorno permitidas para `inventory-swagger` en Keycloak. La configuración no se fijará dentro de las vistas.

La solución independiente del frontend tendrá esta estructura:

```text
frontend/
  InventoryManagement.Web.sln
  src/
    InventoryManagement.Web/
      Controllers/
      Models/
      Services/
      Views/
      wwwroot/
  README.md
  artifacts/
    publish/
      win-x64/
```

Desde `frontend/`, el README incluirá el comando concreto de publicación:

```bash
dotnet publish ./src/InventoryManagement.Web/InventoryManagement.Web.csproj -c Release -r win-x64 --self-contained true -o ./artifacts/publish/win-x64
```

Se entregará **toda la carpeta `artifacts/publish/win-x64/`**, que contendrá `InventoryManagement.Web.exe` y los archivos necesarios; no se asumirá que basta copiar únicamente el `.exe`. La publicación *self contained* permitirá iniciar MVC en Windows x64 sin instalar el runtime ni el SDK de .NET. La API, Keycloak y sus dependencias deberán estar disponibles por separado. Esta publicación solo corresponde al frontend.

`appsettings.json` incluirá los valores locales no secretos: URL base de la API `http://localhost:8080/`, autoridad local de Keycloak y `ClientId=inventory-swagger`. Las variables de entorno permitirán sobrescribir `ASPNETCORE_URLS`, la URL de la API y la autoridad según el equipo. **No habrá `client secret`** para esta configuración pública con PKCE. El README describirá cómo iniciar las dependencias de la API, preparar las URL de retorno en Keycloak, ejecutar el `.exe` y abrir `http://localhost:5090`.

## 11. Criterios de aceptación

### CA 01 Navegación y consulta

- Desde Inicio se puede acceder a Categorías, Productos e Inventario sin escribir rutas manualmente.
- Las listas y detalles reflejan las respuestas de los endpoints GET correspondientes.
- Las listas manejan `PagedResult<T>`, permiten cambiar de página y muestran inactivos; el selector de categorías activas puede incorporar más de una página.
- Cada pantalla presenta estados de carga, vacío y error adecuados.

### CA 02 Categorías y productos

- Se puede crear, consultar, actualizar y desactivar un registro mediante los endpoints indicados para cada recurso.
- Una creación obtiene el `id` del 201; una actualización obtiene el DTO del 200; una desactivación acepta 204 sin deserializar cuerpo.
- Una respuesta fallida no se presenta como éxito; el usuario puede corregir el formulario cuando corresponda.
- Una desactivación requiere confirmación; los registros inactivos permanecen visibles y no se presenta reactivación.
- `CATEGORY_IN_USE` impide desactivar una categoría con productos activos y `DUPLICATE_PRODUCT_SKU` no libera el SKU de un producto inactivo.

### CA 03 Inventario

- Desde un producto se pueden consultar su existencia y su historial.
- Se puede registrar un movimiento mediante el endpoint POST proporcionado.
- El formulario envía `Entry` o `Exit` como cadena y nunca acepta cantidades no enteras o menores que 1.
- Después de registrar un movimiento exitoso, se lee `movementId` y `currentStock` y se vuelven a consultar existencia e historial.
- Si el movimiento falla, la interfaz no cambia la existencia mostrada como si se hubiera aplicado.
- El historial aplica `type`, `startDate`, `endDate`, `page` y `pageSize`; maneja `totalPages=0`, una página vacía fuera de rango y el orden descendente de la API.

### CA 04 Integración y calidad de UI

- Las llamadas a la API se realizan mediante `HttpClient` y `async`/`await` desde el proyecto MVC.
- La URL base es configurable y parte de `http://localhost:8080/` para desarrollo local.
- Los formularios aplican los límites de la sección 8.1 y muestran errores junto a los campos; el precio se mapea como decimal sin redondeo implícito.
- El acceso se realiza con Keycloak mediante Authorization Code con PKCE y el MVC adjunta Bearer tokens; se distinguen respuestas 401 y 403 y permisos de lectura y escritura.
- El inicio de sesión usa `inventory-swagger` con el usuario de inventario, sin reutilizar sus credenciales dentro de MVC; `inventory-api` permanece como audiencia y recurso protegido.
- La interfaz es usable con teclado y en anchos de escritorio y móvil; el estilo Mid century modern se mantiene en todas las vistas.

### CA 05 Publicación

- El comando de publicación *self contained* para `win-x64` figura en el README.
- El conjunto de archivos publicados puede iniciar la interfaz en Windows x64 sin SDK de .NET instalado.
- MVC escucha por defecto en `http://localhost:5090`; se documenta la redirección de Keycloak y la posibilidad de cambiar puerto y URL de la API mediante configuración.
- El README distingue la ejecución de la interfaz de la disponibilidad de la API y Keycloak locales.

## 12. Verificación prevista

| Escenario | Comprobación |
| --- | --- |
| API disponible con datos | Navegar por listas y detalles; comprobar el mapeo visible de las respuestas. |
| API disponible sin datos | Verificar estados vacíos y navegación hacia creación. |
| Paginación de listas | Verificar segunda página, último límite, `totalCount` y categorías activas más allá de la primera página. |
| Altas y ediciones válidas e inválidas | Confirmar `{id}` en 201, DTO en 200, campos preservados tras error, descripción omitida y precio no redondeado. |
| Desactivación confirmada y cancelada | Comprobar que cancelar no llama a DELETE; 204 mantiene registro visible e inactivo; `CATEGORY_IN_USE` conserva estado. |
| Movimiento aceptado y rechazado | Comparar existencia e historial antes y después de `Entry` y `Exit`; verificar `INSUFFICIENT_STOCK` sin éxito falso. |
| Historial filtrado | Probar fechas inclusivas con offset, tipos, cambio de página, `totalPages=0` y una página posterior al último resultado. |
| Acceso | Verificar inicio de sesión de MVC con el cliente público existente, continuidad de Swagger, audiencia y permisos del access token, 401 por token ausente/expirado y 403 por permiso insuficiente. |
| API detenida | Comprobar error recuperable y posterior reintento. |
| Windows x64 sin SDK | Iniciar `InventoryManagement.Web.exe` desde la carpeta publicada y comprobar apertura en `http://localhost:5090` con la API y Keycloak disponibles. |

## 13. Configuración acordada para la implementación

La guía adjunta resuelve los campos, DTO, paginación, semántica de DELETE, códigos de error y permisos. Para la interfaz MVC se han tomado estas decisiones:

1. **Keycloak:** reutilizar `inventory-swagger` como cliente público con Authorization Code y PKCE `S256`; agregar las URL de retorno del MVC sin eliminar las de Swagger. El usuario de inventario iniciará sesión en Keycloak. `inventory-api` seguirá siendo la audiencia del JWT y el recurso `bearerOnly`.
2. **Ejecución local:** servir MVC en `http://localhost:5090`; API en `http://localhost:8080/`. Mantener valores no secretos en `appsettings.json` y permitir sobrescritura mediante variables de entorno, incluido `ASPNETCORE_URLS`. El cliente público no usa secreto.
3. **Entrega:** solución `frontend/InventoryManagement.Web.sln`, proyecto `frontend/src/InventoryManagement.Web/` y publicación de toda la carpeta `frontend/artifacts/publish/win-x64/` mediante el comando de la sección 10.

Estas decisiones no cambian los 13 endpoints de la API. El README deberá explicar la configuración necesaria en el cliente Keycloak existente y los pasos de ejecución local.
