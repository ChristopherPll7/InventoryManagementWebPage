# Pruebas de las 13 operaciones desde MVC

Desde `frontend`:

```powershell
dotnet run --project ./tests/PermissionAcceptance/PermissionAcceptance.csproj -c Release --no-restore
node ./tests/PermissionAcceptance/form-validation.cjs
```

Si faltan los assets, ejecutar primero el restore offline del proyecto de pruebas documentado en README. Requiere SDK .NET; Node.js solo para JavaScript. No requiere API ni Keycloak. El servidor usa un puerto libre de loopback y se detiene al terminar.

## Cobertura

HttpFlowAcceptance.cs utiliza rutas, controladores, binding, Razor, antiforgery y cliente HTTP de producción. Autenticación y API son simuladas. Extrae action y token del HTML, envía formularios con cookies y sigue las redirecciones exitosas comprobando HTTP 200.

| Operaciones API | Flujo MVC |
|---|---|
| GET categories; GET categories/{id} | Listado y detalle |
| POST categories; PUT categories/{id}; DELETE categories/{id} | Alta, edición y desactivación; retorno a detalle |
| GET products; GET products/{id} | Listado y detalle |
| POST products; PUT products/{id}; DELETE products/{id} | Alta, edición y desactivación; retorno a detalle |
| GET inventory/products/{id} | Balance |
| GET inventory/products/{id}/movements | Historial |
| POST inventory/movements | Formulario y retorno a inventario |

Cada operación se prueba con éxito y errores 400, 401, 403, 404, 409 y 500. Se comprueba redirección al login ante 401, estados de error, ausencia de detalles internos de 500 y conservación de formulario ante errores corregibles. La matriz utiliza un código genérico; las suites anteriores cubren también códigos específicos como CATEGORY_NOT_FOUND, PRODUCT_NOT_FOUND, INSUFFICIENT_STOCK y PRODUCT_INACTIVE.

Se verifica que el action de movimientos conserve la query codificada sin texto Razor literal y que un POST sin antiforgery no llame a la API. Las pruebas anteriores cubren DTO, estados esperados, Location, timeout, transporte, paginación, ModelState, retornos locales y permisos independientes.

## Resultado y límites

**409 comprobaciones aprobadas:** 148 .NET anteriores, 252 HTTP/Razor nuevas y 9 JavaScript. La ejecución compila las vistas. Los fallos del bloque HTTP devuelven código de salida distinto de cero.

No son pruebas de navegador ni del backend desplegado. No se verificaron SSO real, persistencia, concurrencia ni presentación visual; no se modificaron datos reales. Se verifica la reacción del frontend, no que los 13 endpoints de una API real funcionen.
