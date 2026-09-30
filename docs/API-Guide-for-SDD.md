# Guía de contratos y uso de la API para el SDD

Fecha de revisión: 2026-09-29.

## 1. Propósito y alcance

Referencia de los 13 endpoints implementados de Categories, Products e Inventory, preparada para incorporarse a un Software Design Document (SDD). Describe el contrato HTTP observado en controladores, DTO, validaciones y consultas del repositorio.

**Example Value** representa un JSON ilustrativo. **Schema** define campos, tipos, obligatoriedad y reglas en las tablas de la sección 6. Los nombres de esquemas enlazados en cada operación evitan repetir las mismas definiciones. Estas tablas describen el contrato funcional; no son una exportación literal del documento OpenAPI de Swagger.

Los UUID y fechas son ficticios. Al ejecutar solicitudes, sustituir los identificadores por los devueltos por la API. Los ejemplos de respuestas no prueban que esos registros existan. Este documento describe el estado actual; el spec previo sigue siendo [001-inventory-domain-spec.md](specs/001-inventory-domain-spec.md).

## 2. Acceso y convenciones

- URL base local: `http://localhost:8080`.
- Swagger local: `http://localhost:8080/swagger`.
- Autenticación: OAuth2 Authorization Code con PKCE mediante Keycloak.
- En Swagger, seleccionar **Authorize** e iniciar sesión. El usuario de desarrollo `adminInventory` obtiene los seis permisos; las credenciales se consultan en el entorno local y no se incluyen aquí.
- En Postman: configurar **Authorization → Bearer Token** con un access token válido, seleccionar método y URL; para POST/PUT usar **Body → raw → JSON**.
- Todos los endpoints requieren `Authorization: Bearer <access_token>`. Estar autenticado no sustituye el permiso requerido.
- En solicitudes con cuerpo usar `Content-Type: application/json`. Las respuestas con cuerpo son JSON; un 204 no tiene cuerpo.
- JSON usa nombres `camelCase`. Los tipos de movimiento se serializan como `"Entry"` y `"Exit"`, no números.
- GET y DELETE no reciben cuerpo. `id` y `productId` de ruta son UUID. Un identificador con formato que no coincide con la ruta `:guid` no selecciona el endpoint.
- `Result<T>` es interno: la API devuelve el DTO o `{code,message}`, no un objeto con `isSuccess`, `value` o `error`.

### Permisos y operaciones

| Método | Ruta | Permiso | Éxito |
| --- | --- | --- | --- |
| POST | /api/categories | categories.write | 201 |
| GET | /api/categories | categories.read | 200 |
| GET | /api/categories/{id} | categories.read | 200 |
| PUT | /api/categories/{id} | categories.write | 200 |
| DELETE | /api/categories/{id} | categories.write | 204 |
| POST | /api/products | products.write | 201 |
| GET | /api/products | products.read | 200 |
| GET | /api/products/{id} | products.read | 200 |
| PUT | /api/products/{id} | products.write | 200 |
| DELETE | /api/products/{id} | products.write | 204 |
| POST | /api/inventory/movements | inventory.write | 201 |
| GET | /api/inventory/products/{productId} | inventory.read | 200 |
| GET | /api/inventory/products/{productId}/movements | inventory.read | 200 |

## 3. Categories

### 3.1 Crear categoría

`POST /api/categories`

Crea una categoría activa. El nombre debe ser único. Devuelve únicamente el identificador y un encabezado `Location` que apunta a `/api/categories/{id}`.

