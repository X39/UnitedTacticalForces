# Generating the API
To generate the API, you have to install the dotnet tool `kiota`, using:
```bash
dotnet tool update --global Microsoft.OpenApi.Kiota  
```

Afterwards, you may use the following command, at project root, to generate the API:
```bash
kiota generate -d https://localhost:7072/openapi/v1.json -l CSharp -n X39.UnitedTacticalForces.WebApp.UtfApi -o UtfApi
```

```bash
A:\dev\git\kiota\src\kiota\bin\Release\net10.0\publish\kiota.exe generate -d https://localhost:7072/openapi/v1.json -l CSharp -n X39.UnitedTacticalForces.WebApp.UtfApi -o UtfApi
```
