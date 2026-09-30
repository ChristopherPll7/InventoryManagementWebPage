$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Instala el SDK de .NET 8 en el equipo que publica. El equipo receptor no lo necesitará.'
}
dotnet publish ./src/InventoryManagement.Web/InventoryManagement.Web.csproj -c Release -r win-x64 --self-contained true -o ./artifacts/publish/win-x64
if ($LASTEXITCODE -ne 0) { throw 'La publicación falló. Revisa los errores mostrados por dotnet.' }
Copy-Item ./README.md ./artifacts/publish/win-x64/README.md -Force
Write-Host 'Publicación lista en artifacts/publish/win-x64. Entrega toda esa carpeta.'
