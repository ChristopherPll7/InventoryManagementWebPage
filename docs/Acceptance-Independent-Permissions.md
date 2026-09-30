# Aceptación: permisos independientes

## Comportamiento acordado en esta corrección

La API conserva la decisión final para cada GET, POST, PUT y DELETE. Los claims solo orientan enlaces y consultas complementarias; no autorizan una escritura en nombre de la API.

Los enlaces **Categorías** y **Productos** permiten crear o abrir un recurso por UUID cuando no hay permiso de lectura del catálogo, sin consultar el listado. Con lectura se selecciona el registro desde el catálogo. Productos también se muestra con permisos exclusivos de inventario. Un usuario sin lectura debe obtener los UUID por un canal autorizado; la interfaz no descubre ni inventa datos que la API le niega. No se utilizan búsquedas por SKU/nombre ni endpoints /lookup.

- Inventario requiere únicamente inventory.read para balance e historial. IsActive es desconocido si no puede consultarse el producto. Cuando el estado es conocido e inactivo, el formulario se bloquea. Cuando es desconocido, la API decide al registrar; PRODUCT_INACTIVE bloquea el formulario tras el rechazo.
- inventory.write permite abrir directamente el formulario de movimientos por UUID y enviar sin GET previo. Sin inventory.read, éxito y errores corregibles regresan al formulario, sin cargar balance ni historial. Con lectura, se mantiene la reconsulta de balance e historial.
- products.write sin categories.read permite introducir categoryId como UUID. La API comprueba existencia y categoría activa. Un 403 de consulta de categorías no destruye el formulario.
- categories.write o products.write sin lectura permiten editar por UUID con un formulario de reemplazo completo. No se presentan campos vacíos como datos actuales: se advierte que todos los valores editables, incluida la descripción, se reemplazan al guardar.
- La confirmación de desactivación usa nombre cuando puede leerse y UUID explícito cuando el GET devuelve 403. No se inventa un nombre. Cancelar no ejecuta DELETE.
- Tras una escritura exitosa sin permiso de lectura, se muestra confirmación e identificador en Operaciones; no se redirige a un detalle prohibido.

Estas alternativas explicitan dos límites del SDD original: no es posible confirmar un nombre que no se puede leer ni conocer IsActive a partir del contrato de balance. No se amplían permisos ni se modifica la API para evitarlos.

## Pruebas automatizadas ejecutables

Desde frontend:

```powershell
dotnet restore ./tests/PermissionAcceptance/PermissionAcceptance.csproj --source ./artifacts/nuget-source --packages ./artifacts/nuget-offline-cache -p:NuGetAudit=false
dotnet run --project ./tests/PermissionAcceptance/PermissionAcceptance.csproj -c Release --no-restore
```

El ejecutable de aceptación usa los controladores y el cliente HTTP reales, con autenticación y respuestas de API simuladas. No añade mecanismos de autenticación de prueba a la aplicación. Un fallo devuelve un código de salida no cero. No sustituye pruebas en navegador con usuarios Keycloak restringidos ni verifica el middleware antiforgery.

| Permisos del usuario simulado | Caso | Resultado esperado |
| --- | --- | --- |
| inventory.read | Abrir inventario | Solo GET de balance e historial; no consulta productos. |
| inventory.write | Abrir formulario y registrar | Sin GET previo; POST y retorno al formulario. |
| inventory.write | INSUFFICIENT_STOCK | Conserva datos y error, sin leer balance prohibido. |
| inventory.write | PRODUCT_INACTIVE | Conserva error y bloquea el formulario. |
| inventory.read + inventory.write | Conflicto de stock | Reconsulta balance/historial sin products.read. |
| inventory.read + products.read | Producto inactivo conocido | Balance e historial disponibles; estado inactivo conservado. |
| products.write | Crear sin categories.read | Categoría manual por UUID; POST y confirmación sin GET posterior. |
| products.write | Editar/desactivar sin products.read | Tolera GET 403; reemplazo completo y confirmación por UUID. |
| products.write | Formulario inválido sin categories.read | Datos y validaciones conservados. |
| categories.write | Crear/editar/desactivar | Independiente de categories.read; confirmación sin detalle posterior. |
| Cada permiso .read por separado | Envío de escritura no permitido | API devuelve 403; UI no lo convierte en éxito. |

## Verificación manual pendiente contra Keycloak/API reales

Crear usuarios de prueba con cada combinación de la tabla, sin heredar los otros permisos. Con UUID de fixtures autorizados, verificar navegación desde Categorías/Productos, formularios, cancelación, validaciones y confirmaciones. Comprobar 403 de escrituras forzadas para cada usuario de solo lectura, permisos revocados, y repetición con los seis permisos para conservar el flujo habitual. Verificar también products.read + inventory.write sin inventory.read: seleccionar producto y abrir Registrar movimiento desde su detalle.

Para productos inactivos, comprobar tanto estado conocido con products.read como PRODUCT_INACTIVE sin ese permiso. No considerar presencia de roles en el token equivalente a estas pruebas.