**Schema de solicitud:** [CategoryRequest](#categoryrequest).

**Example Value — solicitud**

```json
{
  "name": "Herramientas",
  "description": "Herramientas manuales"
}
```

**Respuesta:** `201 Created`. **Schema:** [CreatedId](#createdid).

**Example Value — respuesta**

```json
{
  "id": "11111111-1111-4111-8111-111111111111"
}
```

**Errores relevantes:** 400 por campos inválidos; 409 `DUPLICATE_CATEGORY_NAME`. También aplican los errores comunes de la sección 7.

### 3.2 Listar categorías

`GET /api/categories?page=1&pageSize=20`

Incluye categorías activas e inactivas. Orden: `name ASC`, desempate `id ASC`. No hay filtro por estado ni búsqueda por nombre implementados.

**Schema de solicitud:** parámetros [Pagination](#pagination).

**Example Value — solicitud:** usar la URL indicada, sin cuerpo.

**Respuesta:** `200 OK`. **Schema:** [PagedResult<CategoryDto>](#pagedresult).

**Example Value — respuesta**

```json
{
  "items": [
    {
      "id": "11111111-1111-4111-8111-111111111111",
      "name": "Herramientas",
      "description": "Herramientas manuales",
      "isActive": true,
      "createdAt": "2026-09-29T12:00:00Z",
      "updatedAt": null
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

**Errores relevantes:** 400 `INVALID_PAGINATION` o `INVALID_REQUEST`. También aplican los errores comunes de la sección 7.

### 3.3 Consultar categoría

`GET /api/categories/11111111-1111-4111-8111-111111111111`

Obtiene una categoría por su UUID, incluso si está inactiva.

**Schema de solicitud:** ruta `id`: string/uuid obligatorio.

**Example Value — solicitud:** usar la URL indicada, sin cuerpo.

**Respuesta:** `200 OK`. **Schema:** [CategoryDto](#categorydto).

**Example Value — respuesta**

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "name": "Herramientas",
  "description": "Herramientas manuales",
  "isActive": true,
  "createdAt": "2026-09-29T12:00:00Z",
  "updatedAt": null
}
```

**Errores relevantes:** 404 `CATEGORY_NOT_FOUND`. También aplican los errores comunes de la sección 7.

### 3.4 Actualizar categoría

`PUT /api/categories/11111111-1111-4111-8111-111111111111`

Sustituye nombre y descripción; no es PATCH. Omitir `description` la deja en null. No reactiva una categoría inactiva.

**Schema de solicitud:** [CategoryRequest](#categoryrequest); ruta `id`: UUID obligatorio.

**Example Value — solicitud**

```json
{
  "name": "Herramientas manuales",
  "description": "Herramientas para taller"
}
```

**Respuesta:** `200 OK`. **Schema:** [CategoryDto](#categorydto).

**Example Value — respuesta**

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "name": "Herramientas manuales",
  "description": "Herramientas para taller",
  "isActive": true,
  "createdAt": "2026-09-29T12:00:00Z",
  "updatedAt": "2026-09-29T12:05:00Z"
}
```

**Errores relevantes:** 400 por campos inválidos; 404 `CATEGORY_NOT_FOUND`; 409 `DUPLICATE_CATEGORY_NAME`. También aplican los errores comunes de la sección 7.

### 3.5 Desactivar categoría

`DELETE /api/categories/11111111-1111-4111-8111-111111111111`

Realiza baja lógica (`isActive=false`). No permite desactivar una categoría con productos activos. Repetir la desactivación, cuando no existen productos activos asociados, devuelve 204. El registro permanece consultable y su nombre sigue reservado.

**Schema de solicitud:** ruta `id`: string/uuid obligatorio.

**Example Value — solicitud:** usar la URL indicada, sin cuerpo.

**Respuesta:** `204 No Content`. **Schema:** sin cuerpo.

**Example Value — respuesta:** sin contenido; no enviar ni esperar `{}`.

**Errores relevantes:** 404 `CATEGORY_NOT_FOUND`; 409 `CATEGORY_IN_USE`. También aplican los errores comunes de la sección 7.

## 4. Products

### 4.1 Crear producto

`POST /api/products`

Requiere una categoría existente y activa y un SKU único. Crea el producto activo con stock cero. El SKU se normaliza a mayúsculas. Devuelve `Location: .../api/products/{id}`.

**Schema de solicitud:** [ProductRequest](#productrequest).

**Example Value — solicitud**

```json
{
  "name": "Martillo",
  "description": "Martillo de acero",
  "sku": "HERR-001",
  "price": 150.5,
  "categoryId": "11111111-1111-4111-8111-111111111111"
}
```

**Respuesta:** `201 Created`. **Schema:** [CreatedId](#createdid).

**Example Value — respuesta**

```json
{
  "id": "22222222-2222-4222-8222-222222222222"
}
```

**Errores relevantes:** 400 por campos inválidos; 404 `CATEGORY_NOT_FOUND`; 409 `CATEGORY_INACTIVE` o `DUPLICATE_PRODUCT_SKU`. También aplican los errores comunes de la sección 7.

### 4.2 Listar productos

`GET /api/products?page=1&pageSize=20`

Incluye activos e inactivos, con stock actual. Orden: `name ASC`, desempate `id ASC`. No hay filtros por categoría, SKU o estado implementados.

**Schema de solicitud:** parámetros [Pagination](#pagination).

**Example Value — solicitud:** usar la URL indicada, sin cuerpo.

**Respuesta:** `200 OK`. **Schema:** [PagedResult<ProductDto>](#pagedresult).

**Example Value — respuesta**

```json
{
  "items": [
    {
      "id": "22222222-2222-4222-8222-222222222222",
      "name": "Martillo",
      "description": "Martillo de acero",
      "sku": "HERR-001",
      "price": 150.5,
      "categoryId": "11111111-1111-4111-8111-111111111111",
      "isActive": true,
      "currentStock": 0
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

**Errores relevantes:** 400 `INVALID_PAGINATION` o `INVALID_REQUEST`. También aplican los errores comunes de la sección 7.

### 4.3 Consultar producto

`GET /api/products/22222222-2222-4222-8222-222222222222`

Devuelve los datos del producto y su stock, incluso estando inactivo.

**Schema de solicitud:** ruta `id`: string/uuid obligatorio.

**Example Value — solicitud:** usar la URL indicada, sin cuerpo.

**Respuesta:** `200 OK`. **Schema:** [ProductDto](#productdto).

**Example Value — respuesta**

```json
{
  "id": "22222222-2222-4222-8222-222222222222",
  "name": "Martillo",
  "description": "Martillo de acero",
  "sku": "HERR-001",
  "price": 150.5,
  "categoryId": "11111111-1111-4111-8111-111111111111",
  "isActive": true,
  "currentStock": 0
}
```

**Errores relevantes:** 404 `PRODUCT_NOT_FOUND`. También aplican los errores comunes de la sección 7.

### 4.4 Actualizar producto

`PUT /api/products/22222222-2222-4222-8222-222222222222`

Sustituye los campos editables. Requiere todos los campos obligatorios, incluso si no cambian. Omitir `description` la deja en null. No modifica stock ni estado activo. Categoría y existencia se validan dentro de la operación transaccional.

**Schema de solicitud:** [ProductRequest](#productrequest); ruta `id`: UUID obligatorio.

**Example Value — solicitud**

```json
{
  "name": "Martillo",
  "description": "Martillo de acero con mango reforzado",
  "sku": "HERR-001",
  "price": 175,
  "categoryId": "11111111-1111-4111-8111-111111111111"
}
```

**Respuesta:** `200 OK`. **Schema:** [ProductDto](#productdto).

**Example Value — respuesta**

```json
{
  "id": "22222222-2222-4222-8222-222222222222",
  "name": "Martillo",
  "description": "Martillo de acero con mango reforzado",
  "sku": "HERR-001",
  "price": 175,
  "categoryId": "11111111-1111-4111-8111-111111111111",
  "isActive": true,
  "currentStock": 0
}
```

**Errores relevantes:** 400 por campos inválidos; 404 `PRODUCT_NOT_FOUND` o `CATEGORY_NOT_FOUND`; 409 `CATEGORY_INACTIVE` o `DUPLICATE_PRODUCT_SKU`. También aplican los errores comunes de la sección 7.

### 4.5 Desactivar producto

`DELETE /api/products/22222222-2222-4222-8222-222222222222`

Baja lógica que conserva stock e historial. Desde entonces se rechazan nuevos movimientos. Repetirla devuelve 204. El SKU continúa reservado. No existe endpoint para reactivar.

**Schema de solicitud:** ruta `id`: string/uuid obligatorio.

**Example Value — solicitud:** usar la URL indicada, sin cuerpo.

**Respuesta:** `204 No Content`. **Schema:** sin cuerpo.

**Example Value — respuesta:** sin contenido; no enviar ni esperar `{}`.

**Errores relevantes:** 404 `PRODUCT_NOT_FOUND`. También aplican los errores comunes de la sección 7.

## 5. Inventory

### 5.1 Registrar entrada o salida

`POST /api/inventory/movements`

Usa el mismo endpoint para entradas (`Entry`) y salidas (`Exit`). Requiere un producto existente y activo. El cambio de stock y la creación del movimiento son atómicos. Ejemplo: una entrada de 10 sobre stock 0 deja stock 10. No devuelve `Location`.

**Schema de solicitud:** [RegisterInventoryMovementRequest](#registerinventorymovementrequest).

**Example Value — solicitud**

```json
{
  "productId": "22222222-2222-4222-8222-222222222222",
  "type": "Entry",
  "quantity": 10,
  "reason": "Existencia inicial"
}
```

**Respuesta:** `201 Created`. **Schema:** [InventoryMovementResult](#inventorymovementresult).

**Example Value — respuesta**

```json
{
  "movementId": "33333333-3333-4333-8333-333333333333",
  "currentStock": 10
}
```

**Errores relevantes:** 400 por campos inválidos; 404 `PRODUCT_NOT_FOUND`; 409 `PRODUCT_INACTIVE`, `INSUFFICIENT_STOCK` o `INVENTORY_STOCK_OVERFLOW`. También aplican los errores comunes de la sección 7.

**Example Value — salida posterior de 3 unidades**

```json
{
  "productId": "22222222-2222-4222-8222-222222222222",
  "type": "Exit",
  "quantity": 3,
  "reason": "Salida de almacén"
}
```

Con stock previo de 10, devuelve 201 con un nuevo `movementId` y `currentStock: 7`. Si se solicita más stock del disponible, devuelve 409 y no persiste el movimiento ni un cambio parcial de stock. No hay endpoints PUT o DELETE de movimientos.

### 5.2 Consultar balance

`GET /api/inventory/products/22222222-2222-4222-8222-222222222222`

Devuelve la identidad del producto y el stock actual. Permite consultar productos inactivos.

**Schema de solicitud:** ruta `productId`: string/uuid obligatorio.

**Example Value — solicitud:** usar la URL indicada, sin cuerpo.

**Respuesta:** `200 OK`. **Schema:** [ProductInventoryDto](#productinventorydto).

**Example Value — respuesta**

```json
{
  "productId": "22222222-2222-4222-8222-222222222222",
  "sku": "HERR-001",
  "name": "Martillo",
  "currentStock": 10
}
```

**Errores relevantes:** 404 `PRODUCT_NOT_FOUND`. También aplican los errores comunes de la sección 7.

### 5.3 Consultar historial

`GET /api/inventory/products/22222222-2222-4222-8222-222222222222/movements?type=Entry&startDate=2026-09-29T00:00:00Z&endDate=2026-09-29T23:59:59Z&page=1&pageSize=20`

Filtra por producto, tipo y fechas inclusivas. Orden fijo: `createdAt DESC`, desempate `id DESC`. Incluye historial de productos inactivos. `totalCount` cuenta los movimientos que cumplen los filtros.

**Schema de solicitud:** ruta `productId`: UUID obligatorio; parámetros [HistoryQuery](#historyquery).

**Example Value — solicitud:** usar la URL indicada, sin cuerpo.

**Respuesta:** `200 OK`. **Schema:** [PagedResult<InventoryMovementDto>](#pagedresult).

**Example Value — respuesta**

```json
{
  "items": [
    {
      "id": "33333333-3333-4333-8333-333333333333",
      "productId": "22222222-2222-4222-8222-222222222222",
      "type": "Entry",
      "quantity": 10,
      "reason": "Existencia inicial",
      "createdAt": "2026-09-29T12:10:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

**Errores relevantes:** 400 `INVALID_DATE_RANGE`, `INVALID_PAGINATION` o `INVALID_REQUEST`; 404 `PRODUCT_NOT_FOUND`. También aplican los errores comunes de la sección 7.

Un producto existente sin movimientos devuelve 200 con `items: []`, `totalCount: 0` y `totalPages: 0`. Una página posterior a la última devuelve `items: []` conservando los totales de la consulta.

## 6. Schema: catálogo de contratos

Los tipos se expresan como en OpenAPI: `string/uuid`, `string/date-time`, `integer/int32`, `integer/int64`, `number/decimal` y `boolean`. `nullable` significa que admite JSON `null`. En respuestas, todos los campos indicados se serializan; los nullable pueden contener null. Los esquemas de entrada no incluyen los campos administrados por el servidor.

### CategoryRequest

Usado por POST y PUT de categorías.

| Campo | Tipo | Obligatorio | Regla |
| --- | --- | --- | --- |
| name | string | Sí | No vacío ni solo espacios; máximo 150 caracteres después de recortar espacios externos. |
| description | string nullable | No | Máximo 500 caracteres después de recortar; null permanece null y solo espacios produce cadena vacía. |

### ProductRequest

Representa `CreateProductRequest` y `UpdateProductRequest`, que tienen los mismos campos.

| Campo | Tipo | Obligatorio | Regla |
| --- | --- | --- | --- |
| name | string | Sí | No vacío; máximo 150 caracteres después de recortar. |
| description | string nullable | No | Máximo 1000 caracteres después de recortar; null permanece null y solo espacios produce cadena vacía. |
| sku | string | Sí | No vacío; máximo 80 caracteres después de recortar; se transforma a mayúsculas; debe ser único. |
| price | number/decimal | Sí | Entre 0 y 9999999999999999.99, sin requerir redondeo a dos decimales; ejemplo: 150.50. |
| categoryId | string/uuid | Sí | UUID distinto del vacío; debe corresponder a categoría existente y activa. |

`id`, `isActive` y `currentStock` no son campos editables del contrato. No existe asignación de stock mediante PUT. Para preservar la precisión de precios grandes, los clientes deben usar un tipo decimal apropiado.

### RegisterInventoryMovementRequest

| Campo | Tipo | Obligatorio | Regla |
| --- | --- | --- | --- |
| productId | string/uuid | Sí | UUID no vacío de producto existente y activo. |
| type | string enum | Sí | Entry o Exit; enviar las cadenas, no 1/2. |
| quantity | integer/int32 | Sí | De 1 a 2147483647; las salidas no pueden superar el stock disponible. |
| reason | string nullable | No | Máximo 500 caracteres después de recortar; null permanece null y solo espacios produce cadena vacía. |

El stock resultante debe permanecer entre 0 y 2147483647. Una entrada que exceda ese máximo produce conflicto.

### CreatedId

| Campo | Tipo | Descripción |
| --- | --- | --- |
| id | string/uuid | Identificador generado por el servidor para categoría o producto. |

### CategoryDto

| Campo | Tipo | Descripción |
| --- | --- | --- |
| id | string/uuid | Identificador de categoría. |
| name | string | Nombre normalizado. |
| description | string nullable | Descripción opcional. |
| isActive | boolean | Estado de activación. |
| createdAt | string/date-time | Fecha de creación con offset. |
| updatedAt | string/date-time nullable | Fecha de modificación; null antes de actualizar/desactivar. |

### ProductDto

| Campo | Tipo | Descripción |
| --- | --- | --- |
| id | string/uuid | Identificador de producto. |
| name | string | Nombre normalizado. |
| description | string nullable | Descripción opcional. |
| sku | string | SKU normalizado a mayúsculas. |
| price | number/decimal | Precio con precisión de hasta dos decimales. |
| categoryId | string/uuid | Categoría asociada. |
| isActive | boolean | Estado del producto. |
| currentStock | integer/int32 | Existencias actuales, no negativas. |

El DTO de producto no expone `createdAt` ni `updatedAt`.

### InventoryMovementResult

| Campo | Tipo | Descripción |
| --- | --- | --- |
| movementId | string/uuid | Movimiento creado. |
| currentStock | integer/int32 | Stock resultante al registrar ese movimiento. |

### ProductInventoryDto

| Campo | Tipo | Descripción |
| --- | --- | --- |
| productId | string/uuid | Identificador del producto. |
| sku | string | SKU del producto. |
| name | string | Nombre del producto. |
| currentStock | integer/int32 | Stock actual. |

### InventoryMovementDto

| Campo | Tipo | Descripción |
| --- | --- | --- |
| id | string/uuid | Identificador del movimiento. |
| productId | string/uuid | Producto asociado. |
| type | string enum | Entry o Exit. |
| quantity | integer/int32 | Cantidad positiva del movimiento. |
| reason | string nullable | Motivo opcional. |
| createdAt | string/date-time | Instante de creación con offset. |

### Pagination

Parámetros de query opcionales para categorías, productos e historial.

| Parámetro | Tipo | Predeterminado | Rango |
| --- | --- | --- | --- |
| page | integer/int32 | 1 | 1 a 10000 |
| pageSize | integer/int32 | 20 | 1 a 100 |

### HistoryQuery

Además de Pagination, recibe:

| Parámetro | Tipo | Obligatorio | Regla |
| --- | --- | --- | --- |
| type | string enum | No | Entry o Exit, sin distinción de mayúsculas; los valores numéricos no son válidos. |
| startDate | string/date-time | No | Límite inferior inclusivo; usar ISO 8601 con Z u offset. |
| endDate | string/date-time | No | Límite superior inclusivo; no anterior a startDate. |

Se puede enviar solo un extremo del intervalo. Al usar un offset positivo en una URL, codificar `+` como `%2B` o dejar que el cliente construya los parámetros. No hay parámetro de ordenamiento configurable.

### PagedResult

`T` es CategoryDto, ProductDto o InventoryMovementDto según el endpoint.

| Campo | Tipo | Descripción |
| --- | --- | --- |
| items | array de T | Registros de la página, nunca más de pageSize. |
| page | integer/int32 | Página solicitada. |
| pageSize | integer/int32 | Tamaño solicitado. |
| totalCount | integer/int64 | Total coincidente, no solo el tamaño de items. |
| totalPages | integer/int64 | Techo de totalCount/pageSize; cero si no hay registros. |

Conteo y página son lecturas separadas: bajo modificaciones concurrentes no representan necesariamente una única instantánea. No se devuelven enlaces de navegación; el cliente incrementa `page`.

### ApiError

| Campo | Tipo | Descripción |
| --- | --- | --- |
| code | string | Código estable que identifica el fallo. |
| message | string | Descripción legible del fallo. |

## 7. Errores y respuestas comunes

**Example Value — 409 Conflict**:

```json
{
  "code": "INSUFFICIENT_STOCK",
  "message": "The requested quantity exceeds the available stock."
}
```

**Schema:** [ApiError](#apierror).

| HTTP | Situación | Códigos relevantes |
| --- | --- | --- |
| 400 | JSON, tipos o campos obligatorios inválidos | INVALID_REQUEST |
| 400 | Validación de categoría | INVALID_CATEGORY_NAME, INVALID_CATEGORY_DESCRIPTION |
| 400 | Validación de producto | INVALID_PRODUCT_NAME, INVALID_PRODUCT_DESCRIPTION, INVALID_PRODUCT_SKU, INVALID_PRODUCT_PRICE, INVALID_CATEGORY_ID |
| 400 | Validación de inventario | INVALID_PRODUCT_ID, INVALID_INVENTORY_TYPE, INVALID_INVENTORY_QUANTITY, INVALID_INVENTORY_REASON |
| 400 | Paginación o fechas inválidas | INVALID_PAGINATION, INVALID_DATE_RANGE |
| 400 | Stock previo inválido detectado internamente | INVALID_INVENTORY_STOCK |
| 401 | Token ausente, inválido o expirado | UNAUTHORIZED |
| 403 | Token válido sin permiso para la operación | FORBIDDEN |
| 404 | Recurso inexistente | CATEGORY_NOT_FOUND, PRODUCT_NOT_FOUND |
| 404 | Ruta no encontrada | NOT_FOUND |
| 409 | Nombre/SKU duplicado | DUPLICATE_CATEGORY_NAME, DUPLICATE_PRODUCT_SKU |
| 409 | Categoría inactiva o con productos activos al desactivar | CATEGORY_INACTIVE, CATEGORY_IN_USE |
| 409 | Producto inactivo o saldo no permitido | PRODUCT_INACTIVE, INSUFFICIENT_STOCK, INVENTORY_STOCK_OVERFLOW |
| 405 | Verbo no permitido en la ruta | METHOD_NOT_ALLOWED |
| 415 | Tipo de contenido no soportado | UNSUPPORTED_MEDIA_TYPE |
| 500 | Fallo técnico o código de negocio sin mapeo explícito | INTERNAL_ERROR |

Las condiciones anteriores dependen de la operación. Si una solicitud incumple varias reglas, no debe asumirse que se devolverán todos los errores ni un orden universal. El contrato de error contiene un código y un mensaje.

## 8. Secuencia sugerida para documentar o probar el caso completo

1. Autenticarse y obtener permisos de lectura/escritura.
2. Crear categoría; conservar su `id`.
3. Crear producto con ese `categoryId`; conservar su `id`. Consultar balance: 0.
4. Registrar Entry de 10; comprobar `currentStock: 10`.
5. Registrar Exit de 3; comprobar `currentStock: 7`.
6. Consultar balance e historial; la salida aparece antes que la entrada en el historial sin filtros.
7. Consultar listados paginados y recursos por ID.
8. Actualizar categoría y producto; verificar los cambios sin alterar stock.
9. Intentar desactivar categoría mientras su producto está activo: 409 CATEGORY_IN_USE.
10. Desactivar producto: 204. Comprobar que stock e historial siguen consultables y un nuevo movimiento devuelve 409 PRODUCT_INACTIVE.
11. Desactivar categoría: 204. Los registros siguen presentes en sus listados como inactivos.

Cambiar nombres y SKU al repetir la secuencia: la baja lógica no libera su unicidad. Los ejemplos de las secciones anteriores ilustran cada contrato y no constituyen una captura secuencial de esta ejecución.

## 9. Trazabilidad al código y documentos

| Elemento documentado | Fuente |
| --- | --- |
| Métodos, rutas y respuestas de categorías | [CategoriesController.cs](../src/InventoryManagement.Api/Controllers/CategoriesController.cs) |
| Métodos, rutas y respuestas de productos | [ProductsController.cs](../src/InventoryManagement.Api/Controllers/ProductsController.cs) |
| Métodos, rutas y respuestas de inventario | [InventoryController.cs](../src/InventoryManagement.Api/Controllers/InventoryController.cs) |
| Cuerpos de solicitudes y configuración JSON | [Contracts](../src/InventoryManagement.Api/Contracts) |
| DTO y paginación | [Abstractions](../src/InventoryManagement.Application/Abstractions) |
| Permisos | [Permissions.cs](../src/InventoryManagement.Api/Authorization/Permissions.cs) |
| Mapeo HTTP de errores de negocio | [BusinessErrorResponse.cs](../src/InventoryManagement.Api/Contracts/BusinessErrorResponse.cs) |
| Reglas de categoría | [Category.cs](../src/InventoryManagement.Domain/Categories/Category.cs) |
| Reglas de producto | [ProductDetails.cs](../src/InventoryManagement.Domain/Products/ProductDetails.cs) |
| Reglas de movimientos | [InventoryMovement.cs](../src/InventoryManagement.Domain/Inventory/InventoryMovement.cs) |
| Filtros y orden del historial | [InventoryQueries.cs](../src/InventoryManagement.Infrastructure/Persistence/InventoryQueries.cs) |
| Comportamiento previo especificado | [Especificación de dominio](specs/001-inventory-domain-spec.md) |
| Decisiones de diseño | [ADR](architecture-decisions.md) |
| Colección de aceptación | [Guía Postman](../postman/README.md) |

Este anexo documenta el contrato existente. No añade endpoints ni modifica validaciones, autorización, persistencia o datos.
